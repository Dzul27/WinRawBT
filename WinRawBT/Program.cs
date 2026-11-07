using System;
using System.Threading;
using System.Windows.Forms;
using WinRawBT.Forms;
using WinRawBT.Services;

namespace WinRawBT;

static class Program
{
    // Single-instance mutex name (TASK-02)
    private const string MutexName = "Global\\WinRawBT_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    static void Main()
    {
        // TASK-02: Implement Single-Instance Application Mutex
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show(
                "WinRawBT is already running. Only one instance is allowed to prevent port conflicts.",
                "WinRawBT - Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            _singleInstanceMutex.Dispose();
            return;
        }

        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Initialize configuration service
            var configService = new ConfigService();

            // Run main form
            Application.Run(new MainForm(configService));
        }
        finally
        {
            // Release the mutex on exit
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
    }
}
