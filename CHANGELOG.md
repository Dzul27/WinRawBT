# Changelog

All notable changes to WinRawBT. Format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

## [0.4.0-beta] - 2026-11-17

- New **Paper** selector (58 mm / 80 mm) in the main window. It sizes the Test Connection receipt (32 or 48 columns, saved as `PaperWidthMm` in `config.json`). Jobs from the POS are still sent untouched.

## [0.3.0-beta] - 2026-11-17

- The listener port is now configurable from the **Port** field in the main window (1024-65535, editable only while the service is stopped; default 8080). Invalid values in `config.json` reset to 8080. The Activity Log shows the full URLs on start.
- The status bar now shows the version and a link to the project page (`WinRawBT v0.3.0 · by Dzul27`).

## [0.2.0-beta] - 2026-11-17

- New **Allowed websites** button: choose "Allow all websites" or "Only allow these websites" (add/remove addresses). Applies immediately, no service restart, saved to `config.json`.
- Any browser origin is now allowed by default, so a web POS works with no setup.

## [0.1.0-beta] - 2026-11-17

First public beta.

- Windows tray app that bridges a browser-based POS to a paired Bluetooth thermal printer (SPP/RFCOMM) over `http://localhost:8080/` and `ws://localhost:8080/`.
- Accepts raw ESC/POS bytes, Base64, `rawbt:base64,...` / `intent:base64,...`, `data:` URIs, JSON wrappers and hex text.
- Origin control (`AllowAnyOrigin`, default `true`; `AllowedOrigins`): set `AllowAnyOrigin` to `false` to answer HTTP 403 to unlisted browser origins.
- Connect-on-demand RFCOMM sockets, serialized jobs, activity log with hex preview, optional start with Windows.
- Distribution: about 132 KB zip (`WinRawBT.exe` + `InTheHand.Net.Bluetooth.dll`), .NET Framework 4.8.
- Tested printer: EPPOS 58 mm.
