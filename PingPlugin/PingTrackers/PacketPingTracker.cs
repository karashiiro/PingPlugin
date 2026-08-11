using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Application.Network;
using PingPlugin.GameAddressDetectors;

namespace PingPlugin.PingTrackers;

public class PacketPingTracker : PingTracker
{
    [Signature(
        "48 89 5C 24 ?? 48 89 74 24 ?? 55 57 41 56 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B F9 40 32 F6",
        DetourName = nameof(NetworkModuleUpdateDetour))]
    private readonly Hook<NetworkModuleUpdateDelegate> networkModuleUpdateHook = null!;
    private unsafe delegate void NetworkModuleUpdateDelegate(NetworkModule* self);

    private uint networkModuleRtt;

    public PacketPingTracker(PingConfiguration config, GameAddressDetector addressDetector,
        IPluginLog pluginLog, IGameInteropProvider gameInteropProvider) : base(config, addressDetector, PingTrackerKind.Packets, pluginLog)
    {
        gameInteropProvider.InitializeFromAttributes(this);
        networkModuleUpdateHook.Enable();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        networkModuleUpdateHook.Dispose();
        base.Dispose(true);
    }

    protected override async Task PingLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            NextRTTCalculation(networkModuleRtt);
        }
    }

    private unsafe void NetworkModuleUpdateDetour(NetworkModule* self)
    {
        networkModuleUpdateHook.Original(self);
        networkModuleRtt = *(uint*)((nint)self + 0xbcc);
    }
}
