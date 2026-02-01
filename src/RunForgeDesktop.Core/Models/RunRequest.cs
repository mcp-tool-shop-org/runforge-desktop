using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the request.json file in a run directory.
/// Contains the original training request parameters.
/// </summary>
public sealed record RunRequest
{
    /// <summary>
    /// Unique run identifier.
    /// Format: YYYYMMDD-HHMMSS-slug-rand4
    /// </summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

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
    /// Optional random seed for reproducibility.
    /// </summary>
    [JsonPropertyName("seed")]
    public int? Seed { get; init; }

    /// <summary>
    /// ISO8601 timestamp with timezone offset.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// Device requested by preset.
    /// Values: "cuda", "cpu", or "auto"
    /// </summary>
    [JsonPropertyName("requested_device")]
    public required string RequestedDevice { get; init; }

    /// <summary>
    /// Actual device after GPU gating.
    /// Values: "cuda" or "cpu"
    /// </summary>
    [JsonPropertyName("actual_device")]
    public required string ActualDevice { get; init; }

    /// <summary>
    /// Reason for device selection.
    /// Values: "sufficient_vram", "insufficient_vram", "gpu_unknown", "no_cuda"
    /// </summary>
    [JsonPropertyName("gpu_reason")]
    public required string GpuReason { get; init; }
}
