using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// State of the execution queue (queue.json).
/// </summary>
public sealed record ExecutionQueue
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "execution_queue";

    [JsonPropertyName("max_parallel")]
    public int MaxParallel { get; init; } = 2;

    [JsonPropertyName("jobs")]
    public IReadOnlyList<QueueJob> Jobs { get; init; } = [];

    [JsonPropertyName("last_served_group")]
    public string? LastServedGroup { get; init; }

    /// <summary>Gets queued jobs.</summary>
    public IEnumerable<QueueJob> QueuedJobs => Jobs.Where(j => j.State == "queued");

    /// <summary>Gets running jobs.</summary>
    public IEnumerable<QueueJob> RunningJobs => Jobs.Where(j => j.State == "running");

    /// <summary>Gets succeeded jobs.</summary>
    public IEnumerable<QueueJob> SucceededJobs => Jobs.Where(j => j.State == "succeeded");

    /// <summary>Gets failed jobs.</summary>
    public IEnumerable<QueueJob> FailedJobs => Jobs.Where(j => j.State == "failed");

    /// <summary>Gets canceled jobs.</summary>
    public IEnumerable<QueueJob> CanceledJobs => Jobs.Where(j => j.State == "canceled");
}

/// <summary>
/// A single job in the queue.
/// </summary>
public sealed record QueueJob
{
    [JsonPropertyName("job_id")]
    public required string JobId { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "run";

    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("group_id")]
    public string? GroupId { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>
    /// State: "queued", "running", "succeeded", "failed", "canceled".
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("attempt")]
    public int Attempt { get; init; } = 1;

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }

    [JsonPropertyName("finished_at")]
    public string? FinishedAt { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Whether the job is queued.</summary>
    public bool IsQueued => State == "queued";

    /// <summary>Whether the job is running.</summary>
    public bool IsRunning => State == "running";

    /// <summary>Whether the job is complete (succeeded, failed, or canceled).</summary>
    public bool IsComplete => State is "succeeded" or "failed" or "canceled";
}

/// <summary>
/// State of the execution daemon (daemon.json).
/// </summary>
public sealed record DaemonStatus
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("pid")]
    public int Pid { get; init; }

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }

    [JsonPropertyName("last_heartbeat")]
    public string? LastHeartbeat { get; init; }

    [JsonPropertyName("max_parallel")]
    public int MaxParallel { get; init; } = 2;

    [JsonPropertyName("active_jobs")]
    public int ActiveJobs { get; init; }

    /// <summary>
    /// State: "running", "stopping", "stopped".
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = "stopped";

    /// <summary>Whether the daemon is running.</summary>
    public bool IsRunning => State == "running";

    /// <summary>Whether the daemon is stopping.</summary>
    public bool IsStopping => State == "stopping";

    /// <summary>Whether the daemon is stopped.</summary>
    public bool IsStopped => State == "stopped";

    /// <summary>
    /// Gets whether the daemon appears healthy (heartbeat within last 30 seconds).
    /// </summary>
    public bool IsHealthy
    {
        get
        {
            if (!IsRunning || string.IsNullOrEmpty(LastHeartbeat))
                return false;

            if (DateTime.TryParse(LastHeartbeat, out var lastBeat))
            {
                return (DateTime.UtcNow - lastBeat.ToUniversalTime()).TotalSeconds < 30;
            }
            return false;
        }
    }
}
