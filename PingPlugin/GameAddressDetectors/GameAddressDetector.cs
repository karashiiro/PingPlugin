using System.Net;
using System.Threading.Tasks;

namespace PingPlugin.GameAddressDetectors
{
    public abstract class GameAddressDetector
    {
        protected IPAddress Address { get; set; }

        public abstract Task<IPAddress> GetAddress(bool verbose = false);
    }
}