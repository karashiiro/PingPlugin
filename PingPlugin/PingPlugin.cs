using Dalamud.Plugin;
using System;
using System.Threading.Tasks;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PingTracker pingTracker;
        private PingUI ui;

        public string Name => "Ping Plugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.pingTracker = new PingTracker();
            this.ui = new PingUI();

            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            Task.Run(async () =>
            {
                while (true)
                {
                    this.pluginInterface.Framework.Gui.Chat.Print($"Current ping: {this.pingTracker.LastRTT}ms");
                    await Task.Delay(3000);
                }
            });
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
                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

                this.pluginInterface.Dispose();
                this.pingTracker.Dispose();
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
