# WinRawBT: print from a web POS to a Bluetooth thermal printer on Windows

**English** | [Bahasa Indonesia](README.id.md)

**WinRawBT is a free, open-source Windows tray app that lets a browser-based POS print ESC/POS receipts to a paired Bluetooth thermal printer.** It listens on `http://localhost:8080/` and `ws://localhost:8080/` by default (the port is changeable in the app), takes raw ESC/POS bytes, and sends them over a Bluetooth RFCOMM (Serial Port Profile) socket. No COM-port setup, no Windows print dialog, no printer driver step.

It is a Windows take on the idea behind the Android app **RawBT**: pick a paired printer, click *Start Service*, and print. The download is **about 132 KB zipped** (a 140 KB `.exe` plus one DLL) and runs on the .NET Framework 4.8 that already ships with Windows 10 and 11.

![WinRawBT main window: Bluetooth printer list, Start Service and Test Connection](docs/screenshot.png)

> **Status: beta (v0.1.x).** Not affiliated with or endorsed by the RawBT project. See [Tested printers](#tested-printers) and [Known issues](#known-issues).

**Jump to:** [Install](#install) · [Allow your web POS](#allow-your-web-pos-important) · [Usage](#usage-from-a-web-page) · [FAQ](#faq) · [Comparison](#how-does-winrawbt-compare-to-other-options) · [Tested printers](#tested-printers) · [Known issues](#known-issues)

## Features

- Lists paired Bluetooth devices, or scans and pairs new ones (Secure Simple Pairing, no PIN guessing).
- Test Connection button prints a diagnostic receipt.
- Local HTTP `POST` and WebSocket endpoint on `localhost` only.
- Accepts raw bytes, Base64, RawBT-style `rawbt:base64,...` / `intent:base64,...` URLs, `data:` URIs, JSON wrappers, hex text.
- Connect-on-demand: opens the RFCOMM socket per job, so printers that sleep keep working. Jobs are serialized.
- System tray, optional start with Windows, activity log with hex preview of each job.
- Single small .NET Framework 4.8 executable (built into Windows 10/11), one dependency: [32feet.NET](https://github.com/inthehand/32feet).

## Requirements

- Windows 10 or 11 with a working Bluetooth adapter
- A Bluetooth thermal printer that exposes the Serial Port Profile (SPP)
- To build: .NET SDK 8 or newer (targets `net48`)

## Install

1. Download the zip from [Releases](../../releases) and extract it anywhere (keep `WinRawBT.exe` and `InTheHand.Net.Bluetooth.dll` together).
2. Run `WinRawBT.exe`, choose your printer, click **Test Connection**, then **Start Service**.

## Choose which websites can print

By default **any website** can send print jobs to the bridge, so a web POS works with no setup. The bridge only listens on `localhost`, but that also means any page open in a browser on the same PC can print.

Click the **Allowed websites** button in the main window to pick one of two modes. Changes apply immediately, no restart:

- **Allow all websites** (default): every website may print. Easiest.
- **Only allow these websites**: type your POS address (for example `pos.example.com`) and click **Add**. Everything else gets HTTP 403.

The choice is stored in `%AppData%\WinRawBT\config.json` (`AllowAnyOrigin`, `AllowedOrigins`), which you can also edit by hand while the app is closed. Origins are exact (scheme + host + port, no path); a page opened from a `file://` path sends the origin `null`. Requests without an `Origin` header (curl, desktop apps) are always accepted. A rejected origin is shown in the Activity Log with the fix.

## Usage from a web page

```js
// Raw bytes
await fetch("http://localhost:8080/", {
  method: "POST",
  headers: { "Content-Type": "application/octet-stream" },
  body: new Uint8Array([0x1b, 0x40, ...new TextEncoder().encode("Hello\n"), 0x1d, 0x56, 0x42, 0x00]),
});

// Base64
await fetch("http://localhost:8080/", {
  method: "POST",
  headers: { "Content-Type": "text/plain" },
  body: btoa("\x1b@Hello\n\x1dV\x42\x00"),
});

// WebSocket
const ws = new WebSocket("ws://localhost:8080/");
ws.onopen = () => ws.send(btoa("\x1b@Hello\n"));
```

Responses: `200 {"status":"OK","bytesSent":N}`, `400` bad/empty payload, `403` origin not allowed, `500` printer unreachable. `GET /` returns service status.

From a shell: `curl -X POST --data-binary @receipt.bin -H "Content-Type: application/octet-stream" http://localhost:8080/`

`TestClient/test-pos.html` is a ready-made test page (add `"null"` to `AllowedOrigins` if you open it as a local file).

The examples use the default port 8080; replace `8080` with your port if you changed it.

## Other config keys

| Key | Default | Meaning |
|---|---|---|
| `Port` | `8080` | Listener port. Change it with the **Port** field in the main window (only while the service is stopped, range 1024-65535) or here. Your POS URLs must use the same port |
| `AutoStartService` | `false` | Start the service on launch when a printer is saved |
| `MinimizeToTray` | `true` | Closing the window hides to tray |
| `PrependInit` | `false` | Prepend `ESC @` to jobs that do not start with it |
| `PaperWidthMm` | `58` | Paper width of the Test Connection receipt: `58` (32 columns) or `80` (48 columns). Change it with the **Paper** selector in the main window. It does not affect jobs from your POS, which are sent as-is, so the POS controls the layout for its paper width |

## FAQ

### How do I print from a web POS to a Bluetooth thermal printer on Windows?

Pair the printer in Windows, run WinRawBT, pick the printer, click **Start Service**, then `fetch("http://localhost:8080/", { method: "POST", body: bytes })` from your POS page with ESC/POS bytes. Full snippets are in [Usage from a web page](#usage-from-a-web-page).

### Is there a RawBT for Windows?

RawBT is an Android app and has no Windows version. WinRawBT follows the same workflow on Windows (paired printer, local endpoint, raw ESC/POS bytes) and accepts RawBT-style `rawbt:base64,...` payloads. It is an independent project, not affiliated with RawBT.

### Why not use the browser's Web Bluetooth API?

Web Bluetooth only talks to Bluetooth Low Energy (GATT) devices. Most low-cost 58 mm and 80 mm receipt printers use classic Bluetooth with the Serial Port Profile (SPP/RFCOMM), which a web page cannot open. WinRawBT opens that RFCOMM socket from a native process and exposes it to the page over `localhost`.

### Do I need to create a COM port or install a printer driver?

No. WinRawBT connects to the paired device directly through a Bluetooth RFCOMM socket (using 32feet.NET). You do not map a virtual COM port, and jobs bypass the Windows print dialog and spooler because the bytes are sent raw.

### Which Bluetooth printers work?

Printers that expose the Serial Port Profile (SPP) over classic Bluetooth and accept ESC/POS. Confirmed so far: EPPOS 58 mm. See [Tested printers](#tested-printers) and please [report your model](../../issues/new?template=printer-report.yml). BLE-only printers are not supported.

### What does error 10049 AddressNotAvailable mean?

Windows could not find an SPP service on the device. This usually means the printer is dual-mode or BLE-only, or was paired in a way that did not register the "Serial Port" service. Remove the printer in Windows Bluetooth settings, pair it again as a classic Bluetooth device, and check that Windows lists a "Serial Port" service for it.

### Is it safe to run a local print server that websites can reach?

The bridge binds only to `127.0.0.1`. There is no authentication, and by default any website open on that PC can print (`AllowAnyOrigin: true`). Do not expose the port to a network; set `AllowAnyOrigin` to `false` and list your POS in `AllowedOrigins` if you browse untrusted sites on the same PC.

### Does it convert images or HTML to ESC/POS?

No. WinRawBT forwards bytes only. Generate the ESC/POS bytes in your POS (any ESC/POS encoder library works) and send the result.

## How does WinRawBT compare to other options?

| | WinRawBT | RawBT | QZ Tray | Web Bluetooth |
|---|---|---|---|---|
| Platform | Windows 10/11 | Android | Windows, macOS, Linux | Chromium browsers |
| Classic Bluetooth SPP printers | Yes, direct RFCOMM | Yes | Usually through a virtual COM port | No (BLE only) |
| Needs COM-port setup | No | No | Typically yes for Bluetooth | No |
| Interface for a web POS | `localhost` HTTP + WebSocket | App intent / URL scheme | WebSocket + JS library | Browser API |
| Runtime | .NET Framework 4.8 (built in), about 132 KB zip | Android app | Java | None |
| License | MIT | Free Android app | Open source | n/a |

Comparison reflects each project's public description at the time of writing and is meant as orientation, not a benchmark. Check the other projects' documentation before deciding.

## Build

```
dotnet build -c Release
```

Output is in `bin/Release/net48/`.

## Tested printers

| Printer | Result |
|---|---|
| EPPOS 58 mm thermal printer | Works: test receipt and printing from the bridge |
| Other models | Please [open a printer report](../../issues/new?template=printer-report.yml) with the model, Windows version, and the Activity Log |

## Known issues

- Some printers fail to connect with `10049 AddressNotAvailable` (Windows does not find an SPP service on the device). Dual-mode / BLE-only printers are the usual cause. Re-pair the printer as a classic Bluetooth device and check that Windows shows a "Serial Port" service for it.
- No image or HTML-to-ESC/POS conversion; the bridge forwards bytes only.
- One printer at a time.

## Security notes

- Binds to `localhost` / `127.0.0.1` only. No authentication; protection against websites is the origin allowlist above.
- Do not expose the port to a network.

## Contributing

Issues and pull requests are welcome. For a printer problem, include the printer model and the Activity Log output. See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

[MIT](LICENSE). Uses 32feet.NET (MIT).
