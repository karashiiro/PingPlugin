using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class AggregatePingTracker : PingTracker
    {
        private const string COMTrackerKey = "COM";
        private const string IpHlpApiTrackerKey = "IpHlpApi";

        private readonly IDictionary<string, TrackerInfo> trackerInfos;
        private readonly DecisionTree<string> decisionTree;

        public AggregatePingTracker(PingConfiguration config, GameAddressDetector addressDetector) : base(config, addressDetector)
        {
            // Define trackers
            this.trackerInfos = new Dictionary<string, TrackerInfo>();

            RegisterTracker(COMTrackerKey, new ComponentModelPingTracker(config, addressDetector) { Verbose = false });
            RegisterTracker(IpHlpApiTrackerKey, new IpHlpApiPingTracker(config, addressDetector) { Verbose = false });

            // Create decision tree to solve tracker selection problem
            this.decisionTree = new DecisionTree<string>(
                // If COM is errored
                () => TrackerIsErrored(COMTrackerKey),
                // Just use IpHlpApi
                pass: new DecisionTree<string>(() => TreeResult.Resolve(IpHlpApiTrackerKey)),
                fail: new DecisionTree<string>(
                    // If difference between pings is more than 30
                    () => Math.Abs((long)GetTrackerRTT(COMTrackerKey) - (long)GetTrackerRTT(IpHlpApiTrackerKey)) > 30,
                    pass: new DecisionTree<string>(
                        // Use greater ping value, something's probably subtly broken
                        () => GetTrackerRTT(COMTrackerKey) < GetTrackerRTT(IpHlpApiTrackerKey),
                        pass: new DecisionTree<string>(() => TreeResult.Resolve(IpHlpApiTrackerKey)),
                        fail: new DecisionTree<string>(() => TreeResult.Resolve(COMTrackerKey))),
                    fail: new DecisionTree<string>(
                        // Otherwise use the lower ping value, we'll assume it's more accurate
                        () => GetTrackerRTT(COMTrackerKey) < GetTrackerRTT(IpHlpApiTrackerKey),
                        pass: new DecisionTree<string>(() => TreeResult.Resolve(COMTrackerKey)),
                        fail: new DecisionTree<string>(() => TreeResult.Resolve(IpHlpApiTrackerKey)))
                    )
                );
        }

        protected override void ResetRTT()
        {
            foreach (var (_, ti) in this.trackerInfos)
            {
                ti.Ticked = false;
                ti.LastRTT = 0;
            }

            base.ResetRTT();
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // This can happen because the base PingTracker starts this task in its constructor, so
                // it can run before we run the child class constructor here.
                if (this.decisionTree == null) continue;

                if (SeAddress != null)
                {
                    // Use decision tree to select best ping tracker
                    try
                    {
                        var bestTracker = this.decisionTree.Execute();
                        if (bestTracker != null)
                        {
                            var trackerInfo = this.trackerInfos[bestTracker];
                            if (trackerInfo.Ticked)
                            {
                                // Process result
                                NextRTTCalculation(trackerInfo.LastRTT);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        PluginLog.LogError(e, "Error in best ping tracker selection.");
                    }
                }

                await Task.Delay(3000, token);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var (_, ti) in this.trackerInfos)
                {
                    ti.Tracker?.Dispose();
                }

                base.Dispose(true);
            }
        }

        private void RegisterTracker(string key, PingTracker tracker)
        {
            // Info block registration
            this.trackerInfos.Add(key, new TrackerInfo { Tracker = tracker });

            // Event forwarding
            tracker.OnPingUpdated += payload =>
            {
                this.trackerInfos[key].Ticked = true;
                this.trackerInfos[key].LastRTT = payload.LastRTT;
            };
        }

        private ulong GetTrackerRTT(string key)
        {
            return this.trackerInfos[key].LastRTT;
        }

        private bool TrackerIsErrored(string key)
        {
            return this.trackerInfos[key].Tracker?.Errored ?? true;
        }

        private class TrackerInfo
        {
            public PingTracker Tracker { get; init; }

            public ulong LastRTT { get; set; }

            public bool Ticked { get; set; }
        }
    }
}
