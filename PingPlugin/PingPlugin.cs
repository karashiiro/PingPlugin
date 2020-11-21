using Dalamud.Plugin;
using PingPlugin.Attributes;
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
        
        private PingTracker pingTracker;
        private PingUI ui;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr NetworkInfoFunction(
            IntPtr a1, // Address of network info struct
            ulong a2,  // Always seems to be 1 or 0, usually 0
            ulong a3);

        private Hook<NetworkInfoFunction> netFuncHook;

        public string Name => "PingPlugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.pingTracker = new PingTracker(this.config);
            this.pingTracker.OnPingUpdated += payload =>
            {
                dynamic obj = new ExpandoObject();
                obj.LastRTT = payload.LastRTT;
                obj.AverageRTT = payload.AverageRTT;
                this.pluginInterface.SendMessage(obj);
            };
            InstallHook();

            this.ui = new PingUI(this.pingTracker, this.pluginInterface.UiBuilder, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;

            this.commandManager = new PluginCommandManager<PingPlugin>(this, this.pluginInterface);
        }

        private void InstallHook()
        {
            try
            {
                var lastPing = 0U;
                var netFuncPtr =
                    this.pluginInterface.TargetModuleScanner.ScanText(
                        "40 55 41 54 41 56 48 8D AC 24 ?? ?? ?? ?? B8 10 10 00 00 E8 ?? ?? ?? ?? 48 2B E0");
                this.netFuncHook = new Hook<NetworkInfoFunction>(netFuncPtr, new NetworkInfoFunction((a1, a2, a3) =>
                {
                    var nextPing = (uint)Marshal.ReadInt32(a1 + 0x8C4);
                    // ReSharper disable once InvertIf
                    if (lastPing != nextPing)
                    {
                        this.pingTracker.DoNextRTTCalculation(nextPing);
                        lastPing = nextPing;
                    }
                    return this.netFuncHook.Original(a1, a2, a3);
                }));
                this.netFuncHook.Enable();
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to hook netstats method!");
            }
        }

        private void UninstallHook()
        {
            this.netFuncHook?.Disable();
            this.netFuncHook?.Dispose();
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

            UninstallHook();

            this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.BuildUi;

            this.config.Save();

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
