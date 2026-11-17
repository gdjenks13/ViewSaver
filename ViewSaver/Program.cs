using System;
using System.Threading;
using System.Windows.Forms;
using ViewSaver.Services;

namespace ViewSaver
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure only one instance runs
            using var mutex = new Mutex(true, "ViewSaver_SingleInstance", out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show("ViewSaver is already running.", "ViewSaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Log anything unhandled instead of letting the tray app vanish silently.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => Logger.Error("Unhandled UI exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Logger.Error("Unhandled exception", e.ExceptionObject as Exception);

            // High-DPI mode, visual styles and default font come from the csproj (ApplicationHighDpiMode etc.).
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
