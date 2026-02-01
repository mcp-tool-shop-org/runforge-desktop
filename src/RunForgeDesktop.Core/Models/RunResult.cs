using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the result.json file in a run directory.
/// Contains the training outcome.
/// </summary>
public sealed record RunResult
{
    /// <summary>
    /// Unique run identifier.
    /// </summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>
    /// Run outcome status.
    /// Values: "succeeded" or "failed"
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Process exit code.
    /// 0 indicates success.
    /// </summary>
    [JsonPropertyName("exit_code")]
    public required int ExitCode { get; init; }

    /// <summary>
    /// Total duration of the run in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required long DurationMs { get; init; }

    /// <summary>
    /// Optional error message if the run failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

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
}
