using System.Runtime.InteropServices;
using InTheHand.Net;
using InTheHand.Net.Bluetooth.Sdp;

namespace WinRawBT.Services;

/// <summary>
/// Resolves the RFCOMM channel of a device's Serial Port service through a Win32 SDP query
/// (WSALookupServiceBegin, NS_BTH). Some cheap printers advertise a valid SPP record that the
/// Winsock "connect by service UUID" path rejects with error 10049, while connecting to the
/// channel number found here works.
/// </summary>
internal static class SppChannelResolver
{
    private const int NsBth = 16;
    private const int LupReturnName = 0x0010;
    private const int LupReturnBlob = 0x0200;
    private const int WsaServiceNotFound = 10108;
    private const int WsaNoMoreResults = 10110;
    private const int BufferSize = 64 * 1024;

    [StructLayout(LayoutKind.Sequential)]
    private struct WsaQuerySet
    {
        public int dwSize;
        public IntPtr lpszServiceInstanceName;
        public IntPtr lpServiceClassId;
        public IntPtr lpVersion;
        public IntPtr lpszComment;
        public int dwNameSpace;
        public IntPtr lpNSProviderId;
        public IntPtr lpszContext;
        public int dwNumberOfProtocols;
        public IntPtr lpafpProtocols;
        public IntPtr lpszQueryString;
        public int dwNumberOfCsAddrs;
        public IntPtr lpcsaBuffer;
        public int dwOutputFlags;
        public IntPtr lpBlob;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int cbSize;
        public IntPtr pBlobData;
    }

    [DllImport("ws2_32.dll", CharSet = CharSet.Unicode)]
    private static extern int WSALookupServiceBegin(ref WsaQuerySet query, int flags, out IntPtr handle);

    [DllImport("ws2_32.dll", CharSet = CharSet.Unicode)]
    private static extern int WSALookupServiceNext(IntPtr handle, int flags, ref int length, IntPtr results);

    [DllImport("ws2_32.dll")]
    private static extern int WSALookupServiceEnd(IntPtr handle);

    [DllImport("ws2_32.dll")]
    private static extern int WSAGetLastError();

    [DllImport("ws2_32.dll")]
    private static extern int WSAStartup(ushort versionRequested, IntPtr wsaData);

    /// <summary>
    /// Returns the RFCOMM channel (1..30) of <paramref name="service"/> on <paramref name="address"/>, or
    /// null with <paramref name="failure"/> describing why. <paramref name="serviceNotFound"/> is true when
    /// the device answered but advertises no such service.
    /// </summary>
    public static int? TryResolve(BluetoothAddress address, Guid service, out bool serviceNotFound, out string failure)
    {
        serviceNotFound = false;
        failure = string.Empty;

        IntPtr wsaData = IntPtr.Zero, classId = IntPtr.Zero, context = IntPtr.Zero, results = IntPtr.Zero, handle = IntPtr.Zero;
        try
        {
            wsaData = Marshal.AllocHGlobal(512);
            WSAStartup(0x0202, wsaData);

            classId = Marshal.AllocHGlobal(16);
            Marshal.Copy(service.ToByteArray(), 0, classId, 16);
            context = Marshal.StringToHGlobalUni("(" + BluetoothPrinterService.FormatMac(address).ToLowerInvariant() + ")");

            var query = new WsaQuerySet
            {
                dwNameSpace = NsBth,
                lpServiceClassId = classId,
                lpszContext = context,
            };
            query.dwSize = Marshal.SizeOf(query);

            const int flags = LupReturnName | LupReturnBlob;
            if (WSALookupServiceBegin(ref query, flags, out handle) != 0)
            {
                int error = WSAGetLastError();
                serviceNotFound = error == WsaServiceNotFound;
                failure = serviceNotFound
                    ? "the device advertises no Serial Port (SPP) service"
                    : $"SDP query failed (Winsock error {error})";
                return null;
            }

            results = Marshal.AllocHGlobal(BufferSize);
            while (true)
            {
                int length = BufferSize;
                if (WSALookupServiceNext(handle, flags, ref length, results) != 0)
                {
                    int error = WSAGetLastError();
                    if (error != WsaNoMoreResults)
                    {
                        failure = $"SDP query failed (Winsock error {error})";
                    }
                    break;
                }

                var set = Marshal.PtrToStructure<WsaQuerySet>(results);
                if (set.lpBlob == IntPtr.Zero)
                {
                    continue;
                }

                var blob = Marshal.PtrToStructure<Blob>(set.lpBlob);
                if (blob.pBlobData == IntPtr.Zero || blob.cbSize <= 0)
                {
                    continue;
                }

                var data = new byte[blob.cbSize];
                Marshal.Copy(blob.pBlobData, data, 0, blob.cbSize);

                int channel = ServiceRecordHelper.GetRfcommChannelNumber(ServiceRecord.CreateServiceRecordFromBytes(data));
                if (channel is >= 1 and <= 30)
                {
                    return channel;
                }
            }

            if (failure.Length == 0)
            {
                serviceNotFound = true;
                failure = "the device advertises no Serial Port (SPP) service with an RFCOMM channel";
            }

            return null;
        }
        catch (Exception ex)
        {
            failure = $"SDP query failed ({ex.GetType().Name}): {ex.Message}";
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero) { WSALookupServiceEnd(handle); }
            if (results != IntPtr.Zero) { Marshal.FreeHGlobal(results); }
            if (context != IntPtr.Zero) { Marshal.FreeHGlobal(context); }
            if (classId != IntPtr.Zero) { Marshal.FreeHGlobal(classId); }
            if (wsaData != IntPtr.Zero) { Marshal.FreeHGlobal(wsaData); }
        }
    }
}
