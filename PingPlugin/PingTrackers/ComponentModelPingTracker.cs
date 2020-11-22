using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class ComponentModelPingTracker : TcpTablePingTracker
    {
        private readonly Ping ping;

        public ComponentModelPingTracker(PingConfiguration config) : base(config)
        {
            this.ping = new Ping();
            Task.Run(() => PingLoop(TokenSource.Token));
        }

        private async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var pingReply = await this.ping.SendPingAsync(SeAddress);
                if (pingReply.Status == IPStatus.Success)
                    DoNextRTTCalculation(pingReply.RoundtripTime);
                await Task.Delay(3000, token);
            }

            token.ThrowIfCancellationRequested();
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
