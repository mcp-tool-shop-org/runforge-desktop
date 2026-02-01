using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents an entry in the .ml/outputs/index.json file.
/// This is the primary index structure for browsing runs.
/// </summary>
public sealed record RunIndexEntry
{
    /// <summary>
    /// Unique run identifier.
    /// Format: YYYYMMDD-HHMMSS-slug-rand4 (e.g., "20260201-142355-run-a3f9")
    /// </summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>
    /// ISO8601 timestamp with timezone offset when the run was created.
    /// Format: "2026-02-01T14:23:55-05:00" (never uses Z suffix)
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// User-provided training name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Preset ID used for training.
    /// Values: "std-train" or "hq-train"
    /// </summary>
    [JsonPropertyName("preset_id")]
    public required string PresetId { get; init; }

    /// <summary>
    /// Run outcome status.
    /// Values: "succeeded" or "failed"
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Workspace-relative path to the run directory.
    /// Always uses forward slashes for Windows compatibility.
    /// Example: ".ml/runs/20260201-142355-run-a3f9"
    /// </summary>
    [JsonPropertyName("run_dir")]
    public required string RunDir { get; init; }

    /// <summary>
    /// Summary metrics for the run.
    /// </summary>
    [JsonPropertyName("summary")]
    public required RunSummary Summary { get; init; }

    /// <summary>
    /// Parses the created_at timestamp to a DateTimeOffset.
    /// Returns null if parsing fails.
    /// </summary>
    public DateTimeOffset? ParsedCreatedAt =>
        DateTimeOffset.TryParse(CreatedAt, out var dt) ? dt : null;

    /// <summary>
    /// Whether this run succeeded.
    /// </summary>
    [JsonIgnore]
    public bool IsSucceeded => Status == "succeeded";
}

/// <summary>
/// Summary information for a training run.
/// </summary>
public sealed record RunSummary
{
    /// <summary>
    /// Total duration of the run in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required long DurationMs { get; init; }

    /// <summary>
    /// Final metrics from training.
    /// May be empty if training failed.
    /// </summary>
    [JsonPropertyName("final_metrics")]
    public required Dictionary<string, double> FinalMetrics { get; init; }

    /// <summary>
    /// Device used for training.
    /// Values: "cuda" or "cpu"
    /// </summary>
    [JsonPropertyName("device")]
    public required string Device { get; init; }

    /// <summary>
    /// Gets the duration as a TimeSpan for display.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);
}
