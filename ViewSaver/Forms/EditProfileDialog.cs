using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ViewSaver.Controls;
using ViewSaver.Helpers;
using ViewSaver.Models;
using ViewSaver.Services;

namespace ViewSaver.Forms
{
    /// <summary>
    /// Fine-grained editing of a profile's monitor positions (e.g. to line monitors up exactly).
    /// Works on a copy; nothing is saved until OK passes validation.
    /// </summary>
    public class EditProfileDialog : Form
    {
        private const int ColX = 4;
        private const int ColY = 5;

        private readonly ProfileManager _profileManager;
        private readonly DisplayProfile _original;
        private readonly DisplayProfile _working;
        private readonly DataGridView _grid;
        private readonly MonitorLayoutPreview _preview;
        private readonly TextBox _txtName;
        private bool _syncingSelection;

        public EditProfileDialog(ProfileManager profileManager, DisplayProfile profile)
        {
            _profileManager = profileManager;
            _original = profile;
            _working = ProfileManager.Clone(profile);

            Text = $"Edit Profile - {profile.Name}";
            Icon = IconHelper.GetAppIcon();
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(640, 460);
            Size = new Size(820, 560);
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(10);

            // Name
            var namePanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            namePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            namePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            namePanel.Controls.Add(new Label { Text = "Profile name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 6) }, 0, 0);
            _txtName = new TextBox { Text = profile.Name, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 6) };
            namePanel.Controls.Add(_txtName, 1, 0);

            // Grid of monitors
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", ReadOnly = true, FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Monitor", ReadOnly = true, FillWeight = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resolution", ReadOnly = true, FillWeight = 40 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", ReadOnly = true, FillWeight = 30 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "X", FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Y", FillWeight = 25 });
            _grid.Columns[ColX].DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 230);
            _grid.Columns[ColY].DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 230);
            _grid.CellValidating += Grid_CellValidating;
            _grid.CellEndEdit += (_, _) => SyncFromGrid();
            _grid.SelectionChanged += (_, _) => SyncSelection(fromGrid: true);

            // Preview
            _preview = new MonitorLayoutPreview { Dock = DockStyle.Fill, AllowSelection = true };
            _preview.SelectedIndexChanged += (_, _) => SyncSelection(fromGrid: false);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6
            };
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_preview);
            split.Panel2.Padding = new Padding(0, 6, 0, 0);

            // Helper buttons
            var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var btnAlignTops = new Button { Text = "Align Tops to Selected", AutoSize = true };
            btnAlignTops.Click += (_, _) => AlignTops();
            var btnSideBySide = new Button { Text = "Arrange Side by Side", AutoSize = true };
            btnSideBySide.Click += (_, _) => ArrangeSideBySide();
            var btnReset = new Button { Text = "Reset", AutoSize = true };
            btnReset.Click += (_, _) => { LoadGrid(_original.Monitors.Select(m => (m.PositionX, m.PositionY)).ToList()); SyncFromGrid(); };
            tools.Controls.AddRange(new Control[] { btnAlignTops, btnSideBySide, btnReset });
            var hint = new Label
            {
                Text = "Edit X/Y in pixels. The monitor at (0, 0) becomes the primary display.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(8, 8, 0, 0)
            };
            tools.Controls.Add(hint);

            // OK / Cancel
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 30) };
            var btnOk = new Button { Text = "Save", Size = new Size(90, 30) };
            btnOk.Click += BtnOk_Click;
            buttons.Controls.AddRange(new Control[] { btnCancel, btnOk });

            Controls.Add(split);
            Controls.Add(tools);
            Controls.Add(buttons);
            Controls.Add(namePanel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            LoadGrid(_working.Monitors.Select(m => (m.PositionX, m.PositionY)).ToList());
            _preview.Monitors = _working.Monitors;

            Shown += (_, _) => split.SplitterDistance = Math.Max(120, split.Height / 2 - 20);
        }

        private void LoadGrid(IReadOnlyList<(int X, int Y)> positions)
        {
            _grid.Rows.Clear();
            for (int i = 0; i < _working.Monitors.Count; i++)
            {
                var m = _working.Monitors[i];
                string status = !m.IsEnabled ? "Disabled" : (positions[i].X == 0 && positions[i].Y == 0 ? "Primary" : "Extended");
                int row = _grid.Rows.Add(m.DisplayNumber, m.FriendlyName, $"{m.Width} × {m.Height} @ {m.RefreshRate}Hz",
                    status, positions[i].X, positions[i].Y);
                if (!m.IsEnabled)
                {
                    _grid.Rows[row].ReadOnly = true;
                    _grid.Rows[row].DefaultCellStyle.ForeColor = SystemColors.GrayText;
                }
            }
        }

        private List<(int X, int Y)> ReadGrid()
        {
            var list = new List<(int, int)>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                list.Add((Convert.ToInt32(row.Cells[ColX].Value), Convert.ToInt32(row.Cells[ColY].Value)));
            }
            return list;
        }

        private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != ColX && e.ColumnIndex != ColY) return;
            if (!int.TryParse(e.FormattedValue?.ToString(), out int v) || Math.Abs(v) > 100_000)
            {
                _grid.Rows[e.RowIndex].ErrorText = "Enter a whole number of pixels.";
                e.Cancel = true;
            }
            else
            {
                _grid.Rows[e.RowIndex].ErrorText = string.Empty;
            }
        }

        private void SyncFromGrid()
        {
            var positions = ReadGrid();
            ProfileManager.SetMonitorPositions(_working, positions);
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var m = _working.Monitors[i];
                _grid.Rows[i].Cells[3].Value = !m.IsEnabled ? "Disabled" : m.IsPrimary ? "Primary" : "Extended";
            }
            _preview.Monitors = _working.Monitors;
            _preview.Invalidate();
        }

        private void SyncSelection(bool fromGrid)
        {
            if (_syncingSelection) return;
            _syncingSelection = true;
            try
            {
                if (fromGrid)
                {
                    _preview.SelectedIndex = _grid.CurrentRow?.Index ?? -1;
                }
                else if (_preview.SelectedIndex >= 0 && _preview.SelectedIndex < _grid.Rows.Count)
                {
                    _grid.ClearSelection();
                    _grid.Rows[_preview.SelectedIndex].Selected = true;
                    _grid.CurrentCell = _grid.Rows[_preview.SelectedIndex].Cells[ColX];
                }
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private int SelectedOrPrimaryIndex()
        {
            int idx = _grid.CurrentRow?.Index ?? -1;
            if (idx >= 0 && _working.Monitors[idx].IsEnabled) return idx;
            idx = _working.Monitors.FindIndex(m => m.IsPrimary);
            return idx >= 0 ? idx : _working.Monitors.FindIndex(m => m.IsEnabled);
        }

        private void AlignTops()
        {
            int anchor = SelectedOrPrimaryIndex();
            if (anchor < 0) return;
            var positions = ReadGrid();
            int y = positions[anchor].Y;
            for (int i = 0; i < positions.Count; i++)
                if (_working.Monitors[i].IsEnabled) positions[i] = (positions[i].X, y);
            ApplyPositions(positions);
        }

        /// <summary>Lines enabled monitors up left-to-right (in current X order), touching, tops aligned.</summary>
        private void ArrangeSideBySide()
        {
            int anchor = SelectedOrPrimaryIndex();
            if (anchor < 0) return;
            var positions = ReadGrid();
            var order = Enumerable.Range(0, positions.Count)
                .Where(i => _working.Monitors[i].IsEnabled)
                .OrderBy(i => positions[i].X)
                .ToList();

            int x = 0;
            foreach (int i in order)
            {
                positions[i] = (x, 0);
                x += _working.Monitors[i].Width;
            }

            // Keep the anchor (selected/primary) monitor at the origin so it stays primary.
            int shift = positions[anchor].X;
            for (int i = 0; i < positions.Count; i++)
                if (_working.Monitors[i].IsEnabled) positions[i] = (positions[i].X - shift, 0);

            ApplyPositions(positions);
        }

        private void ApplyPositions(List<(int X, int Y)> positions)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                _grid.Rows[i].Cells[ColX].Value = positions[i].X;
                _grid.Rows[i].Cells[ColY].Value = positions[i].Y;
            }
            SyncFromGrid();
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (!_grid.EndEdit()) return;
            var name = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "Please enter a profile name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var positions = ReadGrid();
            var enabled = Enumerable.Range(0, positions.Count).Where(i => _working.Monitors[i].IsEnabled).ToList();

            // Overlapping desktops are not allowed by Windows.
            foreach (int a in enabled)
            foreach (int b in enabled.Where(b => b > a))
            {
                if (MonitorRect(a, positions).IntersectsWith(MonitorRect(b, positions)))
                {
                    MessageBox.Show(this,
                        $"Monitors {_working.Monitors[a].DisplayNumber} and {_working.Monitors[b].DisplayNumber} overlap. Adjust their positions so they only touch at the edges.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Exactly one monitor must sit at the origin; offer to shift the whole layout.
            if (!enabled.Any(i => positions[i] == (0, 0)) && enabled.Count > 0)
            {
                int anchor = SelectedOrPrimaryIndex();
                var (ax, ay) = positions[anchor];
                var answer = MessageBox.Show(this,
                    $"No monitor is at position (0, 0), which Windows needs for the primary display.\n\n" +
                    $"Shift the whole layout so monitor {_working.Monitors[anchor].DisplayNumber} ({_working.Monitors[anchor].FriendlyName}) is at (0, 0)?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;

                for (int i = 0; i < positions.Count; i++)
                    if (_working.Monitors[i].IsEnabled) positions[i] = (positions[i].X - ax, positions[i].Y - ay);
                ApplyPositions(positions);
            }

            ProfileManager.SetMonitorPositions(_working, positions);
            var validation = _profileManager.ValidateProfile(_working);
            if (!validation.Success)
            {
                var answer = MessageBox.Show(this,
                    $"Windows reports a problem with this layout:\n\n{validation.Message}\n\nSave anyway?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;
            }

            if (name != _original.Name)
                _profileManager.RenameProfile(_original.Id, name);
            _profileManager.UpdateMonitorPositions(_original.Id, positions);

            DialogResult = DialogResult.OK;
            Close();
        }

        private Rectangle MonitorRect(int i, IReadOnlyList<(int X, int Y)> positions) =>
            new(positions[i].X, positions[i].Y, _working.Monitors[i].Width, _working.Monitors[i].Height);
    }
}
