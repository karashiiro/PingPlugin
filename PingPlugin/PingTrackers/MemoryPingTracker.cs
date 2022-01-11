using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlugin.PingTrackers
{
    public class MemoryPingTracker : PingTracker
    {
        private const int PingOffset = 2356;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate IntPtr NetworkInfoFunction(
            IntPtr a1, // Address of network info struct
            ulong a2,  // Always seems to be 1 or 0, usually 0
            ulong a3);

        private Hook<NetworkInfoFunction> netFuncHook;

        private readonly SigScanner sigScanner;

        private MemoryPingTracker(PingConfiguration config) : base(config)
        {
        }

        public MemoryPingTracker(PingConfiguration config, SigScanner sigScanner) : this(config)
        {
            this.sigScanner = sigScanner;
            InstallHook();
        }

        private void InstallHook()
        {
            try
            {
                var lastPing = 0U;
                var netFuncPtr =
                    this.sigScanner.ScanText(
                        "40 55 41 54 41 56 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 4C 8B F1");
                this.netFuncHook = new Hook<NetworkInfoFunction>(netFuncPtr, (a1, a2, a3) =>
                {
                    var nextPing = (uint)Marshal.ReadInt32(a1 + PingOffset);
                    // ReSharper disable once InvertIf
                    if (lastPing != nextPing)
                    {
#if DEBUG
                        PluginLog.Log($"{a1}, {a2}, {a3}");
#endif

                        NextRTTCalculation(nextPing / 2);
                        lastPing = nextPing;
                    }
                    return this.netFuncHook.Original(a1, a2, a3);
                });
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