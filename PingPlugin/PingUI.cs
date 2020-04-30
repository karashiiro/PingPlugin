using System;
using System.Numerics;
using ImGuiNET;

namespace PingPlugin
{
    public class PingUI
    {
        private readonly PingConfiguration config;
        private readonly PingTracker pingTracker;

        private ImFontPtr CourierNew;

        public bool IsVisible { get; set; }

        public PingUI(PingTracker pingTracker, PingConfiguration config)
        {
            this.config = config;
            this.pingTracker = pingTracker;
        }

        public void BuildFonts()
        {
            CourierNew = ImGui.GetIO().Fonts.AddFontFromFileTTF("cour.ttf", 18.66f);
        }

        public void BuildUi()
        {
            if (!IsVisible)
                return;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.SetWindowPos(this.config.WindowPosition, ImGuiCond.FirstUseEver);

            ImGui.Begin("Ping", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
            ImGui.PushFont(CourierNew);
            ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Ping: {this.pingTracker.LastRTT}ms, average {Math.Round(this.pingTracker.AverageRTT), 2}ms"); // Yellow, it's ABGR instead of RGBA for some reason.
            ImGui.PopFont();
            ImGui.End();

            ImGui.PopStyleVar(1);
        }
    }
}
