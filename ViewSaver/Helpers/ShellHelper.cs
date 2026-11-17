using System;
using System.Diagnostics;
using System.Windows.Forms;
using ViewSaver.Services;

namespace ViewSaver.Helpers
{
    public static class ShellHelper
    {
        public const string RepositoryUrl = "https://github.com/gdjenks13/ViewSaver";

        /// <summary>Opens Windows Settings &gt; System &gt; Display.</summary>
        public static void OpenDisplaySettings() => Open("ms-settings:display");

        public static void OpenRepository() => Open(RepositoryUrl);

        public static void Open(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to open {target}", ex);
                MessageBox.Show($"Could not open {target}:\n{ex.Message}", "ViewSaver",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
