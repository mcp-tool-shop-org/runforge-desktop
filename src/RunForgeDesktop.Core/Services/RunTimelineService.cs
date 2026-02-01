using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of run timeline tracking.
/// </summary>
public sealed class RunTimelineService : IRunTimelineService
{
    /// <inheritdoc />
    public RunTimelineState CreateTimeline()
    {
        var milestones = MilestonePatterns.CreateDefaultMilestones();
        return new RunTimelineState
        {
            Milestones = milestones,
            ActiveIndex = -1,
            EpochProgress = null
        };
    }

    /// <inheritdoc />
    public RunTimelineState ProcessLogLines(RunTimelineState currentState, IEnumerable<string> newLines)
    {
        // Create mutable copy of milestones
        var milestones = currentState.Milestones
            .Select(m => new RunMilestone
            {
                Type = m.Type,
                Name = m.Name,
                IsReached = m.IsReached,
                ReachedAtUtc = m.ReachedAtUtc,
                TriggerLine = m.TriggerLine,
                IsActive = false // Reset active state, will be set later
            })
            .ToList();

        var epochProgress = currentState.EpochProgress;
        var now = DateTime.UtcNow;

        foreach (var line in newLines)
        {
            // Detect milestone
            var detectedType = MilestonePatterns.DetectMilestone(line);
            if (detectedType.HasValue)
            {
                var milestone = milestones.FirstOrDefault(m => m.Type == detectedType.Value);
                if (milestone is not null && !milestone.IsReached)
                {
                    milestone.IsReached = true;
                    milestone.ReachedAtUtc = now;
                    milestone.TriggerLine = line.Length > 200 ? line[..200] + "..." : line;
                }
            }

            // Check for epoch progress
            var progress = MilestonePatterns.ExtractEpochProgress(line);
            if (progress.HasValue)
            {
                epochProgress = progress;
            }
        }

        // Determine active milestone (last reached that isn't complete/failed)
        var activeIndex = -1;
        for (int i = milestones.Count - 1; i >= 0; i--)
        {
            if (milestones[i].IsReached)
            {
                // If it's a terminal state, no active milestone
                if (milestones[i].Type == MilestoneType.Completed ||
                    milestones[i].Type == MilestoneType.Failed)
                {
                    activeIndex = -1;
                }
                else
                {
                    activeIndex = i;
                    milestones[i].IsActive = true;
                }
                break;
            }
        }

        return new RunTimelineState
        {
            Milestones = milestones,
            ActiveIndex = activeIndex,
            EpochProgress = epochProgress
        };
    }

    /// <inheritdoc />
    public RunTimelineState SetCompleted(RunTimelineState currentState, bool isSuccess)
    {
        var milestones = currentState.Milestones
            .Select(m => new RunMilestone
            {
                Type = m.Type,
                Name = m.Name,
                IsReached = m.IsReached,
                ReachedAtUtc = m.ReachedAtUtc,
                TriggerLine = m.TriggerLine,
                IsActive = false
            })
            .ToList();

        var now = DateTime.UtcNow;

        if (isSuccess)
        {
            // Mark all milestones up to Completed as reached
            foreach (var milestone in milestones)
            {
                if (milestone.Type == MilestoneType.Failed)
                    continue;

                if (!milestone.IsReached)
                {
                    milestone.IsReached = true;
                    milestone.ReachedAtUtc = now;
                }
            }
        }
        else
        {
            // Mark Failed as reached
            var failedMilestone = milestones.FirstOrDefault(m => m.Type == MilestoneType.Failed);
            if (failedMilestone is not null && !failedMilestone.IsReached)
            {
                failedMilestone.IsReached = true;
                failedMilestone.ReachedAtUtc = now;
            }
        }

        return new RunTimelineState
        {
            Milestones = milestones,
            ActiveIndex = -1, // No active milestone when complete
            EpochProgress = currentState.EpochProgress
        };
    }
}
