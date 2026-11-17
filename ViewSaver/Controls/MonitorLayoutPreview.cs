using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ViewSaver.Models;

namespace ViewSaver.Controls
{
    /// <summary>
    /// Draws a profile's monitor arrangement to scale, numbered like Windows Settings &gt; Display.
    /// Monitors can be clicked to select them (used by the edit dialog).
    /// </summary>
    public class MonitorLayoutPreview : Control
    {
        private IReadOnlyList<MonitorConfiguration> _monitors = Array.Empty<MonitorConfiguration>();
        private readonly List<(RectangleF Bounds, int Index)> _hitAreas = new();
        private int _selectedIndex = -1;

        public event EventHandler? SelectedIndexChanged;

        public MonitorLayoutPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = SystemColors.ControlLight;
            MinimumSize = new Size(120, 80);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<MonitorConfiguration> Monitors
        {
            get => _monitors;
            set
            {
                _monitors = value ?? Array.Empty<MonitorConfiguration>();
                if (_selectedIndex >= _monitors.Count) _selectedIndex = -1;
                Invalidate();
            }
        }

        /// <summary>Index into <see cref="Monitors"/> of the highlighted monitor, or -1.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [DefaultValue(false)]
        public bool AllowSelection { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            _hitAreas.Clear();

            var active = _monitors
                .Select((m, i) => (Monitor: m, Index: i))
                .Where(x => x.Monitor.IsEnabled && x.Monitor.Width > 0 && x.Monitor.Height > 0)
                .ToList();

            if (active.Count == 0)
            {
                TextRenderer.DrawText(g, "No monitor layout to preview", Font, ClientRectangle, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            int minX = active.Min(x => x.Monitor.PositionX);
            int minY = active.Min(x => x.Monitor.PositionY);
            int maxX = active.Max(x => x.Monitor.PositionX + x.Monitor.Width);
            int maxY = active.Max(x => x.Monitor.PositionY + x.Monitor.Height);

            const float margin = 12f;
            float availW = Math.Max(1, ClientSize.Width - margin * 2);
            float availH = Math.Max(1, ClientSize.Height - margin * 2);
            float scale = Math.Min(availW / (maxX - minX), availH / (maxY - minY));
            float offsetX = margin + (availW - (maxX - minX) * scale) / 2;
            float offsetY = margin + (availH - (maxY - minY) * scale) / 2;

            float gap = Math.Max(1.5f, 3f * DeviceDpi / 96f);

            foreach (var (m, index) in active)
            {
                var rect = new RectangleF(
                    offsetX + (m.PositionX - minX) * scale + gap / 2,
                    offsetY + (m.PositionY - minY) * scale + gap / 2,
                    m.Width * scale - gap,
                    m.Height * scale - gap);
                _hitAreas.Add((rect, index));

                bool selected = index == _selectedIndex;
                Color fill = selected ? SystemColors.Highlight
                    : m.IsPrimary ? Color.FromArgb(0, 103, 192) : Color.FromArgb(70, 120, 175);
                Color text = Color.White;

                using (var path = RoundedRect(rect, 4))
                using (var brush = new SolidBrush(fill))
                using (var border = new Pen(selected ? SystemColors.HighlightText : Color.FromArgb(40, 40, 40), selected ? 2.5f : 1f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(border, path);
                }

                // Big display number (as in Windows Settings), with name/resolution beneath when there's room.
                float numberSize = Math.Clamp(Math.Min(rect.Width, rect.Height) * 0.35f, 8f, 40f);
                using var numberFont = new Font(Font.FontFamily, numberSize, FontStyle.Bold, GraphicsUnit.Pixel);
                using var detailFont = new Font(Font.FontFamily, Math.Clamp(numberSize * 0.32f, 8f, 13f), GraphicsUnit.Pixel);
                using var textBrush = new SolidBrush(text);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

                string number = m.DisplayNumber.ToString();
                string detail = $"{m.FriendlyName}\n{m.Width}×{m.Height}{(m.IsPrimary ? " • Primary" : "")}";
                var detailSize = g.MeasureString(detail, detailFont);
                bool showDetail = rect.Height > numberSize + detailSize.Height + 6 && rect.Width > 60;

                if (showDetail)
                {
                    float total = numberSize + detailSize.Height;
                    float top = rect.Top + (rect.Height - total) / 2;
                    g.DrawString(number, numberFont, textBrush, new RectangleF(rect.X, top, rect.Width, numberSize * 1.2f), center);
                    using var detailFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(detail, detailFont, textBrush, new RectangleF(rect.X + 2, top + numberSize * 1.15f, rect.Width - 4, detailSize.Height + 2), detailFormat);
                }
                else
                {
                    g.DrawString(number, numberFont, textBrush, rect, center);
                }
            }

            int disabled = _monitors.Count(m => !m.IsEnabled);
            if (disabled > 0)
            {
                TextRenderer.DrawText(g, $"+{disabled} disabled", Font, new Point(4, ClientSize.Height - Font.Height - 4), SystemColors.GrayText);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowSelection) return;
            Focus();
            var hit = _hitAreas.FirstOrDefault(h => h.Bounds.Contains(e.Location));
            SelectedIndex = hit.Bounds.IsEmpty ? -1 : hit.Index;
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
