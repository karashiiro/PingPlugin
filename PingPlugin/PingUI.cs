using CheapLoc;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PingPlugin
{
    public class PingUI : IDisposable
    {
        private readonly UiBuilder uiBuilder;
        private readonly PingConfiguration config;
        private readonly PingTracker pingTracker;

        private bool resettingGraphPos;
        private bool resettingMonitorPos;
        private bool configVisible;

        private bool fontLoaded;
        private ImFontPtr uiFont;

        public bool CutsceneActive { get; set; }

        public bool ConfigVisible
        {
            get => this.configVisible;
            set => this.configVisible = value;
        }

        public PingUI(PingTracker pingTracker, UiBuilder uiBuilder, PingConfiguration config)
        {
            this.config = config;
            this.uiBuilder = uiBuilder;
            this.pingTracker = pingTracker;

            this.uiBuilder.OnBuildFonts += BuildFont;
#if DEBUG
            ConfigVisible = true;
#endif
        }

        public void BuildUi()
        {
            if (this.config.HideOverlaysDuringCutscenes && CutsceneActive)
                return;

            if (!this.fontLoaded)
            {
                this.uiBuilder.RebuildFonts();
                return;
            }

            ImGui.PushFont(this.uiFont);

            if (ConfigVisible) DrawConfigUi();
            if (this.config.GraphIsVisible) DrawGraph();
            if (this.config.MonitorIsVisible) DrawMonitor();

            ImGui.PopFont();
        }

        private bool fontScaleTooSmall;
        private void DrawConfigUi()
        {
            ImGui.Begin($"{Loc.Localize("ConfigurationWindowTitle", string.Empty)}##PingPlugin Configuration",
                            ref this.configVisible,
                            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
            var lockWindows = this.config.LockWindows;
            if (ImGui.Checkbox(Loc.Localize("LockPluginWindows", string.Empty), ref lockWindows))
            {
                this.config.LockWindows = lockWindows;
                this.config.Save();
            }

            var clickThrough = this.config.ClickThrough;
            if (ImGui.Checkbox(Loc.Localize("ClickThrough", string.Empty), ref clickThrough))
            {
                this.config.ClickThrough = clickThrough;
                this.config.Save();
            }

            var minimalDisplay = this.config.MinimalDisplay;
            if (ImGui.Checkbox(Loc.Localize("MinimalDisplay", string.Empty), ref minimalDisplay))
            {
                this.config.MinimalDisplay = minimalDisplay;
                this.config.Save();
            }

            var queueSize = this.config.PingQueueSize;
            if (ImGui.InputInt(Loc.Localize("RecordedPings", string.Empty), ref queueSize))
            {
                this.config.PingQueueSize = queueSize;
                this.config.Save();
            }

            if (this.fontScaleTooSmall)
            {
                this.uiBuilder.RebuildFonts();
                this.fontScaleTooSmall = false;
            }
            var fontScale = (int)this.config.FontScale;
            if (ImGui.InputInt(Loc.Localize("FontScale", string.Empty), ref fontScale))
            {
                this.config.FontScale = fontScale;
                this.config.Save();
                if (this.config.FontScale >= 8)
                {
                    this.uiBuilder.RebuildFonts();
                }
                else
                {
                    this.fontScaleTooSmall = true;
                }
                this.config.FontScale = Math.Max(8f, fontScale);
            }

            var monitorColor = this.config.MonitorFontColor;
            if (ImGui.ColorEdit4(Loc.Localize("MonitorColor", string.Empty), ref monitorColor))
            {
                this.config.MonitorFontColor = monitorColor;
                this.config.Save();
            }

            var monitorBgAlpha = this.config.MonitorBgAlpha;
            if (ImGui.SliderFloat(Loc.Localize("MonitorOpacity", string.Empty), ref monitorBgAlpha, 0.0f, 1.0f))
            {
                this.config.MonitorBgAlpha = monitorBgAlpha;
                this.config.Save();
            }

            var currentItem = (int)this.config.RuntimeLang;
            var supportedLanguages = new[] { Loc.Localize("English", string.Empty), Loc.Localize("Japanese", string.Empty), Loc.Localize("Spanish", string.Empty), Loc.Localize("German", string.Empty), /*Loc.Localize("Chinese", string.Empty)*/ };
            if (ImGui.Combo(Loc.Localize("Language", string.Empty), ref currentItem, supportedLanguages, supportedLanguages.Length))
            {
                this.config.RuntimeLang = (LangKind)currentItem;
                this.config.Save();
            }

            if (ImGui.Button(Loc.Localize("Defaults", string.Empty)))
            {
                this.config.RestoreDefaults();
                this.config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button(Loc.Localize("ResetWindowPositions", string.Empty)))
            {
                this.config.ResetWindowPositions();
                this.resettingGraphPos = true;
                this.resettingMonitorPos = true;
            }
            ImGui.End();
        }

        private void DrawMonitor()
        {
            var windowFlags = BuildWindowFlags(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.SetNextWindowPos(this.config.MonitorPosition, ImGuiCond.FirstUseEver);

            ImGui.SetNextWindowBgAlpha(this.config.MonitorBgAlpha);

            ImGui.Begin("PingMonitor", windowFlags);
            if (this.resettingMonitorPos)
            {
                ImGui.SetWindowPos(this.config.MonitorPosition);
                this.resettingMonitorPos = false;
            }

            ImGui.TextColored(this.config.MonitorFontColor, this.config.MinimalDisplay
                ? string.Format(CultureInfo.CurrentUICulture, Loc.Localize("UIMinimalDisplay", string.Empty), this.pingTracker.LastRTT,
                    Math.Round(this.pingTracker.AverageRTT, 2))
                : string.Format(CultureInfo.CurrentUICulture, Loc.Localize("UIRegularDisplay", string.Empty), this.pingTracker.LastRTT, Math.Round(this.pingTracker.AverageRTT, 2)));
            ImGui.End();

            ImGui.PopStyleVar();
        }

        private void DrawGraph()
        {
            var windowFlags = BuildWindowFlags(ImGuiWindowFlags.NoResize);

            const float positionScaleFactor = 0.79f;

            ImGui.SetNextWindowPos(this.config.GraphPosition, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(350 + this.config.FontScale * (1.5f / positionScaleFactor), 185 + this.config.FontScale * positionScaleFactor), ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowTitleAlign, new Vector2(0.5f, 0.5f));
            ImGui.Begin($"{Loc.Localize("UIGraphTitle", string.Empty)}##Ping Graph", windowFlags);
            ImGui.PopStyleVar();

            if (this.resettingGraphPos)
            {
                ImGui.SetWindowPos(this.config.GraphPosition);
                this.resettingGraphPos = false;
            }

            var pingArray = this.pingTracker.RTTTimes.ToArray();
            if (pingArray.Length > 0)
            {
                var graphSize = new Vector2(300, 150);

                var max = this.pingTracker.RTTTimes.Max();
                var min = this.pingTracker.RTTTimes.Min();

                const int beginX = 8;
                var lowY = 166 + this.config.FontScale * positionScaleFactor;
                var highY = 22 + this.config.FontScale * positionScaleFactor;
                var avgY = lowY - Rescale((float)this.pingTracker.AverageRTT, max, min, graphSize.Y);

                ImGui.PlotLines(string.Empty, ref pingArray[0], pingArray.Length, 0, null,
                    float.MaxValue, float.MaxValue, graphSize);

                if (!ImGui.IsWindowCollapsed())
                {
                    var lowLineStart = ImGui.GetWindowPos() + new Vector2(beginX, lowY);
                    var lowLineEnd = ImGui.GetWindowPos() + new Vector2(beginX + graphSize.X, lowY);
                    ImGui.GetWindowDrawList().AddLine(lowLineStart, lowLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(
                        lowLineEnd - new Vector2(0, this.config.FontScale / 2),
                        ImGui.GetColorU32(ImGuiCol.Text),
                        min.ToString(CultureInfo.CurrentUICulture) + Loc.Localize("UIMillisecondAbbr", string.Empty));

                    var avgLineStart = ImGui.GetWindowPos() + new Vector2(beginX, avgY);
                    var avgLineEnd = ImGui.GetWindowPos() + new Vector2(beginX + graphSize.X, avgY);
                    ImGui.GetWindowDrawList().AddLine(avgLineStart, avgLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(
                        avgLineEnd - new Vector2(0, this.config.FontScale / 2),
                        ImGui.GetColorU32(ImGuiCol.Text),
                        Math.Round(this.pingTracker.AverageRTT, 2).ToString(CultureInfo.CurrentUICulture) + Loc.Localize("UIMillisecondAbbr", string.Empty));
                    ImGui.GetWindowDrawList().AddText(
                        avgLineEnd - new Vector2(270, 18),
                        ImGui.GetColorU32(ImGuiCol.Text),
                        Loc.Localize("UIAverage", string.Empty));

                    var highLineStart = ImGui.GetWindowPos() + new Vector2(beginX, highY);
                    var highLineEnd = ImGui.GetWindowPos() + new Vector2(beginX + graphSize.X, highY);
                    ImGui.GetWindowDrawList()
                        .AddLine(highLineStart, highLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(
                        highLineEnd - new Vector2(0, this.config.FontScale / 2),
                        ImGui.GetColorU32(ImGuiCol.Text),
                        max.ToString(CultureInfo.CurrentUICulture) + Loc.Localize("UIMillisecondAbbr", string.Empty));
                }
            }
            else
                ImGui.Text(Loc.Localize("UINoData", string.Empty));
            ImGui.End();
        }

        private void BuildFont()
        {
            try
            {
                var filePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "addon", "Hooks",
                    "UIRes", "NotoSansCJKjp-Medium.otf");
                if (!File.Exists(filePath)) throw new FileNotFoundException("Font file not found!");
                var jpRangeHandle = GCHandle.Alloc(GlyphRangesJapanese.GlyphRanges, GCHandleType.Pinned);
                this.uiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(filePath, Math.Max(8, this.config.FontScale), null, jpRangeHandle.AddrOfPinnedObject());
                this.fontLoaded = true;
                jpRangeHandle.Free();
            }
            catch (Exception e)
            {
                PluginLog.LogError(e.Message);
                this.fontLoaded = false;
            }
        }

        private ImGuiWindowFlags BuildWindowFlags(ImGuiWindowFlags flags)
        {
            if (this.config.ClickThrough)
                flags |= ImGuiWindowFlags.NoInputs;
            if (this.config.LockWindows)
                flags |= ImGuiWindowFlags.NoMove;
            return flags;
        }

        private static float Rescale(float input, float max, float min, float scaleFactor)
            => (input - min) / (max - min) * scaleFactor;

        public void Dispose()
        {
            this.uiBuilder.OnBuildFonts -= BuildFont;
            this.uiFont.Destroy();
            this.uiBuilder.RebuildFonts();
        }
    }
}
