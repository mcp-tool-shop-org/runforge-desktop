using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

public class ExecutionQueueTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void ExecutionQueue_Deserializes_FromJson()
    {
        const string json = """
            {
                "version": 1,
                "kind": "execution_queue",
                "max_parallel": 4,
                "last_served_group": "grp_001",
                "jobs": [
                    {
                        "job_id": "job_20260201_120000_0001",
                        "kind": "run",
                        "run_id": "20260201-120000-test",
                        "group_id": "grp_001",
                        "priority": 5,
                        "state": "queued",
                        "attempt": 1,
                        "created_at": "2026-02-01T12:00:00"
                    },
                    {
                        "job_id": "job_20260201_120000_0002",
                        "kind": "run",
                        "run_id": "20260201-120001-test",
                        "group_id": "grp_001",
                        "priority": 0,
                        "state": "running",
                        "attempt": 1,
                        "created_at": "2026-02-01T12:00:01",
                        "started_at": "2026-02-01T12:00:05"
                    }
                ]
            }
            """;

        var queue = JsonSerializer.Deserialize<ExecutionQueue>(json, JsonOptions);

        Assert.NotNull(queue);
        Assert.Equal(1, queue.Version);
        Assert.Equal(4, queue.MaxParallel);
        Assert.Equal("grp_001", queue.LastServedGroup);
        Assert.Equal(2, queue.Jobs.Count);
    }

    [Fact]
    public void ExecutionQueue_QueuedJobs_FiltersCorrectly()
    {
        var queue = new ExecutionQueue
        {
            Jobs = new[]
            {
                new QueueJob { JobId = "j1", RunId = "r1", State = "queued" },
                new QueueJob { JobId = "j2", RunId = "r2", State = "running" },
                new QueueJob { JobId = "j3", RunId = "r3", State = "succeeded" },
                new QueueJob { JobId = "j4", RunId = "r4", State = "queued" },
            }
        };

        Assert.Equal(2, queue.QueuedJobs.Count());
        Assert.Single(queue.RunningJobs);
        Assert.Single(queue.SucceededJobs);
    }

    [Fact]
    public void QueueJob_IsComplete_ReturnsCorrectly()
    {
        var queued = new QueueJob { JobId = "j1", RunId = "r1", State = "queued" };
        var running = new QueueJob { JobId = "j2", RunId = "r2", State = "running" };
        var succeeded = new QueueJob { JobId = "j3", RunId = "r3", State = "succeeded" };
        var failed = new QueueJob { JobId = "j4", RunId = "r4", State = "failed" };
        var canceled = new QueueJob { JobId = "j5", RunId = "r5", State = "canceled" };

        Assert.False(queued.IsComplete);
        Assert.True(queued.IsQueued);
        Assert.False(running.IsComplete);
        Assert.True(running.IsRunning);
        Assert.True(succeeded.IsComplete);
        Assert.True(failed.IsComplete);
        Assert.True(canceled.IsComplete);
    }

    [Fact]
    public void DaemonStatus_Deserializes_FromJson()
    {
        const string json = """
            {
                "version": 1,
                "pid": 12345,
                "started_at": "2026-02-01T12:00:00",
                "last_heartbeat": "2026-02-01T12:05:00",
                "max_parallel": 2,
                "active_jobs": 1,
                "state": "running"
            }
            """;

        var status = JsonSerializer.Deserialize<DaemonStatus>(json, JsonOptions);

        Assert.NotNull(status);
        Assert.Equal(12345, status.Pid);
        Assert.Equal(2, status.MaxParallel);
        Assert.Equal(1, status.ActiveJobs);
        Assert.Equal("running", status.State);
        Assert.True(status.IsRunning);
        Assert.False(status.IsStopped);
    }

    [Fact]
    public void DaemonStatus_StateProperties_WorkCorrectly()
    {
        var running = new DaemonStatus { State = "running" };
        var stopping = new DaemonStatus { State = "stopping" };
        var stopped = new DaemonStatus { State = "stopped" };

        Assert.True(running.IsRunning);
        Assert.False(running.IsStopping);
        Assert.False(running.IsStopped);

        Assert.False(stopping.IsRunning);
        Assert.True(stopping.IsStopping);
        Assert.False(stopping.IsStopped);

        Assert.False(stopped.IsRunning);
        Assert.False(stopped.IsStopping);
        Assert.True(stopped.IsStopped);
    }

    [Fact]
    public void DaemonStatus_IsHealthy_RequiresRecentHeartbeat()
    {
        var recentHeartbeat = new DaemonStatus
        {
            State = "running",
            LastHeartbeat = DateTime.UtcNow.ToString("O"),
        };

        var oldHeartbeat = new DaemonStatus
        {
            State = "running",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5).ToString("O"),
        };

        var stoppedDaemon = new DaemonStatus
        {
            State = "stopped",
            LastHeartbeat = DateTime.UtcNow.ToString("O"),
        };

        Assert.True(recentHeartbeat.IsHealthy);
        Assert.False(oldHeartbeat.IsHealthy);
        Assert.False(stoppedDaemon.IsHealthy);
    }
}
