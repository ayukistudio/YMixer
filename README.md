# YMixer - OldSchool Mixer for Windows 11

![YMixer Screenshot](https://github.com/ayukistudio/YMixer/blob/main/img/1.png)  
*Take control of your audio like never before.*

YMixer is a lightweight, intuitive, and powerful audio mixer designed to bring back the convenience of per-application volume control to Windows 11, while adding modern features like an equalizer and customizable presets. If you've missed the classic Windows 10 volume mixer or want more control over your audio, YMixer is here to fill the gap with style and functionality.

## Why YMixer?

Windows 11 streamlined many features, but the classic per-application volume mixer was simplified, leaving users with fewer options for fine-tuned audio control. YMixer restores and enhances this experience, offering a sleek system tray-based interface that feels both familiar and fresh. Whether you're a gamer, content creator, or just someone who loves perfect audio, YMixer makes managing sound effortless.

### Key Benefits
- **Per-Application Volume Control**: Adjust the volume of individual apps with precision, just like the Windows 10 mixer.
- **System Tray Convenience**: Access the mixer instantly from the system tray, with hover or click-to-open options.
- **Powerful Equalizer**: Fine-tune your audio with a 10-band equalizer, including volume and bass boost controls.
- **Custom Presets**: Save and load audio profiles for specific apps or global settings, perfect for different tasks like gaming, music, or video editing.
- **Dark/Light Theme Support**: Matches your Windows 11 aesthetic with customizable themes.
- **Low Resource Usage**: Lightweight and optimized to run smoothly without taxing your system.

## Features

### 1. Intuitive Volume Mixer
YMixer displays all active audio sessions in a clean, scrollable interface. Each application with active audio gets its own slider, complete with:
- The app's icon for easy identification.
- A volume percentage display for precision.
- Dedicated sliders for global volume and system sounds.

![YMixer Interface](https://github.com/ayukistudio/YMixer/blob/main/img/1.png)

### 2. System Tray Integration
No need to dig through settings—YMixer lives in your system tray. Choose to open it with a click or simply hover over the tray icon for instant access. The mixer pops up near the taskbar, perfectly positioned for quick adjustments.

### 3. 10-Band Equalizer
Take your audio to the next level with YMixer's built-in equalizer:
- Adjust 10 frequency bands (32Hz to 16kHz) with gains from -24dB to +24dB.
- Boost bass for richer sound or fine-tune mids and highs for clarity.
- Apply a volume boost (up to 300%) for extra loudness when needed.
- Save equalizer settings as presets for specific apps or global use.

![YMixer Equalizer](https://github.com/ayukistudio/YMixer/blob/main/img/2.png)

### 4. Preset Management
Create and save custom audio profiles for different scenarios:
- **Global Presets**: Apply equalizer settings to all audio output.
- **Per-Process Presets**: Tailor audio for specific apps, like boosting bass for music players or enhancing voice clarity for video calls.
- Load presets instantly from a dropdown menu, with support for naming and organizing your profiles.

### 5. Theme Customization
YMixer adapts to your preferences with dark and light themes, ensuring it blends seamlessly with your Windows 11 setup. Toggle themes in the settings menu to match your style.

### 6. Settings for Flexibility
Customize YMixer to suit your workflow:
- Enable/disable tray hover-to-open.
- Save profiles automatically for consistent audio settings.
- Show or hide the tray icon based on your preference.

## How It Works

YMixer uses the [AudioSwitcher](https://github.com/xenolightning/AudioSwitcher) library to interact with Windows' Core Audio API, giving it direct access to audio sessions. The equalizer leverages [NAudio](https://github.com/naudio/NAudio) for real-time audio processing, capturing system or microphone input and applying your custom settings.

1. **Launch YMixer**: The app starts minimized to the system tray.
2. **Open the Mixer**: Click or hover over the tray icon to reveal the mixer window, showing all active audio sessions.
3. **Adjust Volumes**: Use sliders to control individual app volumes, global volume, or system sounds.
4. **Access the Equalizer**: Right-click the tray icon and select "Equalizer" to open the 10-band equalizer.
5. **Customize Audio**: Tweak frequency bands, boost bass, or apply volume boosts. Save your settings as a preset for reuse.
6. **Switch Themes or Settings**: Open the settings menu to toggle themes, tray behavior, or profile saving.

## Why Use YMixer on Windows 11?

The Windows 11 audio settings are streamlined but lack the granular control of the Windows 10 volume mixer. YMixer not only restores this functionality but goes beyond with:
- **Enhanced Control**: Per-app volume sliders and equalizer settings give you more power over your audio than ever.
- **Modern Design**: A clean, Windows 11-inspired interface with theme support.
- **Time-Saving Presets**: Switch between audio profiles instantly for work, gaming, or entertainment.
- **Fallback Support**: If system audio capture fails, YMixer can switch to microphone input, ensuring you always have equalizer functionality.

## Installation

1. **Download**: Grab the latest release from the [GitHub Releases](https://github.com/ayukistudio/YMixer/releases) page.
2. **Install**: Run the installer or extract the portable version to your desired folder.
3. **Run**: Launch `YMixer.exe`, and it will appear in your system tray.
4. **Optional**: Ensure your audio drivers are up to date for optimal performance.

## Requirements
- **OS**: Windows 11 (also compatible with Windows 10).
- **Dependencies**: .NET Framework 4.8 or later.
- **Permissions**: Run as Administrator for full audio capture functionality (recommended).

## Troubleshooting
- **No audio processes shown?** Ensure apps are playing sound and check your default playback device in Windows settings.
- **Equalizer not working?** Run YMixer as Administrator, update audio drivers, or restart the Windows Audio service.
- **System audio capture failed?** YMixer will attempt to use a microphone as a fallback. Follow the on-screen troubleshooting steps to resolve.

## Contributing
Love YMixer? Want to make it even better? Contributions are welcome!  
- **Report Issues**: Open an issue on GitHub for bugs or feature requests.
- **Submit Code**: Fork the repo, make your changes, and submit a pull request.
- **Share Feedback**: Let us know how YMixer works for you!

## License
YMixer is licensed under the [MIT License](LICENSE). Feel free to use, modify, and distribute it as you see fit.

## Acknowledgments
- Built with [AudioSwitcher](https://github.com/xenolightning/AudioSwitcher) for audio session management.
- Powered by [NAudio](https://github.com/naudio/NAudio) for real-time audio processing.
- Inspired by the classic Windows 10 volume mixer and the needs of modern Windows 11 users.

---

*YMixer - Because great audio deserves great control.*  
Star the project on [GitHub](https://github.com/ayukistudio/YMixer) and take your Windows 11 audio experience to the next level! 🎵
