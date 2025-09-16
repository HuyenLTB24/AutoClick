# AutoClick

A production-ready .NET 8 console application that automates Android emulator interactions using ADB for running game dungeons and repetitive tasks.

## Features

- **Device Management**: Automatic discovery of ADB devices with parallel processing support
- **Computer Vision**: OpenCV-based screenshot capture and template matching for stage detection
- **State Machine**: Configurable stage detection with action sequences
- **Coordinate Picker**: Interactive UI for selecting tap coordinates (Windows only)
- **Fallback System**: Template matching with coordinate fallbacks
- **Robust Operations**: Retry logic, timeouts, and graceful cancellation
- **Comprehensive Logging**: Detailed logging with debug screenshot support

## Prerequisites

- .NET 8.0 or later
- Android Debug Bridge (ADB) in PATH
- Android emulators or devices connected via USB/network
- Windows (for coordinate picker UI) or Linux

## Installation

1. Clone the repository:
```bash
git clone https://github.com/HuyenLTB24/AutoClick.git
cd AutoClick/AutoClick
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the application:
```bash
dotnet build
```

## Quick Start

1. **Generate sample configuration:**
```bash
dotnet run -- generate-config
```

2. **Create template directories:**
```bash
mkdir -p templates/stages templates/buttons
```

3. **Add template images** to the appropriate directories (PNG format recommended)

4. **Test device connectivity:**
```bash
adb devices
```

5. **Pick coordinates for fallback (Windows only):**
```bash
dotnet run -- pick-coords --device emulator-5554
```

6. **Run automation:**
```bash
dotnet run -- run
```

## Commands

### Run Automation
```bash
# Run on all available devices
dotnet run -- run

# Run on specific devices
dotnet run -- run --devices emulator-5554,emulator-5556

# Dry run (no actual taps)
dotnet run -- run --dry-run

# Enable debug logging and screenshots
dotnet run -- run --debug --debug-screenshots
```

### Coordinate Picker
```bash
# Launch coordinate picker for a device (Windows only)
dotnet run -- pick-coords --device emulator-5554

# With debug logging
dotnet run -- pick-coords --device emulator-5554 --debug
```

### Configuration
```bash
# Generate sample config.json
dotnet run -- generate-config

# Use custom config file
dotnet run -- run --config my-config.json
```

## Configuration

The `config.json` file controls all aspects of the automation:

```json
{
  "templatesDir": "templates",
  "stages": {
    "lobby": ["stages/lobby1.png", "stages/lobby2.png"],
    "in_battle": ["stages/battle1.png"],
    "victory": ["stages/victory1.png"],
    "reward_screen": ["stages/reward1.png"]
  },
  "buttons": {
    "start": ["buttons/start1.png"],
    "skip": ["buttons/skip1.png"],
    "confirm": ["buttons/confirm1.png"],
    "claim": ["buttons/claim1.png"]
  },
  "actionsByStage": {
    "lobby": [
      {"action": "tap", "target": "start"},
      {"action": "wait", "ms": 2000}
    ],
    "in_battle": [
      {"action": "wait", "ms": 1000}
    ],
    "victory": [
      {"action": "tap", "target": "skip"},
      {"action": "wait", "ms": 1000}
    ],
    "reward_screen": [
      {"action": "tap", "target": "claim"},
      {"action": "wait", "ms": 1500}
    ]
  },
  "devicesFallback": {
    "emulator-5554": {
      "start": [540, 1600],
      "skip": [900, 80],
      "confirm": [540, 1200],
      "claim": [540, 1000]
    }
  },
  "matchThreshold": 0.8,
  "scales": [1.0, 0.8, 1.2],
  "retry": 3,
  "pollIntervalMs": 1000,
  "maxWorkers": 4,
  "tapJitterPixels": 5,
  "timeoutSecPerDevice": 300,
  "requiredMatchesPerStage": 1,
  "debugScreenshotsDir": null,
  "pickCoordsWindowSize": [1200, 800]
}
```

### Configuration Options

- **templatesDir**: Directory containing template images
- **stages**: Stage detection templates (stage_name -> [template_paths])
- **buttons**: Button detection templates (button_name -> [template_paths])
- **actionsByStage**: Actions to perform for each detected stage
- **devicesFallback**: Fallback coordinates per device when template matching fails
- **matchThreshold**: Minimum confidence for template matching (0.0-1.0)
- **scales**: Scale factors for multi-scale template matching
- **retry**: Maximum retries for stage detection
- **pollIntervalMs**: Polling interval between screenshots
- **maxWorkers**: Maximum concurrent device workers
- **tapJitterPixels**: Random offset for tap coordinates
- **timeoutSecPerDevice**: Timeout per device worker
- **requiredMatchesPerStage**: Minimum template matches to confirm stage
- **debugScreenshotsDir**: Directory for debug screenshots (null to disable)
- **pickCoordsWindowSize**: Window size for coordinate picker UI

### Action Types

1. **Tap Action:**
```json
{"action": "tap", "target": "button_name"}
```

2. **Wait Action:**
```json
{"action": "wait", "ms": 2000}
```

3. **ADB Command:**
```json
{"action": "adb", "command": "shell input keyevent KEYCODE_BACK"}
```

## Template Images

- Store template images in `templates/stages/` and `templates/buttons/`
- Use PNG format for best results
- Include multiple variations per stage/button for better matching
- Template images should be cropped to show only the relevant UI element

## Coordinate Picker (Windows Only)

The coordinate picker provides an interactive way to select tap coordinates:

1. Takes a screenshot from the specified device
2. Opens a Windows Forms application
3. Click on the screenshot to record coordinates
4. Enter labels for each coordinate
5. Saves coordinates to config.json automatically

## Architecture

- **AdbService**: Handles ADB device communication and screenshot capture
- **TemplateMatchingService**: OpenCV-based computer vision for stage/button detection
- **GameAutomationService**: Orchestrates the automation workflow
- **CoordinatePickerService**: Manages the coordinate selection process
- **Models**: Configuration and data transfer objects

## Troubleshooting

### Common Issues

1. **No devices found:**
   - Ensure ADB is in PATH: `adb devices`
   - Enable USB debugging on Android devices
   - Check device authorization status

2. **Template matching fails:**
   - Verify template images exist in correct directories
   - Lower matchThreshold in config.json
   - Add more template variations
   - Use debug screenshots to verify detection

3. **Coordinate picker not working:**
   - Only available on Windows
   - Ensure device is connected and responsive
   - Check ADB permissions

4. **Performance issues:**
   - Reduce maxWorkers for lower-end devices
   - Increase pollIntervalMs to reduce CPU usage
   - Disable debug screenshots in production

### Debug Mode

Enable debug logging and screenshots for troubleshooting:

```bash
dotnet run -- run --debug --debug-screenshots
```

This will:
- Show detailed logs of template matching
- Save annotated screenshots to `debug_screenshots/`
- Log coordinates and confidence scores

## License

This project is open source. Please ensure compliance with game terms of service when using automation tools.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request