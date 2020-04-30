using Dalamud.Plugin;
using System;
using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PingConfiguration config;
        private PingTracker pingTracker;
        private PingUI ui;

        public string Name => "Ping Plugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.config = (PingConfiguration) this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);
            this.pingTracker = new PingTracker();
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;

            AddCommandHandlers();

            this.pluginInterface.Framework.Network.OnNetworkMessage += OnNetworkMessage;
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
            #if DEBUG
            this.pluginInterface.CommandManager.AddHandler("/pingconfig",
                new CommandInfo((command, args) =>
                {
                    this.ui.ConfigVisible = true;
                }));
            #endif
        }

        private void RemoveCommandHandlers()
        {
            this.pluginInterface.CommandManager.RemoveHandler("/ping");
            this.pluginInterface.CommandManager.RemoveHandler("/pinggraph");
            #if DEBUG
            this.pluginInterface.CommandManager.RemoveHandler("/pingconfig");
            #endif
        }

        private void OnNetworkMessage(IntPtr dataPtr, ushort opCode, uint targetId, NetworkMessageDirection direction)
        {
            if (!this.pluginInterface.Data.IsDataReady || this.pluginInterface.ClientState.LocalPlayer == null)
                return;
            
            if (direction == NetworkMessageDirection.ZoneUp)
                if (opCode == 0x241)
                    this.pingTracker.StartNextRTTWait();

            if (direction == NetworkMessageDirection.ZoneDown)
                if (opCode == 0xC4 && targetId == this.pluginInterface.ClientState.LocalPlayer.ActorId)
                    this.pingTracker.StartNextRTTCalculation();
        }

        #region Logging Shortcuts
        private void Log(string messageTemplate, params object[] values)
            => this.pluginInterface.Log(messageTemplate, values);

        private void LogError(string messageTemplate, params object[] values)
            => this.pluginInterface.LogError(messageTemplate, values);

        private void LogError(Exception exception, string messageTemplate, params object[] values)
            => this.pluginInterface.LogError(exception, messageTemplate, values);
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveCommandHandlers();

                this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, e) => this.ui.ConfigVisible = true;
                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.BuildUi;

                this.config.Save();

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
