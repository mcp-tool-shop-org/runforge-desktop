using System.Diagnostics;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of CLI execution service.
/// Spawns runforge-cli as a subprocess and streams output.
/// Uses PythonDiscoveryService for reliable Python location.
/// </summary>
public sealed class CliExecutionService : ICliExecutionService
{
    private readonly IPythonDiscoveryService _pythonDiscovery;

    private CliExecutionState? _currentExecution;
    private Process? _currentProcess;
    private readonly object _lock = new();

    public CliExecutionService(IPythonDiscoveryService pythonDiscovery)
    {
        _pythonDiscovery = pythonDiscovery;
    }

    /// <inheritdoc />
    public bool IsCliAvailable { get; private set; }

    /// <inheritdoc />
    public string? CliUnavailableReason { get; private set; }

    /// <inheritdoc />
    public CliExecutionState? CurrentExecution => _currentExecution;

    /// <inheritdoc />
    public async Task<bool> CheckCliAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Discover Python installation
            // TODO: Load preferred path from settings
            var pythonFound = await _pythonDiscovery.DiscoverAsync(preferredPath: null, cancellationToken);
            if (!pythonFound)
            {
                CliUnavailableReason = _pythonDiscovery.UnavailableReason ?? "Python 3.10+ not found";
                IsCliAvailable = false;
                return false;
            }

            var pythonPath = _pythonDiscovery.PythonPath!;

            // Check if runforge-cli is available
            var cliResult = await RunProcessAsync(pythonPath, "-m runforge_cli --version", cancellationToken);
            if (cliResult.ExitCode != 0)
            {
                CliUnavailableReason = "runforge-cli is not installed. Run: pip install -e src/runforge-cli";
                IsCliAvailable = false;
                return false;
            }

            CliUnavailableReason = null;
            IsCliAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            CliUnavailableReason = $"Failed to check CLI availability: {ex.Message}";
            IsCliAvailable = false;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<CliExecutionResult> ExecuteRunAsync(
        string workspacePath,
        string runDir,
        Action<string>? onOutput = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        // Ensure Python is discovered
        if (!_pythonDiscovery.IsPythonAvailable)
        {
            return new CliExecutionResult
            {
                ExitCode = 4,
                ErrorMessage = "Python not available. Call CheckCliAvailabilityAsync first."
            };
        }

        var pythonPath = _pythonDiscovery.PythonPath!;

        // Build full run path
        var fullRunPath = Path.Combine(
            workspacePath,
            runDir.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(fullRunPath))
        {
            return new CliExecutionResult
            {
                ExitCode = 3,
                ErrorMessage = $"Run directory not found: {fullRunPath}"
            };
        }

        // Create execution state
        var executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var state = new CliExecutionState
        {
            RunDir = runDir,
            StartedAt = DateTime.UtcNow,
            CancellationSource = executionCts
        };

        lock (_lock)
        {
            if (_currentExecution != null)
            {
                return new CliExecutionResult
                {
                    ExitCode = 4,
                    ErrorMessage = "Another execution is already in progress"
                };
            }
            _currentExecution = state;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build CLI arguments
            var args = $"-m runforge_cli run --run-dir \"{fullRunPath}\" --workspace \"{workspacePath}\"";
            if (dryRun)
            {
                args += " --dry-run";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspacePath
            };

            using var process = new Process { StartInfo = startInfo };

            // Set up output handling
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    onOutput?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    onOutput?.Invoke($"ERROR: {e.Data}");
                }
            };

            process.Start();
            state.ProcessId = process.Id;
            _currentProcess = process;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to exit or cancellation
            try
            {
                await process.WaitForExitAsync(executionCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Try to kill the process gracefully
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill errors
                }

                return new CliExecutionResult
                {
                    ExitCode = -1,
                    WasCancelled = true,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Execution cancelled by user"
                };
            }

            stopwatch.Stop();

            return new CliExecutionResult
            {
                ExitCode = process.ExitCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = process.ExitCode != 0
                    ? $"CLI exited with code {process.ExitCode}"
                    : null
            };
        }
        catch (Exception ex)
        {
            return new CliExecutionResult
            {
                ExitCode = 4,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Failed to execute CLI: {ex.Message}"
            };
        }
        finally
        {
            lock (_lock)
            {
                _currentExecution = null;
                _currentProcess = null;
            }
            executionCts.Dispose();
        }
    }

    /// <inheritdoc />
    public void CancelCurrentExecution()
    {
        lock (_lock)
        {
            _currentExecution?.CancellationSource?.Cancel();

            // Also try to kill the process directly
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

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, output);
    }
}
