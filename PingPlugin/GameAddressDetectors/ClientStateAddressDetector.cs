using Dalamud.Logging;
using Dalamud.Plugin.Services;
using System;
using System.Net;

namespace PingPlugin.GameAddressDetectors
{
    public class ClientStateAddressDetector : GameAddressDetector
    {
        private uint lastDcId;

        private readonly IClientState clientState;
        private readonly IPluginLog pluginLog;

        public ClientStateAddressDetector(IClientState clientState, IPluginLog pluginLog)
        {
            this.clientState = clientState;
            this.pluginLog = pluginLog;
        }

        public override IPAddress GetAddress(bool verbose = false)
        {
            if (!this.clientState.IsLoggedIn || this.clientState.LocalPlayer == null)
            {
                Address = IPAddress.Loopback;
                return Address;
            }

            uint? dcId;
            try
            {
                dcId = this.clientState.LocalPlayer!.CurrentWorld.GameData?.DataCenter.Row;
                if ((dcId == null || dcId == this.lastDcId) && !IPAddress.IsLoopback(Address)) return Address;
                this.lastDcId = (uint)dcId;
            }
            catch (InvalidOperationException)
            {
                return Address;
            }

            /*
             * We might be able to read these from the game itself, but
             * I'm tired of maintaining struct offsets that change every
             * patch. If you know of a function that simply returns the
             * currently-connected IP address, feel free to PR!
             *
             * Historical notes:
             * Previously, I used the Windows TCP connection table to get
             * these, but for some reason this doesn't work in Wine or
             * CrossOver, or with gaming VPNs since these manipulate TCP
             * connections. To get around this, I used a game function that
             * sets the Sending/Receiving data in-game to get a struct that
             * had a value of roughly (2 x ping RTT), but this was annoying
             * to maintain and was also horribly inaccurate.
             *
             * Historical notes (updated):
             * I re-added support for reading the Windows TCP connection table,
             * but I'm keeping this function in order to fall back when
             * accessing the TCP table fails.
             */
            var address = dcId switch
            {
                // updated to use lobby IP as fallback IP addressess, copied from https://arrstatus.com
                1 => IPAddress.Parse("119.252.36.6"), // Elemental
                2 => IPAddress.Parse("119.252.36.7"), // Gaia
                3 => IPAddress.Parse("119.252.36.8"), // Mana
                4 => IPAddress.Parse("204.2.29.6"),   // Aether
                5 => IPAddress.Parse("204.2.29.7"),   // Primal
                6 => IPAddress.Parse("80.239.145.6"),   // Chaos
                7 => IPAddress.Parse("80.239.145.7"),   // Light
                8 => IPAddress.Parse("204.2.29.8"),  // Crystal
                9 => IPAddress.Parse("153.254.80.103"),  // Materia
                10 => IPAddress.Parse("119.252.36.9"), // Meteor
                11 => IPAddress.Parse("204.2.29.9"), // Dynamis
                12 => IPAddress.Parse("80.239.145.8"), // Shadow

                // If you have CN/KR DC IDs and IP addresses, feel free to PR them.
                // World server IP address are fine too, since worlds are hosted
                // alongside the lobby servers.

                _ => IPAddress.Loopback,
            };

            if (verbose && !Equals(address, IPAddress.Loopback) && !Equals(address, Address))
            {
                var dcName = this.clientState.LocalPlayer!.CurrentWorld.GameData.DataCenter.Value?.Name.RawString;
                pluginLog.Verbose($"Data center changed to {dcName}, using FFXIV server address {address}");
            }

            Address = address;
            return Address;
        }
    }
}
