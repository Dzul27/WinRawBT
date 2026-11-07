using System;
using System.Drawing;
using System.Windows.Forms;
using WinRawBT.Helpers;
using WinRawBT.Models;
using WinRawBT.Services;

namespace WinRawBT.Forms;

/// <summary>
/// Main application form (TASK-11, TASK-12, TASK-13, TASK-14)
/// </summary>
public partial class MainForm : Form
{
    private readonly ConfigService _configService;
    private readonly BluetoothPrinterService _bluetoothService;
    private readonly PrintBridgeServer _printServer;
    
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _trayMenu;
    private bool _isClosing = false;

    public MainForm(ConfigService configService)
    {
        InitializeComponent();
        
        _configService = configService;
        _bluetoothService = new BluetoothPrinterService();
        _printServer = new PrintBridgeServer(_bluetoothService);

        // Wire up events
        _printServer.LogMessage += PrintServer_LogMessage;
        _printServer.PrintJobCompleted += PrintServer_PrintJobCompleted;

        // Load initial data
        LoadSettings();
        RefreshPrinters();
    }

    /// <summary>
    /// Thread-safe UI activity logger (TASK-12)
    /// </summary>
    private void AppendLog(string message)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.BeginInvoke(new Action(() => AppendLog(message)));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{timestamp}] {message}\r\n");
        
        // Cap log history at 500 lines (TASK-12)
        if (txtLog.Lines.Length > 500)
        {
            var lines = txtLog.Lines;
            var newLines = new string[500];
            Array.Copy(lines, lines.Length - 500, newLines, 0, 500);
            txtLog.Lines = newLines;
        }

        // Auto-scroll to bottom
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    /// <summary>
    /// Clear log
    /// </summary>
    private void btnClearLog_Click(object? sender, EventArgs e)
    {
        txtLog.Clear();
        AppendLog("Log cleared");
    }

    /// <summary>
    /// Refresh paired Bluetooth printers (TASK-13)
    /// </summary>
    private void btnRefresh_Click(object? sender, EventArgs e)
    {
        RefreshPrinters();
    }

    private void RefreshPrinters()
    {
        try
        {
            cmbPrinters.Items.Clear();
            
            var devices = _bluetoothService.GetPairedDevices();
            
            foreach (var device in devices)
            {
                cmbPrinters.Items.Add(device);
            }

            AppendLog($"Loaded {devices.Count} paired Bluetooth devices");

            // Auto-select saved printer if available
            if (!string.IsNullOrEmpty(_configService.Config.LastPrinterMac))
            {
                foreach (PairedPrinterItem item in cmbPrinters.Items)
                {
                    if (item.MacAddress.Replace(":", "").Equals(
                        _configService.Config.LastPrinterMac.Replace(":", ""), 
                        StringComparison.OrdinalIgnoreCase))
                    {
                        cmbPrinters.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error refreshing printers: {ex.Message}");
        }
    }

    /// <summary>
    /// Test print button (TASK-13)
    /// </summary>
    private async void btnTestPrint_Click(object? sender, EventArgs e)
    {
        if (cmbPrinters.SelectedItem == null)
        {
            MessageBox.Show("Please select a printer first", "No Printer Selected", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedPrinter = (PairedPrinterItem)cmbPrinters.SelectedItem;
        
        try
        {
            btnTestPrint.Enabled = false;
            AppendLog($"Testing connection to {selectedPrinter.Name}...");

            var testReceipt = EscPosBuilder.BuildTestReceipt(
                selectedPrinter.Name, 
                PairedPrinterItem.FormatMacAddress(selectedPrinter.MacAddress)
            );

            var success = await _bluetoothService.SendRawBytesAsync(
                selectedPrinter.BluetoothAddress, 
                testReceipt
            );

            AppendLog($"Test Connection SUCCESS! Receipt printed.");
        }
        catch (Exception ex)
        {
            AppendLog($"Test Connection FAILED: {ex.Message}");
            MessageBox.Show($"Test print failed:\n{ex.Message}", "Print Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnTestPrint.Enabled = true;
        }
    }

    /// <summary>
    /// Toggle service button (TASK-13)
    /// </summary>
    private void btnToggleService_Click(object? sender, EventArgs e)
    {
        if (_printServer.IsRunning)
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
        if (cmbPrinters.SelectedItem == null)
        {
            MessageBox.Show("Please select a printer first", "No Printer Selected", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedPrinter = (PairedPrinterItem)cmbPrinters.SelectedItem;
        _printServer.SetSelectedPrinter(selectedPrinter.BluetoothAddress);

        var port = _configService.Config.Port;
        var success = _printServer.Start(port);

        if (success)
        {
            btnToggleService.Text = "Stop Service";
            lblStatus.Text = $"● Service Running (Port {port})";
            lblStatus.ForeColor = Color.Green;
            cmbPrinters.Enabled = false;
            btnRefresh.Enabled = false;
        }
        else
        {
            MessageBox.Show($"Failed to start service on port {port}.\nThe port may already be in use.", 
                "Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopService()
    {
        _printServer.Stop();
        btnToggleService.Text = $"▶ Start Service ({_configService.Config.Port})";
        lblStatus.Text = "● Service Stopped";
        lblStatus.ForeColor = Color.Gray;
        cmbPrinters.Enabled = true;
        btnRefresh.Enabled = true;
    }

    /// <summary>
    /// Printer selection changed
    /// </summary>
    private void cmbPrinters_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbPrinters.SelectedItem is PairedPrinterItem printer)
        {
            _configService.UpdateLastPrinter(printer.Name, printer.MacAddress);
        }
    }

    /// <summary>
    /// Minimize to tray checkbox
    /// </summary>
    private void chkMinimizeToTray_CheckedChanged(object? sender, EventArgs e)
    {
        _configService.Config.MinimizeToTray = chkMinimizeToTray.Checked;
        _configService.Save();
    }

    /// <summary>
    /// Run on startup checkbox (TASK-15)
    /// </summary>
    private void chkRunOnStartup_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            StartupRegistryService.SetStartup(chkRunOnStartup.Checked);
            _configService.Config.RunOnStartup = chkRunOnStartup.Checked;
            _configService.Save();
            AppendLog($"Run on Startup: {(chkRunOnStartup.Checked ? "Enabled" : "Disabled")}");
        }
        catch (Exception ex)
        {
            AppendLog($"Error setting startup: {ex.Message}");
            chkRunOnStartup.Checked = !chkRunOnStartup.Checked;
        }
    }

    /// <summary>
    /// Server log event handler
    /// </summary>
    private void PrintServer_LogMessage(object? sender, LogEventArgs e)
    {
        AppendLog(e.Message);
    }

    /// <summary>
    /// Print job completed event
    /// </summary>
    private void PrintServer_PrintJobCompleted(object? sender, PrintJobEventArgs e)
    {
        if (e.Success)
        {
            AppendLog($"Print job completed - {e.BytesSent} bytes sent");
        }
        else
        {
            AppendLog($"Print job failed: {e.ErrorMessage}");
        }
    }

    /// <summary>
    /// Load saved settings
    /// </summary>
    private void LoadSettings()
    {
        chkMinimizeToTray.Checked = _configService.Config.MinimizeToTray;
        chkRunOnStartup.Checked = StartupRegistryService.IsStartupEnabled();
    }

    /// <summary>
    /// Initialize system tray (TASK-14)
    /// </summary>
    private void InitializeTray()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Open WinRawBT", null, (s, e) => ShowWindow());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("Test Print", null, async (s, e) => 
        {
            if (cmbPrinters.SelectedItem != null)
            {
                btnTestPrint_Click(s, e);
            }
        });
        _trayMenu.Items.Add("Start/Stop Service", null, (s, e) => btnToggleService_Click(s, e));
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "WinRawBT",
            Icon = SystemIcons.Application,
            ContextMenuStrip = _trayMenu,
            Visible = false
        };

        _notifyIcon.DoubleClick += (s, e) => ShowWindow();
    }

    /// <summary>
    /// Show window from tray
    /// </summary>
    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        _notifyIcon!.Visible = false;
    }

    /// <summary>
    /// Handle form closing (TASK-14)
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_isClosing && e.CloseReason == CloseReason.UserClosing && chkMinimizeToTray.Checked)
        {
            // Minimize to tray instead of closing (TASK-14)
            e.Cancel = true;
            Hide();
            _notifyIcon!.Visible = true;
            _notifyIcon.ShowBalloonTip(2000, "WinRawBT", "Running in background", ToolTipIcon.Info);
        }
        else
        {
            // Clean up resources
            _printServer.Stop();
            _bluetoothService.Dispose();
            _notifyIcon?.Dispose();
            _trayMenu?.Dispose();
            base.OnFormClosing(e);
        }
    }

    /// <summary>
    /// Exit application completely
    /// </summary>
    private void ExitApplication()
    {
        _isClosing = true;
        Close();
    }
}
