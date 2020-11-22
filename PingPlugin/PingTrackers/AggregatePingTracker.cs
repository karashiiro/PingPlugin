using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class AggregatePingTracker : PingTracker
    {
        private readonly IEnumerable<PingTracker> pingTrackers;

        public long SeAddressRaw { get; private set; }
        public IPAddress SeAddress { get; private set; }
        public WinError LastError { get; private set; }

        private CancellationTokenSource TokenSource { get; }

        public AggregatePingTracker(PingConfiguration config, params PingTracker[] pingTrackers) : base(config)
        {
            this.pingTrackers = pingTrackers;
            TokenSource = new CancellationTokenSource();
            Task.Run(() => PingLoop(TokenSource.Token));
        }

        private async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1500, token); // We split the wait in order to prevent running this before the other trackers can complete one loop.

                var bestTracker = GetBestTracker();
                if (bestTracker is TcpTablePingTracker tcpPingTracker)
                {
                    if (tcpPingTracker.Reset)
                    {
                        ResetRTT();
                        tcpPingTracker.Reset = false;
                    }

                    SeAddressRaw = tcpPingTracker.SeAddressRaw;
                    SeAddress = tcpPingTracker.SeAddress;
                    LastError = tcpPingTracker.LastError;
                }

                DoNextRTTCalculation((long)bestTracker.LastRTT);

                lock (RTTTimes)
                {
                    RTTTimes.Enqueue(LastRTT);
                
                    while (RTTTimes.Count > this.config.PingQueueSize)
                        RTTTimes.TryDequeue(out _);
                }
                CalcAverage();

                await Task.Delay(1500, token);
            }

            token.ThrowIfCancellationRequested();
        }

        private PingTracker GetBestTracker()
        {
            var bestPing = 0UL;
            PingTracker bestTracker = null;
            TrampolinePingTracker trampolinePingTracker = null;
            foreach (var tracker in this.pingTrackers)
            {
                if (tracker is TrampolinePingTracker tpt)
                {
                    trampolinePingTracker = tpt;
                    continue;
                }

                if (tracker.LastRTT >= bestPing)
                {
                    bestPing = tracker.LastRTT;
                    bestTracker = tracker;
                }
            }

            if (bestPing != 0)
            {
                return bestTracker;
            }
            else
            {
                return trampolinePingTracker;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var pingTracker in pingTrackers)
                {
                    pingTracker.Dispose();
                }

                base.Dispose(true);
            }
        }
    }
}
