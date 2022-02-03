namespace PingPlugin
{
    public enum WinError
    {
        UNKNOWN = -1,
        NO_ERROR = 0,
        ACCESS_DENIED = 5,
        NOT_ENOUGH_MEMORY = 8,
        OUTOFMEMORY = 14,
        NOT_SUPPORTED = 50,
        INVALID_PARAMETER = 87,
        ERROR_INVALID_NETNAME = 1214,
        WSAEINTR = 10004,
        WSAEACCES = 10013,
        WSAEFAULT = 10014,
        WSAEINVAL = 10022,
        WSAEWOULDBLOCK = 10035,
        WSAEINPROGRESS = 10036,
        WSAEALREADY = 10037,
        WSAENOTSOCK = 10038,
        WSAENETUNREACH = 10051,
        WSAENETRESET = 10052,
        WSAECONNABORTED = 10053,
        WSAECONNRESET = 10054,
        IP_REQ_TIMED_OUT = 11010,
    }
}