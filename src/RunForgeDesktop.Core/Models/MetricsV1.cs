using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the metrics.v1.json interpretability artifact.
/// Enhanced metrics with profile information.
/// </summary>
public sealed record MetricsV1
{
    /// <summary>
    /// Schema version identifier.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Profile ID that generated these metrics.
    /// </summary>
    [JsonPropertyName("profile_id")]
    public required string ProfileId { get; init; }

    /// <summary>
    /// Model family (e.g., "logistic_regression", "random_forest").
    /// </summary>
    [JsonPropertyName("model_family")]
    public required string ModelFamily { get; init; }

    /// <summary>
    /// Metrics by category (e.g., "classification", "regression").
    /// </summary>
    [JsonPropertyName("metrics")]
    public required Dictionary<string, Dictionary<string, double>> Metrics { get; init; }

    /// <summary>
    /// ISO8601 timestamp when metrics were computed.
    /// </summary>
    [JsonPropertyName("computed_at")]
    public required string ComputedAt { get; init; }
}
