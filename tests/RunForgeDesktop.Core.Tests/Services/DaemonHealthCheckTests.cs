using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Services;

public class DaemonHealthCheckTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void DaemonStatus_IsHealthy_DetectsStaleHeartbeat()
    {
        // Simulate daemon that was killed - state says "running" but heartbeat is old
        var staleDaemon = new DaemonStatus
        {
            Pid = 6496,
            State = "running",
            ActiveJobs = 0,
            MaxParallel = 2,
            StartedAt = "2026-02-01T21:18:41.189871",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5).ToString("O"), // 5 minutes ago
        };

        // IsHealthy should be false because heartbeat is stale
        Assert.False(staleDaemon.IsHealthy);
        Assert.True(staleDaemon.IsRunning); // State says running

        // But a fresh daemon should be healthy
        var freshDaemon = new DaemonStatus
        {
            Pid = 1234,
            State = "running",
            ActiveJobs = 1,
            MaxParallel = 2,
            LastHeartbeat = DateTime.UtcNow.ToString("O"),
        };

        Assert.True(freshDaemon.IsHealthy);
        Assert.True(freshDaemon.IsRunning);
    }

    [Fact]
    public void DaemonStatus_IsHealthy_ReturnsFalse_WhenStopped()
    {
        var stoppedDaemon = new DaemonStatus
        {
            Pid = 1234,
            State = "stopped",
            LastHeartbeat = DateTime.UtcNow.ToString("O"), // Even fresh heartbeat
        };

        Assert.False(stoppedDaemon.IsHealthy);
        Assert.True(stoppedDaemon.IsStopped);
    }

    [Fact]
    public void Desktop_CanDetect_CrashedDaemon()
    {
        // This simulates what the Desktop sees when daemon crashes:
        // - daemon.json exists with state="running"
        // - But heartbeat is stale (daemon not updating it)
        // - Desktop should show "Daemon unhealthy" and offer restart

        const string crashedDaemonJson = """
            {
              "version": 1,
              "pid": 6496,
              "started_at": "2026-02-01T21:18:41.189871",
              "last_heartbeat": "2026-02-01T21:21:41.325737",
              "max_parallel": 2,
              "active_jobs": 0,
              "state": "running"
            }
            """;

        var status = JsonSerializer.Deserialize<DaemonStatus>(crashedDaemonJson, JsonOptions);

        Assert.NotNull(status);
        Assert.True(status.IsRunning); // State claims running
        Assert.False(status.IsHealthy); // But heartbeat is from the past (2026-02-01)

        // Desktop should use IsHealthy, not IsRunning, to determine if daemon needs restart
    }
}
