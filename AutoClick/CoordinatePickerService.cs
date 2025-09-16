using System.Text.Json;
using AutoClick.Models;
using AutoClick.Services;
using Microsoft.Extensions.Logging;

namespace AutoClick.Services;

public class CoordinatePickerService
{
    private readonly ILogger<CoordinatePickerService> _logger;
    private readonly AdbService _adbService;

    public CoordinatePickerService(ILogger<CoordinatePickerService> logger, AdbService adbService)
    {
        _logger = logger;
        _adbService = adbService;
    }

    public async Task<bool> PickCoordinatesAsync(string deviceSerial, Config config)
    {
        try
        {
            _logger.LogInformation("Starting coordinate picker for device {Serial}", deviceSerial);

            // Capture screenshot
            using var screenshot = await _adbService.CaptureScreenshotAsync(deviceSerial);
            
            // Save screenshot to temporary file
            var tempDir = Path.GetTempPath();
            var screenshotPath = Path.Combine(tempDir, $"autoclick_screenshot_{deviceSerial}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            
            using (var ms = screenshot.ToMemoryStream())
            {
                await File.WriteAllBytesAsync(screenshotPath, ms.ToArray());
            }

            _logger.LogDebug("Screenshot saved to: {Path}", screenshotPath);

#if WINDOWS
            // Launch coordinate picker UI
            var coordinates = await LaunchCoordinatePickerUI(deviceSerial, screenshotPath, config);
            
            if (coordinates.Count > 0)
            {
                // Update config with selected coordinates
                if (!config.DevicesFallback.ContainsKey(deviceSerial))
                {
                    config.DevicesFallback[deviceSerial] = new Dictionary<string, int[]>();
                }

                foreach (var coord in coordinates)
                {
                    config.DevicesFallback[deviceSerial][coord.Label] = new int[] { coord.X, coord.Y };
                }

                // Save updated config
                await SaveConfigAsync(config);
                
                _logger.LogInformation("Saved {Count} coordinates for device {Serial}", coordinates.Count, deviceSerial);
                return true;
            }
#else
            _logger.LogWarning("Coordinate picker UI is only available on Windows. Screenshot saved to: {Path}", screenshotPath);
            _logger.LogInformation("Manual coordinate selection instructions:");
            _logger.LogInformation("1. Open the screenshot: {Path}", screenshotPath);
            _logger.LogInformation("2. Use an image viewer to identify pixel coordinates");
            _logger.LogInformation("3. Manually edit config.json to add coordinates under devices_fallback.{Serial}", deviceSerial);
#endif

            // Clean up temporary file
            try
            {
                File.Delete(screenshotPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary screenshot: {Path}", screenshotPath);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in coordinate picker for device {Serial}", deviceSerial);
            return false;
        }
    }

#if WINDOWS
    private async Task<List<CoordinateSelection>> LaunchCoordinatePickerUI(string deviceSerial, string screenshotPath, Config config)
    {
        var coordinates = new List<CoordinateSelection>();
        
        await Task.Run(() =>
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            
            using var form = new UI.CoordinatePickerForm(_logger, deviceSerial, screenshotPath, config);
            var result = form.ShowDialog();
            
            if (result == System.Windows.Forms.DialogResult.OK && form.SaveRequested)
            {
                coordinates.AddRange(form.SelectedCoordinates);
            }
        });

        return coordinates;
    }
#endif

    private async Task SaveConfigAsync(Config config)
    {
        try
        {
            var configPath = "config.json";
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(config, jsonOptions);
            await File.WriteAllTextAsync(configPath, json);
            
            _logger.LogInformation("Configuration saved to: {Path}", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            throw;
        }
    }
}