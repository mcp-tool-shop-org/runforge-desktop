using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of <see cref="IExecutionQueueService"/>.
/// </summary>
public sealed partial class ExecutionQueueService : IExecutionQueueService
{
    private readonly IPythonDiscoveryService _pythonService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public ExecutionQueueService(IPythonDiscoveryService pythonService)
    {
        _pythonService = pythonService;
    }

    /// <inheritdoc />
    public async Task<ExecutionQueue> LoadQueueAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var queueFile = Path.Combine(workspacePath, ".runforge", "queue", "queue.json");
        if (!File.Exists(queueFile))
        {
            return new ExecutionQueue();
        }

        try
        {
            var json = await File.ReadAllTextAsync(queueFile, cancellationToken);
            return JsonSerializer.Deserialize<ExecutionQueue>(json, JsonOptions) ?? new ExecutionQueue();
        }
        catch
        {
            return new ExecutionQueue();
        }
    }

    /// <inheritdoc />
    public async Task<DaemonStatus> LoadDaemonStatusAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var daemonFile = Path.Combine(workspacePath, ".runforge", "queue", "daemon.json");
        if (!File.Exists(daemonFile))
        {
            return new DaemonStatus();
        }

        try
        {
            var json = await File.ReadAllTextAsync(daemonFile, cancellationToken);
            return JsonSerializer.Deserialize<DaemonStatus>(json, JsonOptions) ?? new DaemonStatus();
        }
        catch
        {
            return new DaemonStatus();
        }
    }

    /// <inheritdoc />
    public async Task<bool> StartDaemonAsync(string workspacePath, int maxParallel = 2, CancellationToken cancellationToken = default)
    {
        var pythonPath = _pythonService.PythonPath;
        if (string.IsNullOrEmpty(pythonPath))
        {
            return false;
        }

        // Check if daemon is already running
        var status = await LoadDaemonStatusAsync(workspacePath, cancellationToken);
        if (status.IsRunning && status.IsHealthy)
        {
            return true; // Already running
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"-m runforge_cli daemon --workspace \"{workspacePath}\" --max-parallel {maxParallel}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            // Wait a bit for daemon to start
            await Task.Delay(1000, cancellationToken);

            // Check if daemon started successfully
            status = await LoadDaemonStatusAsync(workspacePath, cancellationToken);
            return status.IsRunning;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StopDaemonAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var status = await LoadDaemonStatusAsync(workspacePath, cancellationToken);
        if (!status.IsRunning || status.Pid == 0)
        {
            return true; // Already stopped
        }

        try
        {
            var process = Process.GetProcessById(status.Pid);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch (ArgumentException)
        {
            // Process not found - already stopped
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnqueueRunAsync(
        string workspacePath,
        string runId,
        string? groupId = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        var exitCode = await RunCliCommandAsync(
            workspacePath,
            BuildEnqueueRunArgs(workspacePath, runId, groupId, priority),
            cancellationToken);
        return exitCode == 0;
    }

    private static string BuildEnqueueRunArgs(string workspacePath, string runId, string? groupId, int priority)
    {
        var args = $"enqueue-run --run-id \"{runId}\" --workspace \"{workspacePath}\"";
        if (!string.IsNullOrEmpty(groupId))
        {
            args += $" --group-id \"{groupId}\"";
        }
        if (priority != 0)
        {
            args += $" --priority {priority}";
        }
        return args;
    }

    /// <inheritdoc />
    public async Task<string?> EnqueueSweepAsync(
        string planPath,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var args = $"enqueue-sweep --plan \"{planPath}\"";
        if (!string.IsNullOrEmpty(workspacePath))
        {
            args += $" --workspace \"{workspacePath}\"";
        }

        var output = new List<string>();
        var exitCode = await RunCliCommandAsync(
            workspacePath ?? Path.GetDirectoryName(planPath) ?? ".",
            args,
            cancellationToken,
            line => output.Add(line));

        if (exitCode != 0)
        {
            return null;
        }

        // Parse group ID from output: [RF:GROUP=ENQUEUED grp_xxx runs=N]
        foreach (var line in output)
        {
            var match = GroupEnqueuedRegex().Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\[RF:GROUP=ENQUEUED\s+(\S+)")]
    private static partial Regex GroupEnqueuedRegex();

    /// <inheritdoc />
    public async Task<bool> PauseGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default)
    {
        var exitCode = await RunCliCommandAsync(
            workspacePath,
            $"pause-group --group-id \"{groupId}\" --workspace \"{workspacePath}\"",
            cancellationToken);
        return exitCode == 0;
    }

    /// <inheritdoc />
    public async Task<bool> ResumeGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default)
    {
        var exitCode = await RunCliCommandAsync(
            workspacePath,
            $"resume-group --group-id \"{groupId}\" --workspace \"{workspacePath}\"",
            cancellationToken);
        return exitCode == 0;
    }

    /// <inheritdoc />
    public async Task<int> RetryFailedAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default)
    {
        var output = new List<string>();
        var exitCode = await RunCliCommandAsync(
            workspacePath,
            $"retry-failed --group-id \"{groupId}\" --workspace \"{workspacePath}\"",
            cancellationToken,
            line => output.Add(line));

        if (exitCode != 0)
        {
            return 0;
        }

        // Parse count from output: "Re-enqueued N failed runs"
        foreach (var line in output)
        {
            var match = RetryCountRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }
        }

        return 0;
    }

    [GeneratedRegex(@"Re-enqueued\s+(\d+)")]
    private static partial Regex RetryCountRegex();

    /// <inheritdoc />
    public async Task<int> CancelGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default)
    {
        var output = new List<string>();
        var exitCode = await RunCliCommandAsync(
            workspacePath,
            $"cancel-group --group-id \"{groupId}\" --workspace \"{workspacePath}\"",
            cancellationToken,
            line => output.Add(line));

        if (exitCode != 0)
        {
            return 0;
        }

        // Parse count from output: "Canceled N queued runs"
        foreach (var line in output)
        {
            var match = CancelCountRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }
        }

        return 0;
    }

    [GeneratedRegex(@"Canceled\s+(\d+)")]
    private static partial Regex CancelCountRegex();

    /// <inheritdoc />
    public async Task<QueueStatusSummary> GetQueueStatusAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var queueTask = LoadQueueAsync(workspacePath, cancellationToken);
        var daemonTask = LoadDaemonStatusAsync(workspacePath, cancellationToken);
        var pausedTask = GetPausedGroupsAsync(workspacePath, cancellationToken);

        await Task.WhenAll(queueTask, daemonTask, pausedTask);

        var queue = await queueTask;
        var daemon = await daemonTask;
        var paused = await pausedTask;

        return new QueueStatusSummary
        {
            MaxParallel = queue.MaxParallel,
            TotalJobs = queue.Jobs.Count,
            QueuedCount = queue.QueuedJobs.Count(),
            RunningCount = queue.RunningJobs.Count(),
            SucceededCount = queue.SucceededJobs.Count(),
            FailedCount = queue.FailedJobs.Count(),
            CanceledCount = queue.CanceledJobs.Count(),
            DaemonStatus = daemon,
            PausedGroups = paused,
        };
    }

    private async Task<IReadOnlyList<string>> GetPausedGroupsAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var groupsDir = Path.Combine(workspacePath, ".runforge", "groups");
        if (!Directory.Exists(groupsDir))
        {
            return [];
        }

        var paused = new List<string>();

        foreach (var groupDir in Directory.GetDirectories(groupsDir))
        {
            var groupFile = Path.Combine(groupDir, "group.json");
            if (!File.Exists(groupFile))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(groupFile, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("paused", out var pausedProp) && pausedProp.GetBoolean())
                {
                    paused.Add(Path.GetFileName(groupDir));
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        return paused;
    }

    private async Task<int> RunCliCommandAsync(
        string workspacePath,
        string arguments,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null)
    {
        var pythonPath = _pythonService.PythonPath;
        if (string.IsNullOrEmpty(pythonPath))
        {
            return -1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"-m runforge_cli {arguments}",
            WorkingDirectory = workspacePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                onOutput?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                onOutput?.Invoke(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors
            }
            return -1;
        }
    }
}
