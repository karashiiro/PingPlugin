using CheapLoc;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using PingPlugin.PingTrackers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface.Windowing;

namespace PingPlugin
{
    public class PingUI : IDisposable
    {
        private readonly UiBuilder uiBuilder;
        private readonly DtrBarEntry dtrEntry;
        private readonly DalamudPluginInterface pluginInterface;
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

        public PingUI(PingTracker pingTracker, DalamudPluginInterface pluginInterface, DtrBar dtrBar, PingConfiguration config)
        {
            this.config = config;
            this.uiBuilder = pluginInterface.UiBuilder;
            this.pingTracker = pingTracker;
            this.pluginInterface = pluginInterface;

            var dtrBarTitle = "Ping";
            try
            {
                this.dtrEntry = dtrBar.Get(dtrBarTitle);
            }
            catch (ArgumentException e)
            {
                // This usually only runs once after any given plugin reload
                for (var i = 0; i < 5; i++)
                {
                    PluginLog.LogError(e, $"Failed to acquire DtrBarEntry {dtrBarTitle}, trying {dtrBarTitle}{i}");
                    try
                    {
                        this.dtrEntry = dtrBar.Get(dtrBarTitle + i);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    break;
                }
            }

            if (this.dtrEntry != null)
            {
                this.dtrEntry.Text = "Pinging...";
                this.dtrEntry.Shown = false;
            }

            this.uiBuilder.BuildFonts += BuildFont;
#if DEBUG
            ConfigVisible = true;
#endif
        }

        public void Draw()
        {
            var serverBarShown = this.config.DisplayMode == DisplayMode.ServerBar;
            if (this.dtrEntry != null && this.dtrEntry.Shown != serverBarShown)
            {
                this.dtrEntry.Shown = serverBarShown;
            }

            if (this.config.HideOverlaysDuringCutscenes && CutsceneActive)
                return;

            if (!this.fontLoaded)
            {
                this.uiBuilder.RebuildFonts();
                return;
            }

            if (ConfigVisible) DrawConfigUi();

            if (this.uiFont.IsLoaded()) ImGui.PushFont(this.uiFont);
            if (this.config.GraphIsVisible) DrawGraph();
            if (!serverBarShown && this.config.MonitorIsVisible) DrawMonitor();
            if (this.uiFont.IsLoaded()) ImGui.PopFont();
        }

        private bool fontScaleTooSmall;
        private void DrawConfigUi()
        {
            ImGui.Begin($"{Loc.Localize("ConfigurationWindowTitle", string.Empty)}###PingPluginConfiguration",
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

            ImGui.Spacing();

            var displayModes = Enum.GetNames<DisplayMode>();
            var displayModeIndex = (int)this.config.DisplayMode;
            if (ImGui.Combo(Loc.Localize("DisplayMode", string.Empty),
                    ref displayModeIndex, DisplayModeNames.Names(), displayModes.Length))
            {
                this.config.DisplayMode = (DisplayMode)displayModeIndex;
                this.config.Save();
            }

            switch (this.config.DisplayMode)
            {
                case DisplayMode.Default:
                    var hideAveragePing = this.config.HideAveragePing;
                    if (ImGui.Checkbox(Loc.Localize("HideAveragePing", string.Empty), ref hideAveragePing))
                    {
                        this.config.HideAveragePing = hideAveragePing;
                        this.config.Save();
                    }
                    break;
                case DisplayMode.Micro:
                    var microDisplayLast = this.config.MicroDisplayLast;
                    if (ImGui.Checkbox(Loc.Localize("MicroShowLastPing", string.Empty), ref microDisplayLast))
                    {
                        this.config.MicroDisplayLast = microDisplayLast;
                        this.config.Save();
                    }

                    var microDisplayAverage = this.config.MicroDisplayAverage;
                    if (ImGui.Checkbox(Loc.Localize("MicroShowAveragePing", string.Empty), ref microDisplayAverage))
                    {
                        this.config.MicroDisplayAverage = microDisplayAverage;
                        this.config.Save();
                    }
                    break;
                case DisplayMode.Minimal:
                    break;
                case DisplayMode.ServerBar:
                    // We're reusing localization keys here since it's all the same text
                    var serverBarDisplayLast = this.config.ServerBarDisplayLast;
                    if (ImGui.Checkbox(Loc.Localize("MicroShowLastPing", string.Empty), ref serverBarDisplayLast))
                    {
                        this.config.ServerBarDisplayLast = serverBarDisplayLast;
                        this.pingTracker.ForceSendMessage();
                        this.config.Save();
                    }

                    var serverBarDisplayAverage = this.config.ServerBarDisplayAverage;
                    if (ImGui.Checkbox(Loc.Localize("MicroShowAveragePing", string.Empty), ref serverBarDisplayAverage))
                    {
                        this.config.ServerBarDisplayAverage = serverBarDisplayAverage;
                        this.pingTracker.ForceSendMessage();
                        this.config.Save();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ImGui.Spacing();

            var queueSize = this.config.PingQueueSize;
            if (ImGui.InputInt(Loc.Localize("RecordedPings", string.Empty), ref queueSize))
            {
                this.config.PingQueueSize = Math.Max(queueSize, 1);
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

            var monitorErrorColor = this.config.MonitorErrorFontColor;
            if (ImGui.ColorEdit4(Loc.Localize("MonitorErrorColor", string.Empty), ref monitorErrorColor))
            {
                this.config.MonitorErrorFontColor = monitorErrorColor;
                this.config.Save();
            }

            var monitorBgAlpha = this.config.MonitorBgAlpha;
            if (ImGui.SliderFloat(Loc.Localize("MonitorOpacity", string.Empty), ref monitorBgAlpha, 0.0f, 1.0f))
            {
                this.config.MonitorBgAlpha = monitorBgAlpha;
                this.config.Save();
            }

            var currentItem = (int)this.config.RuntimeLang;
            var supportedLanguages = new[] { "English", "日本語", "Español", "Deutsch", "Français",  /*"中文"*/ };
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

            switch (this.config.DisplayMode)
            {
                case DisplayMode.Minimal:
                case DisplayMode.Default:
                    DrawFullMonitor();
                    break;
                case DisplayMode.Micro:
                    DrawMicroMonitor();
                    break;
                case DisplayMode.ServerBar:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ImGui.End();

            ImGui.PopStyleVar();
        }

        private void DrawFullMonitor()
        {
            string formatString;
            var formatParameters = new List<object>();
            if (this.config.MinimalDisplay)
            {
                formatString = Loc.Localize("UIMinimalDisplay" + (this.config.HideAveragePing ? "NoAverage" : ""), string.Empty);
                formatParameters.Add(this.pingTracker.LastRTT);
            }
            else
            {
                formatString = Loc.Localize("UIRegularDisplay" + (this.config.HideAveragePing ? "NoAverage" : ""), string.Empty);
                formatParameters.Add(this.pingTracker.SeAddress);
                formatParameters.Add(this.pingTracker.LastRTT);
            }

            if (!this.config.HideAveragePing)
                formatParameters.Add(Math.Round(this.pingTracker.AverageRTT, 2));

            ImGui.TextColored(this.config.MonitorFontColor, string.Format(CultureInfo.CurrentUICulture, formatString, formatParameters.ToArray()));
        }

        private void DrawMicroMonitor()
        {
            var text = "";

            if (this.config.MicroDisplayLast)
            {
                text = $"{this.pingTracker.LastRTT}ms";
                if (this.config.MicroDisplayAverage)
                {
                    text += "/";
                }
            }

            if (this.config.MicroDisplayAverage)
            {
                text += $"{Math.Ceiling(this.pingTracker.AverageRTT)}ms";
            }

            ImGui.TextColored(this.config.MonitorFontColor, text);
        }

        public void UpdateDtrBarPing(PingStatsPayload payload)
        {
            if (this.dtrEntry is not { Shown: true }) return;

            var text = "";

            if (this.config.ServerBarDisplayLast)
            {
                text = $"{payload.LastRTT}ms";
                if (this.config.ServerBarDisplayAverage)
                {
                    text += "/";
                }
            }

            if (this.config.ServerBarDisplayAverage)
            {
                text += $"{payload.AverageRTT}ms";
            }

            this.dtrEntry.Text = text;
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
                var filePath = Path.Combine(this.pluginInterface.DalamudAssetDirectory.FullName, "UIRes", "NotoSansCJKjp-Medium.otf");
                if (!File.Exists(filePath)) throw new FileNotFoundException("Font file not found!");

                var fontPx = Math.Min(Math.Max(8, this.config.FontScale), 128);

                {
                    this.uiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(filePath, fontPx, null, ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
                }
            }
            catch (Exception e)
            {
                PluginLog.LogError(e.Message);
            }

            this.fontLoaded = true;
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
            GC.SuppressFinalize(this);
            this.uiBuilder.BuildFonts -= BuildFont;
            this.dtrEntry?.Dispose();
        }
    }
}
