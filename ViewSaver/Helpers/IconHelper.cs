using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ViewSaver.Helpers
{
    public static class IconHelper
    {
        private static Icon? _appIcon;
        private static Icon? _trayIcon;

        /// <summary>Full multi-resolution icon (window title bars, taskbar, Alt+Tab).</summary>
        public static Icon GetAppIcon() => _appIcon ??= LoadEmbeddedIcon(null) ?? CreateFallbackIcon();

        /// <summary>Small-icon variant sized for the notification area at the current DPI.</summary>
        public static Icon GetTrayIcon() => _trayIcon ??= LoadEmbeddedIcon(SystemInformation.SmallIconSize) ?? GetAppIcon();

        private static Icon? LoadEmbeddedIcon(Size? size)
        {
            // Same .ico that is embedded as the exe's ApplicationIcon (Assets\ViewSaver.ico).
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewSaver.ico");
            if (stream == null) return null;
            return size is { } s ? new Icon(stream, s) : new Icon(stream);
        }

        private static Icon CreateFallbackIcon()
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using var bodyBrush = new SolidBrush(Color.FromArgb(64, 64, 64));
                g.FillRectangle(bodyBrush, 2, 2, 28, 20);

                using var screenBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillRectangle(screenBrush, 4, 4, 24, 16);

                using var standBrush = new SolidBrush(Color.FromArgb(96, 96, 96));
                g.FillRectangle(standBrush, 12, 22, 8, 4);
                g.FillRectangle(standBrush, 8, 26, 16, 4);

                using var letterPen = new Pen(Color.White, 2);
                g.DrawLine(letterPen, 10, 6, 16, 16);
                g.DrawLine(letterPen, 22, 6, 16, 16);
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }
    }
}
