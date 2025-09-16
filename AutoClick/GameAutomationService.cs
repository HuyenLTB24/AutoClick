using Microsoft.Extensions.Logging;
using OpenCvSharp;
using AutoClick.Models;
using AutoClick.Services;

namespace AutoClick.Services;

public class GameAutomationService
{
    private readonly ILogger<GameAutomationService> _logger;
    private readonly AdbService _adbService;
    private readonly TemplateMatchingService _templateService;

    public GameAutomationService(
        ILogger<GameAutomationService> logger,
        AdbService adbService,
        TemplateMatchingService templateService)
    {
        _logger = logger;
        _adbService = adbService;
        _templateService = templateService;
    }

    public async Task RunWorkerAsync(string serial, Config config, bool dryRun, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting worker for device {Serial}", serial);
        
        var startTime = DateTime.UtcNow;
        var timeoutTime = startTime.AddSeconds(config.TimeoutSecPerDevice);
        var retryCount = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < timeoutTime)
            {
                try
                {
                    // Capture screenshot
                    using var screen = await _adbService.CaptureScreenshotAsync(serial);
                    
                    // Detect current stage
                    var detection = _templateService.DetectStage(screen, config);
                    
                    // Save debug screenshot if enabled
                    if (!string.IsNullOrEmpty(config.DebugScreenshotsDir))
                    {
                        Directory.CreateDirectory(config.DebugScreenshotsDir);
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                        var debugPath = Path.Combine(config.DebugScreenshotsDir, $"{serial}_{timestamp}.png");
                        _templateService.SaveAnnotatedScreenshot(screen, detection, debugPath);
                    }

                    if (detection.StageName != "unknown")
                    {
                        // Execute actions for this stage
                        await ExecuteStageActionsAsync(serial, detection.StageName, screen, config, dryRun);
                        retryCount = 0; // Reset retry count on successful detection
                    }
                    else
                    {
                        retryCount++;
                        _logger.LogDebug("Stage detection failed for {Serial}, retry {Retry}/{MaxRetry}", 
                            serial, retryCount, config.Retry);
                        
                        if (retryCount >= config.Retry)
                        {
                            _logger.LogWarning("Max retries reached for {Serial}, will continue polling", serial);
                            retryCount = 0;
                        }
                    }

                    // Wait before next poll
                    await Task.Delay(config.PollIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in worker loop for {Serial}", serial);
                    await Task.Delay(Math.Max(config.PollIntervalMs, 5000), cancellationToken);
                }
            }

            if (DateTime.UtcNow >= timeoutTime)
            {
                _logger.LogWarning("Worker for {Serial} timed out after {Timeout} seconds", 
                    serial, config.TimeoutSecPerDevice);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker for {Serial} was cancelled", serial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in worker for {Serial}", serial);
        }
        finally
        {
            _logger.LogInformation("Worker for {Serial} completed", serial);
        }
    }

    private async Task ExecuteStageActionsAsync(string serial, string stageName, Mat screen, Config config, bool dryRun)
    {
        if (!config.ActionsByStage.ContainsKey(stageName))
        {
            _logger.LogDebug("No actions defined for stage {Stage}", stageName);
            return;
        }

        var actions = config.ActionsByStage[stageName];
        _logger.LogInformation("Executing {Count} actions for stage {Stage} on {Serial}", 
            actions.Count, stageName, serial);

        foreach (var action in actions)
        {
            try
            {
                await ExecuteActionAsync(serial, action, screen, config, dryRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action {Action} for {Serial}", action.Action, serial);
            }
        }
    }

    private async Task ExecuteActionAsync(string serial, ActionConfig action, Mat screen, Config config, bool dryRun)
    {
        switch (action.Action.ToLowerInvariant())
        {
            case "tap":
                await ExecuteTapActionAsync(serial, action, screen, config, dryRun);
                break;
                
            case "wait":
                if (action.Ms.HasValue)
                {
                    _logger.LogDebug("Waiting {Ms}ms for {Serial}", action.Ms.Value, serial);
                    await Task.Delay(action.Ms.Value);
                }
                break;
                
            case "adb":
                if (!string.IsNullOrEmpty(action.Command))
                {
                    await _adbService.ExecuteAdbCommandAsync(serial, action.Command, dryRun);
                }
                break;
                
            default:
                _logger.LogWarning("Unknown action type: {Action}", action.Action);
                break;
        }
    }

    private async Task ExecuteTapActionAsync(string serial, ActionConfig action, Mat screen, Config config, bool dryRun)
    {
        if (string.IsNullOrEmpty(action.Target))
        {
            _logger.LogWarning("Tap action missing target");
            return;
        }

        // Try template matching first
        var buttonResult = _templateService.FindButtonByName(screen, action.Target, config);
        
        if (buttonResult.Found)
        {
            _logger.LogDebug("Found button {Target} via template matching at ({X},{Y}) with confidence {Confidence:F3}", 
                action.Target, buttonResult.X, buttonResult.Y, buttonResult.Confidence);
            
            await _adbService.TapAsync(serial, buttonResult.X, buttonResult.Y, dryRun, config.TapJitterPixels);
        }
        else
        {
            // Fallback to saved coordinates
            if (config.DevicesFallback.ContainsKey(serial) && 
                config.DevicesFallback[serial].ContainsKey(action.Target))
            {
                var coords = config.DevicesFallback[serial][action.Target];
                if (coords.Length >= 2)
                {
                    _logger.LogDebug("Using fallback coordinates for {Target} on {Serial}: ({X},{Y})", 
                        action.Target, serial, coords[0], coords[1]);
                    
                    await _adbService.TapAsync(serial, coords[0], coords[1], dryRun, config.TapJitterPixels);
                }
            }
            else
            {
                _logger.LogWarning("No coordinates found for button {Target} on device {Serial}", action.Target, serial);
            }
        }
    }
}