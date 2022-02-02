using Dalamud.Game.ClientState;
using Dalamud.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public abstract class PingTracker : IDisposable
    {
        private readonly CancellationTokenSource tokenSource;
        private readonly ClientState clientState;
        protected readonly PingConfiguration config;

        private uint LastDcId;

        public bool Verbose { get; set; } = true;
        public bool Reset { get; set; }
        public double AverageRTT { get; private set; }
        public IPAddress SeAddress { get; protected set; }
        public ulong LastRTT { get; protected set; }
        public ConcurrentQueue<float> RTTTimes { get; private set; }

        public delegate void PingUpdatedDelegate(PingStatsPayload payload);
        public event PingUpdatedDelegate OnPingUpdated;

        protected PingTracker(PingConfiguration config, ClientState clientState)
        {
            this.tokenSource = new CancellationTokenSource();
            this.config = config;
            this.clientState = clientState;

            SeAddress = IPAddress.Loopback;
            RTTTimes = new ConcurrentQueue<float>();

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

        protected void CalcAverage() => AverageRTT = RTTTimes.Average();

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
                UpdateSeAddress();
                if (!lastAddress.Equals(SeAddress))
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

        private void SendMessage()
        {
            var del = OnPingUpdated;
            del?.Invoke(new PingStatsPayload
            {
                AverageRTT = Convert.ToUInt64(AverageRTT),
                LastRTT = LastRTT,
            });
        }

        private void UpdateSeAddress()
        {
            if (!this.clientState.IsLoggedIn)
            {
                SeAddress = IPAddress.Loopback;
                return;
            }

            var dcId = this.clientState.LocalPlayer!.CurrentWorld.GameData.DataCenter.Row;
            if (dcId == LastDcId) return;
            LastDcId = dcId;

            /*
             * We might be able to read these from the game itself, but
             * I'm tired of maintaining struct offsets that change every
             * patch. If you know of a function that simply returns the
             * currently-connected IP address, feel free to PR!
             *
             * Historical notes:
             * Previously, I used the Windows TCP connection table to get
             * these, but for some reason this doesn't work in Wine or
             * CrossOver, or with gaming VPNs since these manipulate TCP
             * connections. To get around this, I used a game function that
             * sets the Sending/Receiving data in-game to get a struct that
             * had a value of roughly (2 x ping RTT), but this was annoying
             * to maintain and was also horribly inaccurate.
             */
            SeAddress = dcId switch
            {
                // I just copied these from https://is.xivup.com/adv
                1 => IPAddress.Parse("124.150.157.23"), // Elemental
                2 => IPAddress.Parse("124.150.157.36"), // Gaia
                3 => IPAddress.Parse("124.150.157.49"), // Mana
                4 => IPAddress.Parse("204.2.229.84"),   // Aether
                5 => IPAddress.Parse("204.2.229.95"),   // Primal
                6 => IPAddress.Parse("195.82.50.46"),   // Chaos
                7 => IPAddress.Parse("195.82.50.55"),   // Light
                8 => IPAddress.Parse("204.2.229.106"),  // Crystal
                9 => IPAddress.Parse("153.254.80.75"),  // Materia

                // If you have CN/KR DC IDs and IP addresses, feel free to PR them.
                // World server IP address are fine too, since worlds are hosted
                // alongside the lobby servers.

                _ => IPAddress.Loopback,
            };

            if (Verbose && !Equals(SeAddress, IPAddress.Loopback))
            {
                var dcName = this.clientState.LocalPlayer!.CurrentWorld.GameData.DataCenter.Value?.Name.RawString;
                PluginLog.Log($"Data center changed to {dcName}, using FFXIV server address {SeAddress}");
            }
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
