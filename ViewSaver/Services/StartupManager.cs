using System;
using Microsoft.Win32;

namespace ViewSaver.Services
{
    public static class StartupManager
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ViewSaver";
        private static string ExecutablePath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }

        public static bool EnableStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                key?.SetValue(AppName, $"\"{ExecutablePath}\"");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool DisableStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key?.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool SetStartup(bool enabled)
        {
            return enabled ? EnableStartup() : DisableStartup();
        }
    }
}
