using System.Drawing;
using System.Drawing.Drawing2D;

namespace ViewSaver.Helpers
{
    public static class IconHelper
    {
        private static Icon? _cachedIcon;

        public static Icon GetAppIcon()
        {
            if (_cachedIcon == null)
            {
                _cachedIcon = CreateAppIcon();
            }
            return _cachedIcon;
        }

        private static Icon CreateAppIcon()
        {
            var bitmap = new Bitmap(32, 32);
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
