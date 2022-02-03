using CheapLoc;

namespace PingPlugin
{
    public enum DisplayMode
    {
        Default,
        Micro,
        Minimal,
        ServerBar,
    }

    public class DisplayModeNames
    {
        public static string[] Names()
        {
            return new[]
            {
                Loc.Localize("DefaultDisplay", string.Empty),
                Loc.Localize("MicroDisplay", string.Empty),
                Loc.Localize("MinimalDisplay", string.Empty),
                Loc.Localize("ServerBarDisplay", string.Empty),
            };
        }
    }
}