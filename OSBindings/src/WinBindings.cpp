#ifdef _WIN32

#include "OSBindings.h"
#include "Utils.h"

#include "WinSock2.h"
#include "Windows.h"

#include "TlHelp32.h"

// ReSharper disable once CppUnusedIncludeDirective
#include "ws2ipdef.h" // This defines a macro that allows access to MIB_TCP6TABLE_OWNER_PID
#include "iphlpapi.h"

DllExport unsigned long GetProcessFirstIngressConnection(unsigned long pid) {
	DWORD bufferLength = 0;

	// This will fail, but assign the correct buffer size to bufferLength
    DWORD status = GetExtendedTcpTable(nullptr, &bufferLength, FALSE, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
	// Size the buffer accordingly
    auto tcpTable = static_cast<MIB_TCPTABLE_OWNER_PID*>(malloc(bufferLength));
	// Pass in the correctly-sized buffer and buffer length
    status = GetExtendedTcpTable(tcpTable, &bufferLength, FALSE, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);

    for (DWORD i = 0; i < tcpTable->dwNumEntries; i++) {
        const DWORD tcpPid = tcpTable->table[i].dwOwningPid;
        //DWORD tcpLocalAddr = tcpTable->table[i].dwLocalAddr;
        //uint16_t tcpLocalPort = littleEndian(static_cast<uint16_t>(tcpTable->table[i].dwLocalPort));
        const DWORD tcpRemoteAddr = tcpTable->table[i].dwRemoteAddr;
        //uint16_t tcpRemotePort = littleEndian(static_cast<uint16_t>(tcpTable->table[i].dwRemotePort));
        
        if (tcpPid == pid) {
            return tcpRemoteAddr;
        }
    }

    // Deallocate memory assigned to the table
    free(tcpTable);
    return 0;
}

DllExport const unsigned char* GetProcessFirstIngressConnectionIPv6(unsigned long pid) {
	DWORD bufferLength = 0;
    
    DWORD status = GetExtendedTcpTable(nullptr, &bufferLength, FALSE, AF_INET6, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
    auto tcpTable = static_cast<MIB_TCP6TABLE_OWNER_PID*>(malloc(bufferLength));
    status = GetExtendedTcpTable(tcpTable, &bufferLength, FALSE, AF_INET6, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);

    for (DWORD i = 0; i < tcpTable->dwNumEntries; i++) {
        const DWORD tcpPid = tcpTable->table[i].dwOwningPid;
        //UCHAR* tcpLocalAddr = tcpTable->table[i].ucLocalAddr;
        //uint16_t tcpLocalPort = littleEndian(static_cast<uint16_t>(tcpTable->table[i].dwLocalPort));
        const UCHAR* tcpRemoteAddr = tcpTable->table[i].ucRemoteAddr;
        //uint16_t tcpRemotePort = littleEndian(static_cast<uint16_t>(tcpTable->table[i].dwRemotePort));
        
        if (tcpPid == pid) {
            return tcpRemoteAddr;
        }
    }

    free(tcpTable);
    return nullptr;
}

#endif