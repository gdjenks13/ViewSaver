using System;
using System.IO;

namespace ViewSaver.Services
{
    public static class Logger
    {
        private static readonly string LogPath;
        private static readonly object LockObj = new();

        static Logger()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ViewSaver"
            );
            Directory.CreateDirectory(appDataPath);
            LogPath = Path.Combine(appDataPath, "viewsaver.log");
        }

        public static void Error(string message, Exception? ex = null)
        {
            Log("ERROR", message, ex);
        }

        public static void Warning(string message, Exception? ex = null)
        {
            Log("WARN", message, ex);
        }

        public static void Info(string message)
        {
            Log("INFO", message, null);
        }

        private static void Log(string level, string message, Exception? ex)
        {
            try
            {
                lock (LockObj)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var logMessage = $"[{timestamp}] [{level}] {message}";

                    if (ex != null)
                    {
                        logMessage += $"\n  Exception: {ex.GetType().Name}: {ex.Message}";
                        if (ex.StackTrace != null)
                        {
                            logMessage += $"\n  StackTrace: {ex.StackTrace}";
                        }
                    }

                    logMessage += Environment.NewLine;

                    // Rotate log if it gets too large (> 1MB)
                    if (File.Exists(LogPath))
                    {
                        var fileInfo = new FileInfo(LogPath);
                        if (fileInfo.Length > 1024 * 1024)
                        {
                            var backupPath = LogPath + ".old";
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }
                            File.Move(LogPath, backupPath);
                        }
                    }

                    File.AppendAllText(LogPath, logMessage);
                }
            }
            catch
            {
                // Silently fail if logging itself fails
            }
        }

        public static string GetLogPath() => LogPath;
    }
}
