# ToxMoxMicFreezer

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

A Windows application that keeps your selected audio devices' volume locked at 0dB. Perfect for microphones and other audio inputs that need consistent volume levels.

<!-- ![ToxMoxMicFreezer Screenshot](Assets/screenshot.png) -->
<!-- TODO: Add a screenshot of the application here -->

## Features

- **Volume Locking**: Automatically maintains 0dB volume level for selected audio devices
- **Multiple Device Support**: Monitor and lock multiple audio devices simultaneously
- **System Tray Integration**: Runs in the background while minimized to system tray
- **Automatic Detection**: Recognizes new audio devices as they are connected
- **Self-Contained**: Single-file executable with no external dependencies
- **Low Resource Usage**: Minimal CPU and memory footprint

## Requirements

- Windows 10 (19041) or newer
- .NET Desktop Runtime 9.0 or newer

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/ToxMoxMicFreezer/releases) page
2. Run the executable - no installation required
3. Select the devices you want to monitor
4. The application will run in the system tray, keeping your selected devices at 0dB

## Usage

1. **Start the application**: Launch ToxMoxMicFreezer.App.exe
2. **Select devices**: Check the boxes next to any audio devices you want to monitor
3. **Minimize**: The application will continue running in the system tray
4. **Restore**: Double-click the system tray icon to bring the window back
5. **Exit**: Right-click the system tray icon and select "Exit"

### Settings

The application automatically saves your monitored device selections. Configuration is stored in:
```
HKEY_CURRENT_USER\SOFTWARE\ToxMoxMicFreezer
```

## Development

### Prerequisites

- Visual Studio 2022 or newer
- .NET 9.0 SDK

### Building from Source

1. Clone the repository
```
git clone https://github.com/yourusername/ToxMoxMicFreezer.git
```

2. Open the solution in Visual Studio
```
cd ToxMoxMicFreezer
start ToxMoxMicFreezer.sln
```

3. Build the solution
```
dotnet build
```

4. Run the application
```
dotnet run --project ToxMoxMicFreezer.App
```

## How It Works

ToxMoxMicFreezer uses NAudio to interact with Windows audio APIs, monitoring selected devices and actively maintaining their volume at exactly 0dB. The application employs an adaptive adjustment algorithm that determines the optimal step size based on each device's capabilities.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) - .NET audio library
- [WPF UI](https://github.com/lepoco/wpfui) - Modern WPF UI library 