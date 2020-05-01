using System;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using ImGuiNET;

namespace PingPlugin
{
    public class PingUI
    {
        private readonly PingConfiguration config;
        private readonly PingTracker pingTracker;

        private bool resettingGraphPos;
        private bool resettingMonitorPos;
        private bool configVisible;

        public bool ConfigVisible
        {
            get => this.configVisible;
            set => this.configVisible = value;
        }

        public PingUI(PingTracker pingTracker, PingConfiguration config)
        {
            this.config = config;
            this.pingTracker = pingTracker;
        }

        public void BuildUi()
        {
            if (this.ConfigVisible) DrawConfigUi();
            if (this.config.GraphIsVisible) DrawGraph();
            if (this.config.MonitorIsVisible) DrawMonitor();
        }

        private void DrawConfigUi()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 230), ImGuiCond.Always);

            ImGui.Begin("PingPlugin Configuration", ref this.configVisible, ImGuiWindowFlags.NoResize);
            var lockWindows = this.config.LockWindows;
            if (ImGui.Checkbox("Lock plugin windows", ref lockWindows))
            {
                this.config.LockWindows = lockWindows;
                this.config.Save();
            }

            var clickThrough = this.config.ClickThrough;
            if (ImGui.Checkbox("Click through plugin windows", ref clickThrough))
            {
                this.config.ClickThrough = clickThrough;
                this.config.Save();
            }

            var queueSize = this.config.PingQueueSize;
            if (ImGui.InputInt("Ping queue size", ref queueSize))
            {
                this.config.PingQueueSize = queueSize;
                this.config.Save();
            }

            var monitorColor = this.config.MonitorFontColor;
            if (ImGui.ColorEdit4("Monitor Color", ref monitorColor))
            {
                this.config.MonitorFontColor = monitorColor;
                this.config.Save();
            }

            var monitorErrorColor = this.config.MonitorErrorFontColor;
            if (ImGui.ColorEdit4("Monitor Error Color", ref monitorErrorColor))
            {
                this.config.MonitorErrorFontColor = monitorErrorColor;
                this.config.Save();
            }

            var monitorBgAlpha = this.config.MonitorBgAlpha;
            if (ImGui.SliderFloat("Monitor Transparency", ref monitorBgAlpha, 0.0f, 1.0f))
            {
                this.config.MonitorBgAlpha = monitorBgAlpha;
                this.config.Save();
            }

            if (ImGui.Button("Defaults"))
            {
                this.config.RestoreDefaults();
                this.config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset Window Positions"))
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
            ImGui.SetNextWindowSize(new Vector2(186, 100), ImGuiCond.Always); // Auto-resize doesn't seem to work here
            ImGui.SetNextWindowBgAlpha(this.config.MonitorBgAlpha);

            ImGui.Begin("PingMonitor", windowFlags);
            if (this.resettingMonitorPos)
            {
                ImGui.SetWindowPos(this.config.MonitorPosition);
                this.resettingMonitorPos = false;
            }
            ImGui.TextColored(this.config.MonitorFontColor, $"Connected to: {this.pingTracker.SeAddress}\nPing: {this.pingTracker.LastRTT}ms\nAverage ping: {Math.Round(this.pingTracker.AverageRTT), 2}ms");
            if (this.pingTracker.LastStatus != IPStatus.Success)
                ImGui.TextColored(this.config.MonitorErrorFontColor, $"Error: {this.pingTracker.LastStatus}");
            ImGui.End();

            ImGui.PopStyleVar(1);
        }

        private void DrawGraph()
        {
            var windowFlags = BuildWindowFlags(ImGuiWindowFlags.NoResize);

            ImGui.SetNextWindowPos(this.config.GraphPosition, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(362, 210), ImGuiCond.Always);

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
                var avgY = lowY - Rescale((float) this.pingTracker.AverageRTT, max, min, graphSize.Y);

                ImGui.Text("                      Network Latency (ms) vs Pings");
                ImGui.PlotLines(string.Empty, ref pingArray[0], pingArray.Length, 0, null,
                    float.MaxValue, float.MaxValue, graphSize);

                if (!ImGui.IsWindowCollapsed())
                {
                    var lowLineStart = new Vector2(16, lowY);
                    var lowLineEnd = new Vector2(16 + graphSize.X, lowY);
                    ImGui.GetWindowDrawList().AddLine(lowLineStart, lowLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(lowLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), min.ToString(CultureInfo.CurrentUICulture) + "ms");

                    var avgLineStart = new Vector2(16, avgY);
                    var avgLineEnd = new Vector2(16 + graphSize.X, avgY);
                    ImGui.GetWindowDrawList().AddLine(avgLineStart, avgLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(avgLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), Math.Round(this.pingTracker.AverageRTT, 2).ToString(CultureInfo.CurrentUICulture) + "ms");
                    ImGui.GetWindowDrawList().AddText(avgLineEnd - new Vector2(270, 18), ImGui.GetColorU32(ImGuiCol.Text), "Average");

                    var highLineStart = new Vector2(16, highY);
                    var highLineEnd = new Vector2(16 + graphSize.X, highY);
                    ImGui.GetWindowDrawList()
                        .AddLine(highLineStart, highLineEnd, ImGui.GetColorU32(ImGuiCol.PlotLines));
                    ImGui.GetWindowDrawList().AddText(highLineEnd - new Vector2(0, 5), ImGui.GetColorU32(ImGuiCol.Text), max.ToString(CultureInfo.CurrentUICulture) + "ms");
                }
            }
            else
                ImGui.Text("No data to display at this time.");
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
