using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Default implementation of sweep service.
/// </summary>
public sealed class SweepService : ISweepService
{
    private readonly IPythonDiscoveryService _pythonService;
    private SweepExecutionState? _currentExecution;
    private Process? _currentProcess;
    private readonly object _lock = new();

    public SweepService(IPythonDiscoveryService pythonService)
    {
        _pythonService = pythonService;
    }

    public SweepExecutionState? CurrentExecution => _currentExecution;

    public SweepPlan CreatePlan(
        string workspacePath,
        string groupName,
        string? notes,
        JsonElement baseRequest,
        IReadOnlyList<SweepParameterConfig> parameters,
        int maxParallel = 2)
    {
        var sweepParams = parameters.Select(p => new SweepParameter
        {
            Path = p.Path,
            Values = JsonSerializer.SerializeToElement(p.Values)
        }).ToList();

        return new SweepPlan
        {
            Version = 1,
            Kind = "sweep_plan",
            CreatedAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            CreatedBy = $"runforge-desktop@{GetVersion()}",
            Workspace = workspacePath,
            Group = new SweepGroupInfo
            {
                Name = groupName,
                Notes = notes
            },
            BaseRequest = baseRequest,
            Strategy = new SweepStrategy
            {
                Type = "grid",
                Parameters = sweepParams
            },
            Execution = new SweepExecution
            {
                MaxParallel = maxParallel,
                FailFast = false,
                StopOnCancel = true
            }
        };
    }

    public async Task SavePlanAsync(SweepPlan plan, string outputPath, CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonSerializer.Serialize(plan, options);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    public async Task<SweepPlan> LoadPlanAsync(string planPath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(planPath, cancellationToken);
        var plan = JsonSerializer.Deserialize<SweepPlan>(json, Json.JsonOptions.Default);

        if (plan is null)
        {
            throw new InvalidOperationException("Failed to deserialize sweep plan");
        }

        return plan;
    }

    public async Task<SweepExecutionResult> ExecuteSweepAsync(
        string planPath,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        // Check Python availability
        var pythonPath = _pythonService.PythonPath;
        if (string.IsNullOrEmpty(pythonPath))
        {
            return new SweepExecutionResult
            {
                ExitCode = -1,
                ErrorMessage = "Python not found",
                WasCancelled = false,
                DurationMs = 0
            };
        }

        var stopwatch = Stopwatch.StartNew();
        string? groupId = null;

        // Create execution state
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var state = new SweepExecutionState
        {
            PlanPath = planPath,
            StartedAt = DateTime.Now,
            CancellationSource = cts
        };

        lock (_lock)
        {
            _currentExecution = state;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"-m runforge_cli sweep --plan \"{planPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(planPath) ?? Environment.CurrentDirectory
            };

            using var process = new Process { StartInfo = startInfo };

            // Capture output
            var outputLines = new List<string>();
            var groupStartRegex = new Regex(@"\[RF:GROUP=START\s+(\S+)\s+runs=(\d+)\]");
            var groupRunDoneRegex = new Regex(@"\[RF:GROUP=RUN_DONE\s+\S+\s+status=\S+\]");

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is null) return;

                outputLines.Add(e.Data);
                onOutput?.Invoke(e.Data);

                // Parse group ID from start token
                var startMatch = groupStartRegex.Match(e.Data);
                if (startMatch.Success)
                {
                    groupId = startMatch.Groups[1].Value;
                    state.GroupId = groupId;
                    if (int.TryParse(startMatch.Groups[2].Value, out var totalRuns))
                    {
                        state.TotalRuns = totalRuns;
                    }
                }

                // Track progress
                if (groupRunDoneRegex.IsMatch(e.Data))
                {
                    state.RunsCompleted++;
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null) return;
                outputLines.Add(e.Data);
                onOutput?.Invoke(e.Data);
            };

            process.Start();
            state.ProcessId = process.Id;
            _currentProcess = process;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion or cancellation
            while (!process.HasExited)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Ignore kill errors
                    }
                    break;
                }

                await Task.Delay(100, CancellationToken.None);
            }

            await process.WaitForExitAsync(CancellationToken.None);

            stopwatch.Stop();

            var wasCancelled = cts.Token.IsCancellationRequested;
            var errorMessage = process.ExitCode != 0
                ? outputLines.LastOrDefault(l => l.Contains("ERROR:"))
                : null;

            return new SweepExecutionResult
            {
                ExitCode = wasCancelled ? -1 : process.ExitCode,
                GroupId = groupId,
                ErrorMessage = errorMessage,
                DurationMs = stopwatch.ElapsedMilliseconds,
                WasCancelled = wasCancelled
            };
        }
        finally
        {
            lock (_lock)
            {
                _currentExecution = null;
                _currentProcess = null;
            }
            cts.Dispose();
        }
    }

    public async Task<IReadOnlyList<RunGroup>> ListGroupsAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var groupsDir = Path.Combine(workspacePath, ".runforge", "groups");
        if (!Directory.Exists(groupsDir))
        {
            return Array.Empty<RunGroup>();
        }

        var groups = new List<RunGroup>();

        foreach (var dir in Directory.GetDirectories(groupsDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupFile = Path.Combine(dir, "group.json");
            if (!File.Exists(groupFile)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(groupFile, cancellationToken);
                var group = JsonSerializer.Deserialize<RunGroup>(json, Json.JsonOptions.Default);
                if (group is not null)
                {
                    groups.Add(group);
                }
            }
            catch
            {
                // Skip invalid group files
            }
        }

        // Sort by created_at descending (newest first)
        return groups
            .OrderByDescending(g => g.CreatedAt)
            .ToList();
    }

    public async Task<RunGroup?> LoadGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default)
    {
        var groupFile = Path.Combine(workspacePath, ".runforge", "groups", groupId, "group.json");
        if (!File.Exists(groupFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(groupFile, cancellationToken);
        return JsonSerializer.Deserialize<RunGroup>(json, Json.JsonOptions.Default);
    }

    public void CancelCurrentExecution()
    {
        lock (_lock)
        {
            _currentExecution?.CancellationSource?.Cancel();

            try
            {
                _currentProcess?.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors
            }
        }
    }

    public int CalculateGridSize(IReadOnlyList<SweepParameterConfig> parameters)
    {
        if (parameters.Count == 0) return 0;

        var total = 1;
        foreach (var param in parameters)
        {
            total *= param.Values.Count;
        }
        return total;
    }

    private static string GetVersion()
    {
        var assembly = typeof(SweepService).Assembly;
        var version = assembly.GetName().Version;
        return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.3.4";
    }
}
