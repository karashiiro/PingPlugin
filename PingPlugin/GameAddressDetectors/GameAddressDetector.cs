using System.Net;

namespace PingPlugin.GameAddressDetectors
{
    public abstract class GameAddressDetector
    {
        public bool Verbose { get; set; } = true;

        protected IPAddress Address { get; set; }

        public abstract IPAddress GetAddress();
    }
}