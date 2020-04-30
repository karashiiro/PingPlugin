using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PingPlugin
{
    public class PingTracker
    {
        private readonly PingConfiguration config;
        private readonly Stopwatch timer;

        // Everything we want the user to see
        public double AverageRTT { get; private set; }
        public long LastRTT { get; private set; }
        public Queue<float> RTTTimes { get; set; }

        public PingTracker(PingConfiguration config)
        {
            this.config = config;
            this.timer = new Stopwatch();

            RTTTimes = new Queue<float>();
        }

        public void StartNextRTTWait()
            => this.timer.Start();

        public void StartNextRTTCalculation()
        {
            var nextRTT = this.timer.ElapsedMilliseconds;

            RTTTimes.Enqueue(nextRTT);
            CalcAverage();

            while (RTTTimes.Count > this.config.PingQueueSize)
                RTTTimes.Dequeue();

            LastRTT = nextRTT;
            this.timer.Reset();
        }

        private void CalcAverage()
        {
            AverageRTT = RTTTimes.Average();
        }
    }
}
