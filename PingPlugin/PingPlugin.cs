using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PingPlugin.Attributes;
using PingPlugin.GameAddressDetectors;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;
using Dalamud.Game.Network;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private readonly DalamudPluginInterface pluginInterface;

        private readonly PluginCommandManager<PingPlugin> pluginCommandManager;
        private readonly PingConfiguration config;

        private readonly PingTracker pingTracker;
        private readonly PingUI ui;

        internal ICallGateProvider<object, object> IpcProvider;

        public string Name => "PingPlugin";

        public PingPlugin(
            DalamudPluginInterface pluginInterface,
            CommandManager commands,
            ClientState clientState,
            DtrBar dtrBar,
            GameNetwork network)
        {
            this.pluginInterface = pluginInterface;
            
            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.pingTracker = new AggregatePingTracker(this.config, new AggregateAddressDetector(clientState), network);
            this.pingTracker.Start();

            InitIpc();

            this.ui = new PingUI(this.pingTracker, this.pluginInterface, dtrBar, this.config);
            this.pingTracker.OnPingUpdated += this.ui.UpdateDtrBarPing;

            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            this.pluginInterface.UiBuilder.Draw += this.ui.Draw;

            this.pluginCommandManager = new PluginCommandManager<PingPlugin>(this, commands);
        }

        private void InitIpc()
        {
            try
            {
                IpcProvider = this.pluginInterface.GetIpcProvider<object, object>("PingPlugin.Ipc");
                this.pingTracker.OnPingUpdated += payload =>
                {
                    dynamic obj = new ExpandoObject();
                    obj.LastRTT = payload.LastRTT;
                    obj.AverageRTT = payload.AverageRTT;
                    IpcProvider.SendMessage(obj);
                };
            }
            catch (Exception e)
            {
                PluginLog.Error($"Error registering IPC provider:\n{e}");
            }
        }

        [Command("/ping")]
        [HelpMessage("Show/hide the ping monitor.")]
        [ShowInHelp]
        public void PingCommand(string command, string args)
        {
            this.config.MonitorIsVisible = !this.config.MonitorIsVisible;
            this.config.Save();
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

        private void OpenConfigUi()
        {
            this.ui.ConfigVisible = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            
            this.pluginCommandManager.Dispose();

            this.pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            this.pluginInterface.UiBuilder.Draw -= this.ui.Draw;

            this.config.Save();

            this.pingTracker.OnPingUpdated -= this.ui.UpdateDtrBarPing;
            this.ui.Dispose();

            this.pingTracker.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
