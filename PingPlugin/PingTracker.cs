using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin
{
    public class PingTracker : IDisposable
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly int pid;
        private readonly Ping ping;
        private readonly PingConfiguration config;

        // Everything we want the user to see
        public double AverageRTT { get; private set; }
        public IPAddress SeAddress { get; private set; }
        public long SeAddressRaw { get; private set; }
        public IPStatus LastStatus { get; private set; }
        public long LastRTT { get; private set; }
        public Queue<float> RTTTimes { get; }

        public PingTracker(PingConfiguration config)
        {
            this.tokenSource = new CancellationTokenSource();
            this.pid = Process.GetProcessesByName("ffxiv_dx11")[0].Id;
            this.ping = new Ping();
            this.config = config;

            UpdateSeAddress();
            
            RTTTimes = new Queue<float>(this.config.PingQueueSize);
            for (var i = 0; i < this.config.PingQueueSize; i++)
                RTTTimes.Enqueue(0);
            
            Task.Run(() => PingLoop(this.tokenSource.Token));
            Task.Run(() => CheckAddressLoop(this.tokenSource.Token));
        }

        private void NextRTTCalculation(long nextRTT)
        {
            lock (RTTTimes) // Not a huge fan of forcing the UI thread to wait for this, but ultimately it doesn't seem to have a notable effect on perf, so it's probably fine.
            {
                RTTTimes.Enqueue(nextRTT);
                
                while (RTTTimes.Count > this.config.PingQueueSize)
                    RTTTimes.Dequeue();
            }
            CalcAverage();

            LastRTT = nextRTT;
        }

        private void CalcAverage()
        {
            AverageRTT = RTTTimes.Average();
        }

        /*
         * This might be done instead of using the game packets for two reasons (if the first reason proves invalid, the old stuff is committed to roll back).
         * First, there doesn't seem to be a good pair of game packets to use, since the only packets that provide a good indication of latency are the
         * actor cast and skill packet pairs. Casts work well, but skills can obviously only be used on valid targets, making that method moot outside of
         * combat (maybe sensor fusion? seems overcomplicated). Ping packets seem to have little correlation to your actual ping, as their dTimes
         * tend to vary dramatically between exchanges. It's entirely possible that this so-called ping packet is really just a keepalive. There's a number of
         * actions that don't send responses, as well, including movement and chat. Besides all these, there are some other good pairs such as search info
         * settings, weapon draws/sheaths, etc., that could give a very accurate representation of latency, but they're also all actions that are only performed
         * rarely, even in aggregate.
         *
         * So, what's the other reason for doing it this way? No need to update opcodes ever lol.
         */
        private async Task PingLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                var pingReply = await this.ping.SendPingAsync(SeAddress);
                LastStatus = pingReply.Status;
                if (LastStatus == IPStatus.Success)
                    NextRTTCalculation(pingReply.RoundtripTime);
                await Task.Delay(3000, token);
            }
        }

        private async Task CheckAddressLoop(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                UpdateSeAddress();
                await Task.Delay(10000, token); // It's probably not that expensive, but it's not like the address is constantly changing, either.
            }
        }

        private void UpdateSeAddress()
        {
            SeAddressRaw = GetProcessHighestPortAddress(this.pid);
            SeAddress = new IPAddress(SeAddressRaw);
        }

        [DllImport("OSBindings.dll", EntryPoint = "#1")]
        private static extern long GetProcessHighestPortAddress(int pid);

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tokenSource.Cancel();
                this.tokenSource.Dispose();
                ping.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
