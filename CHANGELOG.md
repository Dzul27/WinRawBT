# Changelog

All notable changes to WinRawBT. Format follows [Keep a Changelog](https://keepachangelog.com/).

## [0.1.0-beta] - 2026-11-17

First public beta.

- Windows tray app that bridges a browser-based POS to a paired Bluetooth thermal printer (SPP/RFCOMM) over `http://localhost:8080/` and `ws://localhost:8080/`.
- Accepts raw ESC/POS bytes, Base64, `rawbt:base64,...` / `intent:base64,...`, `data:` URIs, JSON wrappers and hex text.
- Origin control (`AllowAnyOrigin`, default `true`; `AllowedOrigins`): set `AllowAnyOrigin` to `false` to answer HTTP 403 to unlisted browser origins.
- Connect-on-demand RFCOMM sockets, serialized jobs, activity log with hex preview, optional start with Windows.
- Distribution: about 132 KB zip (`WinRawBT.exe` + `InTheHand.Net.Bluetooth.dll`), .NET Framework 4.8.
- Tested printer: EPPOS 58 mm.
