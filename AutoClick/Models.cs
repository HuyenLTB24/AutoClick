using System.Text.Json.Serialization;

namespace AutoClick.Models;

public class Config
{
    [JsonPropertyName("templates_dir")]
    public string TemplatesDir { get; set; } = "templates";

    [JsonPropertyName("stages")]
    public Dictionary<string, List<string>> Stages { get; set; } = new();

    [JsonPropertyName("buttons")]
    public Dictionary<string, List<string>> Buttons { get; set; } = new();

    [JsonPropertyName("actions_by_stage")]
    public Dictionary<string, List<ActionConfig>> ActionsByStage { get; set; } = new();

    [JsonPropertyName("devices_fallback")]
    public Dictionary<string, Dictionary<string, int[]>> DevicesFallback { get; set; } = new();

    [JsonPropertyName("match_threshold")]
    public double MatchThreshold { get; set; } = 0.8;

    [JsonPropertyName("scales")]
    public List<double> Scales { get; set; } = new() { 1.0, 0.8, 1.2 };

    [JsonPropertyName("retry")]
    public int Retry { get; set; } = 3;

    [JsonPropertyName("poll_interval_ms")]
    public int PollIntervalMs { get; set; } = 1000;

    [JsonPropertyName("max_workers")]
    public int MaxWorkers { get; set; } = 4;

    [JsonPropertyName("tap_jitter_pixels")]
    public int TapJitterPixels { get; set; } = 5;

    [JsonPropertyName("timeout_sec_per_device")]
    public int TimeoutSecPerDevice { get; set; } = 300;

    [JsonPropertyName("required_matches_per_stage")]
    public int RequiredMatchesPerStage { get; set; } = 1;

    [JsonPropertyName("debug_screenshots_dir")]
    public string? DebugScreenshotsDir { get; set; }

    [JsonPropertyName("pick_coords_window_size")]
    public int[]? PickCoordsWindowSize { get; set; }
}

public class ActionConfig
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("ms")]
    public int? Ms { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }
}

public class DeviceInfo
{
    public string Serial { get; set; } = "";
    public string Status { get; set; } = "";
}

public class StageDetectionResult
{
    public string StageName { get; set; } = "unknown";
    public double Confidence { get; set; }
    public string? TemplateUsed { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ButtonMatchResult
{
    public bool Found { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public double Confidence { get; set; }
    public double Scale { get; set; }
}

public class CoordinateSelection
{
    public string Label { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}