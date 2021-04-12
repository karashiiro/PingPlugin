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
                        "40 55 57 41 55 41 56 48 8D 6C 24 E8 48 81 EC 18 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 00 48 8B F9 89 54 24 58 48 8B 49 10");
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