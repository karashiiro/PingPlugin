#pragma once

#include <array>
#include <vector>

#define DllExport __declspec( dllexport )

DllExport unsigned long GetFFXIVRemoteAddress(int pid);

const int COURIER_LITTLE_ENDIAN = 1;
inline bool isLittleEndian() {
	// ReSharper disable once CppCStyleCast
	return *(char*)&COURIER_LITTLE_ENDIAN == 1;
}

inline unsigned long littleEndian(unsigned long val) {
	if (isLittleEndian())
		return _byteswap_ulong(val);
	return val;
}