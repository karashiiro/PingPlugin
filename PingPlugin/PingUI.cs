using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace PingPlugin
{
    public class PingUI
    {
        private readonly PingConfiguration config;
        private readonly PingTracker pingTracker;

        private ImFontPtr CourierNew;

        public bool GraphIsVisible { get; set; }
        public bool MonitorIsVisible { get; set; }

        public PingUI(PingTracker pingTracker, PingConfiguration config)
        {
            this.config = config;
            this.pingTracker = pingTracker;
        }

        public void BuildFonts()
        {
            // I could've stuck with the default just fine, but that's too safe.
            CourierNew = ImGui.GetIO().Fonts.AddFontFromFileTTF(Path.Combine(Assembly.GetCallingAssembly().Location, "..", "cour.ttf"), 18.66f);
        }

        public void BuildUi()
        {
            if (GraphIsVisible) DrawGraph();
            if (MonitorIsVisible) DrawMonitor();
        }

        private void DrawMonitor()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.SetWindowPos(this.config.MonitorPosition, ImGuiCond.FirstUseEver);

            ImGui.Begin("PingMonitor", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
            ImGui.PushFont(CourierNew);
            ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Ping: {this.pingTracker.LastRTT}ms\nAverage ping: {Math.Round(this.pingTracker.AverageRTT), 2}ms"); // Yellow, it's ABGR instead of RGBA for some reason.
            ImGui.PopFont();
            ImGui.End();

            ImGui.PopStyleVar(1);
        }

        private void DrawGraph()
        {
            ImGui.SetWindowPos(this.config.GraphPosition, ImGuiCond.FirstUseEver);

            ImGui.Begin("Ping Graph", ImGuiWindowFlags.AlwaysAutoResize);
            var pingArray = this.pingTracker.RTTTimes.ToArray();
            if (pingArray.Length > 0)
                ImGui.PlotLines(string.Empty, ref pingArray[0], pingArray.Length, 0, null, float.MaxValue, float.MaxValue, new Vector2(300, 150));
            else
                ImGui.Text("No data to display at this time.");
            ImGui.End();
        }
    }
}
