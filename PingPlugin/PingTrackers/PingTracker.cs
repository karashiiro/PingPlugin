using System;
using System.Collections.Concurrent;
using System.Linq;

namespace PingPlugin.PingTrackers
{
    public abstract class PingTracker : IDisposable
    {
        protected readonly PingConfiguration config;

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        public double AverageRTT { get; private set; }
        public ulong LastRTT { get; private set; }
        public ConcurrentQueue<float> RTTTimes { get; private set; }

        protected PingTracker(PingConfiguration config)
        {
            this.config = config;

            RTTTimes = new ConcurrentQueue<float>();
        }

        protected void DoNextRTTCalculation(long nextRTT)
        {
            lock (RTTTimes)
            {
                RTTTimes.Enqueue(nextRTT);
                
                while (RTTTimes.Count > this.config.PingQueueSize)
                    RTTTimes.TryDequeue(out _);
            }
            CalcAverage();

            LastRTT = (ulong)nextRTT;

            SendMessage();
        }

        private void SendMessage()
        {
            var del = OnPingUpdated;
            del?.Invoke(new PingStatsPayload
            {
                AverageRTT = Convert.ToUInt64(AverageRTT),
                LastRTT = LastRTT,
            });
        }

        protected void CalcAverage() => AverageRTT = RTTTimes.Average();

        protected void ResetRTT() => RTTTimes = new ConcurrentQueue<float>();

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
