using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;

namespace YMixer
{
    public partial class FMain : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Timer refreshTimer;
        private FlowLayoutPanel processPanel;
        private static CoreAudioController audioController;

        private Dictionary<int, Panel> processPanels = new Dictionary<int, Panel>();

        private Timer trayMouseLeaveTimer;
        private bool isMouseOverTrayIcon = false;
        private Point lastMousePosition = Point.Empty;

        private static Dictionary<string, Image> processIconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<int, string> processExeCache = new Dictionary<int, string>();

        private Settings settings;

        private Panel globalVolumePanel;
        private Panel systemSoundsPanel;
        private IAudioSession systemSoundsSession;

        public FMain()
        {
            settings = Settings.Instance;

            this.Text = "YMixer";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(480, 240);
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.FormClosing += Form1_FormClosing;

            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 40, screen.Bottom - this.Height - 40);

            processPanel = new FlowLayoutPanel();
            processPanel.Dock = DockStyle.Fill;
            processPanel.AutoScroll = true;
            processPanel.WrapContents = false;
            processPanel.FlowDirection = FlowDirection.LeftToRight;
            processPanel.Padding = new Padding(10, 10, 10, 10);
            processPanel.SetDoubleBuffered(true);
            this.Controls.Add(processPanel);

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open Mixer", null, OnOpenMixer);
            trayMenu.Items.Add("Equalizer", null, OnEqualizer);
            trayMenu.Items.Add("Settings", null, OnSettings);
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Volume Mixer";
            trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            UpdateTrayInteraction();

            trayMouseLeaveTimer = new Timer();
            trayMouseLeaveTimer.Interval = 200;
            trayMouseLeaveTimer.Tick += TrayMouseLeaveTimer_Tick;

            this.Opacity = 0;
            this.ShowInTaskbar = false;
            this.Load += (s, e) =>
            {
                this.Hide();
                this.Opacity = 1;
                this.ShowInTaskbar = true;
                ApplyTheme();
            };

            refreshTimer = new Timer();
            refreshTimer.Interval = 1500;
            refreshTimer.Tick += (s, e) => RefreshAudioProcesses();

            audioController = new CoreAudioController();

            ApplyTheme();

            Settings.SettingsChanged += Settings_SettingsChanged;
        }
        public static CoreAudioController AudioController => audioController;

        private void UpdateTrayInteraction()
        {
            trayIcon.MouseMove -= TrayIcon_MouseMove;
            trayIcon.MouseClick -= TrayIcon_MouseClick;
            if (settings.OpenOnTrayHover)
            {
                trayIcon.MouseMove += TrayIcon_MouseMove;
            }
            else
            {
                trayIcon.MouseClick += TrayIcon_MouseClick;
            }
        }

        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            settings = Settings.Instance;
            UpdateTrayInteraction();
            ApplyTheme();
            this.Invalidate();
            this.Refresh();
            processPanel.Invalidate();
            processPanel.Refresh();
        }

        private void TrayIcon_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isMouseOverTrayIcon && !trayMenu.Visible)
            {
                isMouseOverTrayIcon = true;
                ShowMixer();
                trayMouseLeaveTimer.Start();
            }
            lastMousePosition = Cursor.Position;
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowMixer();
            }
        }

        private void TrayMouseLeaveTimer_Tick(object sender, EventArgs e)
        {
            Point cursorPos = Cursor.Position;
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            Rectangle taskbarArea = new Rectangle(workingArea.Right - 200, workingArea.Bottom - 40, 200, 40);

            if (!taskbarArea.Contains(cursorPos))
            {
                isMouseOverTrayIcon = false;
                trayMouseLeaveTimer.Stop();
            }
        }

        private void OnOpenMixer(object sender, EventArgs e)
        {
            ShowMixer();
        }

        private void OnEqualizer(object sender, EventArgs e)
        {
            var equalizerForm = new FEqualizer();
            var screen = Screen.PrimaryScreen.WorkingArea;
            var mainLocation = this.Location;
            var offset = 10;
            var newX = mainLocation.X - equalizerForm.Width - offset;
            newX = Math.Max(screen.Left, Math.Min(newX, screen.Right - equalizerForm.Width));
            equalizerForm.Location = new Point(newX, mainLocation.Y);
            equalizerForm.Show();
        }

        private void OnSettings(object sender, EventArgs e)
        {
            FSettings.ShowSettingsDialog(this);
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void ShowMixer()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 40, screen.Bottom - this.Height - 40);

            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            RefreshAudioProcesses();
            refreshTimer.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                refreshTimer.Stop();
            }
        }

        private void RefreshAudioProcesses()
        {
            var defaultDevice = audioController.DefaultPlaybackDevice;
            if (defaultDevice == null)
                return;

            var sessions = defaultDevice.SessionController.All().ToList();

            var oldPanels = new Dictionary<int, Panel>(processPanels);
            processPanels.Clear();

            List<Panel> newPanels = new List<Panel>();

            int scrollX = 0;
            try
            {
                scrollX = processPanel.HorizontalScroll.Value;
            }
            catch { scrollX = 0; }

            processPanel.SuspendLayout();
            processPanel.Controls.Clear();

            if (globalVolumePanel == null)
                globalVolumePanel = CreateGlobalVolumePanel(defaultDevice);

            processPanel.Controls.Add(globalVolumePanel);

            if (systemSoundsPanel == null)
                systemSoundsPanel = CreateSystemSoundsPanel(defaultDevice, sessions);

            processPanel.Controls.Add(systemSoundsPanel);

            if (sessions.Count == 0)
            {
                var infoPanel = new Panel
                {
                    Width = processPanel.Width - 20,
                    Height = processPanel.Height - 20,
                    BackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.White,
                    Margin = new Padding(0),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
                };

                var infoLabel = new Label
                {
                    Text = "No active audio sessions",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 10.5f, FontStyle.Regular),
                    ForeColor = settings.DarkTheme ? Color.Gray : Color.Gray,
                    BackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.White
                };

                infoPanel.Controls.Add(infoLabel);
                processPanel.Controls.Add(infoPanel);
                processPanel.ResumeLayout();
                return;
            }

            foreach (var session in sessions)
            {
                try
                {
                    if (IsSystemSoundsSession(session))
                        continue;

                    int processId = 0;
                    try
                    {
                        processId = GetProcessIdFromSession(session);
                    }
                    catch
                    {
                        continue;
                    }

                    if (processId == 0)
                        continue;

                    if (session.IsMuted && session.Volume == 0)
                        continue;

                    Process process = null;
                    try
                    {
                        process = Process.GetProcessById(processId);
                    }
                    catch
                    {
                        continue;
                    }

                    Panel panel;
                    if (oldPanels.ContainsKey(processId))
                    {
                        panel = oldPanels[processId];
                        oldPanels.Remove(processId);
                    }
                    else
                    {
                        panel = CreateProcessPanel(process, session);
                    }

                    var trackBar = panel.Controls.OfType<TrackBar>().FirstOrDefault();
                    var volumeLabel = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Tag as string == "VolumeLabel");
                    if (trackBar != null && volumeLabel != null)
                    {
                        int newVolume = (int)session.Volume;
                        if (trackBar.Value != newVolume)
                        {
                            trackBar.Value = Math.Max(trackBar.Minimum, Math.Min(trackBar.Maximum, newVolume));
                            volumeLabel.Text = $"{trackBar.Value}%";
                        }
                    }

                    panel.Tag = processId;

                    processPanels[processId] = panel;
                    newPanels.Add(panel);
                }
                catch
                {
                }
            }

            foreach (var kvp in oldPanels)
            {
                processPanel.Controls.Remove(kvp.Value);
            }

            for (int i = 0; i < newPanels.Count; i++)
            {
                var panel = newPanels[i];
                if (!processPanel.Controls.Contains(panel))
                {
                    processPanel.Controls.Add(panel);
                }
                if (processPanel.Controls.IndexOf(panel) != i + 2)
                {
                    processPanel.Controls.SetChildIndex(panel, i + 2);
                }
            }

            processPanel.ResumeLayout();

            try
            {
                processPanel.HorizontalScroll.Value = Math.Min(scrollX, processPanel.HorizontalScroll.Maximum);
                processPanel.PerformLayout();
            }
            catch { }
        }

        private Panel CreateGlobalVolumePanel(CoreAudioDevice device)
        {
            Color panelBackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
            Color labelForeColor = settings.DarkTheme ? Color.White : Color.Black;
            Color volumeLabelForeColor = settings.DarkTheme ? Color.LightGray : Color.Black;
            Color trackBarColor = settings.DarkTheme ? Color.FromArgb(80, 80, 80) : Color.White;

            var panel = new Panel
            {
                Width = 80,
                Height = 200,
                Margin = new Padding(6, 0, 6, 0),
                BackColor = panelBackColor,
                BorderStyle = BorderStyle.None
            };

            var label = new Label
            {
                Text = "All sounds",
                Location = new Point(0, 4),
                Width = panel.Width,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                ForeColor = labelForeColor,
                BackColor = panelBackColor
            };

            var icon = GetShell32Icon(15);
            var pictureBox = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 28,
                Height = 28,
                Location = new Point((panel.Width - 28) / 2, label.Location.Y + label.Height + 4),
                BackColor = panelBackColor
            };

            float volume = (float)device.Volume;

            int trackBarWidth = 28;
            int trackBarX = pictureBox.Location.X;
            int trackBarY = pictureBox.Location.Y + pictureBox.Height + 6;

            var trackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)Math.Max(0, Math.Min(100, volume)),
                TickStyle = TickStyle.None,
                Width = trackBarWidth,
                Height = 100,
                Orientation = Orientation.Vertical,
                Location = new Point(trackBarX, trackBarY),
                LargeChange = 10,
                SmallChange = 1,
                BackColor = trackBarColor
            };

            var volumeLabel = new Label
            {
                Text = $"{trackBar.Value}%",
                Location = new Point(0, trackBarY + trackBar.Height + 4),
                Width = panel.Width,
                Height = 16,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "VolumeLabel",
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular),
                ForeColor = volumeLabelForeColor,
                BackColor = panelBackColor
            };

            trackBar.Scroll += (s, e) =>
            {
                try
                {
                    device.Volume = trackBar.Value;
                    volumeLabel.Text = $"{trackBar.Value}%";
                }
                catch
                {
                }
            };

            panel.Controls.Add(label);
            panel.Controls.Add(pictureBox);
            panel.Controls.Add(trackBar);
            panel.Controls.Add(volumeLabel);

            trackBar.BringToFront();

            return panel;
        }

        private Panel CreateSystemSoundsPanel(CoreAudioDevice device, List<IAudioSession> sessions)
        {
            Color panelBackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
            Color labelForeColor = settings.DarkTheme ? Color.White : Color.Black;
            Color volumeLabelForeColor = settings.DarkTheme ? Color.LightGray : Color.Black;
            Color trackBarColor = settings.DarkTheme ? Color.FromArgb(80, 80, 80) : Color.White;

            var panel = new Panel
            {
                Width = 80,
                Height = 200,
                Margin = new Padding(6, 0, 6, 0),
                BackColor = panelBackColor,
                BorderStyle = BorderStyle.None
            };

            var label = new Label
            {
                Text = "System",
                Location = new Point(0, 4),
                Width = panel.Width,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Bold),
                ForeColor = labelForeColor,
                BackColor = panelBackColor
            };

            var icon = GetShell32Icon(15);
            var pictureBox = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 28,
                Height = 28,
                Location = new Point((panel.Width - 28) / 2, label.Location.Y + label.Height + 4),
                BackColor = panelBackColor
            };

            IAudioSession sysSession = null;
            foreach (var s in sessions)
            {
                if (IsSystemSoundsSession(s))
                {
                    sysSession = s;
                    break;
                }
            }
            systemSoundsSession = sysSession;

            float volume = sysSession != null ? (float)sysSession.Volume : 100;

            int trackBarWidth = 28;
            int trackBarX = pictureBox.Location.X;
            int trackBarY = pictureBox.Location.Y + pictureBox.Height + 6;

            var trackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)Math.Max(0, Math.Min(100, volume)),
                TickStyle = TickStyle.None,
                Width = trackBarWidth,
                Height = 100,
                Orientation = Orientation.Vertical,
                Location = new Point(trackBarX, trackBarY),
                LargeChange = 10,
                SmallChange = 1,
                BackColor = trackBarColor
            };

            var volumeLabel = new Label
            {
                Text = $"{trackBar.Value}%",
                Location = new Point(0, trackBarY + trackBar.Height + 4),
                Width = panel.Width,
                Height = 16,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "VolumeLabel",
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular),
                ForeColor = volumeLabelForeColor,
                BackColor = panelBackColor
            };

            trackBar.Scroll += (s, e) =>
            {
                try
                {
                    if (systemSoundsSession != null)
                    {
                        systemSoundsSession.Volume = trackBar.Value;
                        volumeLabel.Text = $"{trackBar.Value}%";
                    }
                }
                catch
                {
                }
            };

            panel.Controls.Add(label);
            panel.Controls.Add(pictureBox);
            panel.Controls.Add(trackBar);
            panel.Controls.Add(volumeLabel);

            trackBar.BringToFront();

            return panel;
        }

        private Image GetShell32Icon(int index)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                string shell32Path = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\SHELL32.dll");
                IntPtr[] largeIcons = new IntPtr[1];
                IntPtr[] smallIcons = new IntPtr[1];
                int count = ExtractIconEx(shell32Path, index, largeIcons, smallIcons, 1);
                if (count > 0 && largeIcons[0] != IntPtr.Zero)
                {
                    using (Icon ico = Icon.FromHandle(largeIcons[0]))
                    {
                        return (Image)ico.ToBitmap().Clone();
                    }
                }
            }
            catch { }
            finally
            {
                if (hIcon != IntPtr.Zero)
                    DestroyIcon(hIcon);
            }
            return SystemIcons.Application.ToBitmap();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private bool IsSystemSoundsSession(IAudioSession session)
        {
            try
            {
                if (session == null)
                    return false;
                if (session.ProcessId == 0)
                    return true;
                if (!string.IsNullOrEmpty(session.DisplayName) && session.DisplayName.ToLower().Contains("system sounds"))
                    return true;
            }
            catch { }
            return false;
        }

        private void SyncAllProcessVolumesToGlobal(int globalVolume)
        {
            var defaultDevice = audioController.DefaultPlaybackDevice;
            if (defaultDevice == null)
                return;

            var sessions = defaultDevice.SessionController.All().ToList();
            foreach (var session in sessions)
            {
                if (IsSystemSoundsSession(session))
                    continue;
                try
                {
                    if (session.Volume > globalVolume)
                        session.Volume = globalVolume;
                }
                catch { }
            }
        }

        private Panel CreateProcessPanel(Process process, IAudioSession session)
        {
            Color panelBackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
            Color labelForeColor = settings.DarkTheme ? Color.White : Color.Black;
            Color volumeLabelForeColor = settings.DarkTheme ? Color.LightGray : Color.Black;
            Color trackBarColor = settings.DarkTheme ? Color.FromArgb(80, 80, 80) : Color.White;

            var panel = new Panel
            {
                Width = 80,
                Height = 200,
                Margin = new Padding(6, 0, 6, 0),
                BackColor = panelBackColor,
                BorderStyle = BorderStyle.None
            };

            // Иконка процесса
            var icon = GetProcessIconAuto(process);
            var pictureBox = new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 32,
                Height = 32,
                BackColor = panelBackColor,
                Location = new Point((panel.Width - 32) / 2, 10)
            };

            // Название процесса под иконкой
            var label = new Label
            {
                Text = process.ProcessName,
                Width = panel.Width,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular),
                ForeColor = labelForeColor,
                BackColor = panelBackColor,
                Location = new Point(0, pictureBox.Bottom + 2)
            };

            float volume = (float)session.Volume;

            int trackBarWidth = 28;
            int trackBarHeight = 100;
            int trackBarX = (panel.Width - trackBarWidth) / 2;
            int trackBarY = label.Bottom + 8;

            var trackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)Math.Max(0, Math.Min(100, volume)),
                TickStyle = TickStyle.None,
                Width = trackBarWidth,
                Height = trackBarHeight,
                Orientation = Orientation.Vertical,
                Location = new Point(trackBarX, trackBarY),
                LargeChange = 10,
                SmallChange = 1,
                BackColor = trackBarColor
            };

            var volumeLabel = new Label
            {
                Text = $"{trackBar.Value}%",
                Location = new Point(0, trackBarY + trackBar.Height + 4),
                Width = panel.Width,
                Height = 16,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = "VolumeLabel",
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular),
                ForeColor = volumeLabelForeColor,
                BackColor = panelBackColor
            };

            trackBar.Scroll += (s, e) =>
            {
                try
                {
                    session.Volume = trackBar.Value;
                    volumeLabel.Text = $"{trackBar.Value}%";
                }
                catch
                {
                }
            };

            panel.Controls.Add(pictureBox);
            panel.Controls.Add(label);
            panel.Controls.Add(trackBar);
            panel.Controls.Add(volumeLabel);

            panel.Controls.SetChildIndex(pictureBox, 0);
            panel.Controls.SetChildIndex(label, 1);
            panel.Controls.SetChildIndex(trackBar, 2);
            panel.Controls.SetChildIndex(volumeLabel, 3);

            return panel;
        }

        private Image GetProcessIconAuto(Process process)
        {
            try
            {
                string exePath = GetMainExePathForProcess(process);

                if (string.IsNullOrEmpty(exePath))
                    return SystemIcons.Application.ToBitmap();

                if (processIconCache.TryGetValue(exePath, out var cachedIcon))
                    return cachedIcon;

                Icon ico = null;
                try
                {
                    ico = Icon.ExtractAssociatedIcon(exePath);
                }
                catch { }

                if (ico != null)
                {
                    var bmp = ico.ToBitmap();
                    processIconCache[exePath] = bmp;
                    return bmp;
                }
            }
            catch { }
            return SystemIcons.Application.ToBitmap();
        }

        private string GetMainExePathForProcess(Process process)
        {
            try
            {
                if (processExeCache.TryGetValue(process.Id, out var cachedExe))
                    return cachedExe;

                string[] mainProcessKeywords = { "chrome", "firefox", "msedge", "opera", "discord", "brave", "vivaldi", "yandex", "edgewebview" };

                string procName = process.ProcessName.ToLower();

                if (mainProcessKeywords.Any(k => procName.Contains(k)))
                {
                    var all = Process.GetProcessesByName(process.ProcessName);
                    if (all.Length > 0)
                    {
                        var mainProc = all.OrderBy(p =>
                        {
                            try { return p.StartTime; } catch { return DateTime.MaxValue; }
                        }).FirstOrDefault();
                        if (mainProc != null)
                        {
                            string mainExe = TryGetProcessExe(mainProc);
                            if (!string.IsNullOrEmpty(mainExe))
                            {
                                processExeCache[process.Id] = mainExe;
                                return mainExe;
                            }
                        }
                    }
                }

                Process current = process;
                int maxDepth = 5;
                while (maxDepth-- > 0)
                {
                    string exe = TryGetProcessExe(current);
                    if (!string.IsNullOrEmpty(exe))
                    {
                        processExeCache[process.Id] = exe;
                        return exe;
                    }

                    var parent = GetParentProcess(current);
                    if (parent == null || parent.Id == current.Id)
                        break;
                    current = parent;
                }

                string fallbackExe = process.ProcessName + ".exe";
                processExeCache[process.Id] = fallbackExe;
                return fallbackExe;
            }
            catch
            {
                return process.ProcessName + ".exe";
            }
        }

        private string TryGetProcessExe(Process process)
        {
            try
            {
                return process.MainModule != null ? process.MainModule.FileName : null;
            }
            catch
            {
                return null;
            }
        }

        private Process GetParentProcess(Process process)
        {
            try
            {
                int parentPid = 0;
                var handle = IntPtr.Zero;
                try
                {
                    handle = OpenProcess(ProcessAccessFlags.QueryInformation, false, process.Id);
                    if (handle == IntPtr.Zero)
                        return null;

                    PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                    int returnLength = 0;
                    int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), ref returnLength);
                    if (status == 0)
                    {
                        parentPid = pbi.InheritedFromUniqueProcessId.ToInt32();
                    }
                }
                finally
                {
                    if (handle != IntPtr.Zero)
                        CloseHandle(handle);
                }
                if (parentPid > 0)
                {
                    try
                    {
                        return Process.GetProcessById(parentPid);
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, ref int returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryInformation = 0x0400
        }

        private int GetProcessIdFromSession(IAudioSession session)
        {
            try
            {
                if (session == null)
                    return 0;
                return session.ProcessId;
            }
            catch
            {
                return 0;
            }
        }

        private void ApplyTheme()
        {
            Color formBackColor = settings.DarkTheme ? Color.FromArgb(32, 32, 32) : Color.White;
            Color panelBackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.White;
            Color foreColor = settings.DarkTheme ? Color.White : Color.Black;

            this.BackColor = formBackColor;
            processPanel.BackColor = panelBackColor;

            foreach (Control ctrl in processPanel.Controls)
            {
                if (ctrl is Panel p)
                {
                    p.BackColor = panelBackColor;
                    foreach (Control c in p.Controls)
                    {
                        if (c is Label lbl)
                        {
                            lbl.ForeColor = foreColor;
                            lbl.BackColor = panelBackColor;
                        }
                        if (c is TrackBar tb)
                        {
                            tb.BackColor = settings.DarkTheme ? Color.FromArgb(80, 80, 80) : Color.White;
                        }
                        if (c is PictureBox pb)
                        {
                            pb.BackColor = panelBackColor;
                        }
                    }
                }
            }
            this.Invalidate();
            this.Refresh();
            processPanel.Invalidate();
            processPanel.Refresh();
        }
    }

    public class Settings
    {
        private static Settings _instance;
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YMixer", "settings.json");

        public static event EventHandler SettingsChanged;

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        private bool _darkTheme = false;
        public bool DarkTheme
        {
            get { return _darkTheme; }
            set
            {
                if (_darkTheme != value)
                {
                    _darkTheme = value;
                    Save();
                    if (SettingsChanged != null)
                        SettingsChanged(this, EventArgs.Empty);
                }
            }
        }

        private bool _openOnTrayHover = false;
        public bool OpenOnTrayHover
        {
            get { return _openOnTrayHover; }
            set
            {
                if (_openOnTrayHover != value)
                {
                    _openOnTrayHover = value;
                    Save();
                    if (SettingsChanged != null)
                        SettingsChanged(this, EventArgs.Empty);
                }
            }
        }

        private bool _saveProfiles = false;
        public bool SaveProfiles
        {
            get { return _saveProfiles; }
            set
            {
                if (_saveProfiles != value)
                {
                    _saveProfiles = value;
                    Save();
                    if (SettingsChanged != null)
                        SettingsChanged(this, EventArgs.Empty);
                }
            }
        }

        private bool _showInTray = false;
        public bool ShowInTray
        {
            get { return _showInTray; }
            set
            {
                if (_showInTray != value)
                {
                    _showInTray = value;
                    Save();
                    if (SettingsChanged != null)
                        SettingsChanged(this, EventArgs.Empty);
                }
            }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var loaded = JsonSerializer.Deserialize<Settings>(json);
                    if (loaded != null)
                    {
                        _instance = loaded;
                        return _instance;
                    }
                }
            }
            catch
            {
            }
            _instance = new Settings();
            return _instance;
        }
    }

    public static class ControlExtensions
    {
        public static void SetDoubleBuffered(this Control control, bool enable)
        {
            var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (doubleBufferPropertyInfo != null)
            {
                doubleBufferPropertyInfo.SetValue(control, enable, null);
            }
        }
    }
}