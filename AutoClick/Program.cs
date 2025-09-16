using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AutoClick.Models;
using AutoClick.Services;

namespace AutoClick;

class Program
{
    private static readonly CancellationTokenSource _cancellationTokenSource = new();
    private static ILogger<Program>? _logger;

    static async Task<int> Main(string[] args)
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information);
        });
        
        _logger = loggerFactory.CreateLogger<Program>();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _logger?.LogInformation("Cancellation requested...");
            _cancellationTokenSource.Cancel();
        };

        try
        {
            return await RunAsync(args, loggerFactory);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unhandled exception");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args, ILoggerFactory loggerFactory)
    {
        // Create root command
        var rootCommand = new RootCommand("AutoClick - Android emulator automation tool");

        // Global options
        var configOption = new Option<string>(
            name: "--config",
            description: "Path to configuration file",
            getDefaultValue: () => "config.json");

        var devicesOption = new Option<string[]>(
            name: "--devices",
            description: "Comma-separated list of device serials to target")
        { AllowMultipleArgumentsPerToken = true };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Log actions without executing them",
            getDefaultValue: () => false);

        var debugOption = new Option<bool>(
            name: "--debug",
            description: "Enable debug logging",
            getDefaultValue: () => false);

        var debugScreenshotsOption = new Option<bool>(
            name: "--debug-screenshots",
            description: "Save annotated debug screenshots",
            getDefaultValue: () => false);

        // Add global options
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(devicesOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(debugOption);
        rootCommand.AddOption(debugScreenshotsOption);

        // Main run command
        var runCommand = new Command("run", "Run the automation on specified devices");
        runCommand.SetHandler(async (config, devices, dryRun, debug, debugScreenshots) =>
        {
            await RunAutomationAsync(config, devices, dryRun, debug, debugScreenshots, loggerFactory);
        }, configOption, devicesOption, dryRunOption, debugOption, debugScreenshotsOption);

        // Coordinate picker command
        var pickCoordsCommand = new Command("pick-coords", "Launch coordinate picker for a device");
        var deviceOption = new Option<string>(
            name: "--device",
            description: "Device serial for coordinate picking")
        { IsRequired = true };
        
        pickCoordsCommand.AddOption(deviceOption);
        pickCoordsCommand.SetHandler(async (config, device, debug) =>
        {
            await RunCoordinatePickerAsync(config, device, debug, loggerFactory);
        }, configOption, deviceOption, debugOption);

        // Generate config command
        var generateConfigCommand = new Command("generate-config", "Generate a sample configuration file");
        generateConfigCommand.SetHandler(async (config) =>
        {
            await GenerateSampleConfigAsync(config);
        }, configOption);

        // Add commands to root
        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(pickCoordsCommand);
        rootCommand.AddCommand(generateConfigCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunAutomationAsync(
        string configPath, 
        string[] targetDevices, 
        bool dryRun, 
        bool debug, 
        bool debugScreenshots,
        ILoggerFactory loggerFactory)
    {
        // Create new logger factory with updated level if debug is enabled
        using var actualLoggerFactory = debug ? 
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)) :
            loggerFactory;

        var config = await LoadConfigAsync(configPath);
        if (config == null) return;

        if (debugScreenshots)
        {
            config.DebugScreenshotsDir = "debug_screenshots";
        }

        // Create services
        var adbService = new AdbService(actualLoggerFactory.CreateLogger<AdbService>());
        var templateService = new TemplateMatchingService(actualLoggerFactory.CreateLogger<TemplateMatchingService>());
        var automationService = new GameAutomationService(
            actualLoggerFactory.CreateLogger<GameAutomationService>(),
            adbService,
            templateService);

        // Get available devices
        var allDevices = await adbService.GetAdbDevicesAsync();
        var devicesToUse = FilterDevices(allDevices, targetDevices);

        if (devicesToUse.Count == 0)
        {
            _logger?.LogError("No devices found or specified");
            return;
        }

        _logger?.LogInformation("Starting automation on {Count} devices: {Devices}", 
            devicesToUse.Count, string.Join(", ", devicesToUse.Select(d => d.Serial)));

        if (dryRun)
        {
            _logger?.LogInformation("DRY RUN MODE - No actual taps will be performed");
        }

        // Run workers in parallel
        var semaphore = new SemaphoreSlim(config.MaxWorkers);
        var tasks = devicesToUse.Select(async device =>
        {
            await semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                await automationService.RunWorkerAsync(device.Serial, config, dryRun, _cancellationTokenSource.Token);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        _logger?.LogInformation("All workers completed");
    }

    private static async Task RunCoordinatePickerAsync(
        string configPath, 
        string deviceSerial, 
        bool debug,
        ILoggerFactory loggerFactory)
    {
        // Create new logger factory with updated level if debug is enabled
        using var actualLoggerFactory = debug ? 
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)) :
            loggerFactory;

        var config = await LoadConfigAsync(configPath);
        if (config == null) return;

        var adbService = new AdbService(actualLoggerFactory.CreateLogger<AdbService>());
        var pickerService = new CoordinatePickerService(
            actualLoggerFactory.CreateLogger<CoordinatePickerService>(),
            adbService);

        var success = await pickerService.PickCoordinatesAsync(deviceSerial, config);
        
        if (success)
        {
            _logger?.LogInformation("Coordinate picking completed successfully");
        }
        else
        {
            _logger?.LogWarning("Coordinate picking was cancelled or failed");
        }
    }

    private static async Task GenerateSampleConfigAsync(string configPath)
    {
        if (File.Exists(configPath))
        {
            _logger?.LogWarning("Configuration file already exists: {Path}", configPath);
            return;
        }

        var sampleConfig = new Config
        {
            TemplatesDir = "templates",
            Stages = new Dictionary<string, List<string>>
            {
                { "lobby", new List<string> { "stages/lobby1.png", "stages/lobby2.png" } },
                { "in_battle", new List<string> { "stages/battle1.png" } },
                { "victory", new List<string> { "stages/victory1.png" } },
                { "reward_screen", new List<string> { "stages/reward1.png" } }
            },
            Buttons = new Dictionary<string, List<string>>
            {
                { "start", new List<string> { "buttons/start1.png" } },
                { "skip", new List<string> { "buttons/skip1.png" } },
                { "confirm", new List<string> { "buttons/confirm1.png" } },
                { "claim", new List<string> { "buttons/claim1.png" } }
            },
            ActionsByStage = new Dictionary<string, List<ActionConfig>>
            {
                {
                    "lobby", new List<ActionConfig>
                    {
                        new() { Action = "tap", Target = "start" },
                        new() { Action = "wait", Ms = 2000 }
                    }
                },
                {
                    "in_battle", new List<ActionConfig>
                    {
                        new() { Action = "wait", Ms = 1000 }
                    }
                },
                {
                    "victory", new List<ActionConfig>
                    {
                        new() { Action = "tap", Target = "skip" },
                        new() { Action = "wait", Ms = 1000 }
                    }
                },
                {
                    "reward_screen", new List<ActionConfig>
                    {
                        new() { Action = "tap", Target = "claim" },
                        new() { Action = "wait", Ms = 1500 }
                    }
                }
            },
            DevicesFallback = new Dictionary<string, Dictionary<string, int[]>>
            {
                {
                    "emulator-5554", new Dictionary<string, int[]>
                    {
                        { "start", new[] { 540, 1600 } },
                        { "skip", new[] { 900, 80 } },
                        { "confirm", new[] { 540, 1200 } },
                        { "claim", new[] { 540, 1000 } }
                    }
                }
            },
            MatchThreshold = 0.8,
            Scales = new List<double> { 1.0, 0.8, 1.2 },
            Retry = 3,
            PollIntervalMs = 1000,
            MaxWorkers = 4,
            TapJitterPixels = 5,
            TimeoutSecPerDevice = 300,
            RequiredMatchesPerStage = 1,
            DebugScreenshotsDir = null,
            PickCoordsWindowSize = new[] { 1200, 800 }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(sampleConfig, jsonOptions);
        await File.WriteAllTextAsync(configPath, json);

        _logger?.LogInformation("Sample configuration generated: {Path}", configPath);
        _logger?.LogInformation("Please edit the configuration file and add your template images to the templates directory");
    }

    private static async Task<Config?> LoadConfigAsync(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                _logger?.LogError("Configuration file not found: {Path}", configPath);
                _logger?.LogInformation("Use 'generate-config' command to create a sample configuration");
                return null;
            }

            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<Config>(json);

            if (config == null)
            {
                _logger?.LogError("Failed to deserialize configuration file: {Path}", configPath);
                return null;
            }

            _logger?.LogInformation("Configuration loaded from: {Path}", configPath);
            return config;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading configuration: {Path}", configPath);
            return null;
        }
    }

    private static List<DeviceInfo> FilterDevices(List<DeviceInfo> allDevices, string[] targetDevices)
    {
        if (targetDevices == null || targetDevices.Length == 0)
        {
            return allDevices;
        }

        var targetSet = new HashSet<string>(targetDevices);
        return allDevices.Where(d => targetSet.Contains(d.Serial)).ToList();
    }
}
