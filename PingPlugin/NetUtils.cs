using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace PingPlugin
{
    public static class NetUtils
    {
        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_CONNECTIONS = 4;
        private const int MIB_TCP_STATE_LISTEN = 2;

#pragma warning disable 649
        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRow
        {
            public readonly uint dwState;
            public readonly uint dwLocalAddr;
            public readonly uint dwLocalPort;
            public readonly uint dwRemoteAddr;
            public readonly uint dwRemotePort;
            public readonly uint dwOwningPid;
        }
#pragma warning restore 649

        [DllImport("Iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0);

        public static IPAddress GetXIVServerAddress(int pid)
        {
            var bufferLength = 0;
            GetExtendedTcpTable(new IntPtr(0), ref bufferLength, false, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS);
            var pTcpTable = Marshal.AllocHGlobal(bufferLength);

            var finalAddr = IPAddress.Loopback;
            try
            {
                GetExtendedTcpTable(pTcpTable, ref bufferLength, false, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS);

                var table = new List<TcpRow>();
                var rowSize = Marshal.SizeOf<TcpRow>();
                var dwNumEntries = Marshal.ReadInt32(pTcpTable);
                var pRows = pTcpTable + 4;
                for (var i = 0; i < dwNumEntries && bufferLength - (4 + i * rowSize) >= rowSize; i++)
                {
                    var nextRow = Marshal.PtrToStructure<TcpRow>(pRows + i * rowSize);
                    table.Add(nextRow);
                }

                for (var i = 0; i < table.Count; i++)
                {
                    var state = table[i].dwState;
                    var tcpPid = table[i].dwOwningPid;
                    var tcpRemoteAddr = new IPAddress(table[i].dwRemoteAddr);
                    var tcpRemotePort = (ushort)table[i].dwRemotePort;
                    var trpBytes = BitConverter.GetBytes(tcpRemotePort).Reverse().ToArray();
                    tcpRemotePort = BitConverter.ToUInt16(trpBytes, 0);

                    if (state == MIB_TCP_STATE_LISTEN || IPAddress.Loopback.Equals(tcpRemoteAddr)) continue;

                    // ReSharper disable once InvertIf
                    if ((int)tcpPid == pid && InXIVPortRange(tcpRemotePort))
                    {
                        finalAddr = tcpRemoteAddr;
                        break;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pTcpTable);
            }

            return finalAddr;
        }

        private const ushort XIV_MIN_PORT_1 = 54992;
        private const ushort XIV_MAX_PORT_1 = 54994;
        private const ushort XIV_MIN_PORT_2 = 55006;
        private const ushort XIV_MAX_PORT_2 = 55007;
        private const ushort XIV_MIN_PORT_3 = 55021;
        private const ushort XIV_MAX_PORT_3 = 55040;
        private const ushort XIV_MIN_PORT_4 = 55296;
        private const ushort XIV_MAX_PORT_4 = 55551;

        private static bool InXIVPortRange1(ushort port)
        {
            return port >= XIV_MIN_PORT_1 && port <= XIV_MAX_PORT_1;
        }

        private static bool InXIVPortRange2(ushort port)
        {
            return port >= XIV_MIN_PORT_2 && port <= XIV_MAX_PORT_2;
        }

        private static bool InXIVPortRange3(ushort port)
        {
            return port >= XIV_MIN_PORT_3 && port <= XIV_MAX_PORT_3;
        }

        private static bool InXIVPortRange4(ushort port)
        {
            return port >= XIV_MIN_PORT_4 && port <= XIV_MAX_PORT_4;
        }

        private static bool InXIVPortRange(ushort port)
        {
            return InXIVPortRange1(port) || InXIVPortRange2(port) || InXIVPortRange3(port) || InXIVPortRange4(port);
        }

        [DllImport("Iphlpapi.dll", EntryPoint = "GetRTTAndHopCount", SetLastError = true)]
        public static extern ulong GetRTTAndHopCount(ulong address, ref ulong hopCount, ulong maxHops, ref ulong rtt);
    }
}