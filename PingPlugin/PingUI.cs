using System;
using System.Numerics;
using ImGuiNET;

namespace PingPlugin
{
    public class PingUI
    {
        private readonly PingConfiguration config;
        private readonly PingTracker pingTracker;

        public bool IsVisible { get; set; }

        public PingUI(PingTracker pingTracker, PingConfiguration config)
        {
            this.config = config;
            this.pingTracker = pingTracker;
        }

        public void Draw()
        {
            if (!IsVisible)
                return;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.SetWindowSize(new Vector2(50, 10), ImGuiCond.FirstUseEver); // This doesn't actually work?
            ImGui.SetWindowPos(this.config.WindowPosition, ImGuiCond.FirstUseEver);

            ImGui.Begin("Ping", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar);
            ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Ping: {this.pingTracker.LastRTT}ms, average {Math.Round(this.pingTracker.AverageRTT), 2}ms"); // Yellow, it's ABGR instead of RGBA for some reason.
            ImGui.End();

            ImGui.PopStyleVar(1);
        }
    }
}
