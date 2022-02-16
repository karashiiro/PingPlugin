using Dalamud.Game.ClientState;
using Dalamud.Logging;
using System;
using System.Net;

namespace PingPlugin.GameAddressDetectors
{
    public class ClientStateAddressDetector : GameAddressDetector
    {
        private uint lastDcId;

        private readonly ClientState clientState;

        public ClientStateAddressDetector(ClientState clientState)
        {
            this.clientState = clientState;
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
                if (dcId == null || dcId == this.lastDcId) return Address;
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
                // I just copied these from https://is.xivup.com/adv
                1 => IPAddress.Parse("124.150.157.23"), // Elemental
                2 => IPAddress.Parse("124.150.157.36"), // Gaia
                3 => IPAddress.Parse("124.150.157.49"), // Mana
                4 => IPAddress.Parse("204.2.229.84"),   // Aether
                5 => IPAddress.Parse("204.2.229.95"),   // Primal
                6 => IPAddress.Parse("195.82.50.46"),   // Chaos
                7 => IPAddress.Parse("195.82.50.55"),   // Light
                8 => IPAddress.Parse("204.2.229.106"),  // Crystal
                9 => IPAddress.Parse("153.254.80.75"),  // Materia

                // If you have CN/KR DC IDs and IP addresses, feel free to PR them.
                // World server IP address are fine too, since worlds are hosted
                // alongside the lobby servers.

                _ => IPAddress.Loopback,
            };

            if (verbose && !Equals(address, IPAddress.Loopback) && !Equals(address, Address))
            {
                var dcName = this.clientState.LocalPlayer!.CurrentWorld.GameData.DataCenter.Value?.Name.RawString;
                PluginLog.Log($"Data center changed to {dcName}, using FFXIV server address {address}");
            }

            Address = address;
            return Address;
        }
    }
}