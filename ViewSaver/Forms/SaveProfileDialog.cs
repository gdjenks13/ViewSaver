using System;
using System.Drawing;
using System.Windows.Forms;

namespace ViewSaver.Forms
{
    public partial class SaveProfileDialog : Form
    {
        public string ProfileName => txtName.Text.Trim();

        private TextBox txtName = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        public SaveProfileDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Save Profile";
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblName = new Label
            {
                Text = "Profile Name:",
                Location = new Point(20, 25),
                Size = new Size(100, 20)
            };
            Controls.Add(lblName);

            txtName = new TextBox
            {
                Location = new Point(120, 22),
                Size = new Size(240, 23),
                Text = $"Profile {DateTime.Now:yyyy-MM-dd HH:mm}"
            };
            txtName.TextChanged += TxtName_TextChanged;
            Controls.Add(txtName);

            btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(200, 70),
                Size = new Size(75, 28)
            };
            Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(285, 70),
                Size = new Size(75, 28)
            };
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Load += (s, e) =>
            {
                txtName.SelectAll();
                txtName.Focus();
            };
        }

        private void TxtName_TextChanged(object? sender, EventArgs e)
        {
            btnSave.Enabled = !string.IsNullOrWhiteSpace(txtName.Text);
        }
    }
}
