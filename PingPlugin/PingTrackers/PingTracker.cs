using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace PingPlugin.PingTrackers
{
    public abstract class PingTracker : IDisposable
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly GameAddressDetector addressDetector;
        protected readonly PingConfiguration config;
        private readonly IPluginLog pluginLog;

        public PingTrackerKind Kind { get; }
        public bool Verbose { get; set; } = true;
        public bool Errored { get; protected set; }
        public bool Reset { get; set; }
        public double AverageRTT { get; private set; }
        public IPAddress SeAddress { get; protected set; }
        public ulong LastRTT { get; protected set; }
        public ConcurrentQueue<float> RTTTimes { get; private set; }

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        protected PingTracker(PingConfiguration config, GameAddressDetector addressDetector, PingTrackerKind kind, IPluginLog pluginLog)
        {
            this.tokenSource = new CancellationTokenSource();
            this.config = config;
            this.addressDetector = addressDetector;
            this.pluginLog = pluginLog;

            SeAddress = IPAddress.Loopback;
            RTTTimes = new ConcurrentQueue<float>();
            Kind = kind;
        }

        public virtual void Start()
        {
            Task.Run(() => AddressUpdateLoop(this.tokenSource.Token));
            Task.Run(() => PingLoop(this.tokenSource.Token));
        }

        protected void NextRTTCalculation(ulong nextRTT)
        {
            lock (RTTTimes)
            {
                RTTTimes.Enqueue(nextRTT);

                while (RTTTimes.Count > this.config.PingQueueSize)
                    RTTTimes.TryDequeue(out _);
            }
            CalcAverage();

            LastRTT = nextRTT;
            SendMessage();
        }

        protected void CalcAverage() => AverageRTT = RTTTimes.Count > 1 ? RTTTimes.Average() : 0;

        protected virtual void ResetRTT()
        {
            RTTTimes = new ConcurrentQueue<float>();
            AverageRTT = 0;
            LastRTT = 0;
        }

        protected abstract Task PingLoop(CancellationToken token);

        private async Task AddressUpdateLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var lastAddress = SeAddress;

                try
                {
                    SeAddress = this.addressDetector.GetAddress(Verbose);
                }
                catch (Exception e)
                {
                    pluginLog.Error(e, "Exception thrown in address detection function.");
                }

                if (!Equals(lastAddress, SeAddress))
                {
                    Reset = true;
                    ResetRTT();
                }
                else
                {
                    Reset = false;
                }
                await Task.Delay(10000, token); // It's probably not that expensive, but it's not like the address is constantly changing, either.
            }
        }

        public void ForceSendMessage() => SendMessage();

        private void SendMessage()
        {
            var del = OnPingUpdated;
            del?.Invoke(new PingStatsPayload
            {
                AverageRTT = Convert.ToUInt64(AverageRTT),
                LastRTT = LastRTT,
            });
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tokenSource.Cancel();
                this.tokenSource.Dispose();
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
