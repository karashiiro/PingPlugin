using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;
using PingPlugin.PingTrackers;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PingConfiguration config;
        private IPingTracker pingTracker;
        private PingUI ui;

        public string Name => "Ping Plugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.config = (PingConfiguration) this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.pingTracker = new AggregatePingTracker(this.config,
                new ComponentModelPingTracker(this.config),
                new Win32APIPingTracker(this.config));

            this.pluginInterface.Framework.Network.OnNetworkMessage += OnNetworkMessage;
            
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;
            
            AddCommandHandlers();
        }

        private void OnNetworkMessage(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            const ushort eventPlay = 0x02C3; // TODO: Get goat to hook the handlers for these
            const ushort eventFinish = 0x0239;
            if (opCode == eventPlay)
            {
                var packetData = Marshal.PtrToStructure<EventPlay>(dataPtr);
                
                if ((packetData.Flags & 0x00000400) != 0) // TODO: PR Sapphire, HIDE_UI seems to be wrong now
                    this.ui.CutsceneActive = true;
            }
            else if (opCode == eventFinish)
            {
                this.ui.CutsceneActive = false;
            }
        }

        private void AddCommandHandlers()
        {
            this.pluginInterface.CommandManager.AddHandler("/ping",
                new CommandInfo((command, args) =>
                {
                    this.config.MonitorIsVisible = !this.config.MonitorIsVisible;
                    this.config.Save(); // If you kill the game, nothing is disposed. So, we save changes after they're made.
                })
                {
                    HelpMessage = "Show/hide the ping monitor.",
                    ShowInHelp = true,
                });
            this.pluginInterface.CommandManager.AddHandler("/pinggraph",
                new CommandInfo((command, args) =>
                {
                    this.config.GraphIsVisible = !this.config.GraphIsVisible;
                    this.config.Save();
                })
                {
                    HelpMessage = "Show/hide the ping graph.",
                    ShowInHelp = true,
                });
            this.pluginInterface.CommandManager.AddHandler("/pingconfig",
                new CommandInfo((command, args) =>
                {
                    this.ui.ConfigVisible = true;
                })
                {
                    HelpMessage = "Show/hide PingPlugin's configuration.",
                    ShowInHelp = true,
                });
        }

        private void RemoveCommandHandlers()
        {
            this.pluginInterface.CommandManager.RemoveHandler("/ping");
            this.pluginInterface.CommandManager.RemoveHandler("/pinggraph");
            this.pluginInterface.CommandManager.RemoveHandler("/pingconfig");
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveCommandHandlers();

                this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, e) => this.ui.ConfigVisible = true;
                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.BuildUi;

                this.pluginInterface.Framework.Network.OnNetworkMessage -= OnNetworkMessage;

                this.config.Save();

                this.pingTracker.Dispose();
                this.pluginInterface.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
