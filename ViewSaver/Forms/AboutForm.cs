using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ViewSaver.Helpers;

namespace ViewSaver.Forms
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About ViewSaver";
            Icon = IconHelper.GetAppIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };

            var logo = new PictureBox
            {
                Image = new Icon(IconHelper.GetAppIcon(), 48, 48).ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 16, 0)
            };
            layout.Controls.Add(logo, 0, 0);
            layout.SetRowSpan(logo, 4);

            string version = (typeof(AboutForm).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?").Split('+')[0];
            layout.Controls.Add(new Label
            {
                Text = "ViewSaver - Monitor Profile Manager",
                Font = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold),
                AutoSize = true
            }, 1, 0);
            layout.Controls.Add(new Label { Text = $"Version {version}", AutoSize = true, Margin = new Padding(3, 4, 3, 8) }, 1, 1);
            layout.Controls.Add(new Label
            {
                Text = "Save and quickly switch between different monitor configurations.",
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                Margin = new Padding(3, 0, 3, 8)
            }, 1, 2);

            var link = new LinkLabel { Text = ShellHelper.RepositoryUrl, AutoSize = true };
            link.LinkClicked += (_, _) => ShellHelper.OpenRepository();
            layout.Controls.Add(link, 1, 3);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(85, 28), Anchor = AnchorStyles.Right, Margin = new Padding(3, 16, 0, 0) };
            layout.Controls.Add(ok, 1, 4);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = ok;
        }
    }
}
