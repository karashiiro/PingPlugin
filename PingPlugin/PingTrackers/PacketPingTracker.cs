using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Network;
using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;

namespace PingPlugin.PingTrackers
{
    /// <summary>
    /// Packet-based ping tracker. This should work reliably on all platforms. It uses the Win32 API
    /// function QueryPerformanceCounter, but the game also uses this function to calculate ping, so
    /// there shouldn't be any issue with this.
    /// </summary>
    public class PacketPingTracker : PingTracker
    {
        private readonly GameNetwork network;
        
        // Various fields used for opcode prediction
        private Timer predictionTimeout;
        
        private bool predictedUpOpcodeSet;
        private ushort predictedUpOpcode;
        private uint predictedUpOpcodeTimestamp;
        
        private bool predictedDownOpcodeSet;
        private ushort predictedDownOpcode;

        private long pingMs;
        private bool gotPing;

        public PacketPingTracker(PingConfiguration config, GameAddressDetector addressDetector, GameNetwork network) : base(config, addressDetector, PingTrackerKind.Packets)
        {
            this.network = network;
            this.network.NetworkMessage += OnNetworkMessage;
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (this.gotPing && this.pingMs >= 0)
                {
                    NextRTTCalculation((ulong)this.pingMs);
                    this.gotPing = false;
                }
                
                // This pair of packets arrives every 10 seconds
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
        }

        private void OnNetworkMessage(IntPtr dataPtr, ushort opcode, uint sourceId, uint targetId, NetworkMessageDirection direction)
        {
            // Tracking ping using ping packets is unreliable, because we can't confirm that the server
            // responds to the ping immediately. With ICMP, we know that this is true, but using packets,
            // messages on both the client and server are restrained by TCP ordering requirements. That
            // said, considering TCP-related latency in calculations is arguably more useful than a real
            // ping calculation.
            
            // Predict the up/down Ping opcodes
            if (!this.predictedUpOpcodeSet && direction == NetworkMessageDirection.ZoneUp)
            {
                // Client send, executes first
                TrackPredictedPingUp(dataPtr, opcode);
            }
            else if (this.predictedUpOpcodeSet && !this.predictedDownOpcodeSet && direction == NetworkMessageDirection.ZoneDown)
            {
                // Server send, executes last
                CheckPredictedPingDown(dataPtr, opcode);
            }

            // Calculate ping
            if (this.predictedDownOpcodeSet && this.predictedUpOpcodeSet)
            {
                CalculatePing(dataPtr, opcode, direction);
            }
        }

        private void CheckPredictedPingDown(IntPtr dataPtr, ushort opcode)
        {
            var timestamp = (uint) Marshal.ReadInt32(dataPtr);
            if (timestamp == this.predictedUpOpcodeTimestamp)
            {
                this.predictedDownOpcode = opcode;
                this.predictedDownOpcodeSet = true;
                
                this.predictionTimeout?.Dispose();
                this.predictionTimeout = null;

                if (Verbose)
                {
                    PluginLog.Log("Confirmed PingUp:{OpcodeUp} with PingDown:{OpcodeDown}",
                        "0x" + this.predictedUpOpcode.ToString("X"), "0x" + opcode.ToString("X"));
                }
            }
        }

        private void TrackPredictedPingUp(IntPtr dataPtr, ushort opcode)
        {
            this.predictedUpOpcodeTimestamp = (uint) Marshal.ReadInt32(dataPtr);
            
            this.predictedUpOpcode = opcode;
            this.predictedUpOpcodeSet = true;
            
            // Set a timer for 2 seconds, after which we go back to checking PingUp opcodes
            this.predictionTimeout?.Dispose();
            this.predictionTimeout = new Timer(_ =>
            {
                if (!this.predictedDownOpcodeSet)
                {
                    this.predictedUpOpcodeSet = false;

                    if (Verbose)
                    {
                        PluginLog.Log("No match found, resetting.");
                    }
                }
            });
            this.predictionTimeout.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);

            if (Verbose)
            {
                PluginLog.Log("Testing PingUp:{OpcodeUp}", "0x" + opcode.ToString("X"));
            }
        }

        private void CalculatePing(IntPtr dataPtr, ushort opcode, NetworkMessageDirection direction)
        {
            if (direction == NetworkMessageDirection.ZoneDown && opcode == this.predictedDownOpcode)
            {
                // The response packet has the same timestamp as the request packet, so we can just
                // take it from here instead of keeping state.
                var prevMs = (uint) Marshal.ReadInt32(dataPtr);
                
                if (QueryPerformanceCounter(out var nextNs))
                {
                    var nextMs = nextNs / 10000;
                    this.pingMs = nextMs - prevMs;
                    this.gotPing = true;

                    if (Verbose)
                    {
                        PluginLog.LogDebug("Packet ping: {LastPing}ms", this.pingMs);
                    }
                }
                else
                {
                    PluginLog.LogError("Failed to call QueryPerformanceCounter! (How can this happen?)");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.network.NetworkMessage -= OnNetworkMessage;
                this.predictionTimeout?.Dispose();
            }
            
            base.Dispose(disposing);
        }
        
        // http://pinvoke.net/default.aspx/kernel32.QueryPerformanceCounter
        // "For this particular method, execution time is often critical. ... This will prevent the
        // runtime from doing a security stack walk at runtime."
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
    }
}