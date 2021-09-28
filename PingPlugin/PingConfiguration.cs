using CheapLoc;
using Dalamud.Configuration;
using Dalamud.Logging;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace PingPlugin
{
    public class PingConfiguration : IPluginConfiguration
    {
        public int Version { get; set; }

        public Vector2 GraphPosition { get; set; }
        public Vector2 MonitorPosition { get; set; }
        public float FontScale { get; set; }

        public float MonitorBgAlpha { get; set; }
        public Vector4 MonitorFontColor { get; set; }
        public Vector4 MonitorErrorFontColor { get; set; }

        public bool ClickThrough { get; set; }
        public bool GraphIsVisible { get; set; }
        public bool MonitorIsVisible { get; set; }
        public bool LockWindows { get; set; }
        public bool MinimalDisplay { get; set; }
        public bool MicroDisplayLast { get; set; } = true;
        public bool MicroDisplayAverage { get; set; }
        public DisplayMode DisplayMode { get; set; }
        public bool HideErrors { get; set; } // Generally, the errors are just timeouts, so you may want to hide them.
        public bool HideOverlaysDuringCutscenes { get; set; }
        public bool HideAveragePing { get; set; }
        public string Lang { get; set; }

        [JsonProperty]
        private bool PingPlugin17 { get; set; }

        [JsonIgnore]
        public LangKind RuntimeLang
        {
            get
            {
                _ = Enum.TryParse(Lang, out LangKind langKind);
                return langKind;
            }
            set
            {
                Lang = value.ToString();
                LoadLang();
            }
        }

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
            LoadLang();

            if (!PingPlugin17)
            {
                FontScale = 17.0f;
                PingPlugin17 = true;
            }
        }

        // Chances are the user doesn't expect the window positions to be reset with the other button, so we have a separate thingy instead.
        public void ResetWindowPositions()
        {
            GraphPosition = new Vector2(600, 150);
            MonitorPosition = new Vector2(300, 150);
        }

        public void RestoreDefaults()
        {
            FontScale = 17.0f;
            MonitorBgAlpha = 0.0f;
            MonitorFontColor = new Vector4(1, 1, 0, 1); // Yellow, it's ABGR instead of RGBA for some reason.
            MonitorErrorFontColor = new Vector4(1, 0, 0, 1);
            MonitorIsVisible = true;
            PingQueueSize = 20;
            Lang = LangKind.en.ToString();
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }

        private void LoadLang()
        {
            PluginLog.Log($"Loading lang data from PingPlugin.Lang.lang_{Lang}.json");
            using var langStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PingPlugin.Lang.lang_{Lang}.json");
            // ReSharper disable once AssignNullToNotNullAttribute
            using var langStreamReader = new StreamReader(langStream);
            Loc.Setup(langStreamReader.ReadToEnd());
        }
    }
}
