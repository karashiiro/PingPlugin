using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PingPlugin.Attributes;
using PingPlugin.GameAddressDetectors;
using PingPlugin.PingTrackers;
using System;
using System.Dynamic;
using System.Linq;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private readonly DalamudPluginInterface pluginInterface;

        private readonly PluginCommandManager<PingPlugin> pluginCommandManager;
        private readonly PingConfiguration config;

        private readonly GameAddressDetector addressDetector;
        private readonly PingUI ui;
        
        private PingTracker pingTracker;

        internal ICallGateProvider<object, object> IpcProvider;

        public string Name => "PingPlugin";

        public PingPlugin(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            
            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.addressDetector = this.pluginInterface.Create<AggregateAddressDetector>();
            if (this.addressDetector == null)
            {
                throw new InvalidOperationException("Failed to create game address detector. The provided arguments may be incorrect.");
            }
            
            this.pingTracker = RequestNewPingTracker(this.config.TrackingMode);
            this.pingTracker.Start();

            InitIpc();

            this.ui = this.pluginInterface.Create<PingUI>(this.pingTracker, this.config, (Func<PingTrackerKind, PingTracker>)RequestNewPingTracker);
            if (this.ui == null)
            {
                throw new InvalidOperationException("Failed to create UI object. The provided arguments may be incorrect.");
            }
            
            this.pingTracker.OnPingUpdated += this.ui.UpdateDtrBarPing;

            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            this.pluginInterface.UiBuilder.Draw += this.ui.Draw;

            this.pluginCommandManager = this.pluginInterface.Create<PluginCommandManager<PingPlugin>>(this);
            if (this.pluginCommandManager == null)
            {
                throw new InvalidOperationException("Failed to create command manager. The provided arguments may be incorrect.");
            }
        }
        
        private PingTracker CreatePingTracker<T>(params object[] scopedObjects) where T : PingTracker
            => this.pluginInterface.Create<T>(scopedObjects
                .Concat(new object[] { this.config, this.addressDetector })
                .ToArray());

        private PingTracker RequestNewPingTracker(PingTrackerKind kind)
        {
            this.pingTracker = kind switch
            {
                PingTrackerKind.Aggregate => CreatePingTracker<AggregatePingTracker>(),
                PingTrackerKind.COM => CreatePingTracker<ComponentModelPingTracker>(),
                PingTrackerKind.IpHlpApi => CreatePingTracker<IpHlpApiPingTracker>(),
                PingTrackerKind.Packets => CreatePingTracker<PacketPingTracker>(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            
            if (this.pingTracker == null)
            {
                throw new InvalidOperationException($"Failed to create ping tracker \"{kind}\". The provided arguments may be incorrect.");
            }
            
            this.pingTracker.Start();
            
            return this.pingTracker;
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
