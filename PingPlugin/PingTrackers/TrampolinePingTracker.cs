using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Plugin;

namespace PingPlugin.PingTrackers
{
    public class TrampolinePingTracker : PingTracker
    {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr NetworkInfoFunction(
            IntPtr a1, // Address of network info struct
            ulong a2,  // Always seems to be 1 or 0, usually 0
            ulong a3);

        private Hook<NetworkInfoFunction> netFuncHook;

        private readonly DalamudPluginInterface pluginInterface;

        private TrampolinePingTracker(PingConfiguration config) : base(config)
        {
        }

        public TrampolinePingTracker(PingConfiguration config, DalamudPluginInterface pluginInterface) : this(config)
        {
            this.pluginInterface = pluginInterface;
            InstallHook();
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
                        NextRTTCalculation(nextPing / 2);
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

        protected override Task PingLoop(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UninstallHook();
                base.Dispose(true);
            }
        }
    }
}