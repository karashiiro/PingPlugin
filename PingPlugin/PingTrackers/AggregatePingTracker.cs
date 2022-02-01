using System;
using Dalamud.Game.ClientState;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Logging;

namespace PingPlugin.PingTrackers
{
    public class AggregatePingTracker : PingTracker
    {
        private const string COMTrackerKey = "COM";
        private const string IpHlpApiTrackerKey = "IpHlpApi";

        private readonly IDictionary<string, TrackerInfo> trackerInfos;
        private readonly DecisionTree<TrackerInfo> decisionTree;

        public AggregatePingTracker(PingConfiguration config, ClientState clientState) : base(config, clientState)
        {
            // Define trackers
            this.trackerInfos = new Dictionary<string, TrackerInfo>();
            
            RegisterTracker(COMTrackerKey, new ComponentModelPingTracker(config, clientState) { Verbose = false });
            RegisterTracker(IpHlpApiTrackerKey, new IpHlpApiPingTracker(config, clientState) { Verbose = false });

            // Create decision tree to solve tracker selection problem
            this.decisionTree = new DecisionTree<TrackerInfo>(
                () => this.trackerInfos[COMTrackerKey].LastRTT < this.trackerInfos[IpHlpApiTrackerKey].LastRTT,
                pass: new DecisionTree<TrackerInfo>(() => TreeResult.Resolve(this.trackerInfos[COMTrackerKey])),
                fail: new DecisionTree<TrackerInfo>(() => TreeResult.Resolve(this.trackerInfos[IpHlpApiTrackerKey])));
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (SeAddress != null)
                {
                    // Use decision tree to select best ping tracker
                    try
                    {
                        var bestTracker = this.decisionTree.Execute();
                        if (bestTracker != null)
                        {
                            // Process result
                            NextRTTCalculation(bestTracker.LastRTT);
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
            tracker.OnPingUpdated += payload => this.trackerInfos[key].LastRTT = payload.LastRTT;
        }

        private class TrackerInfo
        {
            public PingTracker Tracker { get; init; }

            public ulong LastRTT { get; set; }
        }
    }
}
