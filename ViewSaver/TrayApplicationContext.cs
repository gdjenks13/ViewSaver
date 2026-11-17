using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ViewSaver.Helpers;
using ViewSaver.Models;
using ViewSaver.Native;
using ViewSaver.Services;

namespace ViewSaver
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ProfileManager _profileManager;
        private readonly HiddenForm _hiddenForm;
        private HotkeyManager? _hotkeyManager;
        private bool _isApplying;

        public TrayApplicationContext()
        {
            _profileManager = new ProfileManager();
            _profileManager.ProfilesChanged += OnProfilesChanged;

            _hiddenForm = new HiddenForm();
            _hiddenForm.HotkeyMessage += OnHotkeyMessage;
            _hiddenForm.Show();
            _hiddenForm.Hide();

            _hotkeyManager = new HotkeyManager(_hiddenForm.Handle);
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            _contextMenu = new ContextMenuStrip();
            BuildContextMenu();

            _trayIcon = new NotifyIcon
            {
                Icon = IconHelper.GetAppIcon(),
                Text = "ViewSaver - Monitor Profile Manager",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += OnTrayIconDoubleClick;

            RegisterAllHotkeys();
            ApplyStartupSetting();
        }

        private void BuildContextMenu()
        {
            _contextMenu.Items.Clear();

            if (_profileManager.Profiles.Any())
            {
                foreach (var profile in _profileManager.Profiles)
                {
                    var menuItem = new ToolStripMenuItem(profile.Name)
                    {
                        Tag = profile.Id
                    };

                    if (profile.Hotkey != null && profile.Hotkey.IsValid)
                    {
                        menuItem.ShortcutKeyDisplayString = profile.Hotkey.ToString();
                    }

                    menuItem.Click += OnProfileMenuItemClick;
                    _contextMenu.Items.Add(menuItem);
                }

                _contextMenu.Items.Add(new ToolStripSeparator());
            }

            var saveItem = new ToolStripMenuItem("Save Current Configuration...");
            saveItem.Click += OnSaveCurrentClick;
            _contextMenu.Items.Add(saveItem);

            var manageItem = new ToolStripMenuItem("Manage Profiles...");
            manageItem.Click += OnManageProfilesClick;
            _contextMenu.Items.Add(manageItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += OnSettingsClick;
            _contextMenu.Items.Add(settingsItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About ViewSaver");
            aboutItem.Click += OnAboutClick;
            _contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExitClick;
            _contextMenu.Items.Add(exitItem);
        }

        private void RegisterAllHotkeys()
        {
            _hotkeyManager?.UnregisterAllHotkeys();

            foreach (var profile in _profileManager.GetProfilesWithHotkeys())
            {
                if (profile.Hotkey != null)
                {
                    try
                    {
                        _hotkeyManager?.RegisterHotkey(profile.Hotkey);
                    }
                    catch (Exception ex)
                    {
                        ShowBalloonTip("Hotkey Error", 
                            $"Failed to register hotkey {profile.Hotkey} for profile '{profile.Name}': {ex.Message}",
                            ToolTipIcon.Warning);
                    }
                }
            }
        }

        private void ApplyStartupSetting()
        {
            if (_profileManager.Settings.StartWithWindows)
            {
                StartupManager.EnableStartup();
            }
        }

        #region Event Handlers

        private void OnProfilesChanged(object? sender, EventArgs e)
        {
            if (_contextMenu.IsHandleCreated)
            {
                _contextMenu.Invoke((Action)BuildContextMenu);
            }
            else
            {
                BuildContextMenu();
            }
            RegisterAllHotkeys();
        }

        private void OnHotkeyMessage(object? sender, Message m)
        {
            _hotkeyManager?.ProcessHotkey(m);
        }

        private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        {
            var profile = _profileManager.GetProfileByHotkey(e.Hotkey);
            if (profile != null)
            {
                ApplyProfileWithNotification(profile.Id);
            }
        }

        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            ShowManageProfilesForm();
        }

        private void OnProfileMenuItemClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string profileId)
            {
                ApplyProfileWithNotification(profileId);
            }
        }

        private void OnSaveCurrentClick(object? sender, EventArgs e)
        {
            ShowSaveProfileDialog();
        }

        private void OnManageProfilesClick(object? sender, EventArgs e)
        {
            ShowManageProfilesForm();
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            ShowSettingsForm();
        }

        private void OnAboutClick(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "ViewSaver - Monitor Profile Manager\n\n" +
                $"Version {Application.ProductVersion.Split('+')[0]}\n\n" +
                "Save and quickly switch between different monitor configurations.",
                "About ViewSaver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            ExitApplication();
        }

        #endregion

        #region Helper Methods

        private void ApplyProfileWithNotification(string profileId)
        {
            // Defer until the tray menu has fully closed and released mouse capture; changing the
            // display topology while the menu is still tearing down is unreliable.
            if (_hiddenForm.IsHandleCreated)
            {
                _hiddenForm.BeginInvoke(() => ApplyProfileNow(profileId));
            }
            else
            {
                ApplyProfileNow(profileId);
            }
        }

        private void ApplyProfileNow(string profileId)
        {
            if (_isApplying) return; // ignore double-clicks / repeated hotkeys while switching
            var profile = _profileManager.GetProfile(profileId);
            if (profile == null) return;

            _isApplying = true;
            try
            {
                var result = _profileManager.ApplyProfile(profileId);

                if (!result.Success)
                {
                    // Always surface failures, even with notifications off, so they never fail silently.
                    ShowBalloonTip("Profile Error", $"Failed to apply '{profile.Name}': {result.Message}", ToolTipIcon.Error);
                }
                else if (_profileManager.Settings.ShowNotifications)
                {
                    var text = $"Display configuration '{profile.Name}' has been applied.";
                    if (!string.IsNullOrEmpty(result.Message)) text += " " + result.Message;
                    ShowBalloonTip("Profile Applied", text, ToolTipIcon.Info);
                }
            }
            finally
            {
                _isApplying = false;
            }
        }

        private void ShowSaveProfileDialog()
        {
            using var dialog = new Forms.SaveProfileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var profile = _profileManager.CaptureNewProfile(dialog.ProfileName);
                    ShowBalloonTip("Profile Saved", $"Profile '{profile.Name}' has been saved.", ToolTipIcon.Info);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save profile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowManageProfilesForm()
        {
            using var form = new Forms.ManageProfilesForm(_profileManager);
            form.ShowDialog();
        }

        private void ShowSettingsForm()
        {
            using var form = new Forms.SettingsForm(_profileManager);
            form.ShowDialog();
            RegisterAllHotkeys(); // Re-register in case hotkeys changed
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            // Windows truncates balloon text beyond 255 characters.
            if (text.Length > 255) text = text[..252] + "...";
            _trayIcon.ShowBalloonTip(5000, title, text, icon);
        }

        private void ExitApplication()
        {
            _trayIcon.Visible = false;
            _hotkeyManager?.Dispose();
            _hiddenForm.Close();
            Application.Exit();
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayIcon.Dispose();
                _contextMenu.Dispose();
                _hotkeyManager?.Dispose();
                _hiddenForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal class HiddenForm : Form
    {
        public event EventHandler<Message>? HotkeyMessage;

        public HiddenForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(0, 0);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-1000, -1000);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                HotkeyMessage?.Invoke(this, m);
            }
            base.WndProc(ref m);
        }
    }
}
