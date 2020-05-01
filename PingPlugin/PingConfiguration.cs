using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PingPlugin
{
    public class PingConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }
        
        public Vector2 GraphPosition { get; set; }
        public Vector2 MonitorPosition { get; set; }

        public float MonitorBgAlpha { get; set; }
        public Vector4 MonitorFontColor { get; set; }
        public Vector4 MonitorErrorFontColor { get; set; }

        public bool ClickThrough { get; set; }
        public bool GraphIsVisible { get; set; }
        public bool MonitorIsVisible { get; set; }
        public bool LockWindows { get; set; }
        public bool MinimalDisplay { get; set; }

        public int PingQueueSize { get; set; }

        public PingConfiguration()
        {
            ResetWindowPositions();
            RestoreDefaults();
        }

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        // Chances are the user doesn't expect the window positions to be reset with the other button, so we have a separate thingy instead.
        public void ResetWindowPositions()
        {
            GraphPosition = new Vector2(600, 150);
            MonitorPosition = new Vector2(300, 150);
        }

        public void RestoreDefaults()
        {
            MonitorBgAlpha = 0.0f;
            MonitorFontColor = new Vector4(1, 1, 0, 1); // Yellow, it's ABGR instead of RGBA for some reason.
            MonitorErrorFontColor = new Vector4(1, 0, 0, 1);
            MonitorIsVisible = true;
            PingQueueSize = 20;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
