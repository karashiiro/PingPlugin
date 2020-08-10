using Dalamud.Game.ClientState;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using PingPlugin.Attributes;
using PingPlugin.PingTrackers;
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
        
        private AggregatePingTracker pingTracker;
        private bool uiHidden;
        private Hook<ToggleUIDelegate> toggleUIHook;
        private PingUI ui;

        private delegate IntPtr ToggleUIDelegate(IntPtr baseAddress, byte unknownByte);

        public string Name => "PingPlugin";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            // Core plugin initialization
            this.pluginInterface = pluginInterface;

            this.config = (PingConfiguration)this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            // Set up ping trackers
            this.pingTracker = new AggregatePingTracker(this.config,
                new ComponentModelPingTracker(this.config),
                new Win32APIPingTracker(this.config)
                /*new LinuxViaWinePingTracker(this.config)*/);
            this.pingTracker.OnPingUpdated += (payload) =>
            {
                dynamic obj = new ExpandoObject();
                obj.LastRTT = payload.LastRTT;
                obj.AverageRTT = payload.AverageRTT;
                this.pluginInterface.SendMessage(obj);
            };

            this.pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;

            // Set up UI
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;

            // Lifted from FPSPlugin, hook the ScrLk UI toggle; the client condition doesn't handle this
            var toggleUiPtr = this.pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 0F B6 B9 ?? ?? ?? ?? B8 ?? ?? ?? ??");
            this.toggleUIHook = new Hook<ToggleUIDelegate>(toggleUiPtr, new ToggleUIDelegate((ptr, b) =>
            {
                this.uiHidden = (Marshal.ReadByte(ptr, 104008) & 4) == 0;
                return this.toggleUIHook.Original(ptr, b);
            }));
            this.toggleUIHook.Enable();

            // Initialize command manager
            this.commandManager = new PluginCommandManager<PingPlugin>(this, this.pluginInterface);
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            this.ui.CutsceneActive = this.pluginInterface.ClientState.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
                                     this.pluginInterface.ClientState.Condition[ConditionFlag.WatchingCutscene] ||
                                     this.pluginInterface.ClientState.Condition[ConditionFlag.WatchingCutscene78] ||
                                     this.uiHidden;
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
            if (disposing)
            {
                this.commandManager.Dispose();

                this.toggleUIHook?.Disable();

                this.pluginInterface.UiBuilder.OnOpenConfigUi -= (sender, e) => this.ui.ConfigVisible = true;
                this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.BuildUi;

                this.pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;

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
