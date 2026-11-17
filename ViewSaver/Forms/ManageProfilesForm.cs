using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ViewSaver.Helpers;
using ViewSaver.Models;
using ViewSaver.Services;

namespace ViewSaver.Forms
{
    public partial class ManageProfilesForm : Form
    {
        private readonly ProfileManager _profileManager;
        private ListView lstProfiles = null!;
        private Button btnApply = null!;
        private Button btnRename = null!;
        private Button btnUpdate = null!;
        private Button btnHotkey = null!;
        private Button btnDelete = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;
        private Button btnClose = null!;
        private Button btnNew = null!;
        private Button btnExport = null!;
        private Button btnImport = null!;
        private RichTextBox txtDetails = null!;
        private Label lblDetails = null!;

        public ManageProfilesForm(ProfileManager profileManager)
        {
            _profileManager = profileManager;
            InitializeComponent();
            LoadProfiles();
        }

        private void InitializeComponent()
        {
            Text = "Manage Profiles - ViewSaver";
            Size = new Size(780, 520);
            MinimumSize = new Size(650, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            Icon = IconHelper.GetAppIcon();

            const int padding = 15;
            const int buttonWidth = 130;
            const int buttonHeight = 30;

            lstProfiles = new ListView
            {
                Location = new Point(padding, padding),
                Size = new Size(400, 280),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
            };
            lstProfiles.Columns.Add("Name", 180);
            lstProfiles.Columns.Add("Hotkey", 100);
            lstProfiles.Columns.Add("Monitors", 60);
            lstProfiles.Columns.Add("Modified", 100);
            lstProfiles.SelectedIndexChanged += LstProfiles_SelectedIndexChanged;
            lstProfiles.DoubleClick += BtnApply_Click;
            Controls.Add(lstProfiles);

            lblDetails = new Label
            {
                Text = "Profile Details:",
                Location = new Point(padding, 305),
                Size = new Size(100, 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            Controls.Add(lblDetails);

            txtDetails = new RichTextBox
            {
                Location = new Point(padding, 325),
                Size = new Size(400, 120),
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            Controls.Add(txtDetails);

            int btnX = 450;
            int btnY = padding;
            int btnSpacing = 38;

            btnApply = new Button
            {
                Text = "Apply Profile",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnApply.Click += BtnApply_Click;
            Controls.Add(btnApply);

            btnY += btnSpacing;
            btnRename = new Button
            {
                Text = "Rename",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRename.Click += BtnRename_Click;
            Controls.Add(btnRename);

            btnY += btnSpacing;
            btnUpdate = new Button
            {
                Text = "Update Profile",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnUpdate.Click += BtnUpdate_Click;
            Controls.Add(btnUpdate);

            btnY += btnSpacing;
            btnHotkey = new Button
            {
                Text = "Set Hotkey...",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnHotkey.Click += BtnHotkey_Click;
            Controls.Add(btnHotkey);

            btnY += btnSpacing;
            btnDelete = new Button
            {
                Text = "Delete",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDelete.Click += BtnDelete_Click;
            Controls.Add(btnDelete);

            btnY += btnSpacing + 20;
            btnMoveUp = new Button
            {
                Text = "Move Up",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMoveUp.Click += BtnMoveUp_Click;
            Controls.Add(btnMoveUp);

            btnY += btnSpacing;
            btnMoveDown = new Button
            {
                Text = "Move Down",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnMoveDown.Click += BtnMoveDown_Click;
            Controls.Add(btnMoveDown);

            btnY += btnSpacing + 20;
            btnNew = new Button
            {
                Text = "Save Current...",
                Location = new Point(btnX, btnY),
                Size = new Size(buttonWidth, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnNew.Click += BtnNew_Click;
            Controls.Add(btnNew);

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(btnX, 410),
                Size = new Size(buttonWidth, buttonHeight),
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(btnClose);

            btnExport = new Button
            {
                Text = "Export...",
                Location = new Point(585, padding),
                Size = new Size(80, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnExport.Click += BtnExport_Click;
            Controls.Add(btnExport);

            btnImport = new Button
            {
                Text = "Import...",
                Location = new Point(675, padding),
                Size = new Size(80, buttonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnImport.Click += BtnImport_Click;
            Controls.Add(btnImport);

            Resize += OnFormResize;

            UpdateButtonStates();
        }

        private void OnFormResize(object? sender, EventArgs e)
        {
            int availableWidth = btnApply.Left - 30; // Space before buttons minus padding
            if (availableWidth > 200)
            {
                lstProfiles.Width = availableWidth;
                txtDetails.Width = availableWidth;
            }

            int detailsTop = lstProfiles.Bottom + 10;
            lblDetails.Top = detailsTop;
            txtDetails.Top = detailsTop + 20;
            txtDetails.Height = ClientSize.Height - txtDetails.Top - 35;
        }

        private void LoadProfiles()
        {
            lstProfiles.Items.Clear();

            foreach (var profile in _profileManager.Profiles)
            {
                var item = new ListViewItem(profile.Name)
                {
                    Tag = profile.Id
                };
                item.SubItems.Add(profile.Hotkey?.ToString() ?? "None");
                item.SubItems.Add(profile.Monitors.Count.ToString());
                item.SubItems.Add(profile.ModifiedAt.ToString("yyyy-MM-dd"));

                lstProfiles.Items.Add(item);
            }

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = lstProfiles.SelectedItems.Count > 0;
            btnApply.Enabled = hasSelection;
            btnRename.Enabled = hasSelection;
            btnUpdate.Enabled = hasSelection;
            btnHotkey.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
            btnMoveUp.Enabled = hasSelection && lstProfiles.SelectedIndices[0] > 0;
            btnMoveDown.Enabled = hasSelection && lstProfiles.SelectedIndices[0] < lstProfiles.Items.Count - 1;
        }

        private void ShowProfileDetails(string profileId)
        {
            var profile = _profileManager.GetProfile(profileId);
            if (profile == null)
            {
                txtDetails.Clear();
                return;
            }

            txtDetails.Clear();
            txtDetails.SelectionFont = new Font(txtDetails.Font, FontStyle.Bold);
            txtDetails.AppendText($"{profile.Name}\n");
            txtDetails.SelectionFont = txtDetails.Font;
            txtDetails.AppendText($"Created: {profile.CreatedAt:yyyy-MM-dd HH:mm}\n");
            txtDetails.AppendText($"Modified: {profile.ModifiedAt:yyyy-MM-dd HH:mm}\n");
            txtDetails.AppendText($"Hotkey: {profile.Hotkey?.ToString() ?? "None"}\n\n");

            txtDetails.SelectionFont = new Font(txtDetails.Font, FontStyle.Bold);
            txtDetails.AppendText("Monitors:\n");
            txtDetails.SelectionFont = txtDetails.Font;

            foreach (var monitor in profile.Monitors.OrderBy(m => m.DisplayOrder))
            {
                string primary = monitor.IsPrimary ? " [Primary]" : "";
                string enabled = monitor.IsEnabled ? "" : " [Disabled]";
                txtDetails.AppendText($"  • {monitor.FriendlyName}{primary}{enabled}\n");
                txtDetails.AppendText($"    {monitor.Width}x{monitor.Height} @ {monitor.RefreshRate}Hz\n");
                txtDetails.AppendText($"    Position: ({monitor.PositionX}, {monitor.PositionY})\n");
            }
        }

        private string? GetSelectedProfileId()
        {
            if (lstProfiles.SelectedItems.Count == 0) return null;
            return lstProfiles.SelectedItems[0].Tag as string;
        }

        #region Event Handlers

        private void LstProfiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateButtonStates();
            var profileId = GetSelectedProfileId();
            if (profileId != null)
            {
                ShowProfileDetails(profileId);
            }
            else
            {
                txtDetails.Clear();
            }
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            UseWaitCursor = true;
            var result = _profileManager.ApplyProfile(profileId);
            UseWaitCursor = false;

            if (result.Success)
            {
                var text = $"Profile '{profile.Name}' has been applied.";
                if (!string.IsNullOrEmpty(result.Message)) text += "\n\n" + result.Message;
                MessageBox.Show(text, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Failed to apply profile '{profile.Name}'.\n\n{result.Message}\n\nDetails: {Services.Logger.GetLogPath()}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRename_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            string? newName = ShowInputDialog("Rename Profile", "Enter new name:", profile.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _profileManager.RenameProfile(profileId, newName);
                LoadProfiles();
            }
        }

        private void BtnUpdate_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            var result = MessageBox.Show(
                $"This will update profile '{profile.Name}' with the current display configuration.\n\nContinue?",
                "Update Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _profileManager.UpdateProfile(profileId);
                LoadProfiles();
                ShowProfileDetails(profileId);
                MessageBox.Show("Profile updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnHotkey_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            using var dialog = new HotkeyDialog(profile.Hotkey);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _profileManager.SetProfileHotkey(profileId, dialog.Hotkey);
                    LoadProfiles();
                    ShowProfileDetails(profileId);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(ex.Message, "Hotkey Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete profile '{profile.Name}'?",
                "Delete Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _profileManager.DeleteProfile(profileId);
                LoadProfiles();
                txtDetails.Clear();
            }
        }

        private void BtnMoveUp_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            int index = lstProfiles.SelectedIndices[0];
            _profileManager.MoveProfileUp(profileId);
            LoadProfiles();
            if (index > 0)
            {
                lstProfiles.Items[index - 1].Selected = true;
            }
        }

        private void BtnMoveDown_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            if (profileId == null) return;

            int index = lstProfiles.SelectedIndices[0];
            _profileManager.MoveProfileDown(profileId);
            LoadProfiles();
            if (index < lstProfiles.Items.Count - 1)
            {
                lstProfiles.Items[index + 1].Selected = true;
            }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            using var dialog = new SaveProfileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _profileManager.CaptureNewProfile(dialog.ProfileName);
                LoadProfiles();
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "viewsaver-profiles.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _profileManager.ExportProfiles(dialog.FileName);
                    MessageBox.Show("Profiles exported successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    int count = _profileManager.ImportProfiles(dialog.FileName);
                    LoadProfiles();
                    MessageBox.Show($"Imported {count} profile(s).", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        private string? ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using var form = new Form
            {
                Text = title,
                Size = new Size(400, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label { Text = prompt, Location = new Point(20, 20), Size = new Size(340, 20) };
            var textBox = new TextBox { Text = defaultValue, Location = new Point(20, 45), Size = new Size(340, 23) };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, 80), Size = new Size(75, 28) };
            var btnCancelDialog = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(285, 80), Size = new Size(75, 28) };

            form.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancelDialog });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancelDialog;

            textBox.SelectAll();

            return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
