using Microsoft.Extensions.Logging;
using OpenCvSharp;
using AutoClick.Models;

namespace AutoClick.Services;

public class TemplateMatchingService
{
    private readonly ILogger<TemplateMatchingService> _logger;

    public TemplateMatchingService(ILogger<TemplateMatchingService> logger)
    {
        _logger = logger;
    }

    public StageDetectionResult DetectStage(Mat screen, Config config)
    {
        var bestResult = new StageDetectionResult();
        var requiredMatches = config.RequiredMatchesPerStage;
        
        foreach (var stage in config.Stages)
        {
            var stageName = stage.Key;
            var templatePaths = stage.Value;
            var matchCount = 0;
            
            foreach (var templatePath in templatePaths)
            {
                var fullPath = Path.Combine(config.TemplatesDir, templatePath);
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Template file not found: {Path}", fullPath);
                    continue;
                }

                try
                {
                    var template = Cv2.ImRead(fullPath, ImreadModes.Color);
                    if (template.Empty())
                    {
                        _logger.LogWarning("Failed to load template: {Path}", fullPath);
                        continue;
                    }

                    var matchResult = FindBestMatch(screen, template, config.MatchThreshold, config.Scales);
                    
                    if (matchResult.Found && matchResult.Confidence > bestResult.Confidence)
                    {
                        bestResult = new StageDetectionResult
                        {
                            StageName = stageName,
                            Confidence = matchResult.Confidence,
                            TemplateUsed = templatePath,
                            X = matchResult.X,
                            Y = matchResult.Y,
                            Width = (int)(template.Width * matchResult.Scale),
                            Height = (int)(template.Height * matchResult.Scale)
                        };
                        
                        matchCount++;
                        _logger.LogDebug("Stage {Stage} matched with confidence {Confidence:F3} using {Template}", 
                            stageName, matchResult.Confidence, templatePath);
                    }

                    template.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing template {Path}", fullPath);
                }
            }

            // If we found enough matches for this stage, we're confident in the detection
            if (matchCount >= requiredMatches && bestResult.StageName == stageName)
            {
                break;
            }
        }

        if (bestResult.Confidence > 0)
        {
            _logger.LogInformation("Detected stage: {Stage} (confidence: {Confidence:F3})", 
                bestResult.StageName, bestResult.Confidence);
        }
        else
        {
            _logger.LogDebug("No stage detected above threshold");
        }

        return bestResult;
    }

    public ButtonMatchResult FindButton(Mat screen, Mat template, double threshold, IEnumerable<double> scales)
    {
        return FindBestMatch(screen, template, threshold, scales);
    }

    private ButtonMatchResult FindBestMatch(Mat screen, Mat template, double threshold, IEnumerable<double> scales)
    {
        var bestResult = new ButtonMatchResult();
        
        foreach (var scale in scales)
        {
            try
            {
                Mat scaledTemplate;
                if (Math.Abs(scale - 1.0) < 0.001)
                {
                    scaledTemplate = template.Clone();
                }
                else
                {
                    scaledTemplate = new Mat();
                    var newSize = new Size((int)(template.Width * scale), (int)(template.Height * scale));
                    Cv2.Resize(template, scaledTemplate, newSize);
                }

                // Skip if scaled template is larger than screen
                if (scaledTemplate.Width > screen.Width || scaledTemplate.Height > screen.Height)
                {
                    scaledTemplate.Dispose();
                    continue;
                }

                var result = new Mat();
                Cv2.MatchTemplate(screen, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                
                Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);
                
                if (maxVal > threshold && maxVal > bestResult.Confidence)
                {
                    bestResult = new ButtonMatchResult
                    {
                        Found = true,
                        X = maxLoc.X + scaledTemplate.Width / 2,
                        Y = maxLoc.Y + scaledTemplate.Height / 2,
                        Confidence = maxVal,
                        Scale = scale
                    };
                }

                result.Dispose();
                scaledTemplate.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in template matching at scale {Scale}", scale);
            }
        }

        return bestResult;
    }

    public ButtonMatchResult FindButtonByName(Mat screen, string buttonName, Config config)
    {
        if (!config.Buttons.ContainsKey(buttonName))
        {
            _logger.LogWarning("Button {ButtonName} not found in config", buttonName);
            return new ButtonMatchResult();
        }

        var templatePaths = config.Buttons[buttonName];
        var bestResult = new ButtonMatchResult();

        foreach (var templatePath in templatePaths)
        {
            var fullPath = Path.Combine(config.TemplatesDir, templatePath);
            
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Button template file not found: {Path}", fullPath);
                continue;
            }

            try
            {
                var template = Cv2.ImRead(fullPath, ImreadModes.Color);
                if (template.Empty())
                {
                    _logger.LogWarning("Failed to load button template: {Path}", fullPath);
                    continue;
                }

                var matchResult = FindBestMatch(screen, template, config.MatchThreshold, config.Scales);
                
                if (matchResult.Found && matchResult.Confidence > bestResult.Confidence)
                {
                    bestResult = matchResult;
                    _logger.LogDebug("Button {ButtonName} found with confidence {Confidence:F3} using {Template}", 
                        buttonName, matchResult.Confidence, templatePath);
                }

                template.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing button template {Path}", fullPath);
            }
        }

        return bestResult;
    }

    public void SaveAnnotatedScreenshot(Mat screen, StageDetectionResult detection, string outputPath)
    {
        try
        {
            var annotated = screen.Clone();
            
            if (detection.Confidence > 0)
            {
                var rect = new Rect(detection.X - detection.Width / 2, detection.Y - detection.Height / 2, 
                                  detection.Width, detection.Height);
                Cv2.Rectangle(annotated, rect, Scalar.Green, 3);
                
                var text = $"{detection.StageName} ({detection.Confidence:F2})";
                Cv2.PutText(annotated, text, new Point(rect.X, rect.Y - 10), 
                           HersheyFonts.HersheySimplex, 0.6, Scalar.Green, 2);
            }

            Cv2.ImWrite(outputPath, annotated);
            annotated.Dispose();
            
            _logger.LogDebug("Saved annotated screenshot: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving annotated screenshot: {Path}", outputPath);
        }
    }
}