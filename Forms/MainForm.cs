using System.Net;
using WinRawBT.Helpers;
using WinRawBT.Models;
using WinRawBT.Services;

namespace WinRawBT.Forms;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;

    private readonly ConfigService _configService;
    private readonly BluetoothPrinterService _bluetooth;
    private readonly PrintBridgeServer _server;

    private AppConfig _config;
    private bool _trayBalloonShown;
    private bool _exitRequested;
    private bool _suppressStartupEvent;
    private bool _suppressTrayEvent;
    private bool _busy;          // scanning or pairing in progress
    private bool _testBusy;      // test print in progress
    private string? _lastGoodMac; // last selected paired device, restored after a declined/failed pairing
    private CancellationTokenSource? _pairCts; // non-null while a pairing attempt is running
    private readonly System.Windows.Forms.Timer _progressTimer = new() { Interval = 1000 };
    private DateTime _progressStart;
    private Action<int>? _progressTick;

    public MainForm()
    {
        InitializeComponent();

        // Single icon copy: the exe's own ApplicationIcon (Assets\app.ico) feeds window, taskbar and tray.
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* keep default icon */ }

        _configService = new ConfigService();
        _config = _configService.Load();
        _bluetooth = new BluetoothPrinterService(AppendLog);
        _server = new PrintBridgeServer(_bluetooth, AppendLog);
        _progressTimer.Tick += ProgressTimer_Tick;
    }

    // ---------------------------------------------------------------------
    // TASK-12: Thread-safe UI activity logger
    // ---------------------------------------------------------------------
    public void AppendLog(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (txtLog.InvokeRequired)
        {
            try { txtLog.BeginInvoke(new Action<string>(AppendLog), message); }
            catch (InvalidOperationException) { /* handle destroyed during shutdown */ }
            return;
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        txtLog.AppendText(line + Environment.NewLine);

        // Cap history at 500 lines to prevent memory bloat.
        if (txtLog.Lines.Length > MaxLogLines)
        {
            string[] lines = txtLog.Lines;
            string[] trimmed = new string[MaxLogLines];
            Array.Copy(lines, lines.Length - MaxLogLines, trimmed, 0, MaxLogLines);
            txtLog.Lines = trimmed;
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }
    }

    // ---------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------
    private void lblCredit_Click(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Dzul27/WinRawBT")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendLog($"[WARN] Could not open the project page: {ex.Message}");
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        notifyIcon.Icon = Icon ?? System.Drawing.SystemIcons.Application;
        lblCredit.Text = $"WinRawBT v{typeof(MainForm).Assembly.GetName().Version?.ToString(3)} \u00B7 by Dzul27";

        _suppressTrayEvent = true;
        chkMinimizeToTray.Checked = _config.MinimizeToTray;
        _suppressTrayEvent = false;

        _suppressStartupEvent = true;
        chkRunOnStartup.Checked = StartupRegistryService.IsEnabled() || _config.RunOnStartup;
        _suppressStartupEvent = false;

        numPort.Value = Math.Min(Math.Max(_config.Port, (int)numPort.Minimum), (int)numPort.Maximum);

        cmbPaperWidth.SelectedIndex = _config.PaperWidthMm == 80 ? 1 : 0;
        btnToggleService.Text = $"\u25B6 Start Service ({_config.Port})";
        ApplyOriginSettings();

        LoadPrinters(selectSavedMac: true);

        // TASK-15 / FR-UX-03: auto-start service if configured and a printer is saved.
        if (_config.AutoStartService && !string.IsNullOrWhiteSpace(_config.LastPrinterMac))
        {
            StartService();
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // TASK-14: minimize to tray instead of closing.
        if (!_exitRequested
            && e.CloseReason == CloseReason.UserClosing
            && chkMinimizeToTray.Checked)
        {
            e.Cancel = true;
            Hide();
            notifyIcon.Visible = true;

            if (!_trayBalloonShown)
            {
                notifyIcon.BalloonTipTitle = "WinRawBT";
                notifyIcon.BalloonTipText = _server.IsRunning
                    ? $"Still running on port {_server.Port}. Right-click the tray icon for options."
                    : "Running in the system tray. Right-click the tray icon for options.";
                notifyIcon.ShowBalloonTip(3000);
                _trayBalloonShown = true;
            }

            return;
        }

        // Real exit: persist config and tear down.
        PersistConfig();
        try { _server.Dispose(); } catch { /* ignore */ }
        try { _bluetooth.Dispose(); } catch { /* ignore */ }
        _progressTimer.Dispose();
        notifyIcon.Visible = false;
    }

    // ---------------------------------------------------------------------
    // TASK-13: Wire controls to services
    // ---------------------------------------------------------------------
    private void btnRefresh_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        LoadPrinters(selectSavedMac: false);
    }

    private void LoadPrinters(bool selectSavedMac)
    {
        List<BluetoothDeviceItem> devices = _bluetooth.GetPairedDevices();
        PopulateDropdown(devices, selectSavedMac ? _config.LastPrinterMac : GetSelectedMac());
        AppendLog($"Loaded {devices.Count} paired Bluetooth device(s).");
    }

    /// <summary>Fill the dropdown and select <paramref name="preferredMac"/> (or the first paired device when none is preferred).</summary>
    private void PopulateDropdown(List<BluetoothDeviceItem> devices, string? preferredMac)
    {
        cmbPrinters.BeginUpdate();
        cmbPrinters.Items.Clear();
        foreach (BluetoothDeviceItem item in devices)
        {
            cmbPrinters.Items.Add(item);
        }
        cmbPrinters.EndUpdate();

        if (!string.IsNullOrWhiteSpace(preferredMac))
        {
            SelectPrinterByMac(preferredMac!);
        }
        else
        {
            int firstPaired = devices.FindIndex(d => d.IsPaired);
            if (firstPaired >= 0)
            {
                cmbPrinters.SelectedIndex = firstPaired;
            }
        }

        _lastGoodMac = GetSelectedMac();
    }

    private void SelectPrinterByMac(string mac)
    {
        for (int i = 0; i < cmbPrinters.Items.Count; i++)
        {
            if (cmbPrinters.Items[i] is BluetoothDeviceItem p && p.IsPaired &&
                string.Equals(p.BluetoothAddress, mac, StringComparison.OrdinalIgnoreCase))
            {
                cmbPrinters.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>MAC of the selected device, only when it is paired (unpaired devices are never persisted or used).</summary>
    private string? GetSelectedMac()
    {
        BluetoothDeviceItem? item = cmbPrinters.SelectedItem as BluetoothDeviceItem;
        return item != null && item.IsPaired ? item.BluetoothAddress : null;
    }

    private BluetoothDeviceItem? GetSelectedPrinter()
    {
        return cmbPrinters.SelectedItem as BluetoothDeviceItem;
    }

    // ---------------------------------------------------------------------
    // TASK-18: Scan for nearby devices and pair them
    // ---------------------------------------------------------------------
    private async void btnScan_Click(object? sender, EventArgs e)
    {
        if (_busy || _testBusy || _server.IsRunning)
        {
            return;
        }

        SetBusy(true);
        StartProgress(s => btnScan.Text = $"Scanning... {s} s");
        AppendLog("Scanning for Bluetooth devices (~10s)...");
        try
        {
            List<BluetoothDeviceItem> devices = await _bluetooth.DiscoverDevicesAsync();
            if (IsDisposed)
            {
                return;
            }

            PopulateDropdown(devices, GetSelectedMac() ?? _config.LastPrinterMac);

            int paired = devices.Count(d => d.IsPaired);
            AppendLog($"Scan complete: {devices.Count} device(s) found ({paired} paired, {devices.Count - paired} not paired).");
            foreach (BluetoothDeviceItem d in devices)
            {
                AppendLog($"  - {d}");
            }

            if (devices.Count - paired > 0)
            {
                AppendLog("Select a [not paired] device in the dropdown to pair with it.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Scan failed ({ex.GetType().Name}): {ex.Message}");
        }
        finally
        {
            StopProgress();
            if (!IsDisposed)
            {
                btnScan.Text = "Scan for Printers";
                SetBusy(false);
            }
        }
    }

    private async void cmbPrinters_SelectionChangeCommitted(object? sender, EventArgs e)
    {
        if (_busy || cmbPrinters.SelectedItem is not BluetoothDeviceItem item)
        {
            return;
        }

        if (item.IsPaired)
        {
            _lastGoodMac = item.BluetoothAddress;
            return;
        }

        await OfferPairingAsync(item);
    }

    private async Task OfferPairingAsync(BluetoothDeviceItem item, string pin = "")
    {
        if (pin.Length == 0)
        {
            DialogResult answer = MessageBox.Show(this,
                $"Pair with {item.Name} now?\n\n" +
                "The app makes one pairing request without a PIN (Secure Simple Pairing). " +
                "Make sure the printer is on and in pairing mode. If it needs a PIN, the app will ask for it " +
                "if the first attempt fails.",
                "WinRawBT - Pair Device", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                AppendLog($"Pairing with {item.Name} ({item.BluetoothAddress}) skipped. It stays [not paired].");
                RestoreLastGoodSelection();
                return;
            }
        }

        bool offerSettings = false;
        _pairCts = new CancellationTokenSource();
        SetBusy(true);
        StartProgress(s =>
        {
            btnCancelPair.Text = $"Cancel ({s} s)";
            lblStatus.Text = $"\u25CF Pairing... {s} s";
        });
        AppendLog($"Pairing with {item.Name} ({item.BluetoothAddress}) - one request, {(pin.Length == 0 ? "no PIN" : "with the PIN you entered")}. " +
                  "Accept any Windows pairing prompt; click Cancel to stop waiting.");
        try
        {
            PairResult result = await _bluetooth.PairAsync(item.BluetoothAddress, pin, _pairCts.Token);
            if (IsDisposed)
            {
                return;
            }

            if (result.Success)
            {
                BluetoothDeviceItem paired = item.AsPaired();
                int index = IndexOfMac(item.BluetoothAddress);
                if (index >= 0)
                {
                    cmbPrinters.Items[index] = paired;
                    cmbPrinters.SelectedIndex = index;
                }

                _lastGoodMac = paired.BluetoothAddress;
                _config.LastPrinterMac = paired.BluetoothAddress;
                PersistConfig();
                AppendLog($"Paired with {paired.Name} ({paired.BluetoothAddress}) and selected it.");
            }
            else if (result.Canceled)
            {
                AppendLog($"Pairing with {item.Name} ({item.BluetoothAddress}) canceled. It stays [not paired].");
                RestoreLastGoodSelection();
            }
            else
            {
                AppendLog($"[ERROR] Pairing with {item.Name} ({item.BluetoothAddress}) failed: {result.Detail}");
                AppendLog("The device remains [not paired] and cannot be used until it is paired.");
                RestoreLastGoodSelection();
                offerSettings = true;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Pairing failed ({ex.GetType().Name}): {ex.Message}");
            RestoreLastGoodSelection();
            offerSettings = true;
        }
        finally
        {
            StopProgress();
            _pairCts?.Dispose();
            _pairCts = null;
            if (!IsDisposed)
            {
                btnCancelPair.Text = "Cancel";
                btnCancelPair.Enabled = true;
                lblStatus.Text = _server.IsRunning ? $"\u25CF Service Running ({_server.Port})" : "\u25CF Service Stopped";
                SetBusy(false);
            }
        }

        if (offerSettings && !IsDisposed)
        {
            string? enteredPin = PromptForPin(item.Name);
            if (enteredPin != null)
            {
                await OfferPairingAsync(item, enteredPin);
                return;
            }

            DialogResult open = MessageBox.Show(this,
                "Pairing was not completed. You can also pair it in Windows Bluetooth settings.\n\n" +
                "Open Windows Bluetooth settings now?",
                "WinRawBT - Pairing failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (open == DialogResult.Yes)
            {
                OpenBluetoothSettings();
            }
        }
    }

    /// <summary>Ask for the printer's PIN (typically 0000 or 1234). Returns null when canceled or empty.</summary>
    private string? PromptForPin(string deviceName)
    {
        using var dialog = new Form
        {
            Text = "WinRawBT - Enter PIN",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 130),
        };
        var label = new Label
        {
            Left = 12, Top = 12, Width = 356, Height = 44,
            Text = $"Pairing with {deviceName} failed without a PIN.\nEnter its PIN (printers often use 0000 or 1234) and retry:",
        };
        var box = new TextBox { Left = 12, Top = 62, Width = 356, MaxLength = 16 };
        var ok = new Button { Text = "Pair", Left = 212, Top = 92, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 293, Top = 92, Width = 75, DialogResult = DialogResult.Cancel };
        dialog.Controls.AddRange(new Control[] { label, box, ok, cancel });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        string pin = box.Text.Trim();
        return pin.Length == 0 ? null : pin;
    }

    private int IndexOfMac(string mac)
    {
        for (int i = 0; i < cmbPrinters.Items.Count; i++)
        {
            if (cmbPrinters.Items[i] is BluetoothDeviceItem p &&
                string.Equals(p.BluetoothAddress, mac, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Re-select the last paired printer (or clear the selection) after an unpaired entry was declined or failed.</summary>
    private void RestoreLastGoodSelection()
    {
        int index = string.IsNullOrWhiteSpace(_lastGoodMac) ? -1 : IndexOfMac(_lastGoodMac!);
        cmbPrinters.SelectedIndex = index;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ApplyUiState();
    }

    private async void btnTestPrint_Click(object? sender, EventArgs e)
    {
        if (_busy || _testBusy)
        {
            return;
        }

        BluetoothDeviceItem? printer = GetSelectedPrinter();
        if (printer == null)
        {
            AppendLog("[WARN] No printer selected.");
            MessageBox.Show(this, "Please select a paired Bluetooth printer first.",
                "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!printer.IsPaired)
        {
            AppendLog($"[WARN] {printer.Name} ({printer.BluetoothAddress}) is not paired; cannot run Test Connection.");
            MessageBox.Show(this,
                $"{printer.Name} is not paired yet. Select it in the list and choose Yes to pair it first.",
                "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _testBusy = true;
        ApplyUiState();
        AppendLog($"Testing connection to {printer.BluetoothAddress}...");

        try
        {
            byte[] receipt = EscPosBuilder.BuildTestReceipt(printer.Name, printer.BluetoothAddress, _config.PaperWidthMm);
            bool ok = await _bluetooth.SendRawBytesAsync(printer.BluetoothAddress, receipt);
            AppendLog(ok
                ? "Test Connection SUCCESS! Receipt printed."
                : "[ERROR] Test Connection FAILED. See messages above.");
        }
        finally
        {
            _testBusy = false;
            if (!IsDisposed)
            {
                ApplyUiState();
            }
        }
    }

    private void btnToggleService_Click(object? sender, EventArgs e)
    {
        if (_server.IsRunning)
        {
            StopService();
        }
        else
        {
            StartService();
        }
    }

    private void StartService()
    {
        BluetoothDeviceItem? printer = GetSelectedPrinter();
        if (printer == null)
        {
            AppendLog("[WARN] Cannot start service without a selected printer.");
            MessageBox.Show(this, "Please select a paired Bluetooth printer before starting the service.",
                "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!printer.IsPaired)
        {
            AppendLog($"[WARN] {printer.Name} ({printer.BluetoothAddress}) is not paired; cannot start the service.");
            MessageBox.Show(this,
                $"{printer.Name} is not paired yet. Select it in the list and choose Yes to pair it first.",
                "WinRawBT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Port = (int)numPort.Value;
        _server.TargetMac = printer.BluetoothAddress;
        _server.PrependInit = _config.PrependInit;
        ApplyOriginSettings();
        if (_config.AllowAnyOrigin)
        {
            AppendLog("[INFO] Any website may print (Allowed websites: All).");
        }
        else
        {
            AppendLog($"[INFO] Allowed websites: {(_config.AllowedOrigins is { Count: > 0 } ? string.Join(", ", _config.AllowedOrigins) : "none (requests without an Origin header only)")}.");
        }
        if (_config.PrependInit)
        {
            AppendLog("[INFO] PrependInit is ON (config.json): ESC @ is added to jobs that do not start with it.");
        }

        try
        {
            _server.Start(_config.Port);
        }
        catch (HttpListenerException ex)
        {
            AppendLog($"[ERROR] Could not start listener on port {_config.Port}: {ex.Message}");
            MessageBox.Show(this,
                $"Port {_config.Port} is already in use or blocked.\n\n" +
                "Another instance of WinRawBT may be running.\n\n" +
                $"Details: {ex.Message}",
                "WinRawBT - Port In Use", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Failed to start service: {ex.Message}");
            return;
        }

        AppendLog($"[INFO] Listening on http://localhost:{_config.Port}/ and ws://localhost:{_config.Port}/");
        UpdateServiceUi(running: true);
        PersistConfig();
    }

    private void StopService()
    {
        _server.Stop();
        UpdateServiceUi(running: false);
    }

    private void UpdateServiceUi(bool running)
    {
        if (running)
        {
            btnToggleService.Text = $"\u25A0 Stop Service ({_config.Port})";
            trayToggleService.Text = "Stop Service";
            lblStatus.Text = $"\u25CF Service Running (Port {_config.Port})";
        }
        else
        {
            btnToggleService.Text = $"\u25B6 Start Service ({_config.Port})";
            trayToggleService.Text = "Start Service";
            lblStatus.Text = "\u25CF Service Stopped";
        }

        ApplyUiState();
    }

    /// <summary>
    /// Single place that derives control enablement from the running/scanning/pairing/testing state:
    /// the printer list, Refresh and Scan are locked while the service runs or an operation is in flight.
    /// </summary>
    private void ApplyUiState()
    {
        bool selectable = !_server.IsRunning && !_busy && !_testBusy;
        cmbPrinters.Enabled = selectable;
        btnRefresh.Enabled = selectable;
        btnScan.Enabled = selectable;
        btnBtSettings.Enabled = !_busy;
        btnCancelPair.Visible = _pairCts != null;

        bool canTest = !_busy && !_testBusy;
        btnTestPrint.Enabled = canTest;
        trayTestPrint.Enabled = canTest;
        numPort.Enabled = !_server.IsRunning;
        btnToggleService.Enabled = !_busy;
        trayToggleService.Enabled = !_busy;
    }

    // ---------------------------------------------------------------------
    // Elapsed-seconds progress shown on the busy button (scan: Scan button, pairing: Cancel button)
    // ---------------------------------------------------------------------
    private void StartProgress(Action<int> onTick)
    {
        _progressStart = DateTime.UtcNow;
        _progressTick = onTick;
        onTick(0);
        _progressTimer.Start();
    }

    private void StopProgress()
    {
        _progressTimer.Stop();
        _progressTick = null;
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        _progressTick?.Invoke((int)(DateTime.UtcNow - _progressStart).TotalSeconds);
    }

    private void btnBtSettings_Click(object? sender, EventArgs e)
    {
        OpenBluetoothSettings();
    }

    private void OpenBluetoothSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth")
            {
                UseShellExecute = true
            });
            AppendLog("Opened Windows Bluetooth settings. Pair the printer there (enter its PIN if asked), then click Refresh here.");
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Could not open Windows Bluetooth settings ({ex.GetType().Name}): {ex.Message}. " +
                      "Open Settings > Bluetooth & devices manually.");
        }
    }

    private void btnCancelPair_Click(object? sender, EventArgs e)
    {
        btnCancelPair.Enabled = false;
        _pairCts?.Cancel();
    }

    private void btnClearLog_Click(object? sender, EventArgs e)
    {
        txtLog.Clear();
    }

    // ---------------------------------------------------------------------
    // TASK-15: startup registry toggle
    // ---------------------------------------------------------------------
    private void chkRunOnStartup_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressStartupEvent)
        {
            return;
        }

        bool enable = chkRunOnStartup.Checked;
        bool ok = StartupRegistryService.SetStartup(enable);
        if (!ok)
        {
            AppendLog("[WARN] Could not modify the Windows startup registry entry.");
        }
        else
        {
            AppendLog(enable
                ? "Registered WinRawBT to run on Windows startup."
                : "Removed WinRawBT from Windows startup.");
        }

        _config.RunOnStartup = enable;
        // When enabling startup, also auto-start the service next launch.
        _config.AutoStartService = enable;
        PersistConfig();
    }

    private void chkMinimizeToTray_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressTrayEvent)
        {
            return;
        }

        _config.MinimizeToTray = chkMinimizeToTray.Checked;
        PersistConfig();
    }

    private void cmbPaperWidth_SelectionChangeCommitted(object? sender, EventArgs e)
    {
        _config.PaperWidthMm = cmbPaperWidth.SelectedIndex == 1 ? 80 : 58;
        PersistConfig();
    }

    // ---------------------------------------------------------------------
    // Allowed websites (origin control); applied live, no service restart needed
    // ---------------------------------------------------------------------
    private void btnOrigins_Click(object? sender, EventArgs e)
    {
        using var dialog = new OriginsForm(_config.AllowAnyOrigin, _config.AllowedOrigins ?? new List<string>());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.AllowAnyOrigin = dialog.AllowAny;
        _config.AllowedOrigins = dialog.Origins;
        PersistConfig();
        ApplyOriginSettings();
        AppendLog(_config.AllowAnyOrigin
            ? "[INFO] Allowed websites: All. Any website may print."
            : $"[INFO] Allowed websites: {string.Join(", ", _config.AllowedOrigins)}. Other websites get HTTP 403.");
    }

    /// <summary>Pushes the origin settings to the bridge (works while it runs) and refreshes the button text.</summary>
    private void ApplyOriginSettings()
    {
        var origins = new List<string>(_config.AllowedOrigins ?? new List<string>());
        _server.AllowedOrigins = origins;
        _server.AllowAnyOrigin = _config.AllowAnyOrigin;
        btnOrigins.Text = _config.AllowAnyOrigin
            ? "Allowed websites: All (click to change)"
            : $"Allowed websites: only {origins.Count} listed (click to change)";
    }

    // ---------------------------------------------------------------------
    // TASK-14: tray menu handlers
    // ---------------------------------------------------------------------
    private void trayOpen_Click(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        notifyIcon.Visible = false;
    }

    private void trayExit_Click(object? sender, EventArgs e)
    {
        _exitRequested = true;
        Close();
    }

    // ---------------------------------------------------------------------
    private void PersistConfig()
    {
        string? mac = GetSelectedMac();
        if (!string.IsNullOrWhiteSpace(mac))
        {
            _config.LastPrinterMac = mac;
        }

        _config.MinimizeToTray = chkMinimizeToTray.Checked;
        _config.RunOnStartup = chkRunOnStartup.Checked;
        if (!_server.IsRunning)
        {
            _config.Port = (int)numPort.Value;
        }
        _configService.Save(_config);
    }
}
