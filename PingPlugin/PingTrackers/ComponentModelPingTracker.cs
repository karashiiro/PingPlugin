using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;
using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace PingPlugin.PingTrackers
{
    public class ComponentModelPingTracker : PingTracker
    {
        private readonly Ping ping;
        private readonly IPluginLog pluginLog;

        public ComponentModelPingTracker(PingConfiguration config, GameAddressDetector addressDetector, IPluginLog pluginLog) : base(config, addressDetector, PingTrackerKind.COM, pluginLog)
        {
            this.ping = new Ping();
            this.pluginLog = pluginLog;
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
                        else if (pingReply.Status != IPStatus.TimedOut)
                        {
                            pluginLog.Warning(
                                $"Got bad status {pingReply.Status} when executing ping - this may be temporary and acceptable.");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // ignored
                    }
                    catch (Exception e)
                    {
                        Errored = true;
                        pluginLog.Error(e, "Error occurred when executing ping.");
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
