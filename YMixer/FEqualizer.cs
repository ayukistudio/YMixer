using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Dsp;
using NAudio.Wave.SampleProviders;

namespace YMixer
{
    public partial class FEqualizer : Form
    {
        private Settings settings;
        private TrackBar[] bandSliders;
        private Label[] bandLabels;
        private Label[] gainLabels;
        private ComboBox processSelector;
        private CheckBox globalModeCheckBox;
        private TextBox presetNameTextBox;
        private Button savePresetButton;
        private Button loadPresetButton;
        private ComboBox presetSelector;
        private TrackBar volumeBoostSlider;
        private Label volumeBoostLabel;
        private TrackBar bassBoostSlider;
        private Label bassBoostLabel;
        private readonly string[] frequencies = { "32Hz", "64Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz" };
        private readonly float[] frequencyValues = { 32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
        private readonly int bandCount = 10;
        private readonly string presetFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YMixer", "equalizer_presets.json");

        private Dictionary<string, EqualizerPreset> presets;
        private string lastAppliedPresetName;
        private IWaveIn waveIn;
        private BufferedWaveProvider waveProvider;
        private WaveToSampleProvider waveToSampleProvider;
        private WaveOutEvent waveOut;
        private Equalizer equalizer;
        private float volumeBoost = 1.0f;
        private float bassBoost = 0.0f;
        private bool usingFallback = false;

        public FEqualizer()
        {
            settings = Settings.Instance;
            presets = LoadPresets();
            Int();
            InitializeAudioProcessing();
            ApplyTheme();
            Settings.SettingsChanged += Settings_SettingsChanged;

            if (settings.SaveProfiles && !string.IsNullOrEmpty(lastAppliedPresetName))
            {
                LoadPreset(lastAppliedPresetName);
            }
        }

        private void InitializeAudioProcessing()
        {
            try
            {
                waveIn = new WasapiLoopbackCapture();
                WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                waveProvider = new BufferedWaveProvider(waveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(50)
                };
                waveToSampleProvider = new WaveToSampleProvider(waveProvider);
                equalizer = new Equalizer(waveToSampleProvider);
                waveOut = new WaveOutEvent();
                waveOut.Init(equalizer);
                waveIn.DataAvailable += (s, e) =>
                {
                    if (waveProvider != null)
                    {
                        waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        ApplyEqualizerSettings();
                    }
                };
                waveIn.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                    {
                        MessageBox.Show($"Recording stopped due to error: {e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                waveIn.WaveFormat = waveFormat;
                waveIn.StartRecording();
                waveOut.Play();
                usingFallback = false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to capture system audio: {ex.Message}\n\n" +
                                      "This is likely due to audio driver issues or permissions.\n" +
                                      "Troubleshooting steps:\n" +
                                      "1. Run the application as Administrator.\n" +
                                      "2. Update your audio drivers.\n" +
                                      "3. Ensure no other application is exclusively using the audio device.\n" +
                                      "4. Restart Windows Audio service (services.msc).\n\n" +
                                      "Falling back to microphone input as a temporary solution.";
                MessageBox.Show(errorMessage, "System Audio Capture Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                try
                {
                    for (int deviceId = 0; deviceId < WaveIn.DeviceCount; deviceId++)
                    {
                        var capabilities = WaveIn.GetCapabilities(deviceId);
                        if (capabilities.Channels > 0)
                        {
                            waveIn = null;
                            waveIn = new WaveIn
                            {
                                DeviceNumber = deviceId,
                                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, capabilities.Channels)
                            };
                            waveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
                            {
                                DiscardOnBufferOverflow = true,
                                BufferDuration = TimeSpan.FromMilliseconds(50)
                            };
                            waveToSampleProvider = new WaveToSampleProvider(waveProvider);
                            equalizer = new Equalizer(waveToSampleProvider);
                            waveOut = new WaveOutEvent();
                            waveOut.Init(equalizer);
                            waveIn.DataAvailable += (s, e) =>
                            {
                                if (waveProvider != null)
                                {
                                    waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                                    ApplyEqualizerSettings();
                                }
                            };
                            waveIn.RecordingStopped += (s, e) =>
                            {
                                if (e.Exception != null)
                                {
                                    MessageBox.Show($"Recording stopped due to error: {e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            };
                            waveIn.StartRecording();
                            waveOut.Play();
                            usingFallback = true;
                            MessageBox.Show($"Switched to microphone input (Device: {capabilities.ProductName}).\n\n" +
                                            "The equalizer will process audio from your microphone instead of system audio. " +
                                            "To equalize system audio (e.g., music), resolve the system audio capture issue using the steps provided earlier.",
                                            "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }
                    MessageBox.Show("No suitable audio input devices found. Please connect a microphone or check your audio settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Fallback to microphone input failed: {ex2.Message}\n\n" +
                                    "The equalizer cannot function without an audio input. Please ensure a microphone is connected or fix the system audio capture issue.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Int()
        {
            this.Text = "Equalizer";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(650, 340);
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.FormClosing += (s, e) => CleanupAudio();

            bandSliders = new TrackBar[bandCount];
            bandLabels = new Label[bandCount];
            gainLabels = new Label[bandCount];

            int x = 20;
            int spacing = 50;
            for (int i = 0; i < bandCount; i++)
            {
                int bandIndex = i;
                bandLabels[i] = new Label
                {
                    Text = frequencies[i],
                    Location = new Point(x, 20),
                    Width = 50,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
                };
                this.Controls.Add(bandLabels[i]);

                bandSliders[i] = new TrackBar
                {
                    Minimum = -24,
                    Maximum = 24,
                    Value = 0,
                    TickStyle = TickStyle.Both,
                    Orientation = Orientation.Vertical,
                    Width = 30,
                    Height = 150,
                    Location = new Point(x + 10, 40),
                    LargeChange = 4,
                    SmallChange = 2
                };
                bandSliders[i].ValueChanged += (s, e) => UpdateGainLabel(bandIndex);
                this.Controls.Add(bandSliders[i]);

                gainLabels[i] = new Label
                {
                    Text = "0 dB",
                    Location = new Point(x, bandSliders[i].Bottom + 5),
                    Width = 50,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
                };
                this.Controls.Add(gainLabels[i]);

                x += spacing;
            }

            var volumeBoostLabelText = new Label
            {
                Text = "Volume Boost",
                Location = new Point(x + 10, 20),
                Width = 80,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
            };
            this.Controls.Add(volumeBoostLabelText);

            volumeBoostSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 300,
                Value = 100,
                TickStyle = TickStyle.Both,
                Orientation = Orientation.Vertical,
                Width = 30,
                Height = 150,
                Location = new Point(x + 20, 40),
                LargeChange = 20,
                SmallChange = 10
            };
            volumeBoostSlider.ValueChanged += (s, e) =>
            {
                volumeBoost = volumeBoostSlider.Value / 100f;
                volumeBoostLabel.Text = $"{volumeBoostSlider.Value}%";
                ApplyEqualizerSettings();
            };
            this.Controls.Add(volumeBoostSlider);

            volumeBoostLabel = new Label
            {
                Text = "100%",
                Location = new Point(x + 10, volumeBoostSlider.Bottom + 5),
                Width = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
            };
            this.Controls.Add(volumeBoostLabel);

            x += spacing;

            var bassBoostLabelText = new Label
            {
                Text = "Bass Boost",
                Location = new Point(x + 10, 20),
                Width = 80,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
            };
            this.Controls.Add(bassBoostLabelText);

            bassBoostSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 24,
                Value = 0,
                TickStyle = TickStyle.Both,
                Orientation = Orientation.Vertical,
                Width = 30,
                Height = 150,
                Location = new Point(x + 20, 40),
                LargeChange = 4,
                SmallChange = 2
            };
            bassBoostSlider.ValueChanged += (s, e) =>
            {
                bassBoost = bassBoostSlider.Value;
                bassBoostLabel.Text = $"{bassBoostSlider.Value} dB";
                ApplyEqualizerSettings();
            };
            this.Controls.Add(bassBoostSlider);

            bassBoostLabel = new Label
            {
                Text = "0 dB",
                Location = new Point(x + 10, bassBoostSlider.Bottom + 5),
                Width = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Regular)
            };
            this.Controls.Add(bassBoostLabel);

            var processLabel = new Label
            {
                Text = "Process:",
                Location = new Point(20, bandSliders[0].Bottom + 40),
                Width = 60,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            this.Controls.Add(processLabel);

            processSelector = new ComboBox
            {
                Location = new Point(80, bandSliders[0].Bottom + 35),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            processSelector.SelectedIndexChanged += ProcessSelector_SelectedIndexChanged;
            processSelector.DropDown += (s, e) => RefreshProcessList();
            this.Controls.Add(processSelector);

            globalModeCheckBox = new CheckBox
            {
                Text = "Global Preset",
                Location = new Point(240, bandSliders[0].Bottom + 35),
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            globalModeCheckBox.CheckedChanged += GlobalModeCheckBox_CheckedChanged;
            this.Controls.Add(globalModeCheckBox);

            var presetLabel = new Label
            {
                Text = "Preset Name:",
                Location = new Point(20, bandSliders[0].Bottom + 70),
                Width = 80,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            this.Controls.Add(presetLabel);

            presetNameTextBox = new TextBox
            {
                Location = new Point(100, bandSliders[0].Bottom + 65),
                Width = 130,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            this.Controls.Add(presetNameTextBox);

            savePresetButton = new Button
            {
                Text = "Save",
                Location = new Point(240, bandSliders[0].Bottom + 65),
                Width = 80,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            savePresetButton.Click += SavePresetButton_Click;
            this.Controls.Add(savePresetButton);

            loadPresetButton = new Button
            {
                Text = "Load",
                Location = new Point(330, bandSliders[0].Bottom + 65),
                Width = 80,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            loadPresetButton.Click += LoadPresetButton_Click;
            this.Controls.Add(loadPresetButton);

            presetSelector = new ComboBox
            {
                Location = new Point(420, bandSliders[0].Bottom + 65),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9f, FontStyle.Regular)
            };
            this.Controls.Add(presetSelector);

            RefreshProcessList();
            RefreshPresetList();
        }

        private void CleanupAudio()
        {
            try
            {
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                }
                if (waveProvider != null)
                {
                    waveProvider.ClearBuffer();
                    waveProvider = null;
                }
                if (waveToSampleProvider != null)
                {
                    waveToSampleProvider = null;
                }
                if (equalizer != null)
                {
                    equalizer = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during cleanup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            settings = Settings.Instance;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color formBackColor = settings.DarkTheme ? Color.FromArgb(32, 32, 32) : Color.White;
            Color controlBackColor = settings.DarkTheme ? Color.FromArgb(40, 40, 40) : Color.FromArgb(245, 245, 245);
            Color foreColor = settings.DarkTheme ? Color.White : Color.Black;
            Color buttonBackColor = settings.DarkTheme ? Color.FromArgb(64, 64, 64) : SystemColors.Control;

            this.BackColor = formBackColor;
            this.ForeColor = foreColor;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Label || ctrl is CheckBox || ctrl is ComboBox || ctrl is TextBox)
                {
                    ctrl.BackColor = formBackColor;
                    ctrl.ForeColor = foreColor;
                }
                else if (ctrl is TrackBar tb)
                {
                    tb.BackColor = settings.DarkTheme ? Color.FromArgb(80, 80, 80) : Color.White;
                }
                else if (ctrl is Button btn)
                {
                    btn.BackColor = buttonBackColor;
                    btn.ForeColor = foreColor;
                    btn.FlatStyle = settings.DarkTheme ? FlatStyle.Flat : FlatStyle.Standard;
                }
            }
        }

        private void UpdateGainLabel(int bandIndex)
        {
            if (bandIndex >= 0 && bandIndex < gainLabels.Length)
            {
                gainLabels[bandIndex].Text = $"{bandSliders[bandIndex].Value} dB";
                ApplyEqualizerSettings();
            }
        }

        private void RefreshProcessList()
        {
            processSelector.Items.Clear();
            var defaultDevice = FMain.AudioController.DefaultPlaybackDevice;
            if (defaultDevice == null)
                return;

            var sessions = defaultDevice.SessionController.All().ToList();
            var processNames = new List<string>();

            foreach (var session in sessions)
            {
                try
                {
                    int processId = session.ProcessId;
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

                    if (!string.IsNullOrEmpty(process.ProcessName))
                    {
                        processNames.Add(process.ProcessName);
                    }
                }
                catch
                {
                }
            }

            processNames = processNames.Distinct().OrderBy(name => name).ToList();
            foreach (var processName in processNames)
            {
                processSelector.Items.Add(processName);
            }

            if (processSelector.Items.Count > 0)
            {
                processSelector.SelectedIndex = 0;
                processSelector.Enabled = true;
            }
            else
            {
                processSelector.Items.Add("(No audio processes)");
                processSelector.SelectedIndex = 0;
                processSelector.Enabled = false;
            }
        }

        private void RefreshPresetList()
        {
            presetSelector.Items.Clear();
            foreach (var preset in presets.Keys)
            {
                presetSelector.Items.Add(preset);
            }
            if (presetSelector.Items.Count > 0)
            {
                presetSelector.SelectedIndex = 0;
            }
        }

        private void ProcessSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!globalModeCheckBox.Checked && processSelector.SelectedItem != null)
            {
                string processName = processSelector.SelectedItem.ToString();
                if (processName == "(No audio processes)")
                    return;

                string presetName = $"Process_{processName}";
                if (presets.ContainsKey(presetName))
                {
                    LoadPreset(presetName);
                }
                else
                {
                    ResetSliders();
                }
            }
        }

        private void GlobalModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            processSelector.Enabled = !globalModeCheckBox.Checked;
            if (globalModeCheckBox.Checked)
            {
                string presetName = "Global_Default";
                if (presets.ContainsKey(presetName))
                {
                    LoadPreset(presetName);
                }
                else
                {
                    ResetSliders();
                }
            }
            else
            {
                ProcessSelector_SelectedIndexChanged(sender, e);
            }
        }

        private void SavePresetButton_Click(object sender, EventArgs e)
        {
            string presetName = presetNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(presetName))
            {
                MessageBox.Show("Please enter a preset name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (globalModeCheckBox.Checked)
            {
                presetName = $"Global_{presetName}";
            }
            else if (processSelector.SelectedItem != null)
            {
                string selectedProcess = processSelector.SelectedItem.ToString();
                if (selectedProcess == "(No audio processes)")
                {
                    MessageBox.Show("No valid process selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                presetName = $"Process_{selectedProcess}_{presetName}";
            }
            else
            {
                MessageBox.Show("Please select a process or enable global mode.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var preset = new EqualizerPreset
            {
                Name = presetName,
                Gains = bandSliders.Select(tb => tb.Value).ToArray(),
                VolumeBoost = volumeBoostSlider.Value,
                BassBoost = bassBoostSlider.Value
            };

            presets[presetName] = preset;
            SavePresets();
            RefreshPresetList();
            presetSelector.SelectedItem = presetName;
            lastAppliedPresetName = presetName;
        }

        private void LoadPresetButton_Click(object sender, EventArgs e)
        {
            if (presetSelector.SelectedItem != null)
            {
                string presetName = presetSelector.SelectedItem.ToString();
                LoadPreset(presetName);
                lastAppliedPresetName = presetName;
            }
        }

        private void LoadPreset(string presetName)
        {
            if (presets.TryGetValue(presetName, out var preset))
            {
                for (int i = 0; i < bandCount && i < preset.Gains.Length; i++)
                {
                    bandSliders[i].Value = preset.Gains[i];
                    gainLabels[i].Text = $"{preset.Gains[i]} dB";
                }
                volumeBoostSlider.Value = preset.VolumeBoost;
                volumeBoostLabel.Text = $"{preset.VolumeBoost}%";
                volumeBoost = preset.VolumeBoost / 100f;
                bassBoostSlider.Value = preset.BassBoost;
                bassBoostLabel.Text = $"{preset.BassBoost} dB";
                bassBoost = preset.BassBoost;
                ApplyEqualizerSettings();
                presetNameTextBox.Text = presetName.StartsWith("Global_") ? presetName.Substring(7) :
                    presetName.StartsWith("Process_") ? presetName.Substring(presetName.IndexOf('_', 8) + 1) : presetName;
            }
        }

        private void ResetSliders()
        {
            for (int i = 0; i < bandCount; i++)
            {
                bandSliders[i].Value = 0;
                gainLabels[i].Text = "0 dB";
            }
            volumeBoostSlider.Value = 100;
            volumeBoostLabel.Text = "100%";
            volumeBoost = 1.0f;
            bassBoostSlider.Value = 0;
            bassBoostLabel.Text = "0 dB";
            bassBoost = 0.0f;
            ApplyEqualizerSettings();
        }

        private void ApplyEqualizerSettings()
        {
            if (equalizer == null)
                return;

            if (waveProvider != null)
            {
                waveProvider.ClearBuffer();
            }
            for (int i = 0; i < bandCount; i++)
            {
                float gain = bandSliders[i].Value;
                if (i <= 2)
                {
                    gain += bassBoost;
                    gain = Math.Max(-24f, Math.Min(24f, gain));
                }
                equalizer.UpdateBand(i, frequencyValues[i], gain);
            }
            equalizer.UpdateVolumeBoost(volumeBoost);
        }

        private void SavePresets()
        {
            try
            {
                var dir = Path.GetDirectoryName(presetFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetFilePath, json);
            }
            catch
            {
            }
        }

        private Dictionary<string, EqualizerPreset> LoadPresets()
        {
            try
            {
                if (File.Exists(presetFilePath))
                {
                    var json = File.ReadAllText(presetFilePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, EqualizerPreset>>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch
            {
            }
            return new Dictionary<string, EqualizerPreset>();
        }
    }

    public class EqualizerPreset
    {
        public string Name { get; set; }
        public int[] Gains { get; set; }
        public int VolumeBoost { get; set; }
        public int BassBoost { get; set; }
    }

    public class Equalizer : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly BiQuadFilter[] filters;
        private readonly float[] gains;
        private float volumeBoost;

        public Equalizer(ISampleProvider source)
        {
            sourceProvider = source;
            filters = new BiQuadFilter[10];
            gains = new float[10];
            volumeBoost = 1.0f;
        }

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public void UpdateBand(int bandIndex, float frequency, float gain)
        {
            if (bandIndex < 0 || bandIndex >= filters.Length)
                return;

            gains[bandIndex] = gain;
            if (filters[bandIndex] == null || Math.Abs(gains[bandIndex]) > 0.01f)
            {
                filters[bandIndex] = BiQuadFilter.PeakingEQ(
                    WaveFormat.SampleRate, frequency, 1.0f, gains[bandIndex]);
            }
            else
            {
                filters[bandIndex] = null;
            }
        }

        public void UpdateVolumeBoost(float boost)
        {
            volumeBoost = boost;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int n = 0; n < samplesRead; n++)
            {
                float sample = buffer[offset + n];

                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] != null)
                    {
                        sample = filters[i].Transform(sample);
                    }
                }

                sample *= volumeBoost;
                const float preAmp = 1.2f;
                sample *= preAmp;
                sample = Math.Sign(sample) * Math.Min(1.0f, Math.Abs(sample));

                sample = Math.Max(-1.0f, Math.Min(1.0f, sample));

                buffer[offset + n] = sample;
            }

            return samplesRead;
        }
    }
}