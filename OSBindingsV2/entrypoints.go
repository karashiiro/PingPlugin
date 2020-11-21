package main

import "C"
import "github.com/karashiiro/PingPlugin/OSBindingsV2/bindings"

// TCPStateListen represents the ID of the listening state of a TCP connection
var TCPStateListen uint32 = 2

//export GetProcessHighestPortAddress
// GetProcessHighestPortAddress returns the IP address associated with the highest port associated with the specified process ID.
func GetProcessHighestPortAddress(pid int) uint64 {
	var finalAddr uint64
	var maxPort uint32

	rows, err := bindings.GetTCPTable()
	if err != nil {
		return 0 // We can't actually return an error in this context, use GetLastError instead
	}

	for _, row := range *rows {
		if row.DwState == TCPStateListen || row.DwRemoteAddr == uint32(16777343) {
			continue
		}

		if row.DwOwningPid == uint32(pid) && row.DwRemotePort > maxPort {
			maxPort = row.DwRemotePort
			finalAddr = uint64(row.DwRemoteAddr)
		}
	}

	return finalAddr
}

//export GetAddressLastRTT
// GetAddressLastRTT returns the last round-trip time of a message to the provided IP address.
func GetAddressLastRTT(address uint64) uint64 {
	rtt, _, err := bindings.GetRTTAndHopCount(address, 51)
	if err != nil {
		return 0 // We can't actually return an error in this context, use GetLastError instead
	}
	return rtt
}

func main() {
	// Noop
}
