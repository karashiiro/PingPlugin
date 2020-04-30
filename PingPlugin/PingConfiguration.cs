using System.Numerics;
using Dalamud.Configuration;

namespace PingPlugin
{
    public class PingConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }
        public Vector2 GraphPosition { get; set; }
        public Vector2 MonitorPosition { get; set; }

        public PingConfiguration()
        {
            GraphPosition = new Vector2(600, 150);
            MonitorPosition = new Vector2(300, 150);
        }
    }
}
