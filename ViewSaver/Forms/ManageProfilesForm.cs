using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ViewSaver.Controls;
using ViewSaver.Helpers;
using ViewSaver.Models;
using ViewSaver.Services;

namespace ViewSaver.Forms
{
    public partial class ManageProfilesForm : Form
    {
        private readonly ProfileManager _profileManager;
        private readonly ToolTip _toolTip = new();
        private ListView lstProfiles = null!;
        private Button btnApply = null!;
        private Button btnEdit = null!;
        private Button btnRename = null!;
        private Button btnUpdate = null!;
        private Button btnHotkey = null!;
        private Button btnDelete = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;
        private Button btnExport = null!;
        private RichTextBox txtDetails = null!;
        private MonitorLayoutPreview preview = null!;
        private SplitContainer splitMain = null!;
        private SplitContainer splitDetails = null!;

        public ManageProfilesForm(ProfileManager profileManager)
        {
            _profileManager = profileManager;
            InitializeComponent();
            LoadProfiles();
        }

        private void InitializeComponent()
        {
            Text = "Manage Profiles - ViewSaver";
            Size = new Size(920, 700);
            MinimumSize = new Size(720, 560);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            Icon = IconHelper.GetAppIcon();
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(10);

            // ---- Left: profile list above a details/preview area; both splitters can be dragged ----
            lstProfiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false
            };
            lstProfiles.Columns.Add("Name", 220);
            lstProfiles.Columns.Add("Hotkey", 110);
            lstProfiles.Columns.Add("Monitors", 70);
            lstProfiles.Columns.Add("Modified", 100);
            lstProfiles.SelectedIndexChanged += LstProfiles_SelectedIndexChanged;
            lstProfiles.DoubleClick += BtnApply_Click;

            txtDetails = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                DetectUrls = false
            };

            preview = new MonitorLayoutPreview { Dock = DockStyle.Fill };

            splitDetails = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            splitDetails.Panel1.Controls.Add(txtDetails);
            splitDetails.Panel1.Controls.Add(new Label { Text = "Profile Details:", Dock = DockStyle.Top, Height = 20 });
            splitDetails.Panel2.Controls.Add(preview);
            splitDetails.Panel2.Controls.Add(new Label { Text = "Layout Preview:", Dock = DockStyle.Top, Height = 20 });

            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6
            };
            splitMain.Panel1.Controls.Add(lstProfiles);
            splitMain.Panel2.Controls.Add(splitDetails);
            splitMain.Panel2.Padding = new Padding(0, 4, 0, 0);

            // ---- Right: buttons ----
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Width = 170,
                Padding = new Padding(10, 0, 0, 0)
            };

            Button Add(string text, EventHandler onClick, string tooltip, bool gapBefore = false)
            {
                var b = new Button { Text = text, Size = new Size(145, 28), Margin = new Padding(0, gapBefore ? 12 : 2, 0, 2) };
                b.Click += onClick;
                _toolTip.SetToolTip(b, tooltip);
                buttons.Controls.Add(b);
                return b;
            }

            btnApply = Add("Apply Profile", BtnApply_Click, "Switch your displays to the selected profile.");
            btnEdit = Add("Edit Layout...", BtnEdit_Click, "Fine-tune monitor positions for the selected profile.");
            btnRename = Add("Rename...", BtnRename_Click, "Rename the selected profile.");
            btnUpdate = Add("Update Profile", BtnUpdate_Click, "Replace the selected profile with your current display setup.");
            btnHotkey = Add("Set Hotkey...", BtnHotkey_Click, "Assign a keyboard shortcut to the selected profile.");
            btnDelete = Add("Delete", BtnDelete_Click, "Delete the selected profile.");

            btnMoveUp = Add("Move Up", BtnMoveUp_Click, "Move the profile up in the tray menu.", gapBefore: true);
            btnMoveDown = Add("Move Down", BtnMoveDown_Click, "Move the profile down in the tray menu.");

            Add("Save Current...", BtnNew_Click, "Save your current display setup as a new profile.", gapBefore: true);
            Add("Display Settings...", (_, _) => ShellHelper.OpenDisplaySettings(),
                "Open Windows Settings > System > Display to arrange your monitors before saving.");

            btnExport = Add("Export Profile...", BtnExport_Click, "Save the selected profile to its own .json file.", gapBefore: true);
            Add("Import Profile...", BtnImport_Click, "Add profile(s) from .json files to your list.");
            Add("Backup All...", BtnBackup_Click, "Save all of your profiles to one backup file.", gapBefore: true);
            Add("Restore Backup...", BtnRestore_Click, "Replace or merge your profiles from a backup file.");

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            var btnClose = new Button { Text = "Close", Size = new Size(145, 30), DialogResult = DialogResult.OK, Margin = new Padding(0) };
            bottom.Controls.Add(btnClose);
            CancelButton = btnClose;

            Controls.Add(splitMain);
            Controls.Add(buttons);
            Controls.Add(bottom);

            Shown += (_, _) =>
            {
                splitMain.SplitterDistance = Math.Max(120, (int)(splitMain.Height * 0.42));
                splitDetails.SplitterDistance = Math.Max(150, (int)(splitDetails.Width * 0.45));
                SelectProfile(_profileManager.Settings.LastSelectedProfileId);
            };

            UpdateButtonStates();
        }

        private void SelectProfile(string? profileId)
        {
            foreach (ListViewItem item in lstProfiles.Items)
            {
                if (item.Tag as string == profileId)
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                }
            }
            if (lstProfiles.Items.Count > 0) lstProfiles.Items[0].Selected = true;
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
            btnEdit.Enabled = hasSelection;
            btnExport.Enabled = hasSelection;
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
                preview.Monitors = Array.Empty<MonitorConfiguration>();
                return;
            }

            preview.Monitors = profile.Monitors;

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

            foreach (var monitor in profile.Monitors.OrderBy(m => m.DisplayNumber))
            {
                string primary = monitor.IsPrimary ? " [Primary]" : "";
                string enabled = monitor.IsEnabled ? "" : " [Disabled]";
                txtDetails.SelectionFont = new Font(txtDetails.Font, FontStyle.Bold);
                txtDetails.AppendText($"  {monitor.DisplayNumber}. ");
                txtDetails.SelectionFont = txtDetails.Font;
                txtDetails.AppendText($"{monitor.FriendlyName}{primary}{enabled}\n");
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
                preview.Monitors = Array.Empty<MonitorConfiguration>();
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
                preview.Monitors = Array.Empty<MonitorConfiguration>();
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

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            var profile = profileId == null ? null : _profileManager.GetProfile(profileId);
            if (profile == null) return;

            using var dialog = new EditProfileDialog(_profileManager, profile);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadProfiles();
                SelectProfile(profileId);
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            var profileId = GetSelectedProfileId();
            var profile = profileId == null ? null : _profileManager.GetProfile(profileId);
            if (profile == null) return;

            string directory = GetExportDirectory();
            using var dialog = new SaveFileDialog
            {
                Title = $"Export Profile '{profile.Name}'",
                Filter = JsonFilter,
                DefaultExt = "json",
                InitialDirectory = directory,
                FileName = SuggestFileName(directory, profile.Name)
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _profileManager.ExportProfile(profile.Id, dialog.FileName);
                RememberExportDirectory(dialog.FileName);
                MessageBox.Show(this, $"Profile '{profile.Name}' exported to:\n{dialog.FileName}", "Export Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Export failed", ex);
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import Profile(s)",
                Filter = JsonFilter,
                DefaultExt = "json",
                InitialDirectory = GetExportDirectory(),
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            int total = 0;
            var errors = new List<string>();
            foreach (var file in dialog.FileNames)
            {
                try
                {
                    total += _profileManager.ImportProfiles(file);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Import of {file} failed", ex);
                    errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            RememberExportDirectory(dialog.FileNames[0]);
            LoadProfiles();
            if (total > 0 && lstProfiles.Items.Count > 0)
                lstProfiles.Items[^1].Selected = true;

            var message = $"Imported {total} profile(s) and added them to the end of your list.";
            if (errors.Count > 0) message += "\n\nSome files could not be imported:\n" + string.Join("\n", errors);
            MessageBox.Show(this, message, "Import Profile", MessageBoxButtons.OK,
                errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void BtnBackup_Click(object? sender, EventArgs e)
        {
            if (_profileManager.Profiles.Count == 0)
            {
                MessageBox.Show(this, "There are no profiles to back up.", "Backup All", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string directory = GetExportDirectory();
            using var dialog = new SaveFileDialog
            {
                Title = "Back Up All Profiles",
                Filter = JsonFilter,
                DefaultExt = "json",
                InitialDirectory = directory,
                FileName = SuggestFileName(directory, $"ViewSaver Backup {DateTime.Now:yyyy-MM-dd}")
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _profileManager.BackupProfiles(dialog.FileName);
                RememberExportDirectory(dialog.FileName);
                MessageBox.Show(this, $"All {_profileManager.Profiles.Count} profile(s) backed up to:\n{dialog.FileName}", "Backup All", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Backup failed", ex);
                MessageBox.Show(this, $"Backup failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestore_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Restore Profiles from Backup",
                Filter = JsonFilter,
                DefaultExt = "json",
                InitialDirectory = GetExportDirectory()
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var choice = _profileManager.Profiles.Count == 0
                ? DialogResult.Yes
                : MessageBox.Show(this,
                    "Replace your existing profiles with the backup?\n\n" +
                    "Yes = replace all current profiles\nNo = keep current profiles and add the backup's profiles\nCancel = do nothing",
                    "Restore Backup", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (choice == DialogResult.Cancel) return;

            try
            {
                int count = choice == DialogResult.Yes
                    ? _profileManager.RestoreProfiles(dialog.FileName)
                    : _profileManager.ImportProfiles(dialog.FileName);
                RememberExportDirectory(dialog.FileName);
                LoadProfiles();
                MessageBox.Show(this, $"Restored {count} profile(s).", "Restore Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Restore failed", ex);
                MessageBox.Show(this, $"Restore failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private const string JsonFilter = "ViewSaver profiles (*.json)|*.json|All files (*.*)|*.*";

        private string GetExportDirectory()
        {
            var dir = _profileManager.Settings.LastExportDirectory;
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                ? dir
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void RememberExportDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir) || dir == _profileManager.Settings.LastExportDirectory) return;
            _profileManager.Settings.LastExportDirectory = dir;
            _profileManager.SaveSettings();
        }

        /// <summary>"Main Office.json", or "Main Office (rev 2).json" etc. if that file already exists.</summary>
        private static string SuggestFileName(string directory, string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(safe)) safe = "ViewSaver Profile";

            var candidate = $"{safe}.json";
            for (int rev = 2; File.Exists(Path.Combine(directory, candidate)); rev++)
                candidate = $"{safe} (rev {rev}).json";
            return candidate;
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
