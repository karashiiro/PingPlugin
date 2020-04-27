using System.Numerics;
using Dalamud.Configuration;

namespace PingPlugin
{
    public class PingConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public Vector2 WindowPosition { get; set; }

        public PingConfiguration()
        {
            WindowPosition = new Vector2(300, 150);
        }
    }
}
