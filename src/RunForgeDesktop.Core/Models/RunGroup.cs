using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Run group v1 schema - the durable "what happened" record for a sweep.
/// Stored in .runforge/groups/{group-id}/group.json.
/// </summary>
public sealed record RunGroup
{
    /// <summary>
    /// Schema version. Must be 1 for this contract.
    /// </summary>
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    /// <summary>
    /// Kind discriminator. Must be "run_group".
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// Unique group identifier (e.g., "grp_20260201_150000_rfdepth").
    /// </summary>
    [JsonPropertyName("group_id")]
    public required string GroupId { get; init; }

    /// <summary>
    /// When the group was created (ISO8601).
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; init; }

    /// <summary>
    /// Tool that created the group (e.g., "runforge-cli@0.3.4").
    /// </summary>
    [JsonPropertyName("created_by")]
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Human-readable group name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional notes/description.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Reference to the plan file (relative path or "embedded").
    /// </summary>
    [JsonPropertyName("plan_ref")]
    public string? PlanRef { get; init; }

    /// <summary>
    /// Overall group status: "running", "completed", "failed", "canceled".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Execution metadata.
    /// </summary>
    [JsonPropertyName("execution")]
    public required GroupExecution Execution { get; init; }

    /// <summary>
    /// Individual run entries.
    /// </summary>
    [JsonPropertyName("runs")]
    public IReadOnlyList<GroupRunEntry> Runs { get; init; } = Array.Empty<GroupRunEntry>();

    /// <summary>
    /// Aggregated summary statistics.
    /// </summary>
    [JsonPropertyName("summary")]
    public required GroupSummary Summary { get; init; }

    /// <summary>
    /// Whether the group is paused (queue mode only).
    /// </summary>
    [JsonPropertyName("paused")]
    public bool Paused { get; init; }

    /// <summary>
    /// Whether the group has completed (successfully or not).
    /// </summary>
    [JsonIgnore]
    public bool IsComplete => Status is "completed" or "failed" or "canceled";

    /// <summary>
    /// Whether the group is still running.
    /// </summary>
    [JsonIgnore]
    public bool IsRunning => Status == "running";

    /// <summary>
    /// Whether the group is paused.
    /// </summary>
    [JsonIgnore]
    public bool IsPaused => Paused;
}

/// <summary>
/// Execution metadata for a group.
/// </summary>
public sealed record GroupExecution
{
    /// <summary>
    /// Maximum parallel runs used.
    /// </summary>
    [JsonPropertyName("max_parallel")]
    public required int MaxParallel { get; init; }

    /// <summary>
    /// When execution started (ISO8601).
    /// </summary>
    [JsonPropertyName("started_at")]
    public required string StartedAt { get; init; }

    /// <summary>
    /// When execution finished (ISO8601), null if still running.
    /// </summary>
    [JsonPropertyName("finished_at")]
    public string? FinishedAt { get; init; }

    /// <summary>
    /// Whether the group was canceled by user.
    /// </summary>
    [JsonPropertyName("cancelled")]
    public required bool Cancelled { get; init; }
}

/// <summary>
/// A single run entry within a group.
/// </summary>
public sealed record GroupRunEntry
{
    /// <summary>
    /// Unique run identifier.
    /// </summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>
    /// Run status: "pending", "running", "succeeded", "failed", "canceled".
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Parameter overrides applied for this run.
    /// Keys are dot-path strings, values are the override values.
    /// </summary>
    [JsonPropertyName("request_overrides")]
    public IReadOnlyDictionary<string, object?> RequestOverrides { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>
    /// Path to result.json for this run.
    /// </summary>
    [JsonPropertyName("result_ref")]
    public string? ResultRef { get; init; }

    /// <summary>
    /// Primary metric from the run (if succeeded and available).
    /// </summary>
    [JsonPropertyName("primary_metric")]
    public GroupMetricValue? PrimaryMetric { get; init; }

    /// <summary>
    /// Whether the run has finished (successfully or not).
    /// </summary>
    [JsonIgnore]
    public bool IsComplete => Status is "succeeded" or "failed" or "canceled";

    /// <summary>
    /// Whether the run succeeded.
    /// </summary>
    [JsonIgnore]
    public bool IsSucceeded => Status == "succeeded";
}

/// <summary>
/// A metric name/value pair.
/// </summary>
public sealed record GroupMetricValue
{
    /// <summary>
    /// Metric name (e.g., "accuracy").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Metric value.
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; init; }
}

/// <summary>
/// Aggregated summary for the group.
/// </summary>
public sealed record GroupSummary
{
    /// <summary>
    /// Total number of runs in the group.
    /// </summary>
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    /// <summary>
    /// Number of succeeded runs.
    /// </summary>
    [JsonPropertyName("succeeded")]
    public required int Succeeded { get; init; }

    /// <summary>
    /// Number of failed runs.
    /// </summary>
    [JsonPropertyName("failed")]
    public required int Failed { get; init; }

    /// <summary>
    /// Number of canceled runs.
    /// </summary>
    [JsonPropertyName("canceled")]
    public required int Canceled { get; init; }

    /// <summary>
    /// Number of runs still pending.
    /// </summary>
    [JsonIgnore]
    public int Pending => Total - Succeeded - Failed - Canceled;

    /// <summary>
    /// Run ID of the best run (by primary metric).
    /// </summary>
    [JsonPropertyName("best_run_id")]
    public string? BestRunId { get; init; }

    /// <summary>
    /// Primary metric from the best run.
    /// </summary>
    [JsonPropertyName("best_primary_metric")]
    public GroupMetricValue? BestPrimaryMetric { get; init; }
}
