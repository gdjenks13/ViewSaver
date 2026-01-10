using System;
using System.Drawing;
using System.Windows.Forms;
using ViewSaver.Helpers;
using ViewSaver.Services;

namespace ViewSaver.Forms
{
    public class SettingsForm : Form
    {
        private readonly ProfileManager _profileManager;
        private CheckBox chkStartWithWindows = null!;
        private CheckBox chkShowNotifications = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public SettingsForm(ProfileManager profileManager)
        {
            _profileManager = profileManager;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "Settings - ViewSaver";
            Size = new Size(400, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = IconHelper.GetAppIcon();

            var grpStartup = new GroupBox
            {
                Text = "Startup",
                Location = new Point(15, 15),
                Size = new Size(355, 60)
            };
            Controls.Add(grpStartup);

            chkStartWithWindows = new CheckBox
            {
                Text = "Start ViewSaver with Windows",
                Location = new Point(15, 25),
                Size = new Size(320, 20),
                AutoSize = true
            };
            grpStartup.Controls.Add(chkStartWithWindows);

            var grpNotifications = new GroupBox
            {
                Text = "Notifications",
                Location = new Point(15, 85),
                Size = new Size(355, 60)
            };
            Controls.Add(grpNotifications);

            chkShowNotifications = new CheckBox
            {
                Text = "Show notification when profile is applied",
                Location = new Point(15, 25),
                Size = new Size(320, 20),
                AutoSize = true
            };
            grpNotifications.Controls.Add(chkShowNotifications);

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(210, 170),
                Size = new Size(75, 28)
            };
            btnOk.Click += BtnOk_Click;
            Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(295, 170),
                Size = new Size(75, 28)
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void LoadSettings()
        {
            chkStartWithWindows.Checked = _profileManager.Settings.StartWithWindows;
            chkShowNotifications.Checked = _profileManager.Settings.ShowNotifications;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            _profileManager.Settings.StartWithWindows = chkStartWithWindows.Checked;
            _profileManager.Settings.ShowNotifications = chkShowNotifications.Checked;
            _profileManager.SaveSettings();

            StartupManager.SetStartup(chkStartWithWindows.Checked);
        }
    }
}
