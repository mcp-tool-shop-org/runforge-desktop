using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the result.json file in a run directory.
/// Contains the training outcome, metrics, effective config, and artifacts.
/// </summary>
public sealed record RunResult
{
    /// <summary>
    /// Schema version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    /// <summary>
    /// Run outcome status.
    /// Values: "succeeded" or "failed"
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// ISO-8601 timestamp when run started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }

    /// <summary>
    /// ISO-8601 timestamp when run finished.
    /// </summary>
    [JsonPropertyName("finished_at")]
    public string? FinishedAt { get; init; }

    /// <summary>
    /// Total duration of the run in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required long DurationMs { get; init; }

    /// <summary>
    /// Summary with primary metric and all metrics.
    /// </summary>
    [JsonPropertyName("summary")]
    public ResultSummary? Summary { get; init; }

    /// <summary>
    /// Effective configuration used for training (post-merge with defaults).
    /// </summary>
    [JsonPropertyName("effective_config")]
    public EffectiveConfig? EffectiveConfig { get; init; }

    /// <summary>
    /// List of generated artifacts.
    /// </summary>
    [JsonPropertyName("artifacts")]
    public List<ArtifactInfo>? Artifacts { get; init; }

    /// <summary>
    /// Error information if the run failed.
    /// </summary>
    [JsonPropertyName("error")]
    public ResultError? Error { get; init; }

    /// <summary>
    /// Process exit code (legacy field, may not be present in newer results).
    /// </summary>
    [JsonPropertyName("exit_code")]
    public int? ExitCode { get; init; }

    /// <summary>
    /// Unique run identifier (legacy field).
    /// </summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>
    /// Whether this run succeeded.
    /// </summary>
    [JsonIgnore]
    public bool IsSucceeded => Status == "succeeded";

    /// <summary>
    /// Gets the duration as a TimeSpan for display.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

    /// <summary>
    /// Gets the primary metric name if available.
    /// </summary>
    [JsonIgnore]
    public string? PrimaryMetricName => Summary?.PrimaryMetric?.Name;

    /// <summary>
    /// Gets the primary metric value if available.
    /// </summary>
    [JsonIgnore]
    public double? PrimaryMetricValue => Summary?.PrimaryMetric?.Value;
}

/// <summary>
/// Summary section of result.json containing metrics.
/// </summary>
public sealed record ResultSummary
{
    /// <summary>
    /// The primary metric used for evaluation.
    /// </summary>
    [JsonPropertyName("primary_metric")]
    public PrimaryMetric? PrimaryMetric { get; init; }

    /// <summary>
    /// All computed metrics.
    /// </summary>
    [JsonPropertyName("metrics")]
    public Dictionary<string, double>? Metrics { get; init; }
}

/// <summary>
/// Primary metric information.
/// </summary>
public sealed record PrimaryMetric
{
    /// <summary>
    /// Name of the primary metric (e.g., "accuracy").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Value of the primary metric.
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }
}

/// <summary>
/// Effective configuration captured during training.
/// </summary>
public sealed record EffectiveConfig
{
    /// <summary>
    /// Model configuration.
    /// </summary>
    [JsonPropertyName("model")]
    public EffectiveModelConfig? Model { get; init; }

    /// <summary>
    /// Device configuration.
    /// </summary>
    [JsonPropertyName("device")]
    public EffectiveDeviceConfig? Device { get; init; }

    /// <summary>
    /// Preset used.
    /// </summary>
    [JsonPropertyName("preset")]
    public string? Preset { get; init; }

    /// <summary>
    /// Dataset configuration.
    /// </summary>
    [JsonPropertyName("dataset")]
    public EffectiveDatasetConfig? Dataset { get; init; }
}

/// <summary>
/// Effective model configuration.
/// </summary>
public sealed record EffectiveModelConfig
{
    /// <summary>
    /// Model family used.
    /// </summary>
    [JsonPropertyName("family")]
    public string? Family { get; init; }

    /// <summary>
    /// Merged hyperparameters (defaults + overrides).
    /// </summary>
    [JsonPropertyName("hyperparameters")]
    public Dictionary<string, JsonElement>? Hyperparameters { get; init; }
}

/// <summary>
/// Effective device configuration.
/// </summary>
public sealed record EffectiveDeviceConfig
{
    /// <summary>
    /// Device type used.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>
/// Effective dataset configuration.
/// </summary>
public sealed record EffectiveDatasetConfig
{
    /// <summary>
    /// Dataset path used.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>
    /// Label column used.
    /// </summary>
    [JsonPropertyName("label_column")]
    public string? LabelColumn { get; init; }
}

/// <summary>
/// Information about a generated artifact.
/// </summary>
public sealed record ArtifactInfo
{
    /// <summary>
    /// Path to the artifact (relative to run dir).
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Type of artifact (e.g., "model", "metrics", "encoder").
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Size of the artifact in bytes.
    /// </summary>
    [JsonPropertyName("bytes")]
    public long Bytes { get; init; }

    /// <summary>
    /// Formatted size for display.
    /// </summary>
    [JsonIgnore]
    public string FormattedSize => FormatBytes(Bytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>
/// Error information from a failed run.
/// </summary>
public sealed record ResultError
{
    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Error type/class name.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Full stack trace (optional).
    /// </summary>
    [JsonPropertyName("traceback")]
    public string? Traceback { get; init; }
}
