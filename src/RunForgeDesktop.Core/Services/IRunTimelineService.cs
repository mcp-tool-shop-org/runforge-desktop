using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Current state of the run timeline.
/// </summary>
public sealed class RunTimelineState
{
    /// <summary>All milestones in order.</summary>
    public required IReadOnlyList<RunMilestone> Milestones { get; init; }

    /// <summary>Index of the current active milestone (or -1 if none).</summary>
    public int ActiveIndex { get; init; } = -1;

    /// <summary>Detected epoch progress (if any).</summary>
    public (int Current, int Total)? EpochProgress { get; init; }

    /// <summary>Whether the run is complete (success or failure).</summary>
    public bool IsComplete => Milestones.Any(m =>
        m.IsReached && (m.Type == MilestoneType.Completed || m.Type == MilestoneType.Failed));
}

/// <summary>
/// Service for tracking run timeline milestones based on log content.
/// </summary>
public interface IRunTimelineService
{
    /// <summary>
    /// Creates a new timeline state for a run.
    /// </summary>
    RunTimelineState CreateTimeline();

    /// <summary>
    /// Updates timeline state based on new log lines.
    /// Returns the updated state.
    /// </summary>
    /// <param name="currentState">Current timeline state.</param>
    /// <param name="newLines">New log lines to process.</param>
    /// <returns>Updated timeline state.</returns>
    RunTimelineState ProcessLogLines(RunTimelineState currentState, IEnumerable<string> newLines);

    /// <summary>
    /// Sets the timeline to completed/failed state based on run result.
    /// </summary>
    /// <param name="currentState">Current timeline state.</param>
    /// <param name="isSuccess">Whether the run succeeded.</param>
    /// <returns>Updated timeline state.</returns>
    RunTimelineState SetCompleted(RunTimelineState currentState, bool isSuccess);
}
