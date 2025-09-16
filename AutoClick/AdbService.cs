using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoClick.Models;

namespace AutoClick.Services;

public class AdbService
{
    private readonly ILogger<AdbService> _logger;

    public AdbService(ILogger<AdbService> logger)
    {
        _logger = logger;
    }

    public async Task<List<DeviceInfo>> GetAdbDevicesAsync()
    {
        var devices = new List<DeviceInfo>();
        
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "devices",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines.Skip(1)) // Skip header line
            {
                var parts = line.Trim().Split('\t');
                if (parts.Length >= 2)
                {
                    var serial = parts[0];
                    var status = parts[1];
                    
                    // Only include authorized and online devices
                    if (status != "unauthorized" && status != "offline")
                    {
                        devices.Add(new DeviceInfo { Serial = serial, Status = status });
                        _logger.LogInformation("Found device: {Serial} ({Status})", serial, status);
                    }
                    else
                    {
                        _logger.LogWarning("Skipping device {Serial} with status: {Status}", serial, status);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ADB devices");
        }

        return devices;
    }

    public async Task<Mat> CaptureScreenshotAsync(string serial)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"-s {serial} exec-out screencap -p",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            
            using var memoryStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"ADB screencap failed with exit code {process.ExitCode}");
            }

            var imageData = memoryStream.ToArray();
            var mat = Cv2.ImDecode(imageData, ImreadModes.Color);
            
            if (mat.Empty())
            {
                throw new InvalidOperationException("Failed to decode screenshot image");
            }

            _logger.LogDebug("Captured screenshot from {Serial}: {Width}x{Height}", serial, mat.Width, mat.Height);
            return mat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing screenshot from {Serial}", serial);
            throw;
        }
    }

    public async Task TapAsync(string serial, int x, int y, bool dryRun, int jitterPixels = 0)
    {
        // Apply jitter
        var random = new Random();
        var actualX = x + random.Next(-jitterPixels, jitterPixels + 1);
        var actualY = y + random.Next(-jitterPixels, jitterPixels + 1);

        _logger.LogInformation("Tap {Serial} at ({X},{Y}){Jitter}", 
            serial, actualX, actualY, 
            jitterPixels > 0 ? $" (jittered from {x},{y})" : "");

        if (dryRun)
        {
            _logger.LogInformation("DRY RUN: Would tap at ({X},{Y})", actualX, actualY);
            return;
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"-s {serial} shell input tap {actualX} {actualY}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Tap command failed with exit code {ExitCode}", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tapping on {Serial} at ({X},{Y})", serial, actualX, actualY);
            throw;
        }
    }

    public async Task ExecuteAdbCommandAsync(string serial, string command, bool dryRun)
    {
        _logger.LogInformation("Execute ADB command on {Serial}: {Command}", serial, command);

        if (dryRun)
        {
            _logger.LogInformation("DRY RUN: Would execute: adb -s {Serial} {Command}", serial, command);
            return;
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"-s {serial} {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ADB command failed with exit code {ExitCode}", process.ExitCode);
            }
            else if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("ADB command output: {Output}", output.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ADB command on {Serial}: {Command}", serial, command);
            throw;
        }
    }
}