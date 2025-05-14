using System;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Drawing;

namespace YMixer
{
    public partial class FSettings : Form
    {
        private CheckBox chkDarkTheme;
        private CheckBox chkOpenOnHover;
        private CheckBox chkSaveProfiles;
        private CheckBox chkShowInTray;
        private Button btnSetAutostart;
        private Button btnRemoveAutostart;
        private Label lblAutostart;

        private const string AUTOSTART_REG_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "YMixer";

        private Settings settings;

        private static FSettings _instance;

        public static DialogResult ShowSettingsDialog(IWin32Window owner)
        {
            if (_instance != null)
            {
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;
                _instance.BringToFront();
                _instance.Focus();
                return DialogResult.None;
            }

            var form = new FSettings();
            _instance = form;
            try
            {
                return owner != null ? form.ShowDialog(owner) : form.ShowDialog();
            }
            finally
            {
                _instance = null;
                form.Dispose();
            }
        }

        public FSettings()
        {
            settings = Settings.Instance;
            InitializeSettingsUI();
            LoadSettingsToUI();
            ApplyTheme(settings.DarkTheme);
            Settings.SettingsChanged += Settings_SettingsChanged;
        }

        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            settings = Settings.Instance;
            ApplyTheme(settings.DarkTheme);
            LoadSettingsToUI();
        }

        private void InitializeSettingsUI()
        {
            this.Text = "Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new System.Drawing.Size(340, 260);
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;
            int spacing = 36;

            chkDarkTheme = new CheckBox
            {
                Text = "Dark theme",
                AutoSize = true,
                Location = new System.Drawing.Point(24, y)
            };
            chkDarkTheme.CheckedChanged += ChkDarkTheme_CheckedChanged;
            this.Controls.Add(chkDarkTheme);

            y += spacing;
            chkOpenOnHover = new CheckBox
            {
                Text = "Open on tray hover",
                AutoSize = true,
                Location = new System.Drawing.Point(24, y)
            };
            this.Controls.Add(chkOpenOnHover);

            y += spacing;
            lblAutostart = new Label
            {
                Text = "Autostart:",
                AutoSize = true,
                Location = new System.Drawing.Point(24, y + 5)
            };
            this.Controls.Add(lblAutostart);

            btnSetAutostart = new Button
            {
                Text = "Enable",
                Width = 80,
                Location = new System.Drawing.Point(120, y)
            };
            btnSetAutostart.Click += BtnSetAutostart_Click;
            this.Controls.Add(btnSetAutostart);

            btnRemoveAutostart = new Button
            {
                Text = "Disable",
                Width = 80,
                Location = new System.Drawing.Point(210, y)
            };
            btnRemoveAutostart.Click += BtnRemoveAutostart_Click;
            this.Controls.Add(btnRemoveAutostart);

            y += spacing;
            chkSaveProfiles = new CheckBox
            {
                Text = "Save mixer profiles",
                AutoSize = true,
                Location = new System.Drawing.Point(24, y)
            };
            this.Controls.Add(chkSaveProfiles);

            y += spacing;
            chkShowInTray = new CheckBox
            {
                Text = "Show in tray (not in hidden icons)",
                AutoSize = true,
                Location = new System.Drawing.Point(24, y)
            };
            this.Controls.Add(chkShowInTray);

            var btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80,
                Location = new System.Drawing.Point(80, this.ClientSize.Height - 40)
            };
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Location = new System.Drawing.Point(180, this.ClientSize.Height - 40)
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadSettingsToUI()
        {
            chkDarkTheme.Checked = settings.DarkTheme;
            chkOpenOnHover.Checked = settings.OpenOnTrayHover;
            chkSaveProfiles.Checked = settings.SaveProfiles;
            chkShowInTray.Checked = settings.ShowInTray;
        }

        private void SaveUIToSettings()
        {
            bool needRestart = false;
            bool prevDarkTheme = settings.DarkTheme;
            bool prevShowInTray = settings.ShowInTray;

            settings.DarkTheme = chkDarkTheme.Checked;
            settings.OpenOnTrayHover = chkOpenOnHover.Checked;
            settings.SaveProfiles = chkSaveProfiles.Checked;
            settings.ShowInTray = chkShowInTray.Checked;

            if (prevDarkTheme != settings.DarkTheme || prevShowInTray != settings.ShowInTray)
                needRestart = true;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SaveUIToSettings();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnSetAutostart_Click(object sender, EventArgs e)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AUTOSTART_REG_PATH, true))
                {
                    if (key == null)
                        throw new Exception("Failed to open autostart registry key.");

                    key.SetValue(APP_NAME, $"\"{exePath}\"");
                }
                MessageBox.Show("Autostart enabled successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error enabling autostart:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemoveAutostart_Click(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AUTOSTART_REG_PATH, true))
                {
                    if (key == null)
                        throw new Exception("Failed to open autostart registry key.");

                    if (key.GetValue(APP_NAME) != null)
                    {
                        key.DeleteValue(APP_NAME);
                        MessageBox.Show("Autostart disabled successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Autostart is already disabled.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error disabling autostart:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyTheme(bool dark)
        {
            if (dark)
            {
                this.BackColor = Color.FromArgb(32, 32, 32);
                this.ForeColor = Color.White;
                foreach (Control ctrl in this.Controls)
                {
                    ApplyDarkThemeToControl(ctrl);
                }
            }
            else
            {
                this.BackColor = SystemColors.Control;
                this.ForeColor = SystemColors.ControlText;
                foreach (Control ctrl in this.Controls)
                {
                    ApplyLightThemeToControl(ctrl);
                }
            }
        }

        private void ApplyDarkThemeToControl(Control ctrl)
        {
            if (ctrl is Button)
            {
                ctrl.BackColor = Color.FromArgb(64, 64, 64);
                ctrl.ForeColor = Color.White;
                ((Button)ctrl).FlatStyle = FlatStyle.Flat;
            }
            else if (ctrl is CheckBox)
            {
                ctrl.BackColor = Color.FromArgb(32, 32, 32);
                ctrl.ForeColor = Color.White;
            }
            else if (ctrl is Label)
            {
                ctrl.BackColor = Color.FromArgb(32, 32, 32);
                ctrl.ForeColor = Color.White;
            }
            else
            {
                ctrl.BackColor = Color.FromArgb(32, 32, 32);
                ctrl.ForeColor = Color.White;
            }

            foreach (Control child in ctrl.Controls)
                ApplyDarkThemeToControl(child);
        }

        private void ApplyLightThemeToControl(Control ctrl)
        {
            ctrl.BackColor = SystemColors.Control;
            ctrl.ForeColor = SystemColors.ControlText;

            if (ctrl is Button)
                ((Button)ctrl).FlatStyle = FlatStyle.Standard;

            foreach (Control child in ctrl.Controls)
                ApplyLightThemeToControl(child);
        }

        private void ChkDarkTheme_CheckedChanged(object sender, EventArgs e)
        {
            ApplyTheme(chkDarkTheme.Checked);
        }
    }
}