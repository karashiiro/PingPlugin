using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using PingPlugin.Attributes;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private readonly CommandManager commandManager;
        private readonly DalamudPluginInterface pluginInterface;
        private readonly PluginCommandManager<PingPlugin> pluginCommandManager;
        private readonly PingConfiguration config;
        private readonly SigScanner sigScanner;

        private readonly AggregatePingTracker pingTracker;
        private readonly PingUI ui;

        public string Name => "PingPlugin";

        public PingPlugin(
            DalamudPluginInterface pluginInterface,
            CommandManager commandManager,
            SigScanner sigScanner)
        {
            this.commandManager = commandManager;
            this.pluginInterface = pluginInterface;
            this.sigScanner = sigScanner;

            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.pingTracker = new AggregatePingTracker(this.config,
                new ComponentModelPingTracker(this.config),
                new Win32APIPingTracker(this.config),
                new TrampolinePingTracker(this.config, this.sigScanner));
            this.pingTracker.OnPingUpdated += payload =>
            {
                dynamic obj = new ExpandoObject();
                obj.LastRTT = payload.LastRTT;
                obj.AverageRTT = payload.AverageRTT;
                //this.pluginInterface.SendMessage(obj);
            };

            this.ui = new PingUI(this.pingTracker, this.pluginInterface, this.config);

            this.pluginInterface.UiBuilder.OpenConfigUi += (_, _) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.Draw += this.ui.BuildUi;

            this.pluginCommandManager = new PluginCommandManager<PingPlugin>(this, this.commandManager);
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

            this.pluginCommandManager.Dispose();

            this.pluginInterface.UiBuilder.OpenConfigUi -= (_, _) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.Draw -= this.ui.BuildUi;

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
