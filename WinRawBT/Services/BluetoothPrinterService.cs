using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using WinRawBT.Models;

namespace WinRawBT.Services;

/// <summary>
/// Bluetooth printer discovery and communication service (TASK-04, TASK-05)
/// </summary>
public class BluetoothPrinterService : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
    private BluetoothClient? _bluetoothClient;
    
    // Standard Serial Port Profile UUID for Bluetooth printers
    private static readonly Guid SppUuid = new Guid("00001101-0000-1000-8000-00805F9B34FB");

    public BluetoothPrinterService()
    {
        try
        {
            _bluetoothClient = new BluetoothClient();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bluetooth initialization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get list of paired Bluetooth devices (TASK-04)
    /// </summary>
    public List<PairedPrinterItem> GetPairedDevices()
    {
        var devices = new List<PairedPrinterItem>();

        try
        {
            if (_bluetoothClient == null)
            {
                System.Diagnostics.Debug.WriteLine("Bluetooth client not initialized");
                return devices;
            }

            // Check if Bluetooth radio is available and enabled
            if (!BluetoothRadio.IsSupported)
            {
                System.Diagnostics.Debug.WriteLine("Bluetooth is not supported on this system");
                return devices;
            }

            var radio = BluetoothRadio.PrimaryRadio;
            if (radio == null || radio.Mode == RadioMode.PowerOff)
            {
                System.Diagnostics.Debug.WriteLine("Bluetooth radio is turned off");
                return devices;
            }

            // Get paired devices
            var pairedDevices = _bluetoothClient.PairedDevices;
            
            foreach (var device in pairedDevices)
            {
                devices.Add(new PairedPrinterItem
                {
                    Name = device.DeviceName,
                    BluetoothAddress = device.DeviceAddress
                });
            }

            // Sort by name
            devices = devices.OrderBy(d => d.Name).ToList();
        }
        catch (Exception ex)
        {
            // Return empty list on error (per TASK-04 DoD)
            System.Diagnostics.Debug.WriteLine($"Error getting paired devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Send raw bytes to Bluetooth printer (TASK-05)
    /// Thread-safe implementation with semaphore lock
    /// </summary>
    public async Task<bool> SendRawBytesAsync(BluetoothAddress address, byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));
        }

        // Use semaphore to ensure thread-safe sequential access (TASK-05)
        await _sendLock.WaitAsync().ConfigureAwait(false);
        
        try
        {
            var endPoint = new BluetoothEndPoint(address, SppUuid);
            
            using var client = new BluetoothClient();
            
            // Connect to printer
            await Task.Run(() => client.Connect(endPoint)).ConfigureAwait(false);
            
            // Get the network stream
            var stream = client.GetStream();
            
            // Write the payload
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            
            // Wait 250ms for hardware buffer transmission (per NFR)
            await Task.Delay(250).ConfigureAwait(false);
            
            // Close connection
            stream.Close();
            client.Close();
            
            return true;
        }
        catch (SocketException ex)
        {
            // Socket error - printer unreachable, powered off, etc.
            System.Diagnostics.Debug.WriteLine($"Bluetooth connection error: {ex.Message}");
            throw new Exception($"Printer unreachable or powered off: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending to printer: {ex.Message}");
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _bluetoothClient?.Dispose();
        _sendLock.Dispose();
    }
}
