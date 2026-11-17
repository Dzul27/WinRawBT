namespace WinRawBT.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label lblPrinters;
    private System.Windows.Forms.ComboBox cmbPrinters;
    private System.Windows.Forms.Button btnRefresh;
    private System.Windows.Forms.Button btnScan;
    private System.Windows.Forms.Button btnBtSettings;
    private System.Windows.Forms.Button btnCancelPair;
    private System.Windows.Forms.Label lblControls;
    private System.Windows.Forms.Button btnTestPrint;
    private System.Windows.Forms.Button btnToggleService;
    private System.Windows.Forms.CheckBox chkRunOnStartup;
    private System.Windows.Forms.CheckBox chkMinimizeToTray;
    private System.Windows.Forms.Label lblLog;
    private System.Windows.Forms.Button btnClearLog;
    private System.Windows.Forms.TextBox txtLog;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private System.Windows.Forms.ContextMenuStrip trayMenu;
    private System.Windows.Forms.ToolStripMenuItem trayOpen;
    private System.Windows.Forms.ToolStripMenuItem trayTestPrint;
    private System.Windows.Forms.ToolStripMenuItem trayToggleService;
    private System.Windows.Forms.ToolStripSeparator traySeparator;
    private System.Windows.Forms.ToolStripMenuItem trayExit;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.lblPrinters = new System.Windows.Forms.Label();
        this.cmbPrinters = new System.Windows.Forms.ComboBox();
        this.btnRefresh = new System.Windows.Forms.Button();
        this.btnScan = new System.Windows.Forms.Button();
        this.btnBtSettings = new System.Windows.Forms.Button();
        this.btnCancelPair = new System.Windows.Forms.Button();
        this.lblControls = new System.Windows.Forms.Label();
        this.btnTestPrint = new System.Windows.Forms.Button();
        this.btnToggleService = new System.Windows.Forms.Button();
        this.chkRunOnStartup = new System.Windows.Forms.CheckBox();
        this.chkMinimizeToTray = new System.Windows.Forms.CheckBox();
        this.lblLog = new System.Windows.Forms.Label();
        this.btnClearLog = new System.Windows.Forms.Button();
        this.txtLog = new System.Windows.Forms.TextBox();
        this.statusStrip = new System.Windows.Forms.StatusStrip();
        this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
        this.trayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
        this.trayOpen = new System.Windows.Forms.ToolStripMenuItem();
        this.trayTestPrint = new System.Windows.Forms.ToolStripMenuItem();
        this.trayToggleService = new System.Windows.Forms.ToolStripMenuItem();
        this.traySeparator = new System.Windows.Forms.ToolStripSeparator();
        this.trayExit = new System.Windows.Forms.ToolStripMenuItem();
        this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);

        this.statusStrip.SuspendLayout();
        this.trayMenu.SuspendLayout();
        this.SuspendLayout();

        // lblPrinters
        this.lblPrinters.AutoSize = true;
        this.lblPrinters.Location = new System.Drawing.Point(12, 12);
        this.lblPrinters.Name = "lblPrinters";
        this.lblPrinters.Size = new System.Drawing.Size(220, 15);
        this.lblPrinters.Text = "1. Select Bluetooth Printer (scan to find and pair new ones):";

        // cmbPrinters
        this.cmbPrinters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbPrinters.FormattingEnabled = true;
        this.cmbPrinters.Location = new System.Drawing.Point(12, 32);
        this.cmbPrinters.Name = "cmbPrinters";
        this.cmbPrinters.Size = new System.Drawing.Size(492, 23);
        this.cmbPrinters.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Left
            | System.Windows.Forms.AnchorStyles.Right;
        this.cmbPrinters.SelectionChangeCommitted += new System.EventHandler(this.cmbPrinters_SelectionChangeCommitted);

        // btnRefresh
        this.btnRefresh.Location = new System.Drawing.Point(12, 62);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new System.Drawing.Size(96, 25);
        this.btnRefresh.Text = "Refresh";
        this.btnRefresh.UseVisualStyleBackColor = true;
        this.btnRefresh.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Left;
        this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

        // btnScan
        this.btnScan.Location = new System.Drawing.Point(114, 62);
        this.btnScan.Name = "btnScan";
        this.btnScan.Size = new System.Drawing.Size(150, 25);
        this.btnScan.Text = "Scan for Printers";
        this.btnScan.UseVisualStyleBackColor = true;
        this.btnScan.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Left;
        this.btnScan.Click += new System.EventHandler(this.btnScan_Click);

        // btnBtSettings
        this.btnBtSettings.Location = new System.Drawing.Point(270, 62);
        this.btnBtSettings.Name = "btnBtSettings";
        this.btnBtSettings.Size = new System.Drawing.Size(150, 25);
        this.btnBtSettings.Text = "Bluetooth Settings...";
        this.btnBtSettings.UseVisualStyleBackColor = true;
        this.btnBtSettings.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Left;
        this.btnBtSettings.Click += new System.EventHandler(this.btnBtSettings_Click);

        // btnCancelPair (visible only while a pairing attempt runs)
        this.btnCancelPair.Location = new System.Drawing.Point(426, 62);
        this.btnCancelPair.Name = "btnCancelPair";
        this.btnCancelPair.Size = new System.Drawing.Size(78, 25);
        this.btnCancelPair.Text = "Cancel";
        this.btnCancelPair.UseVisualStyleBackColor = true;
        this.btnCancelPair.Visible = false;
        this.btnCancelPair.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Left;
        this.btnCancelPair.Click += new System.EventHandler(this.btnCancelPair_Click);

        // lblControls
        this.lblControls.AutoSize = true;
        this.lblControls.Location = new System.Drawing.Point(12, 100);
        this.lblControls.Name = "lblControls";
        this.lblControls.Size = new System.Drawing.Size(180, 15);
        this.lblControls.Text = "2. Hardware && Service Controls:";

        // btnTestPrint
        this.btnTestPrint.Location = new System.Drawing.Point(12, 120);
        this.btnTestPrint.Name = "btnTestPrint";
        this.btnTestPrint.Size = new System.Drawing.Size(240, 34);
        this.btnTestPrint.Text = "Test Connection (Print Receipt)";
        this.btnTestPrint.UseVisualStyleBackColor = true;
        this.btnTestPrint.Click += new System.EventHandler(this.btnTestPrint_Click);

        // btnToggleService
        this.btnToggleService.Location = new System.Drawing.Point(264, 120);
        this.btnToggleService.Name = "btnToggleService";
        this.btnToggleService.Size = new System.Drawing.Size(240, 34);
        this.btnToggleService.Text = "\u25B6 Start Service (8080)";
        this.btnToggleService.UseVisualStyleBackColor = true;
        this.btnToggleService.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Right;
        this.btnToggleService.Click += new System.EventHandler(this.btnToggleService_Click);

        // chkRunOnStartup
        this.chkRunOnStartup.AutoSize = true;
        this.chkRunOnStartup.Location = new System.Drawing.Point(12, 164);
        this.chkRunOnStartup.Name = "chkRunOnStartup";
        this.chkRunOnStartup.Size = new System.Drawing.Size(160, 19);
        this.chkRunOnStartup.Text = "Run on Windows Startup";
        this.chkRunOnStartup.UseVisualStyleBackColor = true;
        this.chkRunOnStartup.CheckedChanged += new System.EventHandler(this.chkRunOnStartup_CheckedChanged);

        // chkMinimizeToTray
        this.chkMinimizeToTray.AutoSize = true;
        this.chkMinimizeToTray.Location = new System.Drawing.Point(264, 164);
        this.chkMinimizeToTray.Name = "chkMinimizeToTray";
        this.chkMinimizeToTray.Size = new System.Drawing.Size(130, 19);
        this.chkMinimizeToTray.Text = "Minimize to Tray";
        this.chkMinimizeToTray.UseVisualStyleBackColor = true;
        this.chkMinimizeToTray.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Right;
        this.chkMinimizeToTray.CheckedChanged += new System.EventHandler(this.chkMinimizeToTray_CheckedChanged);

        // lblLog
        this.lblLog.AutoSize = true;
        this.lblLog.Location = new System.Drawing.Point(12, 194);
        this.lblLog.Name = "lblLog";
        this.lblLog.Size = new System.Drawing.Size(80, 15);
        this.lblLog.Text = "Activity Log:";

        // btnClearLog
        this.btnClearLog.Location = new System.Drawing.Point(424, 190);
        this.btnClearLog.Name = "btnClearLog";
        this.btnClearLog.Size = new System.Drawing.Size(80, 23);
        this.btnClearLog.Text = "Clear Log";
        this.btnClearLog.UseVisualStyleBackColor = true;
        this.btnClearLog.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Right;
        this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);

        // txtLog (dark console theme)
        this.txtLog.BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        this.txtLog.ForeColor = System.Drawing.Color.FromArgb(0, 221, 118);
        this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtLog.Location = new System.Drawing.Point(12, 218);
        this.txtLog.Multiline = true;
        this.txtLog.Name = "txtLog";
        this.txtLog.ReadOnly = true;
        this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtLog.Size = new System.Drawing.Size(492, 178);
        this.txtLog.WordWrap = true;
        this.txtLog.Anchor = System.Windows.Forms.AnchorStyles.Top
            | System.Windows.Forms.AnchorStyles.Bottom
            | System.Windows.Forms.AnchorStyles.Left
            | System.Windows.Forms.AnchorStyles.Right;

        // statusStrip
        this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
        this.statusStrip.Location = new System.Drawing.Point(0, 420);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new System.Drawing.Size(516, 22);
        this.statusStrip.SizingGrip = false;

        // lblStatus
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Text = "\u25CF Service Stopped";

        // trayMenu
        this.trayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.trayOpen,
            this.trayTestPrint,
            this.trayToggleService,
            this.traySeparator,
            this.trayExit});
        this.trayMenu.Name = "trayMenu";
        this.trayMenu.Size = new System.Drawing.Size(180, 98);

        // trayOpen
        this.trayOpen.Name = "trayOpen";
        this.trayOpen.Text = "Open WinRawBT";
        this.trayOpen.Click += new System.EventHandler(this.trayOpen_Click);

        // trayTestPrint
        this.trayTestPrint.Name = "trayTestPrint";
        this.trayTestPrint.Text = "Test Print";
        this.trayTestPrint.Click += new System.EventHandler(this.btnTestPrint_Click);

        // trayToggleService
        this.trayToggleService.Name = "trayToggleService";
        this.trayToggleService.Text = "Start Service";
        this.trayToggleService.Click += new System.EventHandler(this.btnToggleService_Click);

        // trayExit
        this.trayExit.Name = "trayExit";
        this.trayExit.Text = "Exit";
        this.trayExit.Click += new System.EventHandler(this.trayExit_Click);

        // notifyIcon
        this.notifyIcon.ContextMenuStrip = this.trayMenu;
        this.notifyIcon.Text = "WinRawBT";
        this.notifyIcon.Visible = false;
        this.notifyIcon.DoubleClick += new System.EventHandler(this.trayOpen_Click);

        // MainForm
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(516, 442);
        this.Controls.Add(this.lblPrinters);
        this.Controls.Add(this.cmbPrinters);
        this.Controls.Add(this.btnRefresh);
        this.Controls.Add(this.btnScan);
        this.Controls.Add(this.lblControls);
        this.Controls.Add(this.btnTestPrint);
        this.Controls.Add(this.btnToggleService);
        this.Controls.Add(this.chkRunOnStartup);
        this.Controls.Add(this.chkMinimizeToTray);
        this.Controls.Add(this.lblLog);
        this.Controls.Add(this.btnClearLog);
        this.Controls.Add(this.txtLog);
        this.Controls.Add(this.statusStrip);
        this.Controls.Add(this.btnBtSettings);
        this.Controls.Add(this.btnCancelPair);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimumSize = new System.Drawing.Size(532, 481);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "WinRawBT - Bluetooth ESC/POS Service";
        this.Load += new System.EventHandler(this.MainForm_Load);
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);

        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.trayMenu.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
