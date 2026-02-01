using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the metrics.json file in a run directory.
/// Phase 2.1 strict schema: exactly 3 keys.
/// </summary>
public sealed record TrainingMetrics
{
    /// <summary>
    /// Classification accuracy (0.0 - 1.0).
    /// </summary>
    [JsonPropertyName("accuracy")]
    public required double Accuracy { get; init; }

    /// <summary>
    /// Total number of samples (train + validation).
    /// </summary>
    [JsonPropertyName("num_samples")]
    public required int NumSamples { get; init; }

    /// <summary>
    /// Number of features in the dataset.
    /// </summary>
    [JsonPropertyName("num_features")]
    public required int NumFeatures { get; init; }

    /// <summary>
    /// Gets accuracy as a percentage string for display.
    /// </summary>
    [JsonIgnore]
    public string AccuracyPercent => $"{Accuracy:P1}";
}
