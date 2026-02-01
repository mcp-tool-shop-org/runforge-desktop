using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the feature_importance.v1.json interpretability artifact.
/// Generated for RandomForest models.
/// </summary>
public sealed record FeatureImportanceV1
{
    /// <summary>
    /// Schema version identifier.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Model family (should be "random_forest" or similar).
    /// </summary>
    [JsonPropertyName("model_family")]
    public required string ModelFamily { get; init; }

    /// <summary>
    /// Feature importances indexed by feature name.
    /// Values are normalized (sum to 1.0).
    /// </summary>
    [JsonPropertyName("importances")]
    public required Dictionary<string, double> Importances { get; init; }

    /// <summary>
    /// Importance computation method.
    /// Values: "gini", "permutation", "shap", etc.
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// ISO8601 timestamp when importances were computed.
    /// </summary>
    [JsonPropertyName("computed_at")]
    public required string ComputedAt { get; init; }

    /// <summary>
    /// Gets the top-k most important features.
    /// </summary>
    public IEnumerable<KeyValuePair<string, double>> GetTopFeatures(int k = 10) =>
        Importances.OrderByDescending(x => x.Value).Take(k);
}
