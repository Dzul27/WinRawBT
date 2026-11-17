# Product Requirements Document (PRD): AndroidRawBT — Bluetooth ESC/POS Print Bridge for Android

| Document Metadata | Details |
| --- | --- |
| **Product Name** | AndroidRawBT (working name; Android companion of WinRawBT) |
| **Document Version** | 1.0 (draft) |
| **Date** | November 17, 2026 |
| **Target Platform** | Android 8.0+ (`minSdk` 26, `targetSdk` 35), phones and tablets, ARM64 / ARMv7 / x86_64 |
| **Core Technology Stack** | Kotlin, Jetpack Compose, Coroutines, Ktor CIO server (HTTP + WebSocket), Android `BluetoothSocket` (RFCOMM/SPP), DataStore |
| **Repository** | Separate repo (`Dzul27/AndroidRawBT`), same license as WinRawBT |
| **Distribution** | Signed APK on GitHub Releases (pre-release first); F-Droid after stabilization; Google Play deferred |
| **Status** | Draft — ready for engineering review |

---

## 1. Executive Summary & Problem Statement

### 1.1 Problem Statement

Android tablets and phones are common Web POS terminals. Printing a receipt silently to a Bluetooth thermal printer from a browser requires a bridge app, because browsers cannot open Bluetooth Classic RFCOMM sockets. The established bridge, RawBT, is closed-source and ad/branding-driven, and its behaviour cannot be audited or adapted.

WinRawBT already provides the same workflow on Windows (`http://localhost:<port>` + WebSocket, raw ESC/POS, connect-on-demand RFCOMM). POS integrators who support both platforms currently need two different bridges with different behaviour and different failure modes.

### 1.2 Product Vision

**AndroidRawBT** is a lightweight, open-source Android app that runs a foreground service exposing the **same local print API as WinRawBT** (`http://127.0.0.1:<port>/` and `ws://127.0.0.1:<port>/`, default 8080). A POS script written once works on Windows and Android without changes. The user selects a paired printer, taps **Test Connection**, taps **Start Service**, and prints from the browser with no print dialog, no account, and no ads.

### 1.3 Positioning

* **Not a RawBT clone:** it is the Android half of the WinRawBT family — identical API contract, decoder, CORS rules, and queue semantics.
* **Open-source, ad-free, account-free, no telemetry.**
* **Compatible with RawBT-style payloads and `rawbt:` links** (see FR-SVC-06) so existing integrations keep working.

---

## 2. Goals & Success Metrics

| Goal | Metric / KPI | Target |
| --- | --- | --- |
| **Fast first-time setup** | Time for a non-technical user to go from install to a printed test receipt (printer already paired) | < 60 seconds |
| **High print reliability** | Successful print rate across printer sleep/wake cycles | > 99% |
| **Low print latency** | Time from HTTP/WS request receipt to first byte written to the RFCOMM socket | < 800 ms |
| **Minimal resource footprint** | Idle RAM with service running | < 80 MB |
| **Small distribution size** | Release APK (R8 minified, no heavy libraries) | < 8 MB |
| **Cross-platform API parity** | POS script behaviour identical against WinRawBT and AndroidRawBT | No platform-specific code paths required in client |

---

## 3. User Personas & Use Cases

* **Retail Cashier / Store Operator (Android tablet):** Opens the app, confirms the printer with one tap, leaves the service running, and prints from a browser-based POS without any system print dialog.
* **POS Software Developer:** Targets one local endpoint (`http://localhost:8080`, `ws://localhost:8080`; port configurable) on both Windows and Android; may also use `rawbt:` links.
* **IT Support / Field Technician:** Needs a readable activity log, a one-tap hardware test print, and clear indication when the OS has killed the service.

---

## 4. Functional Requirements

### 4.1 Bluetooth Device Management (FR-BT)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-BT-01** | **Paired Device Enumeration:** List bonded Bluetooth Classic devices (`BluetoothAdapter.bondedDevices`) showing name and MAC address. Requires `BLUETOOTH_CONNECT` on Android 12+. | P0 |
| **FR-BT-02** | **Manual Refresh:** A "Refresh" action re-reads bonded devices without restarting the service. | P0 |
| **FR-BT-03** | **Device Persistence:** Persist the last selected printer MAC (and all settings) in DataStore; auto-select on launch. | P1 |
| **FR-BT-04** | **Scan & Pair:** A "Scan & Pair" action runs Bluetooth discovery (~12 s) and lists nearby devices, marking unbonded ones `[not paired]` (unnamed shown as `(unnamed)`). Selecting an unpaired device offers to pair via `BluetoothDevice.createBond()`; the Android system pairing dialog handles confirmation/PIN entry — the app never guesses or auto-submits a PIN. Unpaired devices are never used by Test Connection or Start Service. Scan/pair is disabled while the service is running; failure or cancel re-selects the previous printer and offers a shortcut to Android Bluetooth settings. | P0 |
| **FR-BT-05** | **Permission Flow:** Request `BLUETOOTH_CONNECT` and `BLUETOOTH_SCAN` (declared `neverForLocation`) on Android 12+; legacy `BLUETOOTH`, `BLUETOOTH_ADMIN` (`maxSdkVersion=30`) and `ACCESS_FINE_LOCATION` (`maxSdkVersion=30`, discovery only) on Android 8–11. A denied permission shows an inline explanation and a shortcut to app settings. If Bluetooth is off, offer to enable it. | P0 |

### 4.2 Connection Testing (FR-TEST)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-TEST-01** | **One-Tap Test Print:** "Test Connection" opens an RFCOMM socket to the selected printer using SPP UUID `00001101-0000-1000-8000-00805F9B34FB` and sends a formatted ESC/POS byte array. | P0 |
| **FR-TEST-02** | **Diagnostic Receipt Content:** Same content as WinRawBT: app header, `CONNECTED`, printer MAC, timestamp, followed by feed/cut (`GS V 66 0`). | P0 |
| **FR-TEST-03** | **UI State Locking:** Button disabled during a test; result (success or error details) written to the Activity Log. | P0 |
| **FR-TEST-04** | **Paper Width:** Settings provides "Paper width": 58 mm (32 columns, default) or 80 mm (48 columns), persisted in DataStore. It only sizes the Test Connection receipt (separator rules). Print jobs from the POS are forwarded byte-for-byte, so the receipt layout for 58 mm or 80 mm paper is the POS's responsibility (documented in README). Automatic width detection and layout conversion are out of scope. | P1 |

### 4.3 Local Print Bridge Service (FR-SVC)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-SVC-01** | **Start / Stop Toggle:** One toggle starts/stops a **Foreground Service** (type `connectedDevice`) with a persistent notification showing status, port, and a *Stop* action. | P0 |
| **FR-SVC-02** | **HTTP POST Listener:** Listen on `http://127.0.0.1:<port>/` (default 8080; configurable 1024–65535 in the UI, editable only while stopped, persisted). Accept `POST` with raw bytes (`application/octet-stream`) or Base64 / RawBT-style text (`text/plain`, `application/json`). The payload decoder is functionally identical to WinRawBT's `PayloadDecoder`: `data:` URIs, JSON wrappers (`{"data":"..."}`, byte arrays), URL-encoded/multipart forms, hex text, URL-safe / wrapped / unpadded Base64, and `rawbt:base64,...` / `intent:base64,...#Intent;...;end;` bodies. Raw binary is never re-encoded. Each job logs Content-Type, byte counts, decode path, and a hex+ASCII preview. | P0 |
| **FR-SVC-03** | **CORS, Private-Network Headers & Origin Control:** Answer `OPTIONS` preflight. Default `AllowAnyOrigin = true` → accept every origin, send `Access-Control-Allow-Origin: *`. When `false`, a request carrying an `Origin` header is accepted only if listed in `AllowedOrigins`, else `403` before any data is printed (covers simple cross-site POSTs and WebSocket upgrades). Requests without `Origin` are always accepted. For allowed origins the matching origin is echoed. Preflight responses include the headers required by Chrome Local Network Access (e.g. `Access-Control-Allow-Private-Network: true`). | P0 |
| **FR-SVC-04** | **WebSocket Compatibility:** Accept WebSocket upgrades on `ws://127.0.0.1:<port>/` (same port) for drop-in compatibility with existing RawBT-style JavaScript clients. | P1 |
| **FR-SVC-05** | **Serialized Print Queue:** All jobs (HTTP, WS, deep link, test print) pass through a single FIFO mutex (`kotlinx.coroutines.sync.Mutex`) so concurrent requests never collide on the Bluetooth socket. | P0 |
| **FR-SVC-06** | **`rawbt:` Deep Link:** Register an `intent-filter` for the `rawbt:` scheme (not generic `intent:`). A `rawbt:base64,...` link opened by the browser or another app is decoded by the shared decoder and printed through the same queue, then the activity closes. If RawBT is also installed, Android's app chooser applies. | P1 |

### 4.4 Android UX, Logging & Lifecycle (FR-UX)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-UX-01** | **Real-Time Activity Log:** Timestamped, auto-scrolling log of service status, incoming byte counts, and Bluetooth/socket errors; "Clear Log" action. Memory-only, capped (e.g. last 500 lines). | P0 |
| **FR-UX-02** | **Two-Screen UI (Compose):** *Main* — printer dropdown, Refresh, Scan & Pair, Port field, Test Connection, Start/Stop, auto-start switches, status, log. *Settings* — paper width (58 / 80 mm), `AllowAnyOrigin`, `AllowedOrigins` editor, language, battery guidance, About. | P0 |
| **FR-UX-03** | **Localization:** English and Indonesian, following system locale (`values/` and `values-in/`). | P1 |
| **FR-UX-04** | **Auto-Start:** Two switches, both default **off**: (a) start the service on device boot (`RECEIVE_BOOT_COMPLETED`), (b) start the service when the app opens and a default printer is saved. | P2 |
| **FR-UX-05** | **Onboarding Screen:** First launch walks through (1) Bluetooth permissions, (2) notification permission (Android 13+), (3) battery-optimization exemption (`REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`) with per-OEM guidance links (Xiaomi, Oppo/Realme, Vivo, Samsung, Huawei), (4) a note that Chrome will show a Local Network Access prompt on first POS use. | P0 |
| **FR-UX-06** | **Service Health Indicator:** Main screen shows `Running (port N)` / `Stopped` / `Stopped by system` (detected via a persisted "should be running" flag vs. actual service state) with a one-tap restart. | P1 |

---

## 5. Non-Functional Requirements (NFR)

1. **Connect-on-Demand Sockets:** Thermal printers sleep and drop idle RFCOMM links. Open the socket per job, write, flush, wait ~250 ms for hardware buffer drain, then close. Never hold a permanent connection. Cancel discovery before connecting.
2. **Network Isolation:** Bind strictly to `127.0.0.1` (never `0.0.0.0`) in v1. **Accepted risk:** any other app on the same device can reach loopback and print; `AllowedOrigins` only protects against browsers. Optional token authentication is deferred.
3. **Foreground-Service Compliance:** Android 14+ requires a declared FGS type (`connectedDevice`) with matching permissions (`FOREGROUND_SERVICE_CONNECTED_DEVICE`, `BLUETOOTH_CONNECT`). Starting an FGS from the background on Android 12+ is restricted; start only from user action or the boot receiver. [INFERENCE: re-verify boot-start behaviour per target API before release.]
4. **Permissions Footprint:** `INTERNET` (loopback server), `BLUETOOTH_CONNECT`, `BLUETOOTH_SCAN`, `POST_NOTIFICATIONS`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_CONNECTED_DEVICE`, `RECEIVE_BOOT_COMPLETED`, `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`. No location (Android 12+), no storage, no contacts, no analytics SDKs.
5. **Privacy:** No telemetry, no account, no cloud calls. Print payloads are never persisted; the log shows previews only and is memory-only.
6. **Robust Server Lifecycle:** Port conflicts (`BindException`) abort start cleanly with a user-visible message. The service survives configuration changes and UI process death.

---

## 6. Technical Architecture & API Specification

### 6.1 Data Flow

```
[Web POS in Chrome / other app]
      │  HTTP POST / WebSocket / rawbt: deep link
      ▼
[Ktor CIO Server on 127.0.0.1:<port>]  ← Foreground Service (connectedDevice)
      │  Origin check → Payload Decoder (shared) → byte[]
      ▼
[FIFO Mutex Queue]
      │  BluetoothSocket.createRfcommSocketToServiceRecord(SPP UUID)
      ▼
[Android RFCOMM / SPP stack] ───► [Paired Bluetooth Thermal Printer]
```

### 6.2 Local Print API Contract

Identical to WinRawBT.

**Endpoint:** `POST http://localhost:8080/`

* **Option A (Base64 / RawBT text):** `Content-Type: text/plain`, body `G0AbYQFB...`
* **Option B (Raw binary):** `Content-Type: application/octet-stream`, body raw bytes

**Responses**

* `200 OK`: `{"status": "OK", "bytesSent": 248}`
* `GET /`: returns service status (same as WinRawBT).
* `400 Bad Request`: `{"status": "ERROR", "message": "Empty payload or invalid Base64"}`
* `403 Forbidden`: origin rejected (`AllowAnyOrigin=false` and origin not listed)
* `500 Internal Server Error`: `{"status": "ERROR", "message": "Bluetooth connection failed: <reason>"}`

### 6.3 Platform Constraint: Chrome Local Network Access

Since Chrome 142 a website calling loopback addresses triggers a **permission prompt** to the user; Chrome 156 (stable Oct 20, 2026) removed the temporary enterprise opt-out. This is a browser-side behaviour the server cannot bypass. AndroidRawBT's responsibilities: send correct preflight headers (FR-SVC-03), explain the prompt in onboarding (FR-UX-05) and README, and keep WebSocket usable. No flag-flipping workarounds are documented. [INFERENCE: exact prompt behaviour on Chrome for Android must be verified on-device during QA.]

---

## 7. User Interface (UI) Specification

```
+--------------------------------------------+
| AndroidRawBT                      [⚙ Settings] |
+--------------------------------------------+
| Printer                                    |
| [ RPP02N (DC:0D:51:58:..)             ▾ ]  |
| [ Refresh ]   [ Scan & Pair ]              |
|                                            |
| Port  [ 8080 ]                             |
| [ Test Connection ]  [ ▶ Start Service ]   |
|                                            |
| [ ] Start on device boot                   |
| [ ] Start when app opens                   |
|--------------------------------------------|
| Activity Log                    [Clear]    |
| 08:01:10 Loaded 3 paired devices.          |
| 08:01:15 Testing 00:11:22:33:44:55...      |
| 08:01:17 Test OK. Receipt printed.         |
| 08:01:22 Service started 127.0.0.1:8080    |
| 08:02:05 Job 512 bytes ... Printed OK.     |
|--------------------------------------------|
| ● Running (port 8080)                      |
+--------------------------------------------+
```

---

## 8. Edge Cases & Error Handling Matrix

| Scenario | System Behavior | User Feedback |
| --- | --- | --- |
| **Printer off / out of range** | `BluetoothSocket.connect()` throws `IOException` after timeout; socket closed, mutex released; HTTP `500`. | Log: `[ERROR] Printer unreachable or powered off.` |
| **Printer asleep** | Connect-on-demand wakes it; one automatic retry after a short delay before failing. | Log shows retry attempt. |
| **Port already in use** | `BindException` at start; service does not enter foreground. | Dialog: port occupied or another instance is running. |
| **Bluetooth turned off** | Job fails fast without connect attempt; HTTP `500`. | Notification/log: Bluetooth is off, with enable shortcut in app. |
| **Permission revoked while running** | `SecurityException` caught; service keeps running but jobs fail with a clear message. | Status shows `Permission missing`; tap opens app settings. |
| **Notification permission denied (Android 13+)** | Service still runs as FGS (notification may be hidden by the system). | Onboarding warns that status/stop controls may be invisible. |
| **OS kills service (Doze / OEM)** | Persisted "should be running" flag differs from real state on next app open. | Status `Stopped by system`; one-tap restart; link to battery guidance. |
| **Printer not paired** | Scan lists it `[not paired]`; selecting offers system pairing via `createBond()`. | Success → selectable and saved. Failure → reason logged, previous printer restored, shortcut to Bluetooth settings. |
| **Rapid consecutive prints from POS** | FIFO mutex processes jobs in order. | Log shows each job queued/completed in order. |
| **Chrome LNA prompt denied** | Browser blocks the request before it reaches the server (no log line). | README/onboarding: re-allow "Local network access" for the site in Chrome site settings. |
| **Origin rejected** | `403` before printing. | Log: `[WARN] Rejected request from origin "..."` with config hint. |
| **`rawbt:` link with invalid payload** | Decoder error; nothing printed. | Toast + log entry. |

---

## 9. Release Milestones

* **Phase 1 (MVP):** Compose UI (Main), paired-device list, permission flow, Test Connection, Foreground Service with HTTP POST listener (`127.0.0.1:8080`), shared payload decoder (Base64/raw/RawBT/JSON/hex), FIFO queue, activity log, configurable port, onboarding with battery exemption.
* **Phase 2 (Production Readiness):** WebSocket listener, CORS + `AllowAnyOrigin`/`AllowedOrigins` + LNA preflight headers, Settings screen, Scan & Pair, `rawbt:` deep link, auto-start switches (boot / app open), service health indicator, EN + ID localization, signed APK on GitHub Releases (pre-release `v0.1.0-beta`).
* **Phase 3 (Distribution):** F-Droid submission, README (EN + ID) with Chrome LNA guidance, printer compatibility issue template.
* **Out of scope / deferred:** BLE (GATT) printers; HTML/image-to-ESC/POS rasterizer and automatic paper-width detection or layout conversion; multi-printer routing (`?printer=kitchen`); USB and Wi-Fi/LAN printers; token authentication; Google Play distribution.

---

## 10. Open Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| OEM background-kill policies | Service stops mid-shift | Onboarding exemption flow, health indicator, one-tap restart |
| Chrome LNA prompt friction | Cashiers see an unexpected prompt | Onboarding + README guidance; correct preflight headers |
| Other local apps can print (loopback is device-wide) | Unauthorized print jobs | Documented accepted risk; token auth planned post-v1 |
| Printer variance (SPP quirks, sleep timing, 58 mm vs 80 mm) | Failed or garbled prints | Retry-once policy, 250 ms drain, printer-report issue template; test on both 58 mm and 80 mm printers before release (WinRawBT has only confirmed EPPOS 58 mm so far) |
| Android 14/15 FGS and boot-start rules | Auto-start may be blocked on some versions | Verify per API level; fall back to manual start with clear status |
