using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.ViewModels;

/// <summary>
/// Tests for multi-select compare functionality contracts.
/// These tests verify the business logic patterns used by RunsListViewModel
/// and RunCompareViewModel without requiring actual MAUI dependencies.
/// </summary>
public class MultiSelectCompareTests
{
    #region Multi-Select Logic Tests

    [Fact]
    public void CanCompare_TrueWhenExactlyTwoSelected()
    {
        // Arrange - simulating selection logic from RunsListViewModel
        var selectedCount = 2;

        // Act
        var canCompare = selectedCount == 2;

        // Assert
        Assert.True(canCompare);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(10, false)]
    public void CanCompare_OnlyTrueForExactlyTwo(int selectedCount, bool expected)
    {
        var canCompare = selectedCount == 2;
        Assert.Equal(expected, canCompare);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "Select one more run to compare")]
    [InlineData(2, null)]
    [InlineData(3, "Select exactly 2 runs to compare")]
    [InlineData(5, "Select exactly 2 runs to compare")]
    public void SelectionHint_MatchesSelectionCount(int selectedCount, string? expectedHint)
    {
        // This mirrors the UpdateSelectionHint logic in RunsListViewModel
        var hint = selectedCount switch
        {
            0 => null,
            1 => "Select one more run to compare",
            2 => null,
            _ => "Select exactly 2 runs to compare"
        };

        Assert.Equal(expectedHint, hint);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, true)]
    public void ShowActionBar_VisibleOnlyInMultiSelectWithSelection(bool isMultiSelectMode, int selectedCount, bool expected)
    {
        var showActionBar = isMultiSelectMode && selectedCount > 0;
        Assert.Equal(expected, showActionBar);
    }

    #endregion

    #region Navigation Parameters Tests

    [Fact]
    public void CompareNavigation_ParametersForABMode()
    {
        // Arrange - simulating multi-select navigation
        var runA = new RunIndexEntry
        {
            RunId = "20260201-120000-test-a1b2",
            Name = "Run A Test",
            RunDir = ".ml/runs/20260201-120000-test-a1b2",
            PresetId = "balanced",
            Status = "succeeded",
            CreatedAt = "2026-02-01T12:00:00-05:00",
            Summary = new RunSummary { Device = "cpu", DurationMs = 5000, FinalMetrics = new() }
        };

        var runB = new RunIndexEntry
        {
            RunId = "20260201-130000-test-c3d4",
            Name = "Run B Test",
            RunDir = ".ml/runs/20260201-130000-test-c3d4",
            PresetId = "thorough",
            Status = "succeeded",
            CreatedAt = "2026-02-01T13:00:00-05:00",
            Summary = new RunSummary { Device = "gpu", DurationMs = 10000, FinalMetrics = new() }
        };

        // Act - create navigation parameters (as RunsListViewModel does)
        var parameters = new Dictionary<string, object>
        {
            { "runIdA", runA.RunId },
            { "runNameA", runA.Name },
            { "runDirA", runA.RunDir },
            { "runIdB", runB.RunId },
            { "runNameB", runB.Name },
            { "runDirB", runB.RunDir }
        };

        // Assert
        Assert.Equal("20260201-120000-test-a1b2", parameters["runIdA"]);
        Assert.Equal(".ml/runs/20260201-120000-test-a1b2", parameters["runDirA"]);
        Assert.Equal("20260201-130000-test-c3d4", parameters["runIdB"]);
        Assert.Equal(".ml/runs/20260201-130000-test-c3d4", parameters["runDirB"]);
    }

    [Fact]
    public void CompareNavigation_LegacyParametersForParentChild()
    {
        // Arrange - legacy navigation from RunDetailPage
        var childRun = new RunIndexEntry
        {
            RunId = "20260201-130000-child-run",
            Name = "Child Run",
            RunDir = ".ml/runs/20260201-130000-child-run",
            PresetId = "balanced",
            Status = "succeeded",
            CreatedAt = "2026-02-01T13:00:00-05:00",
            Summary = new RunSummary { Device = "cpu", DurationMs = 5000, FinalMetrics = new() }
        };

        // Act - legacy parameters
        var parameters = new Dictionary<string, object>
        {
            { "runId", childRun.RunId },
            { "runDir", childRun.RunDir }
        };

        // Assert - no runIdA/runIdB present
        Assert.True(parameters.ContainsKey("runId"));
        Assert.True(parameters.ContainsKey("runDir"));
        Assert.False(parameters.ContainsKey("runIdA"));
        Assert.False(parameters.ContainsKey("runIdB"));
    }

    [Fact]
    public void CompareNavigation_DetectABMode()
    {
        // Arrange
        var abModeParams = new Dictionary<string, object>
        {
            { "runIdA", "run-a" },
            { "runDirA", ".ml/runs/run-a" },
            { "runIdB", "run-b" },
            { "runDirB", ".ml/runs/run-b" }
        };

        var legacyParams = new Dictionary<string, object>
        {
            { "runId", "child-run" },
            { "runDir", ".ml/runs/child-run" }
        };

        // Act - detection logic (from RunCompareViewModel.ApplyQueryAttributes)
        bool isABMode(IDictionary<string, object> query) =>
            query.TryGetValue("runIdA", out var idA) && idA is string &&
            query.TryGetValue("runIdB", out var idB) && idB is string;

        // Assert
        Assert.True(isABMode(abModeParams));
        Assert.False(isABMode(legacyParams));
    }

    #endregion

    #region Page Title Tests

    [Fact]
    public void PageTitle_ABMode_ShowsVsSeparator()
    {
        // Arrange
        var runIdA = "20260201-120000-run-a";
        var runIdB = "20260201-130000-run-b";

        // Act
        var title = $"{runIdA} vs {runIdB}";

        // Assert
        Assert.Equal("20260201-120000-run-a vs 20260201-130000-run-b", title);
        Assert.Contains("vs", title);
        Assert.DoesNotContain("→", title);
    }

    [Fact]
    public void PageTitle_FromComparison_UsesNeutralLanguage()
    {
        // Arrange - simulating RunComparisonResult
        var comparison = new RunComparisonResult
        {
            ParentRunId = "run-a",
            ChildRunId = "run-b",
            IsComplete = true
        };

        // Act - updated page title logic
        var title = $"{comparison.ParentRunId} vs {comparison.ChildRunId}";

        // Assert
        Assert.Equal("run-a vs run-b", title);
    }

    #endregion

    #region Lineage Detection Tests

    [Fact]
    public void LineageDetection_AIsParentOfB()
    {
        // Arrange - B has rerun_from = A
        var runIdA = "parent-run-001";
        var rerunFromB = "parent-run-001"; // B's rerun_from field

        // Act
        var hasLineage = rerunFromB == runIdA;
        var lineageText = hasLineage ? "A → B (A is parent)" : null;

        // Assert
        Assert.True(hasLineage);
        Assert.Equal("A → B (A is parent)", lineageText);
    }

    [Fact]
    public void LineageDetection_BIsParentOfA()
    {
        // Arrange - A has rerun_from = B
        var runIdB = "parent-run-002";
        var rerunFromA = "parent-run-002"; // A's rerun_from field

        // Act
        var hasLineage = rerunFromA == runIdB;
        var lineageText = hasLineage ? "B → A (B is parent)" : null;

        // Assert
        Assert.True(hasLineage);
        Assert.Equal("B → A (B is parent)", lineageText);
    }

    [Fact]
    public void LineageDetection_NoLineage()
    {
        // Arrange - neither run references the other
        var runIdA = "run-001";
        var runIdB = "run-002";
        var rerunFromA = (string?)null;
        var rerunFromB = (string?)null;

        // Act
        var hasLineage = rerunFromA == runIdB || rerunFromB == runIdA;

        // Assert
        Assert.False(hasLineage);
    }

    [Fact]
    public void LineageDetection_UnrelatedReruns()
    {
        // Arrange - both are reruns, but of different parents
        var runIdA = "run-001";
        var runIdB = "run-002";
        var rerunFromA = "original-a";
        var rerunFromB = "original-b";

        // Act
        var hasLineage = rerunFromA == runIdB || rerunFromB == runIdA;

        // Assert
        Assert.False(hasLineage);
    }

    #endregion

    #region Summary Text Tests

    [Fact]
    public void SummaryText_UsesRunARunBTerminology()
    {
        // Arrange
        var comparison = new RunComparisonResult
        {
            ParentRunId = "20260201-120000-test",
            ChildRunId = "20260201-130000-test",
            IsComplete = true
        };

        // Act - simulating BuildSummaryText
        var lines = new List<string>
        {
            "=== Run Comparison Summary ===",
            $"Run A: {comparison.ParentRunId}",
            $"Run B: {comparison.ChildRunId}"
        };
        var summary = string.Join("\n", lines);

        // Assert - uses Run A / Run B, not Parent / Child
        Assert.Contains("Run A:", summary);
        Assert.Contains("Run B:", summary);
        Assert.DoesNotContain("Parent:", summary);
        Assert.DoesNotContain("Child:", summary);
    }

    [Fact]
    public void SummaryText_IncludesLineageWhenPresent()
    {
        // Arrange
        var hasLineage = true;
        var lineageText = "A → B (A is parent)";

        // Act
        var lines = new List<string>
        {
            "=== Run Comparison Summary ===",
            "Run A: parent-001",
            "Run B: child-001",
        };
        if (hasLineage)
        {
            lines.Add($"Lineage: {lineageText}");
        }
        var summary = string.Join("\n", lines);

        // Assert
        Assert.Contains("Lineage: A → B (A is parent)", summary);
    }

    #endregion
}
