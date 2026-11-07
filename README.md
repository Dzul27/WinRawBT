# WinRawBT - Windows Bluetooth ESC/POS Print Bridge

A lightweight C# Windows desktop application that enables web-based Point of Sale (Web POS) applications to print directly to Bluetooth thermal printers via HTTP and WebSocket, replicating the Android RawBT workflow on Windows.

## Features

✅ **Zero COM-Port Configuration** - Select paired Bluetooth printer by MAC address  
✅ **HTTP POST Listener** - Accept print jobs at `http://localhost:40213/`  
✅ **WebSocket Support** - Compatible with existing RawBT JavaScript SDKs  
✅ **CORS Enabled** - Works with HTTPS web applications  
✅ **Thread-Safe Printing** - Sequential print queue with semaphore lock  
✅ **System Tray Mode** - Run in background while service is active  
✅ **Windows Startup** - Auto-start with Windows option  
✅ **Single Instance** - Prevents port conflicts  

## Requirements

- **Windows 10/11** (x64)
- **.NET 8.0 Runtime** (for development) or Self-Contained (for deployment)
- **Bluetooth-enabled thermal printer** supporting ESC/POS

## Quick Start

### 1. Build and Run

```bash
cd WinRawBT
dotnet build
dotnet run
```

### 2. Configure Printer

1. Pair your Bluetooth thermal printer in Windows Settings
2. Launch WinRawBT
3. Click "Refresh" to detect paired printers
4. Select your printer from the dropdown
5. Click "Test Connection" to verify

### 3. Start Service

1. Click "Start Service (40213)" to begin listening
2. The service will listen on `http://localhost:40213/`

### 4. Print from Web App

Use the test harness in `TestClient/test-pos.html` or integrate with your web application.

## Architecture

```
[Web POS Application]
       │
       ├─ HTTP POST → http://localhost:40213/
       └─ WebSocket → ws://localhost:40213/
       │
       ▼
[WinRawBT Print Bridge Server]
       │
       ▼
[Bluetooth RFCOMM / SPP]
       │
       ▼
[Paired Thermal Printer]
```

## API Documentation

### HTTP POST Endpoint

**URL:** `POST http://localhost:40213/`

**Headers:**
- `Content-Type: text/plain` (Base64-encoded data)
- `Content-Type: application/octet-stream` (Raw binary)

**Success Response (200 OK):**
```json
{
  "status": "OK",
  "bytesSent": 248
}
```

**Error Response (400/500):**
```json
{
  "status": "ERROR",
  "message": "Printer unreachable or powered off"
}
```

### WebSocket Endpoint

**URL:** `ws://localhost:40213/`

- Accepts binary or Base64-encoded text frames
- Automatically forwards to selected printer

## Project Structure

```
WinRawBT/
├── Program.cs                   # Entry point & single-instance mutex
├── WinRawBT.csproj             # Project configuration
├── Models/
│   ├── AppConfig.cs            # Configuration model
│   └── PairedPrinterItem.cs    # Bluetooth device model
├── Services/
│   ├── ConfigService.cs        # Configuration persistence
│   ├── BluetoothPrinterService.cs  # Bluetooth RFCOMM communication
│   ├── PrintBridgeServer.cs    # HTTP/WebSocket server
│   └── StartupRegistryService.cs    # Windows startup management
├── Helpers/
│   ├── EscPosBuilder.cs        # ESC/POS command builder
│   └── PayloadDecoder.cs       # Base64/Raw payload decoder
└── Forms/
    ├── MainForm.cs             # Main UI logic
    └── MainForm.Designer.cs    # UI layout
```

## Building Single-File Executable

To create a portable `.exe` that runs without installing .NET:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `bin/Release/net8.0-windows/win-x64/publish/WinRawBT.exe`

## Configuration

Configuration is stored in `%AppData%\WinRawBT\config.json`:

```json
{
  "lastPrinterMac": "001122334455",
  "lastPrinterName": "RPP02N-Thermal",
  "port": 40213,
  "autoStartService": false,
  "minimizeToTray": true,
  "runOnStartup": false
}
```

## Dependencies

- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [InTheHand.Net.Bluetooth (32feet.NET)](https://github.com/inthehand/32feet)
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json)

## Troubleshooting

### "Port 40213 is already in use"
- Only one instance can run at a time
- Check if another application is using the port

### "Printer unreachable or powered off"
- Ensure printer is turned on
- Check Bluetooth is enabled on PC
- Verify printer is paired in Windows Settings

### "Bluetooth is not supported"
- Ensure PC has Bluetooth hardware
- Check Bluetooth drivers are installed

## Testing

Use the included test harness:

```bash
# Open in browser
start TestClient/test-pos.html
```

Or use curl:

```bash
# Send Base64-encoded ESC/POS data
curl -X POST http://localhost:40213/ -d "G0AbYQFBTkRST0lEIFJBV0JUIEFMTCBPTg=="
```

## License

See [LICENSE](LICENSE) file.

## Acknowledgments

Inspired by [RawBT](https://play.google.com/store/apps/details?id=ru.a402m.rawbt) for Android.
