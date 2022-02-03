using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;
using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class ComponentModelPingTracker : PingTracker
    {
        private readonly Ping ping;

        public ComponentModelPingTracker(PingConfiguration config, GameAddressDetector addressDetector) : base(config, addressDetector)
        {
            this.ping = new Ping();
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (SeAddress != null)
                {
                    try
                    {
                        var pingReply = await this.ping.SendPingAsync(SeAddress);

                        Errored = pingReply.Status != IPStatus.Success;

                        if (!Errored)
                        {
                            NextRTTCalculation((ulong)pingReply.RoundtripTime);
                        }
                        else
                        {
                            PluginLog.LogWarning($"Got bad status {pingReply.Status} when executing ping - this may be temporary and acceptable.");
                        }
                    }
                    catch (Exception e)
                    {
                        Errored = true;
                        PluginLog.LogError(e, "Error occurred when executing ping.");
                    }
                }

                await Task.Delay(3000, token);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.ping.Dispose();
                base.Dispose(true);
            }
        }
    }
}
