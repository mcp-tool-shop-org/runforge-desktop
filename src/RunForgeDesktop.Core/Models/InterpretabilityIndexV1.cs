using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the interpretability.index.v1.json artifact (Phase 3.6).
/// Unified index of all interpretability artifacts available for a run.
/// </summary>
public sealed record InterpretabilityIndexV1
{
    /// <summary>
    /// Schema version identifier.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Run ID this index belongs to.
    /// </summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>
    /// Model family that generated the artifacts.
    /// </summary>
    [JsonPropertyName("model_family")]
    public required string ModelFamily { get; init; }

    /// <summary>
    /// List of available artifacts.
    /// </summary>
    [JsonPropertyName("artifacts")]
    public required List<ArtifactEntry> Artifacts { get; init; }

    /// <summary>
    /// ISO8601 timestamp when index was generated.
    /// </summary>
    [JsonPropertyName("generated_at")]
    public required string GeneratedAt { get; init; }

    /// <summary>
    /// Checks if a specific artifact type is available.
    /// </summary>
    public bool HasArtifact(string artifactType) =>
        Artifacts.Any(a => a.Type == artifactType && a.Available);

    /// <summary>
    /// Gets an artifact entry by type.
    /// </summary>
    public ArtifactEntry? GetArtifact(string artifactType) =>
        Artifacts.FirstOrDefault(a => a.Type == artifactType);
}

/// <summary>
/// Represents an entry in the interpretability index.
/// </summary>
public sealed record ArtifactEntry
{
    /// <summary>
    /// Artifact type identifier.
    /// Values: "metrics.v1", "feature_importance.v1", "linear_coefficients.v1"
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Schema version of the artifact.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Relative path to the artifact file within the run directory.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Whether the artifact is available (file exists and is valid).
    /// </summary>
    [JsonPropertyName("available")]
    public required bool Available { get; init; }

    /// <summary>
    /// Reason if not available (e.g., "not_supported_for_model", "generation_failed").
    /// Null if available.
    /// </summary>
    [JsonPropertyName("unavailable_reason")]
    public string? UnavailableReason { get; init; }
}
