using Dalamud.Plugin;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Game.Internal.Network;
using PingPlugin.PingTrackers;

namespace PingPlugin
{
    public class PingPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PingConfiguration config;
        private IntPtr chatLogObject;
        private IPingTracker pingTracker;
        private PingUI ui;

        public string Name => "Ping Plugin";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetBaseUIObjDelegate();
        private GetBaseUIObjDelegate getBaseUIObj;
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetUI2ObjByNameDelegate(IntPtr getBaseUIObj, string UIName, int index = 1);
        private GetUI2ObjByNameDelegate getUI2ObjByName;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            var getBaseUIObjScan = this.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            var getUI2ObjByNameScan = this.pluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");
            this.getBaseUIObj = Marshal.GetDelegateForFunctionPointer<GetBaseUIObjDelegate>(getBaseUIObjScan);
            this.getUI2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUI2ObjByNameDelegate>(getUI2ObjByNameScan);
            this.chatLogObject = this.getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "ChatLog");

            this.config = (PingConfiguration) this.pluginInterface.GetPluginConfig() ?? new PingConfiguration();
            this.config.Initialize(this.pluginInterface);

            this.pingTracker = new AggregatePingTracker(this.config,
                new ComponentModelPingTracker(this.config),
                new Win32APIPingTracker(this.config));

            this.pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            
            this.ui = new PingUI(this.pingTracker, this.config);

            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, e) => this.ui.ConfigVisible = true;
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.BuildUi;
            
            AddCommandHandlers();
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            if (this.pluginInterface.ClientState.LocalPlayer == null)
            {
                this.ui.CutsceneActive = false;
                this.chatLogObject = IntPtr.Zero;
                return;
            }

            if (this.chatLogObject == IntPtr.Zero)
            {
                this.chatLogObject = this.getUI2ObjByName(Marshal.ReadIntPtr(getBaseUIObj(), 0x20), "ChatLog");
                return;
            }
            
            var chatLogProperties = Marshal.ReadIntPtr(this.chatLogObject, 0xC8);
            if (chatLogProperties == IntPtr.Zero)
                return;

            var hidden = Marshal.ReadByte(chatLogProperties + 0x73) == 0;

            this.ui.CutsceneActive = hidden;
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
