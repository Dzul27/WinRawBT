using WinRawBT.Forms;

namespace WinRawBT;

internal static class Program
{
    // TASK-02: named mutex to enforce a single running instance.
    private const string MutexName = @"Global\WinRawBT_SingleInstance_Mutex";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "WinRawBT is already running.\n\n" +
                "Check the system tray for the existing instance. " +
                "Running a second copy would conflict on the local print port.",
                "WinRawBT Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());

        GC.KeepAlive(mutex);
    }
}
