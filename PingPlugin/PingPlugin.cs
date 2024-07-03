using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PingPlugin.Attributes;
using PingPlugin.GameAddressDetectors;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Network;
using Dalamud.Plugin.Services;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IGameNetwork network;
        private readonly IPluginLog pluginLog;

        private readonly PluginCommandManager<PingPlugin> pluginCommandManager;
        private readonly PingConfiguration config;

        private readonly GameAddressDetector addressDetector;
        private readonly PingUI ui;
        
        private PingTracker pingTracker;

        internal ICallGateProvider<object, object> IpcProvider;

        public string Name => "PingPlugin";

        public PingPlugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDtrBar dtrBar, IGameNetwork network, IPluginLog pluginLog)
        {
            this.pluginInterface = pluginInterface;
            this.network = network;
            this.pluginLog = pluginLog;
            
            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface, this.pluginLog);

            this.addressDetector = this.pluginInterface.Create<AggregateAddressDetector>();
            if (this.addressDetector == null)
            {
                throw new InvalidOperationException("Failed to create game address detector. The provided arguments may be incorrect.");
            }
            
            this.pingTracker = RequestNewPingTracker(this.config.TrackingMode);
            this.pingTracker.Verbose = false;

            InitIpc();

            // Most of these can't be created using service injection because the service container only checks ctors for
            // exact types.
            this.ui = new PingUI(this.pingTracker, this.pluginInterface, dtrBar, this.config, RequestNewPingTracker, pluginLog);
            this.pingTracker.OnPingUpdated += this.ui.UpdateDtrBarPing;

            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            this.pluginInterface.UiBuilder.Draw += this.ui.Draw;

            this.pluginCommandManager = new PluginCommandManager<PingPlugin>(this, commands);
        }

        private PingTracker RequestNewPingTracker(PingTrackerKind kind)
        {
            this.pingTracker?.Dispose();
            
            PingTracker newTracker = kind switch
            {
                PingTrackerKind.Aggregate => new AggregatePingTracker(this.config, this.addressDetector, this.network, this.pluginLog),
                PingTrackerKind.COM => new ComponentModelPingTracker(this.config, this.addressDetector, this.pluginLog),
                PingTrackerKind.IpHlpApi => new IpHlpApiPingTracker(this.config, this.addressDetector, this.pluginLog),
                PingTrackerKind.Packets => new PacketPingTracker(this.config, this.addressDetector, this.network, this.pluginLog),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            this.pingTracker = newTracker;
            if (this.pingTracker == null)
            {
                throw new InvalidOperationException($"Failed to create ping tracker \"{kind}\". The provided arguments may be incorrect.");
            }
            
            this.pingTracker.Start();
            
            return newTracker;
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
                pluginLog.Error($"Error registering IPC provider:\n{e}");
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
