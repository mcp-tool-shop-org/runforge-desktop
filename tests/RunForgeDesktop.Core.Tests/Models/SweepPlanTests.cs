using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

/// <summary>
/// Tests for SweepPlan and RunGroup model deserialization.
/// </summary>
public class SweepPlanTests
{
    #region SweepPlan Tests

    [Fact]
    public void SweepPlan_Deserialize_ValidJson()
    {
        var json = """
        {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "runforge-desktop@0.3.4",
            "workspace": "C:\\workspace",
            "group": {
                "name": "Test Sweep",
                "notes": "Test notes"
            },
            "base_request": {
                "version": 1,
                "preset": "balanced"
            },
            "strategy": {
                "type": "grid",
                "parameters": [
                    { "path": "model.hyperparameters.n_estimators", "values": [50, 100] }
                ]
            },
            "execution": {
                "max_parallel": 2,
                "fail_fast": false,
                "stop_on_cancel": true
            }
        }
        """;

        var plan = JsonSerializer.Deserialize<SweepPlan>(json);

        Assert.NotNull(plan);
        Assert.Equal(1, plan.Version);
        Assert.Equal("sweep_plan", plan.Kind);
        Assert.Equal("Test Sweep", plan.Group.Name);
        Assert.Equal("Test notes", plan.Group.Notes);
        Assert.Equal("grid", plan.Strategy.Type);
        Assert.Single(plan.Strategy.Parameters);
        Assert.Equal(2, plan.Execution.MaxParallel);
        Assert.False(plan.Execution.FailFast);
        Assert.True(plan.Execution.StopOnCancel);
    }

    [Fact]
    public void SweepPlan_Strategy_GridType()
    {
        var json = """
        {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test",
            "workspace": "/workspace",
            "group": { "name": "Test" },
            "base_request": {},
            "strategy": {
                "type": "grid",
                "parameters": [
                    { "path": "model.family", "values": ["rf", "xgb"] },
                    { "path": "model.hyperparameters.n_estimators", "values": [50, 100, 200] }
                ]
            },
            "execution": { "max_parallel": 4, "fail_fast": true, "stop_on_cancel": true }
        }
        """;

        var plan = JsonSerializer.Deserialize<SweepPlan>(json);

        Assert.NotNull(plan);
        Assert.Equal("grid", plan.Strategy.Type);
        Assert.Equal(2, plan.Strategy.Parameters.Count);

        // Check parameter paths
        Assert.Equal("model.family", plan.Strategy.Parameters[0].Path);
        Assert.Equal("model.hyperparameters.n_estimators", plan.Strategy.Parameters[1].Path);
    }

    [Fact]
    public void SweepPlan_NullNotes_IsOptional()
    {
        var json = """
        {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test",
            "workspace": "/workspace",
            "group": { "name": "Test" },
            "base_request": {},
            "strategy": { "type": "grid", "parameters": [] },
            "execution": { "max_parallel": 1, "fail_fast": false, "stop_on_cancel": true }
        }
        """;

        var plan = JsonSerializer.Deserialize<SweepPlan>(json);

        Assert.NotNull(plan);
        Assert.Null(plan.Group.Notes);
    }

    #endregion

    #region RunGroup Tests

    [Fact]
    public void RunGroup_Deserialize_ValidJson()
    {
        var json = """
        {
            "version": 1,
            "kind": "run_group",
            "group_id": "grp_20260201_150000_test",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "runforge-cli@0.3.4",
            "name": "Test Group",
            "notes": "Test notes",
            "plan_ref": "plan.json",
            "status": "running",
            "execution": {
                "max_parallel": 2,
                "started_at": "2026-02-01T15:00:10Z",
                "finished_at": null,
                "cancelled": false
            },
            "runs": [
                {
                    "run_id": "run_001",
                    "status": "succeeded",
                    "request_overrides": { "model.hyperparameters.n_estimators": 50 },
                    "result_ref": ".runforge/runs/run_001/result.json",
                    "primary_metric": { "name": "accuracy", "value": 0.9112 }
                }
            ],
            "summary": {
                "total": 2,
                "succeeded": 1,
                "failed": 0,
                "canceled": 0,
                "best_run_id": "run_001",
                "best_primary_metric": { "name": "accuracy", "value": 0.9112 }
            }
        }
        """;

        var group = JsonSerializer.Deserialize<RunGroup>(json);

        Assert.NotNull(group);
        Assert.Equal(1, group.Version);
        Assert.Equal("run_group", group.Kind);
        Assert.Equal("grp_20260201_150000_test", group.GroupId);
        Assert.Equal("Test Group", group.Name);
        Assert.Equal("running", group.Status);
        Assert.True(group.IsRunning);
        Assert.False(group.IsComplete);
    }

    [Fact]
    public void RunGroup_ExecutionMetadata()
    {
        var json = """
        {
            "version": 1,
            "kind": "run_group",
            "group_id": "grp_001",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test",
            "name": "Test",
            "status": "completed",
            "execution": {
                "max_parallel": 4,
                "started_at": "2026-02-01T15:00:00Z",
                "finished_at": "2026-02-01T15:10:00Z",
                "cancelled": false
            },
            "runs": [],
            "summary": { "total": 0, "succeeded": 0, "failed": 0, "canceled": 0 }
        }
        """;

        var group = JsonSerializer.Deserialize<RunGroup>(json);

        Assert.NotNull(group);
        Assert.Equal(4, group.Execution.MaxParallel);
        Assert.Equal("2026-02-01T15:00:00Z", group.Execution.StartedAt);
        Assert.Equal("2026-02-01T15:10:00Z", group.Execution.FinishedAt);
        Assert.False(group.Execution.Cancelled);
    }

    [Fact]
    public void RunGroup_StatusHelpers()
    {
        var running = new RunGroup
        {
            Version = 1,
            Kind = "run_group",
            GroupId = "grp_001",
            CreatedAt = "2026-02-01T15:00:00Z",
            CreatedBy = "test",
            Name = "Test",
            Status = "running",
            Execution = new GroupExecution
            {
                MaxParallel = 1,
                StartedAt = "2026-02-01T15:00:00Z",
                Cancelled = false
            },
            Summary = new GroupSummary
            {
                Total = 2,
                Succeeded = 0,
                Failed = 0,
                Canceled = 0
            }
        };

        Assert.True(running.IsRunning);
        Assert.False(running.IsComplete);

        var completed = running with { Status = "completed" };
        Assert.False(completed.IsRunning);
        Assert.True(completed.IsComplete);

        var failed = running with { Status = "failed" };
        Assert.False(failed.IsRunning);
        Assert.True(failed.IsComplete);

        var canceled = running with { Status = "canceled" };
        Assert.False(canceled.IsRunning);
        Assert.True(canceled.IsComplete);
    }

    [Fact]
    public void GroupRunEntry_StatusHelpers()
    {
        var pending = new GroupRunEntry
        {
            RunId = "run_001",
            Status = "pending"
        };

        Assert.False(pending.IsComplete);
        Assert.False(pending.IsSucceeded);

        var succeeded = pending with { Status = "succeeded" };
        Assert.True(succeeded.IsComplete);
        Assert.True(succeeded.IsSucceeded);

        var failed = pending with { Status = "failed" };
        Assert.True(failed.IsComplete);
        Assert.False(failed.IsSucceeded);
    }

    [Fact]
    public void GroupSummary_PendingCalculation()
    {
        var summary = new GroupSummary
        {
            Total = 10,
            Succeeded = 5,
            Failed = 2,
            Canceled = 1
        };

        // Pending = Total - Succeeded - Failed - Canceled = 10 - 5 - 2 - 1 = 2
        Assert.Equal(2, summary.Pending);
    }

    [Fact]
    public void GroupSummary_BestRunOptional()
    {
        var json = """
        {
            "total": 5,
            "succeeded": 0,
            "failed": 5,
            "canceled": 0,
            "best_run_id": null,
            "best_primary_metric": null
        }
        """;

        var summary = JsonSerializer.Deserialize<GroupSummary>(json);

        Assert.NotNull(summary);
        Assert.Null(summary.BestRunId);
        Assert.Null(summary.BestPrimaryMetric);
    }

    [Fact]
    public void GroupRunEntry_RequestOverrides()
    {
        var json = """
        {
            "run_id": "run_001",
            "status": "succeeded",
            "request_overrides": {
                "model.family": "xgboost",
                "model.hyperparameters.n_estimators": 100,
                "model.hyperparameters.max_depth": null
            }
        }
        """;

        var entry = JsonSerializer.Deserialize<GroupRunEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal(3, entry.RequestOverrides.Count);
        Assert.True(entry.RequestOverrides.ContainsKey("model.family"));
    }

    #endregion
}
