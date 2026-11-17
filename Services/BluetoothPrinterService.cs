using System.Net.Sockets;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using WinRawBT.Models;

namespace WinRawBT.Services;

/// <summary>
/// Enumerates paired Bluetooth devices (TASK-04) and sends raw ESC/POS bytes over
/// a connect-on-demand RFCOMM/SPP socket, serialized with a semaphore (TASK-05).
/// </summary>
public sealed class BluetoothPrinterService : IDisposable
{
    /// <summary>Standard Serial Port Profile (SPP) service UUID.</summary>
    private static readonly Guid SppUuid = new("00001101-0000-1000-8000-00805F9B34FB");

    // FIFO gate ensures only one RFCOMM socket is active at a time (FR-SVC-05).
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Action<string> _log;

    private bool _disposed;

    public BluetoothPrinterService(Action<string>? logger = null)
    {
        _log = logger ?? (_ => { });
    }

    /// <summary>
    /// Return all paired Bluetooth devices. Returns an empty list (and logs a warning)
    /// when Bluetooth is unavailable or turned off, rather than throwing.
    /// </summary>
    public List<BluetoothDeviceItem> GetPairedDevices()
    {
        var results = new List<BluetoothDeviceItem>();
        try
        {
            using var client = new BluetoothClient();
            foreach (BluetoothDeviceInfo device in client.PairedDevices)
            {
                string mac = FormatMac(device.DeviceAddress);
                results.Add(new BluetoothDeviceItem(device.DeviceName, mac, isPaired: true));
            }
        }
        catch (Exception ex)
        {
            _log($"[WARN] Could not enumerate Bluetooth devices (is Bluetooth on?): {ex.Message}");
            return new List<BluetoothDeviceItem>();
        }

        return results;
    }

    /// <summary>Time spent in the Bluetooth inquiry during a scan.</summary>
    private static readonly TimeSpan InquiryDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Run a Bluetooth inquiry (~10 s) off the UI thread and return every device found
    /// (nearby discoverable + remembered + authenticated), merged with the paired list.
    /// Paired devices sort first, then by name, then MAC. Never throws: returns whatever
    /// could be found (possibly empty) and logs a warning when Bluetooth is unavailable.
    /// </summary>
    public async Task<List<BluetoothDeviceItem>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            _log("[WARN] Bluetooth scan canceled.");
            return new List<BluetoothDeviceItem>();
        }

        try
        {
            return await Task.Run(DiscoverCore, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log($"[WARN] Bluetooth scan failed ({ex.GetType().Name}): {ex.Message}");
            return new List<BluetoothDeviceItem>();
        }
        finally
        {
            ReleaseGate();
        }
    }

    private List<BluetoothDeviceItem> DiscoverCore()
    {
        var byMac = new Dictionary<string, BluetoothDeviceItem>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var client = new BluetoothClient { InquiryLength = InquiryDuration };
            // The Windows implementation of this overload inquires for nearby discoverable devices
            // and also returns remembered and authenticated (paired) ones.
            IReadOnlyCollection<BluetoothDeviceInfo> found = client.DiscoverDevices(255);

            foreach (BluetoothDeviceInfo device in found)
            {
                string mac = FormatMac(device.DeviceAddress);
                string? name = device.DeviceName;
                // Unnamed devices report their raw hex address as name.
                if (string.Equals(name, device.DeviceAddress.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, mac, StringComparison.OrdinalIgnoreCase))
                {
                    name = null;
                }

                byMac[mac] = new BluetoothDeviceItem(name, mac, device.Authenticated);
            }
        }
        catch (Exception ex)
        {
            _log($"[WARN] Bluetooth scan failed (is Bluetooth on?): {ex.Message}");
        }

        // Make sure every paired device is present even if the inquiry missed it.
        // GetPairedDevices logs its own warning when Bluetooth is unavailable.
        foreach (BluetoothDeviceItem paired in GetPairedDevices())
        {
            if (!byMac.TryGetValue(paired.BluetoothAddress, out BluetoothDeviceItem? existing))
            {
                byMac[paired.BluetoothAddress] = paired;
            }
            else if (!existing.IsPaired)
            {
                byMac[paired.BluetoothAddress] = new BluetoothDeviceItem(
                    existing.IsUnnamed ? paired.Name : existing.Name, paired.BluetoothAddress, isPaired: true);
            }
        }

        return byMac.Values
            .OrderByDescending(d => d.IsPaired)
            .ThenBy(d => d.IsUnnamed)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => d.BluetoothAddress, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pair with the device at <paramref name="macAddress"/> with a single Secure Simple Pairing request
    /// (empty PIN; never a guessed PIN), then enable its Serial Port (SPP) service. Devices that need a PIN
    /// must be paired by the user in Windows Bluetooth settings. The native pairing call cannot be aborted,
    /// so on cancellation this returns immediately while the call finishes (and releases the gate) in the
    /// background. Never throws; failure details are returned and logged.
    /// </summary>
    public async Task<PairResult> PairAsync(string macAddress, string pin = "", CancellationToken cancellationToken = default)
    {
        if (!TryParseMac(macAddress, out BluetoothAddress address))
        {
            string detail = $"Invalid Bluetooth address: {macAddress}";
            _log($"[ERROR] {detail}");
            return new PairResult(false, detail);
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return new PairResult(false, "Pairing canceled.", canceled: true);
        }

        Task<PairResult> work = Task.Run(() => PairCore(address, pin ?? string.Empty));
        Task canceledSignal = Task.Delay(Timeout.Infinite, cancellationToken);
        Task finished = await Task.WhenAny(work, canceledSignal).ConfigureAwait(false);
        if (finished != work)
        {
            _ = work.ContinueWith(_ => ReleaseGate(), TaskScheduler.Default);
            _log("[WARN] Pairing canceled; Windows may take a little while to abandon the request.");
            return new PairResult(false, "Pairing canceled.", canceled: true);
        }

        ReleaseGate();
        return await work.ConfigureAwait(false);
    }

    private PairResult PairCore(BluetoothAddress address, string pin)
    {
        string mac = FormatMac(address);
        try
        {
            var device = new BluetoothDeviceInfo(address);
            try { device.Refresh(); } catch (Exception ex) { _log($"[WARN] Could not refresh device info for {mac}: {ex.Message}"); }

            string method = "already paired";
            if (!device.Authenticated)
            {
                method = pin.Length == 0 ? "no PIN (Secure Simple Pairing)" : "PIN entered by the user";
                _log($"[INFO] Pairing with {mac} using {method} (one attempt; no PIN is guessed)...");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                bool paired;
                try
                {
                    // Empty PIN, never null: a null PIN makes the 32feet native auth callback throw on a native thread and kills the process.
                    paired = BluetoothSecurity.PairRequest(address, pin);
                }
                catch (Exception ex)
                {
                    string error = $"Pairing request threw {ex.GetType().Name}: {ex.Message}";
                    _log($"[ERROR] {error} (after {timer.Elapsed.TotalSeconds:0.0} s)");
                    return new PairResult(false, error + " " + ManualPairingAdvice);
                }

                if (!paired)
                {
                    string detail = $"Windows refused the pairing request after {timer.Elapsed.TotalSeconds:0.0} s " +
                        "(no PIN / Secure Simple Pairing). The printer is usually not in pairing mode, is connected to " +
                        "another phone/PC, or needs a PIN. " + ManualPairingAdvice;
                    _log($"[ERROR] {detail}");
                    return new PairResult(false, detail);
                }

                _log($"[OK] Paired with {mac} using {method} in {timer.Elapsed.TotalSeconds:0.0} s.");
            }
            else
            {
                _log($"[INFO] {mac} is already paired.");
            }

            try
            {
                device.Refresh();
                device.SetServiceState(BluetoothService.SerialPort, true);
                _log($"[INFO] Serial Port (SPP) service enabled for {mac}.");
            }
            catch (Exception ex)
            {
                // Pairing itself succeeded; Windows often enables SPP on its own.
                _log($"[WARN] Paired, but could not enable the Serial Port service for {mac}: {ex.Message}");
            }

            return new PairResult(true, $"Paired with {mac} ({method}).");
        }
        catch (Exception ex)
        {
            string detail = $"Pairing failed ({ex.GetType().Name}): {ex.Message}";
            _log($"[ERROR] {detail}");
            return new PairResult(false, detail);
        }
    }

    private const string ManualPairingAdvice =
        "Put the printer in pairing mode (and disconnect it from other devices), or pair it yourself in " +
        "Windows Bluetooth settings (enter the PIN there if it asks for one), then click Refresh.";

    private void ReleaseGate()
    {
        try { _gate.Release(); }
        catch (ObjectDisposedException) { /* service disposed while an operation was in flight */ }
    }

    /// <summary>
    /// Connect on-demand to the given address over SPP, write the payload, flush, wait
    /// 250 ms for the hardware buffer, then dispose the socket. Thread-safe/FIFO.
    /// Returns false (never throws) on any failure.
    /// </summary>
    public async Task<bool> SendRawBytesAsync(
        BluetoothAddress address,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        if (payload == null || payload.Length == 0)
        {
            _log("[ERROR] Refusing to send empty payload.");
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => SendCore(address, payload), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log("[ERROR] Print job canceled.");
            return false;
        }
        finally
        {
            ReleaseGate();
        }
    }

    /// <summary>Overload accepting a MAC string such as XX:XX:XX:XX:XX:XX.</summary>
    public Task<bool> SendRawBytesAsync(
        string macAddress,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseMac(macAddress, out BluetoothAddress address))
        {
            _log($"[ERROR] Invalid Bluetooth address: {macAddress}");
            return Task.FromResult(false);
        }

        return SendRawBytesAsync(address, payload, cancellationToken);
    }

    /// <summary>Upper bound for the RFCOMM connect attempt (the BT stack usually gives up after ~5 s).</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private bool SendCore(BluetoothAddress address, byte[] payload)
    {
        BluetoothClient? client = null;
        try
        {
            client = new BluetoothClient();
            BluetoothEndPoint endpoint = ResolveEndpoint(address);

            _log($"[INFO] Connecting to {FormatMac(address)} ...");
            BluetoothClient connecting = client;
            Task connectTask = Task.Run(() => connecting.Connect(endpoint));
            if (!connectTask.Wait(ConnectTimeout))
            {
                // Closing the socket aborts the pending Connect; observe its fault so it is not unobserved.
                try { client.Close(); } catch { /* ignore */ }
                _ = connectTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                _log($"[ERROR] Connect to {FormatMac(address)} timed out after {ConnectTimeout.TotalSeconds:0} s. {HintFor(SocketError.TimedOut)}");
                return false;
            }

            using NetworkStream stream = client.GetStream();
            stream.Write(payload, 0, payload.Length);
            stream.Flush();

            // NFR-1: allow the hardware buffer to transmit before closing.
            Thread.Sleep(250);

            _log($"[OK] Sent {payload.Length} bytes to {FormatMac(address)}.");
            return true;
        }
        catch (Exception ex)
        {
            _log($"[ERROR] {Describe(ex)}");
            return false;
        }
        finally
        {
            try { client?.Close(); } catch { /* ignore */ }
            client?.Dispose();
        }
    }

    /// <summary>
    /// Prefer the RFCOMM channel found in the device's SDP record: Windows' own connect-by-UUID path
    /// fails with 10049 on printers whose (valid) SPP record it refuses to match. Falls back to the UUID.
    /// </summary>
    private BluetoothEndPoint ResolveEndpoint(BluetoothAddress address)
    {
        int? channel = SppChannelResolver.TryResolve(address, SppUuid, out bool serviceNotFound, out string failure);
        if (channel is int found)
        {
            _log($"[INFO] Serial Port (SPP) service found on RFCOMM channel {found}.");
            return new BluetoothEndPoint(address, BluetoothService.Empty, found);
        }

        _log(serviceNotFound
            ? $"[WARN] No SPP/RFCOMM service on {FormatMac(address)}: {failure}. BLE-only printers are not supported."
            : $"[WARN] Could not resolve the SPP channel ({failure}); trying the SPP UUID instead.");
        return new BluetoothEndPoint(address, SppUuid);
    }

    /// <summary>Full, untruncated description of a failure including socket error code/name and a hint.</summary>
    private static string Describe(Exception ex)
    {
        Exception root = ex;
        while (root is AggregateException { InnerExceptions.Count: > 0 } agg)
        {
            root = agg.InnerExceptions[0];
        }

        Exception? cursor = root;
        SocketException? socket = null;
        while (cursor != null)
        {
            if (cursor is SocketException se)
            {
                socket = se;
                break;
            }

            cursor = cursor.InnerException;
        }

        if (socket == null)
        {
            return $"Print failed ({root.GetType().Name}): {root.Message}";
        }

        return $"Printer connection failed [{socket.SocketErrorCode} / error {socket.ErrorCode}]: " +
               $"{socket.Message} -> {HintFor(socket.SocketErrorCode)}";
    }

    private static string HintFor(SocketError code) => code switch
    {
        SocketError.TimedOut or SocketError.HostDown or SocketError.HostUnreachable or SocketError.NetworkDown or
        SocketError.ConnectionRefused or SocketError.NetworkUnreachable
            => "Hint: printer is off, asleep, out of range, or already connected to another device (phone/PC). " +
               "Power it on, disconnect it elsewhere and try again; if it still fails, remove and re-pair it in Windows.",
        SocketError.AccessDenied
            => "Hint: access denied - check that the printer is paired and Bluetooth is enabled.",
        SocketError.AddressNotAvailable
            => "Hint: Windows found no usable Serial Port (SPP/RFCOMM) service on this device. It may be a BLE-only " +
               "printer (not supported) or the pairing is stale: remove the device in Windows Bluetooth settings, " +
               "put the printer in pairing mode and pair it again.",
        _ => "Hint: check that Bluetooth is on and the printer is paired, powered on and in range."
    };

    public static string FormatMac(BluetoothAddress address)
    {
        byte[] bytes = address.ToByteArray(); // little-endian
        // BluetoothAddress stores the 6 significant bytes least-significant first.
        return string.Join(":",
            bytes.Take(6).Reverse().Select(b => b.ToString("X2")));
    }

    public static bool TryParseMac(string mac, out BluetoothAddress address)
    {
        address = BluetoothAddress.None;
        if (string.IsNullOrWhiteSpace(mac))
        {
            return false;
        }

        string normalized = mac.Replace(":", string.Empty).Replace("-", string.Empty).Trim();
        return BluetoothAddress.TryParse(normalized, out address);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
