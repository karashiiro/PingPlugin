using Dalamud.Plugin;
using System;
using Dalamud.Game.Command;

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
            this.pingTracker = new PingTracker(this.config);
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;

            AddCommandHandlers();
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
