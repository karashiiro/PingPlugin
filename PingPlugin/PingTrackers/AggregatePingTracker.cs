﻿using Dalamud.Plugin.Services;
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
        private readonly IPluginLog pluginLog;
        private string currentTracker = "";

        public AggregatePingTracker(PingConfiguration config, GameAddressDetector addressDetector, IPluginLog pluginLog)
            : base(config, addressDetector, PingTrackerKind.Aggregate, pluginLog)
        {
            this.pluginLog = pluginLog;

            // Define trackers
            this.trackerInfos = new Dictionary<string, TrackerInfo>();

            RegisterTracker(COMTrackerKey, new ComponentModelPingTracker(config, addressDetector, pluginLog) { Verbose = false });
            RegisterTracker(IpHlpApiTrackerKey, new IpHlpApiPingTracker(config, addressDetector, pluginLog) { Verbose = false });

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
                        fail: new DecisionTree<string>(() => TreeResult.Resolve(COMTrackerKey))
                    ),
                    // Otherwise, default to IpHlpApi
                    fail: new DecisionTree<string>(() => TreeResult.Resolve(IpHlpApiTrackerKey))
                )
            );
        }

        public override void Start()
        {
            foreach (var (_, ti) in this.trackerInfos)
            {
                ti.Tracker.Start();
            }

            base.Start();
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
                // Use decision tree to select best ping tracker
                try
                {
                    var bestTracker = this.decisionTree.Execute();
                    if (!string.IsNullOrEmpty(bestTracker))
                    {
                        ProcessBestResult(bestTracker);
                    }
                    else if (!string.IsNullOrEmpty(this.currentTracker))
                    {
                        ProcessBestResult(this.currentTracker);
                    }
                }
                catch (Exception e)
                {
                    pluginLog.Error(e, "Error in best ping tracker selection.");
                }

                await Task.Delay(3000, token);
            }
        }

        private void ProcessBestResult(string bestTracker)
        {
            var trackerInfo = this.trackerInfos[bestTracker];
            if (trackerInfo.Ticked)
            {
                // Process result
                NextRTTCalculation(trackerInfo.LastRTT);
                trackerInfo.Ticked = false;

                if (this.currentTracker != bestTracker)
                {
                    // Swap logging priorities
                    trackerInfo.Tracker.Verbose = true;
                    if (!string.IsNullOrEmpty(this.currentTracker))
                    {
                        this.trackerInfos[this.currentTracker].Tracker.Verbose = false;
                    }

                    // Update the current tracker
                    this.currentTracker = bestTracker;

                    if (Verbose)
                    {
                        pluginLog.Debug("Retrieving ping from tracker {PingTrackerKey}", bestTracker);
                    }
                }
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