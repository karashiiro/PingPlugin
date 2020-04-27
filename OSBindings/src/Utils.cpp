#include "Utils.h"

#include <intrin.h>

const int COURIER_LITTLE_ENDIAN = 1;
bool isLittleEndian() {
	// ReSharper disable once CppCStyleCast
	return *(char*)&COURIER_LITTLE_ENDIAN == 1;
}

unsigned short littleEndian(unsigned short val) {
    if (isLittleEndian())
        return _byteswap_ushort(val);
    return val;
}

unsigned long littleEndian(unsigned long val) {
    if (isLittleEndian())
        return _byteswap_ulong(val);
    return val;
}

unsigned long long littleEndian(unsigned long long val) {
    if (isLittleEndian())
        return _byteswap_uint64(val);
    return val;
}