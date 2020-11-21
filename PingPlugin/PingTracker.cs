using System;
using System.Collections.Concurrent;
using System.Linq;

namespace PingPlugin
{
    public class PingTracker
    {
        private readonly PingConfiguration config;

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        public double AverageRTT { get; private set; }
        public ulong LastRTT { get; private set; }
        public ConcurrentQueue<float> RTTTimes { get; }

        public PingTracker(PingConfiguration config)
        {
            this.config = config;

            RTTTimes = new ConcurrentQueue<float>();
        }

        public void DoNextRTTCalculation(long nextRTT)
        {
            lock (RTTTimes)
            {
                RTTTimes.Enqueue(nextRTT);
                
                while (RTTTimes.Count > this.config.PingQueueSize)
                    RTTTimes.TryDequeue(out _);
            }
            CalcAverage();

            LastRTT = (ulong)nextRTT;
        }

        public void SendMessage()
        {
            var del = OnPingUpdated;
            del?.Invoke(new PingStatsPayload
            {
                AverageRTT = Convert.ToUInt64(AverageRTT),
                LastRTT = LastRTT,
            });
        }

        private void CalcAverage() => AverageRTT = RTTTimes.Average();
    }
}
