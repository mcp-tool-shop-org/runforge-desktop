using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents the request.json file in a run directory.
/// Contains the configuration for what a training run should do.
///
/// Implements V1 Contract (docs/V1_CONTRACT.md):
/// - Schema version 1
/// - Forward compatibility via JsonExtensionData
/// - Unknown fields preserved on round-trip
/// </summary>
public sealed record RunRequest
{
    /// <summary>
    /// Schema version (integer). Required.
    /// Breaking changes bump this number.
    /// </summary>
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Preset identifier. Required.
    /// Values: "fast", "balanced", "thorough", "custom"
    /// </summary>
    [JsonPropertyName("preset")]
    public required string Preset { get; init; }

    /// <summary>
    /// Dataset configuration. Required.
    /// </summary>
    [JsonPropertyName("dataset")]
    public required RunRequestDataset Dataset { get; init; }

    /// <summary>
    /// Model configuration. Required.
    /// </summary>
    [JsonPropertyName("model")]
    public required RunRequestModel Model { get; init; }

    /// <summary>
    /// Device configuration. Required.
    /// </summary>
    [JsonPropertyName("device")]
    public required RunRequestDevice Device { get; init; }

    /// <summary>
    /// ISO-8601 UTC timestamp when request was created. Required.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// Client that created this request. Required.
    /// Format: "client@version" (e.g., "runforge-vscode@0.3.6")
    /// </summary>
    [JsonPropertyName("created_by")]
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Schema URL for validation. Optional.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>
    /// Parent run ID if this is a rerun. Optional.
    /// </summary>
    [JsonPropertyName("rerun_from")]
    public string? RerunFrom { get; init; }

    /// <summary>
    /// User-facing label for this run. Optional.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// User tags for organization. Optional.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Free-form user notes. Optional.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Extension data for forward compatibility.
    /// Preserves unknown fields from newer schema versions.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    /// <summary>
    /// Parsed created_at timestamp.
    /// Returns null if parsing fails.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset? ParsedCreatedAt =>
        DateTimeOffset.TryParse(CreatedAt, out var result) ? result : null;

    /// <summary>
    /// Validates that all required fields are present and valid.
    /// </summary>
    /// <returns>List of validation errors, empty if valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Version < 1)
            errors.Add("version must be >= 1");

        if (string.IsNullOrWhiteSpace(Preset))
            errors.Add("preset is required");

        if (Dataset is null)
            errors.Add("dataset is required");
        else
        {
            if (string.IsNullOrWhiteSpace(Dataset.Path))
                errors.Add("dataset.path is required");
            if (string.IsNullOrWhiteSpace(Dataset.LabelColumn))
                errors.Add("dataset.label_column is required");
        }

        if (Model is null)
            errors.Add("model is required");
        else
        {
            if (string.IsNullOrWhiteSpace(Model.Family))
                errors.Add("model.family is required");
        }

        if (Device is null)
            errors.Add("device is required");
        else
        {
            if (string.IsNullOrWhiteSpace(Device.Type))
                errors.Add("device.type is required");
        }

        if (string.IsNullOrWhiteSpace(CreatedAt))
            errors.Add("created_at is required");

        if (string.IsNullOrWhiteSpace(CreatedBy))
            errors.Add("created_by is required");

        return errors;
    }

    /// <summary>
    /// Returns true if this request passes validation.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => Validate().Count == 0;
}

/// <summary>
/// Dataset configuration for a run request.
/// </summary>
public sealed record RunRequestDataset
{
    /// <summary>
    /// Workspace-relative path to the dataset file. Required.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Name of the label column. Required.
    /// </summary>
    [JsonPropertyName("label_column")]
    public required string LabelColumn { get; init; }

    /// <summary>
    /// Extension data for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Model configuration for a run request.
/// </summary>
public sealed record RunRequestModel
{
    /// <summary>
    /// Model family identifier. Required.
    /// Values: "logistic_regression", "random_forest", "linear_svc"
    /// </summary>
    [JsonPropertyName("family")]
    public required string Family { get; init; }

    /// <summary>
    /// Hyperparameter overrides. Optional.
    /// Free-form JSON object.
    /// </summary>
    [JsonPropertyName("hyperparameters")]
    public Dictionary<string, JsonElement>? Hyperparameters { get; init; }

    /// <summary>
    /// Extension data for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Device configuration for a run request.
/// </summary>
public sealed record RunRequestDevice
{
    /// <summary>
    /// Device type. Required.
    /// Values: "cpu", "gpu"
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Reason for device selection. Optional.
    /// Explains why GPU was blocked or unavailable.
    /// </summary>
    [JsonPropertyName("gpu_reason")]
    public string? GpuReason { get; init; }

    /// <summary>
    /// Extension data for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
