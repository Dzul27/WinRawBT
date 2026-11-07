# Product Requirements Document (PRD): WinRawBT — Bluetooth ESC/POS Print Bridge for Windows

| Document Metadata | Details |
| --- | --- |
| **Product Name** | WinRawBT (Windows Raw Bluetooth Print Service) |
| **Document Version** | 1.0 |
| **Date** | November 7, 2026 |
| **Target Platform** | Windows 10 / Windows 11 (x64 / ARM64) |
| **Core Technology Stack** | C#, .NET 8 (WinForms/WPF), 32feet.NET (`InTheHand.Net.Bluetooth`), `HttpListener` |
| **Status** | Ready for Engineering |

---

## 1. Executive Summary & Problem Statement

### 1.1 Problem Statement

Web-based Point of Sale (Web POS) and lightweight desktop applications struggle with silent, direct thermal printing on Windows via Bluetooth. Standard Windows workflows require users to pair a Bluetooth printer, navigate deep into Control Panel/Device Manager to identify an arbitrary Virtual COM Port (e.g., `COM4`, `COM7`), and configure generic/text-only print drivers. Furthermore, Windows frequently reassigns or drops Virtual COM ports when portable thermal printers enter auto-sleep mode.

On Android, **RawBT** solved this by acting as a background bridge that connects directly to paired Bluetooth printers via MAC address and listens for local print jobs from browsers or apps. Windows currently lacks a lightweight, native equivalent with the same frictionless UX.

### 1.2 Product Vision

**WinRawBT** is a lightweight C# Windows desktop utility and background service that replicates the Android RawBT workflow. Users simply select an already-paired Bluetooth printer from a dropdown list, click **Test Connection** to verify hardware readiness, and click **Start Service** to open a local communication bridge (`localhost:40213`) that accepts raw ESC/POS commands over HTTP and WebSockets.

---

## 2. Goals & Success Metrics

| Goal | Metric / KPI | Target |
| --- | --- | --- |
| **Zero COM-Port Configuration** | Time taken by a non-technical user to configure a paired printer | < 30 seconds |
| **High Print Reliability** | Successful print rate across printer sleep/wake cycles | > 99.5% |
| **Low Print Latency** | Time from HTTP/WS request receipt to first byte transmitted over RFCOMM | < 500 ms |
| **Minimal Resource Footprint** | Idle RAM usage when running in System Tray | < 45 MB |

---

## 3. User Personas & Use Cases

* **Retail Cashier / Store Operator:** Wants to turn on the PC and Bluetooth receipt printer, verify it works with one click, and print receipts from a browser-based POS without seeing the Windows Print Dialog.
* **POS Software Developer:** Needs a standardized local endpoint (`http://localhost:40213` or `ws://localhost:40213`) on Windows so existing Android RawBT web integrations work cross-platform without code changes.
* **IT Support / Field Technician:** Needs a clear real-time diagnostic log and a one-click hardware test print to isolate whether an issue comes from the web app, the network, or the physical Bluetooth printer.

---

## 4. Functional Requirements

### 4.1 Bluetooth Device Management (FR-BT)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-BT-01** | **Paired Device Enumeration:** The application shall query the Windows Bluetooth stack via RFCOMM (`BluetoothClient.PairedDevices`) and list all currently paired Bluetooth devices showing both `DeviceName` and `BluetoothAddress` (MAC). | P0 |
| **FR-BT-02** | **Manual Refresh:** The UI shall provide a "Refresh" button to re-scan paired devices without restarting the application. | P0 |
| **FR-BT-03** | **Device Persistence:** The application shall save the last selected printer's MAC address in local configuration (`appsettings.json` or User Settings) and auto-select it on launch. | P1 |

### 4.2 Connection Testing (FR-TEST)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-TEST-01** | **One-Click Test Print:** The UI shall provide a "Test Connection" button that connects to the selected printer via SPP UUID (`00001101-0000-1000-8000-00805F9B34FB`) and sends a formatted ESC/POS byte array. | P0 |
| **FR-TEST-02** | **Diagnostic Receipt Content:** The test receipt shall print the application header, connection status (`CONNECTED`), printer MAC address, and current timestamp, followed by a paper feed/cut command (`GS V 66 0`). | P0 |
| **FR-TEST-03** | **UI State Locking:** During a connection test, the button shall be disabled to prevent duplicate concurrent socket requests, and the result (Success/Error details) shall be written to the Activity Log. | P0 |

### 4.3 Local Print Bridge Service (FR-SVC)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-SVC-01** | **Start / Stop Toggle:** The user shall be able to start and stop the background listener service via a single toggle button. | P0 |
| **FR-SVC-02** | **HTTP POST Listener:** The service shall listen on `http://localhost:40213/` (configurable port) for incoming `POST` requests containing raw binary bytes (`application/octet-stream`) or Base64-encoded ESC/POS strings (`text/plain` or `application/json`). | P0 |
| **FR-SVC-03** | **CORS Support:** The HTTP listener shall automatically respond to `OPTIONS` preflight requests and inject `Access-Control-Allow-Origin: *` headers so browser-based Web POS apps (HTTPS/HTTP) can print directly via `fetch()` or `XMLHttpRequest`. | P0 |
| **FR-SVC-04** | **WebSocket Compatibility:** The service shall accept WebSocket upgrades on `ws://localhost:40213/` to maintain drop-in compatibility with existing Android RawBT JavaScript SDKs. | P1 |
| **FR-SVC-05** | **Thread-Safe Print Queue:** All incoming print jobs shall be serialized using a concurrency lock (`SemaphoreSlim`) so simultaneous clicks in the POS app do not cause Bluetooth socket collisions or garbled receipts. | P0 |

### 4.4 Desktop UX, Logging & Lifecycle (FR-UX)

| ID | Requirement | Priority |
| --- | --- | --- |
| **FR-UX-01** | **Real-Time Activity Log:** The main window shall display a timestamped, auto-scrolling text log showing service status, incoming byte counts, and Bluetooth socket errors. | P0 |
| **FR-UX-02** | **Minimize to System Tray:** Closing or minimizing the window while the service is running shall hide the app to the Windows System Tray (`NotifyIcon`) with a context menu (*Open Dashboard*, *Test Print*, *Stop Service*, *Exit*). | P1 |
| **FR-UX-03** | **Auto-Start with Windows:** Provide a checkbox option to register the application in the Windows Startup registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) and automatically start the listener service if a default printer is saved. | P2 |

---

## 5. Non-Functional Requirements (NFR)

1. **Connect-on-Demand Socket Architecture:** Portable Bluetooth thermal printers aggressively enter sleep/standby mode, dropping idle RFCOMM sockets. The engine **must not** rely on a permanently open socket. Instead, it must open an RFCOMM stream on job arrival, flush the byte buffer, wait 250ms for hardware buffer transmission, and cleanly dispose of the socket.
2. **Security & Network Isolation:** The `HttpListener` must bind strictly to `localhost` (`127.0.0.1`) by default so external devices on the same LAN cannot send unauthorized print jobs without explicit user configuration.
3. **No Administrator Privileges Required:** Binding to `http://localhost:<port>/` in Windows does not require elevated `netsh http add urlacl` permissions, ensuring standard cashier accounts can run the software without UAC prompts.

---

## 6. Technical Architecture & API Specification

### 6.1 Data Flow Diagram

```
[Web POS / Desktop App] 
       │  (HTTP POST / WebSocket: Base64 or Raw Bytes)
       ▼
[Localhost Listener (Port 40213)] 
       │  (Queued via SemaphoreSlim)
       ▼
[Payload Decoder (Base64 -> Byte[])] 
       │  (32feet.NET BluetoothClient)
       ▼
[Windows RFCOMM / SPP Stack] ───► [Paired Bluetooth Thermal Printer]

```

### 6.2 Local Print API Contract

**Endpoint:** `POST http://localhost:40213/`

**Supported Request Formats:**

* **Option A (Base64 String - Default RawBT Web Style):**
* Header: `Content-Type: text/plain`
* Body: `G0AbYQFBTkRST0lEIFJBV0JUIEFMTCBPTg==...`


* **Option B (Raw Binary Stream):**
* Header: `Content-Type: application/octet-stream`
* Body: Raw byte buffer (`Uint8Array` / `byte[]`)



**Responses:**

* `200 OK`: `{"status": "OK", "bytesSent": 248}`
* `400 Bad Request`: `{"status": "ERROR", "message": "Empty payload or invalid Base64"}`
* `500 Internal Server Error`: `{"status": "ERROR", "message": "Bluetooth connection failed: Host is down"}`

---

## 7. User Interface (UI) Specification

```
+-------------------------------------------------------------------+
|  WinRawBT - Bluetooth ESC/POS Service                    [_][□][X]|
+-------------------------------------------------------------------+
|  1. Select Paired Bluetooth Printer:                              |
|  [ RPP02N-Thermal (00:11:22:33:44:55)          | v ]  [ Refresh ] |
|                                                                   |
|  2. Hardware & Service Controls:                                  |
|  [ Test Connection (Print Receipt) ]   [ ▶ Start Service (40213) ]|
|                                                                   |
|  [x] Run on Windows Startup            [x] Minimize to Tray       |
|  ---------------------------------------------------------------  |
|  Activity Log:                                       [Clear Log]  |
|  +-------------------------------------------------------------+  |
|  | [08:01:10] Loaded 3 paired Bluetooth devices.               |  |
|  | [08:01:15] Testing connection to 00:11:22:33:44:55...       |  |
|  | [08:01:17] Test Connection SUCCESS! Receipt printed.        |  |
|  | [08:01:22] Service started on http://localhost:40213/       |  |
|  | [08:02:05] Incoming print job (512 bytes)... Printed OK.    |  |
|  +-------------------------------------------------------------+  |
|  Status: ● Service Running (Port 40213)                           |
+-------------------------------------------------------------------+

```

---

## 8. Edge Cases & Error Handling Matrix

| Scenario | System Behavior | User Feedback |
| --- | --- | --- |
| **Printer is turned off or out of range** | `BluetoothClient.Connect()` throws `SocketException` after timeout (~5–10s). Lock is released; HTTP returns `500`. | Log displays `[ERROR] Printer unreachable or powered off.` |
| **Port 40213 is already in use** | `HttpListener.Start()` throws `HttpListenerException`. Service aborts startup cleanly. | Modal alert notifies user that another instance is running or port is occupied. |
| **User pairs a new printer while app is open** | Clicking **Refresh** re-queries `BluetoothClient.PairedDevices` without interrupting the active service. | Dropdown updates immediately with the newly paired device. |
| **Rapid consecutive print clicks from POS** | `SemaphoreSlim(1, 1)` queues incoming HTTP requests in FIFO order and processes each sequentially. | Log shows each job queued and completed in order without dropping data. |

---

## 9. Release Milestones

* **Phase 1 (MVP):** WinForms UI, Paired Bluetooth Discovery, ESC/POS Test Print, HTTP POST Listener (`localhost:40213`), Base64/Raw decoding, and Activity Log.
* **Phase 2 (Production Readiness):** WebSocket (`ws://`) listener support, System Tray (`NotifyIcon`) background persistence, local configuration saving (`appsettings.json`), and Windows Startup registry toggle.
* **Phase 3 (Advanced Features):** Built-in HTML/Image-to-ESC/POS rasterizer (for printing logos and QR codes directly from JSON/HTML payloads) and multi-printer routing by query parameter (`?printer=kitchen`).

Would you like me to generate the complete Visual Studio project structure (including the `.csproj` file and the `Form1.Designer.cs` UI layout code) so you can compile Phase 1 right away?
