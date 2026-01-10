using System;
using System.Drawing;
using System.Windows.Forms;
using ViewSaver.Helpers;
using ViewSaver.Models;

namespace ViewSaver.Forms
{
    public class HotkeyDialog : Form
    {
        public HotkeyBinding? Hotkey { get; private set; }

        private CheckBox chkCtrl = null!;
        private CheckBox chkAlt = null!;
        private CheckBox chkShift = null!;
        private CheckBox chkWin = null!;
        private ComboBox cmbKey = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Button btnClear = null!;
        private Label lblPreview = null!;

        public HotkeyDialog(HotkeyBinding? currentHotkey = null)
        {
            Hotkey = currentHotkey;
            InitializeComponent();
            LoadCurrentHotkey();
        }

        private void InitializeComponent()
        {
            Text = "Set Hotkey";
            Size = new Size(350, 220);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = IconHelper.GetAppIcon();

            var grpModifiers = new GroupBox
            {
                Text = "Modifiers",
                Location = new Point(15, 15),
                Size = new Size(200, 55)
            };
            Controls.Add(grpModifiers);

            chkCtrl = new CheckBox
            {
                Text = "Ctrl",
                Location = new Point(10, 22),
                Size = new Size(50, 20),
                AutoSize = true
            };
            chkCtrl.CheckedChanged += OnHotkeyChanged;
            grpModifiers.Controls.Add(chkCtrl);

            chkAlt = new CheckBox
            {
                Text = "Alt",
                Location = new Point(60, 22),
                Size = new Size(45, 20),
                AutoSize = true
            };
            chkAlt.CheckedChanged += OnHotkeyChanged;
            grpModifiers.Controls.Add(chkAlt);

            chkShift = new CheckBox
            {
                Text = "Shift",
                Location = new Point(105, 22),
                Size = new Size(50, 20),
                AutoSize = true
            };
            chkShift.CheckedChanged += OnHotkeyChanged;
            grpModifiers.Controls.Add(chkShift);

            chkWin = new CheckBox
            {
                Text = "Win",
                Location = new Point(155, 22),
                Size = new Size(50, 20),
                AutoSize = true
            };
            chkWin.CheckedChanged += OnHotkeyChanged;
            grpModifiers.Controls.Add(chkWin);

            var lblKey = new Label
            {
                Text = "Key:",
                Location = new Point(15, 85),
                Size = new Size(40, 20)
            };
            Controls.Add(lblKey);

            cmbKey = new ComboBox
            {
                Location = new Point(60, 82),
                Size = new Size(155, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbKey.SelectedIndexChanged += OnHotkeyChanged;
            PopulateKeys();
            Controls.Add(cmbKey);

            lblPreview = new Label
            {
                Text = "Hotkey: None",
                Location = new Point(15, 120),
                Size = new Size(300, 20),
                Font = new Font(Font, FontStyle.Bold)
            };
            Controls.Add(lblPreview);

            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(15, 150),
                Size = new Size(75, 28)
            };
            btnClear.Click += BtnClear_Click;
            Controls.Add(btnClear);

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(165, 150),
                Size = new Size(75, 28)
            };
            btnOk.Click += BtnOk_Click;
            Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(250, 150),
                Size = new Size(75, 28)
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void PopulateKeys()
        {
            cmbKey.Items.Add("(None)");

            for (int i = 1; i <= 12; i++)
            {
                cmbKey.Items.Add($"F{i}");
            }

            for (int i = 0; i <= 9; i++)
            {
                cmbKey.Items.Add($"D{i}");
            }

            for (char c = 'A'; c <= 'Z'; c++)
            {
                cmbKey.Items.Add(c.ToString());
            }

            for (int i = 0; i <= 9; i++)
            {
                cmbKey.Items.Add($"NumPad{i}");
            }

            cmbKey.Items.Add("Space");
            cmbKey.Items.Add("Tab");
            cmbKey.Items.Add("Enter");
            cmbKey.Items.Add("Escape");
            cmbKey.Items.Add("Insert");
            cmbKey.Items.Add("Delete");
            cmbKey.Items.Add("Home");
            cmbKey.Items.Add("End");
            cmbKey.Items.Add("PageUp");
            cmbKey.Items.Add("PageDown");
            cmbKey.Items.Add("Up");
            cmbKey.Items.Add("Down");
            cmbKey.Items.Add("Left");
            cmbKey.Items.Add("Right");

            cmbKey.SelectedIndex = 0;
        }

        private void LoadCurrentHotkey()
        {
            if (Hotkey != null)
            {
                chkCtrl.Checked = Hotkey.Control;
                chkAlt.Checked = Hotkey.Alt;
                chkShift.Checked = Hotkey.Shift;
                chkWin.Checked = Hotkey.Win;

                string keyName = Hotkey.Key.ToString();
                for (int i = 0; i < cmbKey.Items.Count; i++)
                {
                    if (cmbKey.Items[i]?.ToString() == keyName)
                    {
                        cmbKey.SelectedIndex = i;
                        break;
                    }
                }
            }
            UpdatePreview();
        }

        private void OnHotkeyChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var hotkey = BuildHotkey();
            if (hotkey != null && hotkey.IsValid)
            {
                lblPreview.Text = $"Hotkey: {hotkey}";
                btnOk.Enabled = true;
            }
            else
            {
                lblPreview.Text = "Hotkey: None";
                btnOk.Enabled = true; // Allow clearing
            }
        }

        private HotkeyBinding? BuildHotkey()
        {
            if (cmbKey.SelectedIndex <= 0) return null;

            string keyName = cmbKey.SelectedItem?.ToString() ?? "";
            if (!Enum.TryParse<Keys>(keyName, out var key)) return null;

            return new HotkeyBinding
            {
                Key = key,
                Control = chkCtrl.Checked,
                Alt = chkAlt.Checked,
                Shift = chkShift.Checked,
                Win = chkWin.Checked
            };
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            chkCtrl.Checked = false;
            chkAlt.Checked = false;
            chkShift.Checked = false;
            chkWin.Checked = false;
            cmbKey.SelectedIndex = 0;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            Hotkey = BuildHotkey();
        }
    }
}
