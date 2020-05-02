#ifdef _WIN32

#include "OSBindings.h"

#include "WinSock2.h"
#include "Windows.h"

#include "TlHelp32.h"

#include "iphlpapi.h"

DllExport unsigned long GetFFXIVRemoteAddress(int pid) {
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
		const DWORD tcpRemotePort = littleEndian(tcpTable->table[i].dwRemotePort);

		if (state == MIB_TCP_STATE_LISTEN || tcpRemoteAddr == 16777343)
			continue;
		
		if (tcpPid == pid && tcpRemotePort > maxPort) { // This is specific to FFXIV, but oh well, it performs best
			maxPort = tcpRemotePort;
			finalAddr = tcpRemoteAddr;
		}
	}

	// Deallocate memory assigned to the table
	free(tcpTable);
	return finalAddr;
}

#endif
