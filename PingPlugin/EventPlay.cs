using System.Runtime.InteropServices;

namespace PingPlugin
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EventPlay
    {
        public readonly ulong ActorId;
        public readonly uint EventId;
        public readonly ushort Scene;
        public readonly ushort Padding1;
        public readonly uint Flags;
        public readonly uint Param3;
        public readonly byte Param4;
        public readonly byte Padding2;
        public readonly ushort Padding3;
        public readonly uint Param5;
        public readonly ulong Unknown;
    }
}
