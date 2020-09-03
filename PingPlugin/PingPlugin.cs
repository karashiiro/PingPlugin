using Dalamud.Game.ClientState;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using PingPlugin.Attributes;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;
using System.Runtime.InteropServices;
using Dalamud.Hooking;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<PingPlugin> commandManager;
        private PingConfiguration config;
        
        private AggregatePingTracker pingTracker;
        private PingUI ui;

        public string Name => "PingPlugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            // Core plugin initialization
            this.pluginInterface = pluginInterface;

            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            // Set up ping trackers
            this.pingTracker = new AggregatePingTracker(this.config,
                new ComponentModelPingTracker(this.config),
                new Win32APIPingTracker(this.config)
                /*new LinuxViaWinePingTracker(this.config)*/);
            this.pingTracker.OnPingUpdated += (payload) =>
            {
                dynamic obj = new ExpandoObject();
                obj.LastRTT = payload.LastRTT;
                obj.AverageRTT = payload.AverageRTT;
                this.pluginInterface.SendMessage(obj);
            };

            // Set up UI
            this.ui = new PingUI(this.pingTracker, this.pluginInterface.UiBuilder, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;

            // Initialize command manager
            this.commandManager = new PluginCommandManager<PingPlugin>(this, this.pluginInterface);
        }

        [Command("/ping")]
        [HelpMessage("Show/hide the ping monitor.")]
        [ShowInHelp]
        public void PingCommand(string command, string args)
        {
            this.config.MonitorIsVisible = !this.config.MonitorIsVisible;
            this.config.Save(); // If you kill the game, nothing is disposed. So, we save changes after they're made.
        }

        [Command("/pinggraph")]
        [HelpMessage("Show/hide the ping graph.")]
        [ShowInHelp]
        public void PingGraphCommand(string command, string args)
        {
            this.config.GraphIsVisible = !this.config.GraphIsVisible;
            this.config.Save();
        }

        [Command("/pingconfig")]
        [HelpMessage("Show PingPlugin's configuration.")]
        [ShowInHelp]
        public void PingConfigCommand(string command, string args)
        {
            this.ui.ConfigVisible = true;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.BuildUi;

            this.config.Save();

            this.pingTracker.Dispose();
            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
