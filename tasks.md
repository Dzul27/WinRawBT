Each atomic task below is scoped to a single responsibility, can be implemented and verified independently, and includes explicit technical deliverables and a Definition of Done (DoD).

---

## Epic 1: Project Scaffolding & Configuration Persistence

| Task ID | Task Name | Target File(s) | Dependencies |
| --- | --- | --- | --- |
| **TASK-01** | Initialize .NET 8 WinForms Project & NuGet Packages | `WinRawBT.csproj` | None |
| **TASK-02** | Implement Single-Instance Application Mutex | `Program.cs` | TASK-01 |
| **TASK-03** | Build Local Configuration Manager | `Services/ConfigService.cs` | TASK-01 |

* **TASK-01: Initialize .NET 8 WinForms Project & NuGet Packages**
* **Action:** Create a `.NET 8.0-windows` WinForms project. Install `InTheHand.Net.Bluetooth` (v4.1+) and `System.Text.Json`.
* **DoD:** Project compiles cleanly with `dotnet build` with zero warnings.


* **TASK-02: Implement Single-Instance Application Mutex**
* **Action:** Wrap `Application.Run()` in `Program.cs` with a named `System.Threading.Mutex` (`Global\WinRawBT_SingleInstance_Mutex`).
* **DoD:** Launching a second `.exe` instance shows a warning dialog and exits immediately, preventing port `40213` binding conflicts.


* **TASK-03: Build Local Configuration Manager**
* **Action:** Create `AppConfig` model (`LastPrinterMac`, `Port = 40213`, `AutoStartService`, `MinimizeToTray`, `RunOnStartup`) and save/load methods targeting `%AppData%\WinRawBT\config.json`.
* **DoD:** Settings persist across application restarts and default gracefully if `config.json` does not exist.



---

## Epic 2: Bluetooth RFCOMM Core & ESC/POS Engine

| Task ID | Task Name | Target File(s) | Dependencies |
| --- | --- | --- | --- |
| **TASK-04** | Implement Paired Bluetooth Device Discovery | `Models/PairedPrinterItem.cs`, `Services/BluetoothPrinterService.cs` | TASK-01 |
| **TASK-05** | Implement Thread-Safe RFCOMM Raw Byte Sender | `Services/BluetoothPrinterService.cs` | TASK-04 |
| **TASK-06** | Build ESC/POS Test Receipt Generator | `Helpers/EscPosBuilder.cs` | TASK-05 |

* **TASK-04: Implement Paired Bluetooth Device Discovery**
* **Action:** Create `PairedPrinterItem` (`Name`, `BluetoothAddress`) and query `new BluetoothClient().PairedDevices`. Format MAC addresses as `XX:XX:XX:XX:XX:XX`.
* **DoD:** Calling `GetPairedDevices()` returns a list of paired Windows Bluetooth devices without throwing exceptions when Bluetooth is turned off (returns empty list + logs warning).


* **TASK-05: Implement Thread-Safe RFCOMM Raw Byte Sender**
* **Action:** Implement `SendRawBytesAsync(BluetoothAddress address, byte[] payload)` guarded by `SemaphoreSlim(1, 1)`. Connect on-demand via `BluetoothService.SerialPort` (SPP UUID `00001101-0000-1000-8000-00805F9B34FB`), write bytes, flush stream, wait `250ms`, and dispose socket.
* **DoD:** Concurrent calls queue sequentially in FIFO order without socket collision; unreachable printers timeout cleanly and return `false`.


* **TASK-06: Build ESC/POS Test Receipt Generator**
* **Action:** Create static method `BuildTestReceipt(string printerName, string macAddress)` returning a `byte[]` with `ESC @` (Init), `ESC a 1` (Center), bold header, MAC/Timestamp details, 3 line feeds, and `GS V 66 0` (Cut).
* **DoD:** Sending output via `SendRawBytesAsync` prints a formatted diagnostic receipt on a 58mm/80mm thermal printer.



---

## Epic 3: Local Print Bridge Server (HTTP & WebSocket)

| Task ID | Task Name | Target File(s) | Dependencies |
| --- | --- | --- | --- |
| **TASK-07** | Implement Payload Auto-Decoder (Raw vs. Base64) | `Helpers/PayloadDecoder.cs` | TASK-01 |
| **TASK-08** | Implement `HttpListener` Lifecycle & CORS Handler | `Services/PrintBridgeServer.cs` | TASK-03 |
| **TASK-09** | Implement `POST /` Print Endpoint | `Services/PrintBridgeServer.cs` | TASK-05, TASK-07, TASK-08 |
| **TASK-10** | Implement WebSocket (`ws://`) Upgrade Handler | `Services/PrintBridgeServer.cs` | TASK-05, TASK-07, TASK-08 |

* **TASK-07: Implement Payload Auto-Decoder (Raw vs. Base64)**
* **Action:** Write `DecodePayload(byte[] rawBody, string? contentType)` that strips optional data-URI prefixes, checks `Convert.TryFromBase64String` when content is text/JSON, or passes raw binary (`application/octet-stream`) untouched.
* **DoD:** Both raw `byte[]` buffers and Base64 ESC/POS strings decode into identical ESC/POS byte arrays.


* **TASK-08: Implement `HttpListener` Lifecycle & CORS Handler**
* **Action:** Create `Start(int port)` and `Stop()` using `HttpListener` bound to `http://localhost:{port}/`. Add CORS headers (`Access-Control-Allow-Origin: *`, `Access-Control-Allow-Methods: POST, GET, OPTIONS`, `Access-Control-Allow-Headers: *`) and respond `200 OK` to `OPTIONS` preflight requests.
* **DoD:** Browser `fetch()` calls from `https://` or `http://` origins pass CORS preflight checks without errors.


* **TASK-09: Implement `POST /` Print Endpoint**
* **Action:** Read `Request.InputStream`, decode via `PayloadDecoder`, forward to `SendRawBytesAsync`, and return JSON `{"status":"OK","bytesSent":N}` (`200`) or `{"status":"ERROR","message":"..."}` (`400`/`500`).
* **DoD:** `curl -X POST http://localhost:40213/ -d "<base64>"` triggers a physical print and returns HTTP 200.


* **TASK-10: Implement WebSocket (`ws://`) Upgrade Handler**
* **Action:** Detect `context.Request.IsWebSocketRequest`, accept via `AcceptWebSocketAsync(null)`, read incoming `ArraySegment<byte>` frames (Text Base64 or Binary), and forward to `SendRawBytesAsync`.
* **DoD:** Standard Android RawBT JavaScript WebSocket client (`new WebSocket("ws://localhost:40213/")`) connects and prints without modifying client-side JS code.



---

## Epic 4: Desktop UI, System Tray & Windows Lifecycle

| Task ID | Task Name | Target File(s) | Dependencies |
| --- | --- | --- | --- |
| **TASK-11** | Build Main Form UI Layout | `Forms/MainForm.Designer.cs` | TASK-01 |
| **TASK-12** | Implement Thread-Safe UI Activity Logger | `Forms/MainForm.cs` | TASK-11 |
| **TASK-13** | Wire UI Controls to Bluetooth & Server Services | `Forms/MainForm.cs` | TASK-06, TASK-09, TASK-12 |
| **TASK-14** | Implement System Tray (`NotifyIcon`) & Background Mode | `Forms/MainForm.cs` | TASK-11, TASK-13 |
| **TASK-15** | Implement Windows Startup Registry Toggle | `Services/StartupRegistryService.cs` | TASK-03, TASK-11 |

* **TASK-11: Build Main Form UI Layout**
* **Action:** Construct `MainForm` (`FixedSingle`, `520x420`) containing: `cmbPrinters` (DropDownList), `btnRefresh`, `btnTestPrint`, `btnToggleService`, `chkRunOnStartup`, `chkMinimizeToTray`, `txtLog` (ReadOnly, Multiline, Dark Console theme), and `lblStatus` (StatusStrip).
* **DoD:** Form renders cleanly at 100% and 125% Windows DPI scaling.


* **TASK-12: Implement Thread-Safe UI Activity Logger**
* **Action:** Create `AppendLog(string message)` using `BeginInvoke` to append `[HH:mm:ss] message` to `txtLog` and cap log history at 500 lines to prevent memory bloat.
* **DoD:** Background HTTP/Bluetooth threads log messages in real time without cross-thread `InvalidOperationException`.


* **TASK-13: Wire UI Controls to Bluetooth & Server Services**
* **Action:** Connect `btnRefresh`, `btnTestPrint`, and `btnToggleService` to their respective services. Disable `cmbPrinters` while service is running. Auto-select saved printer MAC on startup.
* **DoD:** User can select a printer, run a test print, and start/stop the listener service entirely from the UI.


* **TASK-14: Implement System Tray (`NotifyIcon`) & Background Mode**
* **Action:** Add `NotifyIcon` with `ContextMenuStrip` (*Open WinRawBT*, *Test Print*, *Start/Stop Service*, *Exit*). Intercept `FormClosing` when `CloseReason.UserClosing` and `chkMinimizeToTray.Checked` is true: cancel close, set `Hide()`, and show tray balloon tip.
* **DoD:** Clicking `[X]` hides the app to the System Tray while keeping port `40213` active.


* **TASK-15: Implement Windows Startup Registry Toggle**
* **Action:** Write `SetStartup(bool enable)` modifying `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with key `"WinRawBT"` pointing to `Application.ExecutablePath`.
* **DoD:** Checking "Run on Windows Startup" adds the registry value; unchecking removes it without requiring Admin elevation.



---

## Epic 5: Testing Harness & Single-File Packaging

| Task ID | Task Name | Target File(s) | Dependencies |
| --- | --- | --- | --- |
| **TASK-16** | Create Browser POS Test Harness | `TestClient/test-pos.html` | TASK-09, TASK-10 |
| **TASK-17** | Configure Single-File Self-Contained `.exe` Build | `WinRawBT.csproj` | All Tasks |

* **TASK-16: Create Browser POS Test Harness**
* **Action:** Build a standalone `test-pos.html` file with two buttons: "Print via HTTP POST (`fetch`)" and "Print via WebSocket (`ws://`)", sending sample Base64 ESC/POS receipts to `localhost:40213`.
* **DoD:** Opening `test-pos.html` in Chrome/Edge successfully prints receipts through both HTTP and WebSocket channels.


* **TASK-17: Configure Single-File Self-Contained `.exe` Build**
* **Action:** Configure `PublishSingleFile=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`, and `IncludeNativeLibrariesForSelfExtract=true` in `WinRawBT.csproj`.
* **DoD:** Running `dotnet publish -c Release` produces a single portable `WinRawBT.exe` that runs on a clean Windows 10/11 PC without installing the .NET runtime.
