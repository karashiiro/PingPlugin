// ReSharper disable CppCStyleCast
#ifdef _WIN32

#include "OSBindings.h"

#include <WinSock2.h>
#include <Windows.h>
#include <Ws2tcpip.h>
#include <iphlpapi.h>

// The highest port is not actually the one with the zone packets, but it's on the same address as the one we want.
DllExport unsigned long GetProcessHighestPortAddress(int pid) {
	DWORD bufferLength = 0;

	// This will fail, but assign the correct buffer size to bufferLength
	DWORD status = GetExtendedTcpTable(nullptr, &bufferLength, FALSE, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
	// Size the buffer accordingly
	auto tcpTable = static_cast<MIB_TCPTABLE_OWNER_PID*>(malloc(bufferLength));
	// Pass in the correctly-sized buffer and buffer length
	status = GetExtendedTcpTable(tcpTable, &bufferLength, FALSE, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);

	DWORD finalAddr = 0;
	DWORD maxPort = 0;
	for (DWORD i = 0; i < tcpTable->dwNumEntries; i++) {
		const DWORD state = tcpTable->table[i].dwState;
		const DWORD tcpPid = tcpTable->table[i].dwOwningPid;
		const DWORD tcpRemoteAddr = tcpTable->table[i].dwRemoteAddr;
		const DWORD tcpLocalPort = littleEndian(tcpTable->table[i].dwLocalPort);

		if (state == MIB_TCP_STATE_LISTEN || tcpRemoteAddr == LOCALHOST) continue;
		
		if (tcpPid == pid && tcpLocalPort > maxPort) { // This is specific to FFXIV, but oh well, it performs best
			maxPort = tcpLocalPort;
			finalAddr = tcpRemoteAddr;
		}
	}

	free(tcpTable);
	return finalAddr;
}

DllExport unsigned long GetAddressLastRTT(unsigned long address) {
	ULONG rtt = 0;
	ULONG hopCount = 0;
	GetRTTAndHopCount(address, &hopCount, 51, &rtt);
	return rtt;
}

#endif
