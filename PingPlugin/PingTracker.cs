using System.Diagnostics;

namespace PingPlugin
{
    public class PingTracker
    {
        private readonly Stopwatch timer;

        private long n;

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
            RollAverageRTT(nextRTT);
            LastRTT = nextRTT;
            this.timer.Reset();
        }

        private void RollAverageRTT(double newValue)
        {
            AverageRTT = (n * AverageRTT + newValue) / (n - 1);
            n++;
        }
    }
}
