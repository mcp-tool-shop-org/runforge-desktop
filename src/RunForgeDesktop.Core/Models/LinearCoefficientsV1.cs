using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the linear_coefficients.v1.json interpretability artifact.
/// Generated for linear models (LogisticRegression, LinearSVC).
/// </summary>
public sealed record LinearCoefficientsV1
{
    /// <summary>
    /// Schema version identifier.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Model family (e.g., "logistic_regression", "linear_svc").
    /// </summary>
    [JsonPropertyName("model_family")]
    public required string ModelFamily { get; init; }

    /// <summary>
    /// Coefficients per class (for multiclass) or single class (for binary).
    /// Key is class label, value is dictionary of feature name to coefficient.
    /// </summary>
    [JsonPropertyName("coefficients")]
    public required Dictionary<string, Dictionary<string, double>> Coefficients { get; init; }

    /// <summary>
    /// Intercept per class.
    /// Key is class label, value is intercept value.
    /// </summary>
    [JsonPropertyName("intercepts")]
    public required Dictionary<string, double> Intercepts { get; init; }

    /// <summary>
    /// Whether coefficients are in standardized space.
    /// Always true for Phase 3.6 — raw coefficients require unstandardization.
    /// </summary>
    [JsonPropertyName("standardized")]
    public required bool Standardized { get; init; }

    /// <summary>
    /// ISO8601 timestamp when coefficients were extracted.
    /// </summary>
    [JsonPropertyName("computed_at")]
    public required string ComputedAt { get; init; }

    /// <summary>
    /// Gets the top-k features by absolute coefficient magnitude for a class.
    /// </summary>
    public IEnumerable<KeyValuePair<string, double>> GetTopFeaturesForClass(string classLabel, int k = 10)
    {
        if (!Coefficients.TryGetValue(classLabel, out var classCoeffs))
        {
            return [];
        }

        return classCoeffs.OrderByDescending(x => Math.Abs(x.Value)).Take(k);
    }

    /// <summary>
    /// Gets all class labels.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<string> ClassLabels => Coefficients.Keys.ToList();
}
