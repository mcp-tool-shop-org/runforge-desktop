using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Sweep plan v1 schema - input to CLI sweep command.
/// This is the recipe for generating multiple runs from a base request.
/// </summary>
public sealed record SweepPlan
{
    /// <summary>
    /// Schema version. Must be 1 for this contract.
    /// </summary>
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Kind discriminator. Must be "sweep_plan".
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// When this plan was created (ISO8601).
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// Tool that created the plan (e.g., "runforge-desktop@0.3.4").
    /// </summary>
    [JsonPropertyName("created_by")]
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Workspace path where runs will be created.
    /// </summary>
    [JsonPropertyName("workspace")]
    public required string Workspace { get; init; }

    /// <summary>
    /// Group metadata.
    /// </summary>
    [JsonPropertyName("group")]
    public required SweepGroupInfo Group { get; init; }

    /// <summary>
    /// Base request that will be cloned and modified for each run.
    /// </summary>
    [JsonPropertyName("base_request")]
    public required JsonElement BaseRequest { get; init; }

    /// <summary>
    /// Strategy for generating runs (grid, list, etc.).
    /// </summary>
    [JsonPropertyName("strategy")]
    public required SweepStrategy Strategy { get; init; }

    /// <summary>
    /// Execution parameters.
    /// </summary>
    [JsonPropertyName("execution")]
    public required SweepExecution Execution { get; init; }

    /// <summary>
    /// Creates a new sweep plan with v1 defaults.
    /// </summary>
    public static SweepPlan Create(
        string workspace,
        string groupName,
        string? notes,
        JsonElement baseRequest,
        SweepStrategy strategy,
        int maxParallel = 2)
    {
        return new SweepPlan
        {
            Version = 1,
            Kind = "sweep_plan",
            CreatedAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            CreatedBy = $"runforge-desktop@{GetVersion()}",
            Workspace = workspace,
            Group = new SweepGroupInfo
            {
                Name = groupName,
                Notes = notes
            },
            BaseRequest = baseRequest,
            Strategy = strategy,
            Execution = new SweepExecution
            {
                MaxParallel = maxParallel,
                FailFast = false,
                StopOnCancel = true
            }
        };
    }

    private static string GetVersion()
    {
        var assembly = typeof(SweepPlan).Assembly;
        var version = assembly.GetName().Version;
        return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.3.4";
    }
}

/// <summary>
/// Group metadata for a sweep.
/// </summary>
public sealed record SweepGroupInfo
{
    /// <summary>
    /// Human-readable name for this sweep/group.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional notes/description.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Strategy for generating sweep runs.
/// </summary>
public sealed record SweepStrategy
{
    /// <summary>
    /// Strategy type: "grid" or "list".
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Parameters to vary (for grid strategy).
    /// </summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyList<SweepParameter> Parameters { get; init; } = Array.Empty<SweepParameter>();
}

/// <summary>
/// A single parameter to sweep over.
/// </summary>
public sealed record SweepParameter
{
    /// <summary>
    /// JSON path to the parameter (e.g., "model.hyperparameters.max_depth").
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Values to try. Can include null for "no value" / default.
    /// </summary>
    [JsonPropertyName("values")]
    public required JsonElement Values { get; init; }
}

/// <summary>
/// Execution parameters for the sweep.
/// </summary>
public sealed record SweepExecution
{
    /// <summary>
    /// Maximum parallel runs.
    /// </summary>
    [JsonPropertyName("max_parallel")]
    public required int MaxParallel { get; init; }

    /// <summary>
    /// If true, stop all runs on first failure.
    /// </summary>
    [JsonPropertyName("fail_fast")]
    public required bool FailFast { get; init; }

    /// <summary>
    /// If true, stop remaining runs on cancel signal.
    /// </summary>
    [JsonPropertyName("stop_on_cancel")]
    public required bool StopOnCancel { get; init; }
}
