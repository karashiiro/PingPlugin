using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PingPlugin
{
    public class PingTracker
    {
        private readonly Stopwatch timer;

        private Queue<long> rttTimes = new Queue<long>();

        // Everything we want the user to see
        public double AverageRTT { get; private set; }
        public long LastRTT { get; private set; }

        public PingTracker()
        {
            this.timer = new Stopwatch();
        }

        public void StartNextRTTWait()
            => this.timer.Start();

        public void StartNextRTTCalculation()
        {
            var nextRTT = this.timer.ElapsedMilliseconds;

            rttTimes.Enqueue(nextRTT);
            CalcAverage();

            if (rttTimes.Count > 10)
                rttTimes.Dequeue();

            LastRTT = nextRTT;
            this.timer.Reset();
        }

        private void CalcAverage()
        {
            AverageRTT = rttTimes.Average();
        }
    }
}
