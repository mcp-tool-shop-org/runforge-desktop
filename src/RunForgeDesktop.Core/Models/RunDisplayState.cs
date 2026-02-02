namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Display state for a run in the Visual Activity System.
/// Combines queue state with result status for unified visualization.
/// </summary>
public enum RunDisplayState
{
    /// <summary>Run is in queue, waiting to execute. Color: Amber.</summary>
    Queued,

    /// <summary>Run is currently executing. Color: Blue (animated).</summary>
    Running,

    /// <summary>Run completed successfully. Color: Gray.</summary>
    Completed,

    /// <summary>Run failed. Color: Red.</summary>
    Failed,

    /// <summary>Run is marked as running but no recent activity. Color: Pulsing amber.</summary>
    Stalled,

    /// <summary>Run state cannot be determined. Color: Gray.</summary>
    Unknown
}
