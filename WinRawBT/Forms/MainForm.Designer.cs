using System.Drawing;
using System.Windows.Forms;

namespace WinRawBT.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // UI Controls
    private Label lblSelectPrinter;
    private ComboBox cmbPrinters;
    private Button btnRefresh;
    private Button btnTestPrint;
    private Button btnToggleService;
    private CheckBox chkRunOnStartup;
    private CheckBox chkMinimizeToTray;
    private Label lblLog;
    private Button btnClearLog;
    protected TextBox txtLog;
    protected StatusStrip statusStrip;
    protected ToolStripStatusLabel lblStatus;

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
        
        // Form settings (TASK-11)
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(520, 420);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "WinRawBT - Bluetooth ESC/POS Service";
        
        // Initialize controls
        this.lblSelectPrinter = new Label();
        this.cmbPrinters = new ComboBox();
        this.btnRefresh = new Button();
        this.btnTestPrint = new Button();
        this.btnToggleService = new Button();
        this.chkRunOnStartup = new CheckBox();
        this.chkMinimizeToTray = new CheckBox();
        this.lblLog = new Label();
        this.btnClearLog = new Button();
        this.txtLog = new TextBox();
        this.statusStrip = new StatusStrip();
        this.lblStatus = new ToolStripStatusLabel();
        
        this.statusStrip.SuspendLayout();
        this.SuspendLayout();

        // lblSelectPrinter (TASK-11)
        this.lblSelectPrinter.AutoSize = true;
        this.lblSelectPrinter.Location = new Point(12, 15);
        this.lblSelectPrinter.Name = "lblSelectPrinter";
        this.lblSelectPrinter.Size = new Size(165, 15);
        this.lblSelectPrinter.TabIndex = 0;
        this.lblSelectPrinter.Text = "1. Select Paired Bluetooth Printer:";

        // cmbPrinters (TASK-11)
        this.cmbPrinters.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.cmbPrinters.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbPrinters.FormattingEnabled = true;
        this.cmbPrinters.Location = new Point(12, 35);
        this.cmbPrinters.Name = "cmbPrinters";
        this.cmbPrinters.Size = new Size(350, 23);
        this.cmbPrinters.TabIndex = 1;
        this.cmbPrinters.SelectedIndexChanged += new System.EventHandler(this.cmbPrinters_SelectedIndexChanged);

        // btnRefresh (TASK-11)
        this.btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRefresh.Location = new Point(368, 33);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new Size(140, 26);
        this.btnRefresh.TabIndex = 2;
        this.btnRefresh.Text = "Refresh";
        this.btnRefresh.UseVisualStyleBackColor = true;
        this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

        // btnTestPrint (TASK-11)
        this.btnTestPrint.Location = new Point(12, 70);
        this.btnTestPrint.Name = "btnTestPrint";
        this.btnTestPrint.Size = new Size(175, 30);
        this.btnTestPrint.TabIndex = 3;
        this.btnTestPrint.Text = "Test Connection (Print Receipt)";
        this.btnTestPrint.UseVisualStyleBackColor = true;
        this.btnTestPrint.Click += new System.EventHandler(this.btnTestPrint_Click);

        // btnToggleService (TASK-11)
        this.btnToggleService.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnToggleService.Location = new Point(333, 70);
        this.btnToggleService.Name = "btnToggleService";
        this.btnToggleService.Size = new Size(175, 30);
        this.btnToggleService.TabIndex = 4;
        this.btnToggleService.Text = "▶ Start Service (40213)";
        this.btnToggleService.UseVisualStyleBackColor = true;
        this.btnToggleService.Click += new System.EventHandler(this.btnToggleService_Click);

        // chkRunOnStartup (TASK-11)
        this.chkRunOnStartup.AutoSize = true;
        this.chkRunOnStartup.Location = new Point(12, 110);
        this.chkRunOnStartup.Name = "chkRunOnStartup";
        this.chkRunOnStartup.Size = new Size(149, 19);
        this.chkRunOnStartup.TabIndex = 5;
        this.chkRunOnStartup.Text = "Run on Windows Startup";
        this.chkRunOnStartup.UseVisualStyleBackColor = true;
        this.chkRunOnStartup.CheckedChanged += new System.EventHandler(this.chkRunOnStartup_CheckedChanged);

        // chkMinimizeToTray (TASK-11)
        this.chkMinimizeToTray.AutoSize = true;
        this.chkMinimizeToTray.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.chkMinimizeToTray.Location = new Point(368, 110);
        this.chkMinimizeToTray.Name = "chkMinimizeToTray";
        this.chkMinimizeToTray.Size = new Size(117, 19);
        this.chkMinimizeToTray.TabIndex = 6;
        this.chkMinimizeToTray.Text = "Minimize to Tray";
        this.chkMinimizeToTray.UseVisualStyleBackColor = true;
        this.chkMinimizeToTray.CheckedChanged += new System.EventHandler(this.chkMinimizeToTray_CheckedChanged);

        // lblLog (TASK-11)
        this.lblLog.AutoSize = true;
        this.lblLog.Location = new Point(12, 140);
        this.lblLog.Name = "lblLog";
        this.lblLog.Size = new Size(67, 15);
        this.lblLog.TabIndex = 7;
        this.lblLog.Text = "Activity Log:";

        // btnClearLog (TASK-11)
        this.btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnClearLog.Location = new Point(433, 135);
        this.btnClearLog.Name = "btnClearLog";
        this.btnClearLog.Size = new Size(75, 23);
        this.btnClearLog.TabIndex = 8;
        this.btnClearLog.Text = "Clear Log";
        this.btnClearLog.UseVisualStyleBackColor = true;
        this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);

        // txtLog (TASK-11) - Dark Console Theme
        this.txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.txtLog.BackColor = Color.FromArgb(30, 30, 30);
        this.txtLog.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtLog.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.txtLog.Location = new Point(12, 160);
        this.txtLog.Multiline = true;
        this.txtLog.Name = "txtLog";
        this.txtLog.ReadOnly = true;
        this.txtLog.ScrollBars = ScrollBars.Vertical;
        this.txtLog.Size = new Size(496, 200);
        this.txtLog.TabIndex = 9;

        // statusStrip (TASK-11)
        this.statusStrip.Items.AddRange(new ToolStripItem[] { this.lblStatus });
        this.statusStrip.Location = new Point(0, 370);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new Size(520, 25);
        this.statusStrip.TabIndex = 10;
        this.statusStrip.Text = "statusStrip";

        // lblStatus (TASK-11)
        this.lblStatus.ForeColor = Color.Gray;
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new Size(110, 20);
        this.lblStatus.Text = "● Service Stopped";

        // Add controls to form
        this.Controls.Add(this.statusStrip);
        this.Controls.Add(this.txtLog);
        this.Controls.Add(this.btnClearLog);
        this.Controls.Add(this.lblLog);
        this.Controls.Add(this.chkMinimizeToTray);
        this.Controls.Add(this.chkRunOnStartup);
        this.Controls.Add(this.btnToggleService);
        this.Controls.Add(this.btnTestPrint);
        this.Controls.Add(this.btnRefresh);
        this.Controls.Add(this.cmbPrinters);
        this.Controls.Add(this.lblSelectPrinter);

        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();

        // Initialize system tray
        InitializeTray();
    }
}
