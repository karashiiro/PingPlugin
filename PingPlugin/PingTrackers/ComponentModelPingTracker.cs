using Dalamud.Game.ClientState;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class ComponentModelPingTracker : PingTracker
    {
        private readonly Ping ping;

        public ComponentModelPingTracker(PingConfiguration config, ClientState clientState) : base(config, clientState)
        {
            this.ping = new Ping();
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (SeAddress != null)
                {
                    var pingReply = await this.ping.SendPingAsync(SeAddress);
                    if (pingReply.Status == IPStatus.Success && pingReply.RoundtripTime > 0)
                        NextRTTCalculation((ulong)pingReply.RoundtripTime);
                    SendMessage();
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
