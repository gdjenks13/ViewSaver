# ViewSaver

**Save and instantly switch between multiple monitor configurations on Windows.**

ViewSaver is a lightweight system tray application that lets you save your current display setup (resolution, refresh rate, monitor arrangement, primary display, scaling) and restore it with a single click or keyboard shortcut. Perfect for users who frequently switch between different monitor configurations—such as docking/undocking a laptop, gaming setups, or presentation modes.

## Features

- **Save Display Profiles** — Capture resolution, refresh rate, positioning, primary display, enabled/disabled state, and scaling
- **One-Click Switching** — Apply any saved profile from the system tray menu
- **Custom Hotkeys** — Assign keyboard shortcuts (e.g., Ctrl+Alt+1) for instant switching
- **Runs in Background** — Lives quietly in your system tray (hidden icons area)
- **Starts with Windows** — Enabled by default so your profiles are always ready
- **Import/Export** — Backup and share your profile configurations

## Requirements

- Windows 10 or Windows 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract to a folder of your choice
3. Run `ViewSaver.exe`
4. The application will appear in your system tray

## Quick Start

1. **Save a profile**: Right-click the tray icon → "Save Current Configuration..." → Enter a name
2. **Apply a profile**: Right-click the tray icon → Click the profile name
3. **Set a hotkey**: Right-click → "Manage Profiles..." → Select profile → "Set Hotkey..."

## Usage

### Saving a Profile

1. Configure your displays using Windows Settings (System > Display)
2. Right-click the ViewSaver icon in the system tray
3. Select "Save Current Configuration..."
4. Enter a name for the profile

### Applying a Profile

1. Right-click the ViewSaver icon in the system tray
2. Click on the profile name you want to apply

### Setting Hotkeys

1. Right-click the ViewSaver icon and select "Manage Profiles..."
2. Select a profile from the list
3. Click "Set Hotkey..."
4. Choose your modifier keys (Ctrl, Alt, Shift, Win) and a key
5. Click OK

### Managing Profiles

- **Rename**: Change the profile name
- **Update**: Replace the profile with current display settings
- **Delete**: Remove the profile
- **Move Up/Down**: Change the order in the menu

## Building from Source

### Prerequisites

- Visual Studio 2026 (or VS Code / any editor)
- .NET 10 SDK

### Build Steps

```bash
cd ViewSaver
dotnet restore
dotnet build --configuration Release
```

The output will be in `ViewSaver\bin\Release\net10.0-windows\`

## File Locations

Profiles and settings are stored in:

```
%APPDATA%\ViewSaver\
├── profiles.json
├── settings.json
└── viewsaver.log
```

## Troubleshooting

### Hotkey not working

- Make sure no other application is using the same hotkey
- Try a different key combination
- Restart ViewSaver

### Profile not applying correctly

- The failure notification now includes the reason; full details are written to `%APPDATA%\ViewSaver\viewsaver.log`
- Profiles are matched to your GPU and monitors by hardware path, so reboots, driver updates, or an additional adapter (e.g. an enabled integrated GPU) no longer break them
- If a monitor in the profile is disconnected, the profile cannot be applied until it is reconnected
- Re-saving a profile with "Update" stores the latest identifiers and is recommended after hardware changes

### Application not starting with Windows

1. Open Settings
2. Check "Start ViewSaver with Windows"
3. Click OK

## License

MIT License — See [LICENSE](LICENSE) file for details.

## Disclaimer

**USE AT YOUR OWN RISK.** This software modifies Windows display settings. While it uses standard Windows APIs, incorrect configurations could temporarily render your display unusable. If this happens, reboot into Safe Mode to reset display settings.

The authors are not responsible for any damage, data loss, or system issues that may arise from using this software. Always ensure you can access your system through alternative means (e.g., Remote Desktop) when experimenting with display configurations.
