# ToxMox's Mic Freezer+

A Windows desktop application that prevents external applications from changing your audio device volumes. Perfect for streamers, content creators, and anyone who needs consistent audio levels.

![ToxMox's Mic Freezer+ in action](./ToxMoxMicFreezer.gif)

## Features

- **Volume Freezing/Enforcement** - Locks audio device volumes to prevent unwanted changes from other applications
- **Real-time Audio Level Metering** - Visual feedback with stereo peak level indicators for all devices
- **Smart Popup Notifications** - 4-phase throttling system prevents notification spam while keeping you informed
- **Device Favorites** - Organize your most-used devices with drag-and-drop for quick access
- **System Tray Integration** - Minimizes to tray with full-featured WPF context menu
- **Automatic Device Handling** - Seamlessly handles audio device changes without losing settings
- **Pause/Resume Functionality** - Temporarily disable volume enforcement with timed or manual resume
- **Per-Device Volume Control** - Individual volume sliders with 0dB reference markers
- **Settings Persistence** - All settings saved to Windows registry and restored on startup

## System Requirements

- Windows 10.0.19041.0 or later
- .NET 9.0 (included in self-contained build)
- x64 architecture

## Installation

### Download Release
Download the latest release from the [Releases](../../releases) page. The application is distributed as a single self-contained executable.

### Build from Source

#### Prerequisites
- .NET 9.0 SDK
- Windows 10.0.19041.0 SDK or later
- Visual Studio 2022 or VS Code (optional)

#### Build Commands
```bash
# Debug build (for development)
dotnet build ToxMoxMicFreezer.App/ToxMoxMicFreezer.App.csproj -r win-x64

# Release build (self-contained single executable)
dotnet publish ToxMoxMicFreezer.App/ToxMoxMicFreezer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

**Important**: Always use the `-r win-x64` flag to ensure proper Windows runtime targeting.

The release build output will be in: `./ToxMoxMicFreezer.App/bin/Release/net9.0-windows10.0.19041.0/win-x64/publish/`

## Usage

### Getting Started
1. Run `ToxMoxMicFreezer.exe`
2. The main window shows all your audio devices organized in tabs (Recording/Playback)
3. Click the Snowflake next to any device you want to freeze/monitor
4. Any external attempts to change the volume will be reverted and logged
5. Click the help icon in the top right of the app for instructions

### System Tray
Right-click the system tray icon to access:
- **Pause/Resume** - Temporarily disable volume enforcement
  - Pause for 5, 15, 30, or 60 minutes
  - Resume manually at any time
- **Mute Popups** - Control notification behavior
  - Mute for 5, 15, 30, or 60 minutes
  - Mute until manually enabled
- **Settings** - Configure application behavior
  - Run at Windows startup
  - Minimize to tray on start
  - Start minimized
  - Enable/disable popup notifications
- **Show/Hide** - Toggle main window visibility
- **Exit** - Close the application

### Shortcuts
- **Double-click title bar** - Maximize/restore window
- **Click and drag volume bars** - Adjust device volume when not frozen
- **Single-click volume dB value text** - Edit volume value directly
- **Right-click volume bar** - Set to 0dB

### Device Organization
- **Favorites Tab** - Star your most-used devices for quick access
- **Drag and Drop** - Reorder devices within the favorites tab
- **Auto-balancing** - Devices automatically balance between left/right columns

## Architecture

### Technology Stack
- **Framework**: .NET 9.0 WPF application
- **UI Framework**: WPF-UI for modern controls and styling
- **Audio Backend**: NAudio with Windows Core Audio API (WASAPI)
- **System Tray**: H.NotifyIcon with pure WPF menus
- **Notifications**: Windows toast notifications via CommunityToolkit

### Core Components
- **Event-driven Architecture** - Audio level monitoring uses WASAPI event callbacks for efficiency
- **Modular Service Design** - Specialized services handle different aspects of functionality
- **State Machine** - Device loading follows a predictable state progression
- **Registry Persistence** - All settings and device selections saved to Windows registry

## Dependencies

- **NAudio** (2.2.1) - Audio device management and Core Audio API - Ms-PL License
- **WPF-UI** (4.0.2) - Modern WPF controls and styling - MIT License
- **H.NotifyIcon.WPF** (2.1.3) - System tray icon implementation - MIT License
- **System.Drawing.Common** (8.0.7) - Graphics support - MIT License
- **CommunityToolkit.WinUI.Notifications** (7.1.2) - Toast notifications - MIT License

## License

This project is licensed under the Apache License, Version 2.0 - see the [LICENSE](LICENSE) file for details.

Copyright 2025 CrunchFocus LLC. See the [NOTICE](NOTICE) file for additional copyright information.

### Third-Party Licenses
See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for licenses of included dependencies.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## About

ToxMox's Mic Freezer+ is developed by [CrunchFocus LLC](https://crunchfocus.com), the creators of [OverMox](https://overmox.com), a realtime 3D effects engine for content creators.

## Troubleshooting

### Application won't start
- Ensure you have Windows 10.0.19041.0 or later
- Check Windows Event Viewer for error messages

### Devices not appearing
- Make sure audio devices are enabled in Windows Sound settings
- Check that devices aren't disabled in Device Manager

### Volume enforcement not working
- Ensure the device snowflake is checked (frozen)
- Check that the application isn't paused (banner at the top of the UI and system tray icon shows pause state)

### High CPU usage
- This is typically caused by audio metering - try minimizing the window
- Switching tabs pauses metering for hidden devices

## Support

For bug reports and feature requests, please use the [Issues](../../issues) page.

## Acknowledgments

This application was created largely with the assistance of AI technology, demonstrating the collaborative potential between human creativity and artificial intelligence in modern software development.