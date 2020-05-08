using ImGuiNET;
using PingPlugin.PingTrackers;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace PingPlugin
{
    public class PingUI
    {
        private readonly PingConfiguration config;
        private readonly IPingTracker pingTracker;

        private bool resettingGraphPos;
        private bool resettingMonitorPos;
        private bool configVisible;

        public bool ConfigVisible
        {
            get => this.configVisible;
            set => this.configVisible = value;
        }
        public bool CutsceneActive { get; set; }

        public PingUI(IPingTracker pingTracker, PingConfiguration config)
        {
            this.config = config;
            this.pingTracker = pingTracker;
        }

        public void BuildUi()
        {
            if (this.config.HideOverlaysDuringCutscenes && CutsceneActive)
                return;

            if (this.ConfigVisible) DrawConfigUi();
            if (this.config.GraphIsVisible) DrawGraph();
            if (this.config.MonitorIsVisible) DrawMonitor();
        }

        private void DrawConfigUi()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 305), ImGuiCond.Always);

            ImGui.Begin("PingPlugin Configuration", ref this.configVisible, ImGuiWindowFlags.NoResize);
            var lockWindows = this.config.LockWindows;
            if (ImGui.Checkbox(Properties.Lang.LockPluginWindows, ref lockWindows))
            {
                this.config.LockWindows = lockWindows;
                this.config.Save();
            }

            var clickThrough = this.config.ClickThrough;
            if (ImGui.Checkbox(Properties.Lang.ClickThrough, ref clickThrough))
            {
                this.config.ClickThrough = clickThrough;
                this.config.Save();
            }

            var hideDuringCutscences = this.config.HideOverlaysDuringCutscenes;
            if (ImGui.Checkbox(Properties.Lang.HideOverlaysDuringCutscenes, ref hideDuringCutscences))
            {
                this.config.HideOverlaysDuringCutscenes = hideDuringCutscences;
                this.config.Save();
            }

            var minimalDisplay = this.config.MinimalDisplay;
            if (ImGui.Checkbox(Properties.Lang.MinimalDisplay, ref minimalDisplay))
            {
                this.config.MinimalDisplay = minimalDisplay;
                this.config.Save();
            }

            var hideErrors = this.config.HideErrors;
            if (ImGui.Checkbox(Properties.Lang.HideErrors, ref hideErrors))
            {
                this.config.HideErrors = hideErrors;
                this.config.Save();
            }

            var queueSize = this.config.PingQueueSize;
            if (ImGui.InputInt(Properties.Lang.RecordedPings, ref queueSize))
            {
                this.config.PingQueueSize = queueSize;
                this.config.Save();
            }

            var monitorColor = this.config.MonitorFontColor;
            if (ImGui.ColorEdit4(Properties.Lang.MonitorColor, ref monitorColor))
            {
                this.config.MonitorFontColor = monitorColor;
                this.config.Save();
            }

            var monitorErrorColor = this.config.MonitorErrorFontColor;
            if (ImGui.ColorEdit4(Properties.Lang.MonitorErrorColor, ref monitorErrorColor))
            {
                this.config.MonitorErrorFontColor = monitorErrorColor;
                this.config.Save();
            }

            var monitorBgAlpha = this.config.MonitorBgAlpha;
            if (ImGui.SliderFloat(Properties.Lang.MonitorOpacity, ref monitorBgAlpha, 0.0f, 1.0f))
            {
                this.config.MonitorBgAlpha = monitorBgAlpha;
                this.config.Save();
            }

            if (ImGui.Button(Properties.Lang.Defaults))
            {
                this.config.RestoreDefaults();
                this.config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button(Properties.Lang.ResetWindowPositions))
            {
                this.config.ResetWindowPositions();
                this.resettingGraphPos = true;
                this.resettingMonitorPos = true;
            }
            ImGui.End();
        }

        private void DrawMonitor()
        {
            var windowFlags = BuildWindowFlags(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.SetNextWindowPos(this.config.MonitorPosition, ImGuiCond.FirstUseEver);

            var x = this.config.MinimalDisplay ? 206 : 186;
            var y = 33;
            if (!this.config.MinimalDisplay)
                y = 75;
            if (this.pingTracker.LastError != WinError.NO_ERROR)
                y += 25;
            ImGui.SetNextWindowSize(new Vector2(x, y), ImGuiCond.Always);

            ImGui.SetNextWindowBgAlpha(this.config.MonitorBgAlpha);

            ImGui.Begin("PingMonitor", windowFlags);
            if (this.resettingMonitorPos)
            {
                ImGui.SetWindowPos(this.config.MonitorPosition);
                this.resettingMonitorPos = false;
            }

            ImGui.TextColored(this.config.MonitorFontColor, this.config.MinimalDisplay
                ? string.Format(Properties.Lang.UIMinimalDisplay, this.pingTracker.LastRTT,
                    Math.Round(this.pingTracker.AverageRTT, 2))
                : string.Format(Properties.Lang.UIRegularDisplay, this.pingTracker.SeAddress, this.pingTracker.LastRTT, Math.Round(this.pingTracker.AverageRTT, 2)));
            if (this.pingTracker.LastError != WinError.NO_ERROR)
                ImGui.TextColored(this.config.MonitorErrorFontColor, string.Format(Properties.Lang.UIError, (Enum.IsDefined(typeof(WinError), this.pingTracker.LastError) ? this.pingTracker.LastError.ToString() : ((int)this.pingTracker.LastError).ToString())));
            ImGui.End();

            ImGui.PopStyleVar();
        }

        private void DrawGraph()
        {
            var windowFlags = BuildWindowFlags(ImGuiWindowFlags.NoResize);

            ImGui.SetNextWindowPos(this.config.GraphPosition, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(367, 210), ImGuiCond.Always);

            ImGui.Begin("Ping Graph", windowFlags);
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

                const int lowY = 383;
                const int highY = 239;
                var avgY = lowY - Rescale((float)this.pingTracker.AverageRTT, max, min, graphSize.Y);

                ImGui.Text("                      " + Properties.Lang.UIGraphTitle);
                ImGui.PlotLines(string.Empty, ref pingArray[0], pingArray.Length, 0, null,
                    float.MaxValue, float.MaxValue, graphSize);

                if (!ImGui.IsWindowCollapsed())
                {
                    var lowLineStart = new Vector2(16, lowY);
                    var lowLineEnd = new Vector2(16 + graphSize.X, lowY);
                    ImGui.GetWindowDrawList().AddLine(lowLineStart, lowLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(lowLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), min.ToString(CultureInfo.CurrentUICulture) + Properties.Lang.UIMillisecondAbbr);

                    var avgLineStart = new Vector2(16, avgY);
                    var avgLineEnd = new Vector2(16 + graphSize.X, avgY);
                    ImGui.GetWindowDrawList().AddLine(avgLineStart, avgLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(avgLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), Math.Round(this.pingTracker.AverageRTT, 2).ToString(CultureInfo.CurrentUICulture) + Properties.Lang.UIMillisecondAbbr);
                    ImGui.GetWindowDrawList().AddText(avgLineEnd - new Vector2(270, 18), ImGui.GetColorU32(ImGuiCol.Text), Properties.Lang.UIAverage);

                    var highLineStart = new Vector2(16, highY);
                    var highLineEnd = new Vector2(16 + graphSize.X, highY);
                    ImGui.GetWindowDrawList()
                        .AddLine(highLineStart, highLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(highLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), max.ToString(CultureInfo.CurrentUICulture) + Properties.Lang.UIMillisecondAbbr);
                }
            }
            else
                ImGui.Text(Properties.Lang.UINoData);
            ImGui.End();
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
    }
}
