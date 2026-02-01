using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

public class RunIndexEntryTests
{
    private const string ValidIndexJson = """
    [
        {
            "run_id": "20260201-142355-test-run-a3f9",
            "created_at": "2026-02-01T14:23:55-05:00",
            "name": "Test Run",
            "preset_id": "std-train",
            "status": "succeeded",
            "run_dir": ".ml/runs/20260201-142355-test-run-a3f9",
            "summary": {
                "duration_ms": 5432,
                "final_metrics": {
                    "accuracy": 0.95,
                    "loss": 0.12
                },
                "device": "cuda"
            }
        }
    ]
    """;

    [Fact]
    public void Deserialize_ValidJson_ReturnsEntry()
    {
        // Act
        var entries = JsonSerializer.Deserialize<List<RunIndexEntry>>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(entries);
        Assert.Single(entries);

        var entry = entries[0];
        Assert.Equal("20260201-142355-test-run-a3f9", entry.RunId);
        Assert.Equal("2026-02-01T14:23:55-05:00", entry.CreatedAt);
        Assert.Equal("Test Run", entry.Name);
        Assert.Equal("std-train", entry.PresetId);
        Assert.Equal("succeeded", entry.Status);
        Assert.True(entry.IsSucceeded);
        Assert.Equal(".ml/runs/20260201-142355-test-run-a3f9", entry.RunDir);
    }

    [Fact]
    public void Deserialize_ValidJson_ParsesSummary()
    {
        // Act
        var entries = JsonSerializer.Deserialize<List<RunIndexEntry>>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(entries);
        var summary = entries[0].Summary;

        Assert.Equal(5432, summary.DurationMs);
        Assert.Equal("cuda", summary.Device);
        Assert.Equal(2, summary.FinalMetrics.Count);
        Assert.Equal(0.95, summary.FinalMetrics["accuracy"]);
        Assert.Equal(0.12, summary.FinalMetrics["loss"]);
    }

    [Fact]
    public void ParsedCreatedAt_ValidTimestamp_ReturnsDateTimeOffset()
    {
        // Arrange
        var entries = JsonSerializer.Deserialize<List<RunIndexEntry>>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(entries);
        var parsed = entries[0].ParsedCreatedAt;

        Assert.NotNull(parsed);
        Assert.Equal(2026, parsed.Value.Year);
        Assert.Equal(2, parsed.Value.Month);
        Assert.Equal(1, parsed.Value.Day);
        Assert.Equal(14, parsed.Value.Hour);
        Assert.Equal(23, parsed.Value.Minute);
        Assert.Equal(55, parsed.Value.Second);
    }

    [Fact]
    public void Duration_ReturnsTimeSpan()
    {
        // Arrange
        var entries = JsonSerializer.Deserialize<List<RunIndexEntry>>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(entries);
        var duration = entries[0].Summary.Duration;

        Assert.Equal(5432, duration.TotalMilliseconds);
    }

    [Fact]
    public void Deserialize_FailedRun_IsSucceededFalse()
    {
        // Arrange
        var json = """
        [
            {
                "run_id": "20260201-142355-failed-b2d7",
                "created_at": "2026-02-01T14:23:55-05:00",
                "name": "Failed Run",
                "preset_id": "hq-train",
                "status": "failed",
                "run_dir": ".ml/runs/20260201-142355-failed-b2d7",
                "summary": {
                    "duration_ms": 1234,
                    "final_metrics": {},
                    "device": "cpu"
                }
            }
        ]
        """;

        // Act
        var entries = JsonSerializer.Deserialize<List<RunIndexEntry>>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(entries);
        Assert.False(entries[0].IsSucceeded);
        Assert.Empty(entries[0].Summary.FinalMetrics);
    }
}
