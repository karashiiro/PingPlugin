package bindings

import (
	"syscall"
	"unsafe"
)

var (
	iphlpapi                       = syscall.NewLazyDLL("iphlpapi.dll")
	procGetExtendedTCPTable        = iphlpapi.NewProc("GetExtendedTcpTable")
	procGetRTTAndHopCount          = iphlpapi.NewProc("GetRTTAndHopCount")
	afinet                  uint32 = 2
)

// MIBTCPTableOwnerPID is a Golang equivalent of _MIB_TCPTABLE_OWNER_PID
type MIBTCPTableOwnerPID struct {
	DwNumEntries uint32
	Table        [1]MIBTCPRowOwnerPID
}

// MIBTCPRowOwnerPID is a Golang equivalent of _MIB_TCPROW_OWNER_PID
type MIBTCPRowOwnerPID struct {
	DwState      uint32
	DwLocalAddr  uint32
	DwLocalPort  uint32
	DwRemoteAddr uint32
	DwRemotePort uint32
	DwOwningPid  uint32
}

// GetTCPTable returns the extended TCP table on Windows. Heavily constrained for simplicity when adding the Linux equivalent of this function.
func GetTCPTable() (*[]MIBTCPRowOwnerPID, error) {
	var buffer []byte
	var pTCPTable *MIBTCPTableOwnerPID
	var dwSize uint32
	for {
		ret, _, errno := procGetExtendedTCPTable.Call(
			uintptr(unsafe.Pointer(pTCPTable)),
			uintptr(unsafe.Pointer(&dwSize)),
			uintptr(uint32(0)), // FALSE
			uintptr(afinet),
			uintptr(uint32(4)), // TCP_TABLE_OWNER_PID_CONNECTIONS
			uintptr(uint32(0)), // Reserved
		)
		if ret != 0 {
			if syscall.Errno(ret) == syscall.ERROR_INSUFFICIENT_BUFFER {
				buffer = make([]byte, int(dwSize))
				pTCPTable = (*MIBTCPTableOwnerPID)(unsafe.Pointer(&buffer[0]))
				continue
			}
			return nil, errno
		}
		rows := make([]MIBTCPRowOwnerPID, int(pTCPTable.DwNumEntries))
		for i := 0; i < int(pTCPTable.DwNumEntries); i++ {
			offset := uintptr(i) * unsafe.Sizeof(pTCPTable.Table[0])
			rows[i] = *(*MIBTCPRowOwnerPID)(unsafe.Pointer(uintptr(unsafe.Pointer(&pTCPTable.Table[0])) + offset))
		}
		return &rows, nil
	}
}

// GetRTTAndHopCount returns the RTT and hop count of the provided address, on Windows.
func GetRTTAndHopCount(address uint64, maxHops int64) (uint64, uint64, error) {
	rtt := uint64(0)
	hopCount := uint64(0)
	procGetRTTAndHopCount.Call(
		uintptr(address),
		uintptr(unsafe.Pointer(&rtt)),
		uintptr(maxHops),
		uintptr(unsafe.Pointer(&hopCount)),
	)
	return rtt, hopCount, nil
}
