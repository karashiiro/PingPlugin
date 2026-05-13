using Dalamud.Utility;

namespace PingPlugin;

public static class WineDetector
{
    private static bool? isWine;

    public static bool IsWINE()
    {
        return isWine ??= Util.IsWine();
    }
}