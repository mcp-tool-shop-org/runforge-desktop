using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for comparing run requests and identifying differences.
/// </summary>
public interface IRunRequestComparer
{
    /// <summary>
    /// Compares the current request with its parent (if rerun_from is set).
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="currentRunDir">Current run directory (workspace-relative).</param>
    /// <param name="currentRequest">The current run request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diff result with list of differences.</returns>
    Task<RunRequestDiffResult> CompareWithParentAsync(
        string workspacePath,
        string currentRunDir,
        RunRequest currentRequest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two run requests directly.
    /// </summary>
    /// <param name="parent">The parent (original) request.</param>
    /// <param name="current">The current (modified) request.</param>
    /// <returns>List of differences.</returns>
    IReadOnlyList<DiffItem> Compare(RunRequest parent, RunRequest current);
}

/// <summary>
/// Default implementation of run request comparison.
/// </summary>
public sealed class RunRequestComparer : IRunRequestComparer
{
    private readonly IRunRequestService _requestService;

    public RunRequestComparer(IRunRequestService requestService)
    {
        _requestService = requestService;
    }

    /// <inheritdoc />
    public async Task<RunRequestDiffResult> CompareWithParentAsync(
        string workspacePath,
        string currentRunDir,
        RunRequest currentRequest,
        CancellationToken cancellationToken = default)
    {
        // Check if this is a rerun
        if (string.IsNullOrEmpty(currentRequest.RerunFrom))
        {
            return RunRequestDiffResult.NoParent;
        }

        try
        {
            // Build parent run directory path
            var parentRunDir = Path.Combine(
                workspacePath,
                ".ml", "runs",
                currentRequest.RerunFrom);

            // Load parent request
            var loadResult = await _requestService.LoadAsync(parentRunDir, cancellationToken);

            if (!loadResult.IsSuccess || loadResult.Value is null)
            {
                // Parent not found - return no parent result
                return new RunRequestDiffResult
                {
                    HasParent = false,
                    ParentRunId = currentRequest.RerunFrom,
                    Differences = Array.Empty<DiffItem>()
                };
            }

            var parentRequest = loadResult.Value;
            var differences = Compare(parentRequest, currentRequest);

            return new RunRequestDiffResult
            {
                HasParent = true,
                ParentRunId = currentRequest.RerunFrom,
                Differences = differences,
                ParentRequest = parentRequest,
                CurrentRequest = currentRequest
            };
        }
        catch
        {
            // If we can't load parent, return no parent
            return new RunRequestDiffResult
            {
                HasParent = false,
                ParentRunId = currentRequest.RerunFrom,
                Differences = Array.Empty<DiffItem>()
            };
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DiffItem> Compare(RunRequest parent, RunRequest current)
    {
        var differences = new List<DiffItem>();

        // Compare preset
        if (!string.Equals(parent.Preset, current.Preset, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "preset",
                ParentValue = parent.Preset ?? "(none)",
                CurrentValue = current.Preset ?? "(none)"
            });
        }

        // Compare dataset.path
        if (!string.Equals(parent.Dataset?.Path, current.Dataset?.Path, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "dataset.path",
                ParentValue = parent.Dataset?.Path ?? "(none)",
                CurrentValue = current.Dataset?.Path ?? "(none)"
            });
        }

        // Compare dataset.label_column
        if (!string.Equals(parent.Dataset?.LabelColumn, current.Dataset?.LabelColumn, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "dataset.label_column",
                ParentValue = parent.Dataset?.LabelColumn ?? "(none)",
                CurrentValue = current.Dataset?.LabelColumn ?? "(none)"
            });
        }

        // Compare model.family
        if (!string.Equals(parent.Model?.Family, current.Model?.Family, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "model.family",
                ParentValue = parent.Model?.Family ?? "(none)",
                CurrentValue = current.Model?.Family ?? "(none)"
            });
        }

        // Compare model.hyperparameters
        var parentHyperparams = SerializeHyperparameters(parent.Model?.Hyperparameters);
        var currentHyperparams = SerializeHyperparameters(current.Model?.Hyperparameters);
        if (!string.Equals(parentHyperparams, currentHyperparams, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "model.hyperparameters",
                ParentValue = parentHyperparams,
                CurrentValue = currentHyperparams
            });
        }

        // Compare device.type
        if (!string.Equals(parent.Device?.Type, current.Device?.Type, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "device.type",
                ParentValue = parent.Device?.Type ?? "(none)",
                CurrentValue = current.Device?.Type ?? "(none)"
            });
        }

        // Compare name
        if (!string.Equals(parent.Name, current.Name, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "name",
                ParentValue = parent.Name ?? "(none)",
                CurrentValue = current.Name ?? "(none)"
            });
        }

        // Compare notes
        if (!string.Equals(parent.Notes, current.Notes, StringComparison.Ordinal))
        {
            differences.Add(new DiffItem
            {
                Field = "notes",
                ParentValue = TruncateForDisplay(parent.Notes) ?? "(none)",
                CurrentValue = TruncateForDisplay(current.Notes) ?? "(none)"
            });
        }

        return differences;
    }

    private static string SerializeHyperparameters(Dictionary<string, JsonElement>? hyperparameters)
    {
        if (hyperparameters is null || hyperparameters.Count == 0)
        {
            return "(default)";
        }

        try
        {
            return JsonSerializer.Serialize(hyperparameters, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }
        catch
        {
            return "(error)";
        }
    }

    private static string? TruncateForDisplay(string? value, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
