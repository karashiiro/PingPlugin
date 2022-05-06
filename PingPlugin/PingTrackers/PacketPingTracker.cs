using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Network;
using Dalamud.Logging;
using PingPlugin.GameAddressDetectors;

namespace PingPlugin.PingTrackers
{
    public class PacketPingTracker : PingTracker
    {
        private readonly GameNetwork network;
        private readonly Stopwatch pingTimer;
        
        private Timer predictionTimeout;
        
        private bool predictedUpOpcodeSet;
        private uint predictedUpOpcodeTimestamp;
        private ushort predictedUpOpcode;
        
        private bool predictedDownOpcodeSet;
        private ushort predictedDownOpcode;

        private long pingMs;

        public PacketPingTracker(PingConfiguration config, GameAddressDetector addressDetector, GameNetwork network) : base(config, addressDetector)
        {
            this.pingTimer = new Stopwatch();
            
            this.network = network;
            this.network.NetworkMessage += OnNetworkMessage;
        }

        protected override async Task PingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NextRTTCalculation((ulong) this.pingMs);
                await Task.Delay(3000, token);
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
            if (this.predictedUpOpcodeSet && !this.predictedDownOpcodeSet && direction == NetworkMessageDirection.ZoneDown)
            {
                CheckPredictedPingDown(dataPtr, opcode);
            }
            else if (!this.predictedUpOpcodeSet && direction == NetworkMessageDirection.ZoneUp)
            {
                TrackPredictedPingUp(dataPtr, opcode);
            }

            // Calculate ping
            if (this.predictedDownOpcodeSet && this.predictedUpOpcodeSet)
            {
                CalculatePing(opcode, direction);
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
            var timestamp = (uint) Marshal.ReadInt32(dataPtr);

            this.predictedUpOpcode = opcode;
            this.predictedUpOpcodeTimestamp = timestamp;
            this.predictedUpOpcodeSet = true;
                
            this.pingTimer.Reset();
            this.pingTimer.Start();

            // Set a timer for 15 seconds, after which we go back to checking PingUp opcodes
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
            this.predictionTimeout.Change(Timeout.InfiniteTimeSpan, new TimeSpan(0, 0, 15));

            if (Verbose)
            {
                PluginLog.Log("Testing PingUp:{OpcodeUp}", "0x" + opcode.ToString("X"));
            }
        }

        private void CalculatePing(ushort opcode, NetworkMessageDirection direction)
        {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (direction == NetworkMessageDirection.ZoneUp && opcode == this.predictedUpOpcode)
            {
                this.pingTimer.Restart();
            }
            else if (direction == NetworkMessageDirection.ZoneDown && opcode == this.predictedDownOpcode)
            {
                if (this.pingTimer.IsRunning)
                {
                    this.pingTimer.Stop();
                    this.pingMs = this.pingTimer.ElapsedMilliseconds;

                    if (Verbose)
                    {
                        PluginLog.LogDebug("Packet ping: {LastPing}ms", this.pingMs);
                    }
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
    }
}