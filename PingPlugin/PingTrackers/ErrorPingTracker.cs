using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using PingPlugin.GameAddressDetectors;

namespace PingPlugin.PingTrackers;

public class ErrorPingTracker : PingTracker
{
    public ErrorPingTracker(PingConfiguration config, GameAddressDetector addressDetector, PingTrackerKind kind,
        IPluginLog pluginLog) : base(config, addressDetector, kind, pluginLog)
    {
        Errored = true;
    }

    protected override Task PingLoop(CancellationToken token)
    {
        return Task.CompletedTask;
    }
}