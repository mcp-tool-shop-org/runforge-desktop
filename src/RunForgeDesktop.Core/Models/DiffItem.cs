namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents a single difference between parent and current run requests.
/// </summary>
public sealed record DiffItem
{
    /// <summary>
    /// The field name that differs (e.g., "preset", "model.family").
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// The value in the parent request.
    /// </summary>
    public required string ParentValue { get; init; }

    /// <summary>
    /// The value in the current request.
    /// </summary>
    public required string CurrentValue { get; init; }

    /// <summary>
    /// Human-readable display name for the field.
    /// </summary>
    public string DisplayName => Field switch
    {
        "preset" => "Preset",
        "dataset.path" => "Dataset Path",
        "dataset.label_column" => "Label Column",
        "model.family" => "Model Family",
        "model.hyperparameters" => "Hyperparameters",
        "device.type" => "Device Type",
        "name" => "Run Name",
        "notes" => "Notes",
        _ => Field
    };
}

/// <summary>
/// Result of comparing two run requests.
/// </summary>
public sealed record RunRequestDiffResult
{
    /// <summary>
    /// Whether a parent request was found and compared.
    /// </summary>
    public required bool HasParent { get; init; }

    /// <summary>
    /// The parent run ID (from rerun_from field).
    /// </summary>
    public string? ParentRunId { get; init; }

    /// <summary>
    /// List of differences between parent and current.
    /// Empty if no differences or no parent.
    /// </summary>
    public required IReadOnlyList<DiffItem> Differences { get; init; }

    /// <summary>
    /// The parent request, if found.
    /// </summary>
    public RunRequest? ParentRequest { get; init; }

    /// <summary>
    /// The current request being compared.
    /// </summary>
    public RunRequest? CurrentRequest { get; init; }

    /// <summary>
    /// Whether there are any differences.
    /// </summary>
    public bool HasDifferences => Differences.Count > 0;

    /// <summary>
    /// Creates a result for when there is no parent.
    /// </summary>
    public static RunRequestDiffResult NoParent => new()
    {
        HasParent = false,
        Differences = Array.Empty<DiffItem>()
    };
}
