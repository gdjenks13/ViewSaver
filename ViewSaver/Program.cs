using System;
using System.Windows.Forms;

namespace ViewSaver
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure only one instance runs
            using var mutex = new System.Threading.Mutex(true, "ViewSaver_SingleInstance", out bool createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("ViewSaver is already running.", "ViewSaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
