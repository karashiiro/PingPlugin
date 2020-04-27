using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
            this.pingTracker = new PingTracker();
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.pluginInterface.CommandManager.AddHandler("/ping",
                new CommandInfo((command, args) => this.ui.IsVisible = !this.ui.IsVisible));

            Task.Run(async () =>
            {
                while (true)
                {
                    this.pluginInterface.Framework.Gui.Chat.Print($"Ping: {this.pingTracker.LastRTT}ms");
                    await Task.Delay(3000);
                }
            });
        }

        private void OnNetworkMessage(IntPtr dataPtr)
        {
            if (!this.pluginInterface.Data.IsDataReady)
                return;

            var op = Marshal.ReadInt16(dataPtr);
            if (op == this.pluginInterface.Data.ServerOpCodes["ReqSomething"])
                this.pingTracker.StartNextRTTWait();
            if (op == this.pluginInterface.Data.ServerOpCodes["Something"])
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
                this.pluginInterface.CommandManager.RemoveHandler("/ping");

                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

                this.pluginInterface.SavePluginConfig(this.config);

                this.pluginInterface.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PingPlugin()
        {
            Dispose(false);
        }
        #endregion
    }
}
