using System.Text.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

public class ExecutionQueueServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void ExecutionQueue_CanDeserialize_RealQueueJson()
    {
        // This is actual output from the daemon - validates the contract
        const string json = """
            {
              "version": 1,
              "kind": "execution_queue",
              "max_parallel": 2,
              "jobs": [
                {
                  "job_id": "job_20260201_212047_0001",
                  "kind": "run",
                  "run_id": "20260201-212047-sweep-0000",
                  "group_id": "grp_20260201_212047_Reconnect_Test",
                  "priority": 0,
                  "state": "failed",
                  "attempt": 1,
                  "created_at": "2026-02-01T21:20:47.503673",
                  "started_at": "2026-02-01T21:20:48.303532",
                  "finished_at": "2026-02-01T21:20:50.327098",
                  "error": "[2026-02-02 02:20:49] ERROR: Dataset not found"
                }
              ],
              "last_served_group": "grp_20260201_212047_Reconnect_Test"
            }
            """;

        var queue = JsonSerializer.Deserialize<ExecutionQueue>(json, JsonOptions);

        Assert.NotNull(queue);
        Assert.Equal(1, queue.Version);
        Assert.Equal(2, queue.MaxParallel);
        Assert.Equal("grp_20260201_212047_Reconnect_Test", queue.LastServedGroup);
        Assert.Single(queue.Jobs);

        var job = queue.Jobs[0];
        Assert.Equal("job_20260201_212047_0001", job.JobId);
        Assert.Equal("failed", job.State);
        Assert.True(job.IsComplete);
        Assert.False(job.IsQueued);
        Assert.False(job.IsRunning);
        Assert.Contains("Dataset not found", job.Error);
    }

    [Fact]
    public void DaemonStatus_CanDeserialize_RealDaemonJson()
    {
        // Actual daemon.json output
        const string json = """
            {
              "version": 1,
              "pid": 6496,
              "started_at": "2026-02-01T21:18:41.189871",
              "last_heartbeat": "2026-02-01T21:20:51.289677",
              "max_parallel": 2,
              "active_jobs": 1,
              "state": "running"
            }
            """;

        var status = JsonSerializer.Deserialize<DaemonStatus>(json, JsonOptions);

        Assert.NotNull(status);
        Assert.Equal(6496, status.Pid);
        Assert.Equal(2, status.MaxParallel);
        Assert.Equal(1, status.ActiveJobs);
        Assert.Equal("running", status.State);
        Assert.True(status.IsRunning);
        Assert.False(status.IsStopped);
    }

    [Fact]
    public void QueueStatusSummary_CalculatesCorrectly()
    {
        var queue = new ExecutionQueue
        {
            MaxParallel = 2,
            Jobs = new[]
            {
                new QueueJob { JobId = "j1", RunId = "r1", State = "queued" },
                new QueueJob { JobId = "j2", RunId = "r2", State = "running" },
                new QueueJob { JobId = "j3", RunId = "r3", State = "succeeded" },
                new QueueJob { JobId = "j4", RunId = "r4", State = "failed" },
                new QueueJob { JobId = "j5", RunId = "r5", State = "canceled" },
            }
        };

        var daemon = new DaemonStatus
        {
            Pid = 1234,
            State = "running",
            ActiveJobs = 1,
            MaxParallel = 2,
            LastHeartbeat = DateTime.UtcNow.ToString("O"),
        };

        var summary = new QueueStatusSummary
        {
            MaxParallel = queue.MaxParallel,
            TotalJobs = queue.Jobs.Count,
            QueuedCount = queue.QueuedJobs.Count(),
            RunningCount = queue.RunningJobs.Count(),
            SucceededCount = queue.SucceededJobs.Count(),
            FailedCount = queue.FailedJobs.Count(),
            CanceledCount = queue.CanceledJobs.Count(),
            DaemonStatus = daemon,
            PausedGroups = new[] { "grp_test" },
        };

        Assert.Equal(5, summary.TotalJobs);
        Assert.Equal(1, summary.QueuedCount);
        Assert.Equal(1, summary.RunningCount);
        Assert.Equal(1, summary.SucceededCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(1, summary.CanceledCount);
        Assert.True(summary.DaemonStatus.IsRunning);
        Assert.Single(summary.PausedGroups);
    }
}
