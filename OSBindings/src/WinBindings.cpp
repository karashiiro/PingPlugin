// ReSharper disable CppCStyleCast
#ifdef _WIN32

#include "OSBindings.h"

#include <WinSock2.h>
#include <Windows.h>
#include <Ws2tcpip.h>
#include <iphlpapi.h>
#include <tcpestats.h>

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

DllExport unsigned long GetAddressLastRTT(unsigned long address) {
	ULONG rtt = 0;

	DWORD bufferLength = 0;
	GetTcpTable(nullptr, &bufferLength, FALSE);
	const auto tcpTable = static_cast<MIB_TCPTABLE*>(malloc(bufferLength));
	GetTcpTable(tcpTable, &bufferLength, FALSE);

	PMIB_TCPROW tcpRow = nullptr;
	for (DWORD i = 0; i < tcpTable->dwNumEntries; i++) {
		if (tcpTable->table[i].dwRemoteAddr == address) {
			tcpRow = &tcpTable->table[i];
		}
	}

	if (tcpRow != nullptr) {
		const auto eStatsRowRw = static_cast<TCP_ESTATS_PATH_RW_v0*>(malloc(sizeof(TCP_ESTATS_PATH_RW_v0)));
		const auto rwSize = sizeof(TCP_ESTATS_PATH_RW_v0);
		GetPerTcpConnectionEStats(tcpRow, TcpConnectionEstatsPath,
			(PUCHAR)eStatsRowRw, 0, rwSize, 
			nullptr, 0, 0, 
			nullptr, 0, 0);
		if (!eStatsRowRw->EnableCollection) { // Data collection seems not to be enabled, let's enable it
			eStatsRowRw->EnableCollection = true;
			SetPerTcpConnectionEStats(tcpRow, TcpConnectionEstatsPath, (PUCHAR)eStatsRowRw, 0, rwSize, 0);
		}

		const auto eStatsRowRod = static_cast<TCP_ESTATS_PATH_ROD_v0*>(malloc(sizeof(TCP_ESTATS_PATH_ROD_v0)));
		const auto rodSize = sizeof(TCP_ESTATS_PATH_ROD_v0);
		GetPerTcpConnectionEStats(tcpRow, TcpConnectionEstatsPath,
			nullptr, 0, 0, 
			nullptr, 0, 0, 
			(PUCHAR)eStatsRowRod, 0, rodSize);
		rtt = eStatsRowRod->SampleRtt;
		
		free(eStatsRowRod);
		free(eStatsRowRw);
	}

	free(tcpTable);
	return rtt;
}

#endif
