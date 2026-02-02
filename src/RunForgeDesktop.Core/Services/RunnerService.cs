using System.Diagnostics;
using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Spawns and manages training runner processes.
/// </summary>
public class RunnerService : IRunnerService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IPythonDiscoveryService _pythonDiscovery;
    private readonly Dictionary<string, Process> _runningProcesses = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public RunnerService(IWorkspaceService workspaceService, IPythonDiscoveryService pythonDiscovery)
    {
        _workspaceService = workspaceService;
        _pythonDiscovery = pythonDiscovery;
    }

    public async Task<RunManifest> CreateRunAsync(string name, string preset, string datasetPath, DeviceType device)
    {
        var workspace = _workspaceService.CurrentWorkspacePath
            ?? throw new InvalidOperationException("No workspace selected");

        var runId = RunContract.GenerateRunId(name);
        var runFolder = RunContract.GetRunFolder(workspace, runId);

        // Create run folder
        Directory.CreateDirectory(runFolder);

        // Create manifest
        var manifest = new RunManifest
        {
            RunId = runId,
            Name = name,
            Status = RunStatus.Pending,
            Device = device,
            Preset = preset,
            DatasetPath = datasetPath,
            OutputPath = runFolder,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TotalEpochs = 10 // Default - fast demo (20s total)
        };

        // Write manifest
        var manifestPath = RunContract.GetManifestPath(runFolder);
        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        await File.WriteAllTextAsync(manifestPath, json);

        // Create empty metrics file
        var metricsPath = RunContract.GetMetricsPath(runFolder);
        await File.WriteAllTextAsync(metricsPath, "");

        return manifest;
    }

    public async Task<StartRunResult> StartRunAsync(string runId)
    {
        var manifest = await GetRunAsync(runId);
        if (manifest == null)
            return StartRunResult.Fail("Run not found. The run manifest may have been deleted.");

        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace))
        {
            await MarkRunFailedAsync(manifest, "No workspace selected. Go to Dashboard → Select Workspace.");
            return StartRunResult.Fail(manifest.Error!);
        }

        var runFolder = RunContract.GetRunFolder(workspace, runId);

        // Find Python
        if (!_pythonDiscovery.IsPythonAvailable)
        {
            await _pythonDiscovery.DiscoverAsync();
        }

        if (!_pythonDiscovery.IsPythonAvailable)
        {
            var reason = _pythonDiscovery.UnavailableReason
                ?? "Python 3.10+ not found. Install from python.org or Microsoft Store.";
            await MarkRunFailedAsync(manifest, reason);
            return StartRunResult.Fail(reason);
        }

        var pythonPath = _pythonDiscovery.PythonPath!;

        // Create the runner script path (we'll create a simple training simulator)
        var runnerScript = Path.Combine(workspace, ".ml", "runner.py");

        // If runner doesn't exist, create a simulator
        if (!File.Exists(runnerScript))
        {
            await CreateSimulatorScriptAsync(runnerScript);
        }

        // Start the process
        var stdoutPath = RunContract.GetStdoutPath(runFolder);
        var stderrPath = RunContract.GetStderrPath(runFolder);

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{runnerScript}\" \"{runFolder}\" {manifest.TotalEpochs} {manifest.Device}",
            WorkingDirectory = workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };

        // Capture output to files
        var stdoutWriter = new StreamWriter(stdoutPath, append: false) { AutoFlush = true };
        var stderrWriter = new StreamWriter(stderrPath, append: false) { AutoFlush = true };

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
                stdoutWriter.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
                stderrWriter.WriteLine(e.Data);
        };

        process.Exited += async (s, e) =>
        {
            stdoutWriter.Dispose();
            stderrWriter.Dispose();
            _runningProcesses.Remove(runId);

            // Update manifest on exit
            var m = await GetRunAsync(runId);
            if (m != null && m.Status == RunStatus.Running)
            {
                m.Status = process.ExitCode == 0 ? RunStatus.Completed : RunStatus.Failed;
                m.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (process.ExitCode != 0)
                    m.Error = $"Process exited with code {process.ExitCode}";
                await SaveManifestAsync(m);
            }
        };

        process.EnableRaisingEvents = true;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            stdoutWriter.Dispose();
            stderrWriter.Dispose();
            var errorMsg = $"Failed to start Python process: {ex.Message}";
            await MarkRunFailedAsync(manifest, errorMsg);
            return StartRunResult.Fail(errorMsg);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _runningProcesses[runId] = process;

        // Update manifest to Running
        manifest.Status = RunStatus.Running;
        manifest.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        manifest.ProcessId = process.Id;
        await SaveManifestAsync(manifest);

        return StartRunResult.Ok();
    }

    public async Task<bool> CancelRunAsync(string runId)
    {
        if (_runningProcesses.TryGetValue(runId, out var process))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                _runningProcesses.Remove(runId);

                // Update manifest
                var manifest = await GetRunAsync(runId);
                if (manifest != null)
                {
                    manifest.Status = RunStatus.Cancelled;
                    manifest.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    manifest.Error = null; // Not an error - user cancelled
                    await SaveManifestAsync(manifest);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public bool IsRunning(string runId)
    {
        if (_runningProcesses.TryGetValue(runId, out var process))
        {
            return !process.HasExited;
        }
        return false;
    }

    public async Task<List<RunManifest>> GetAllRunsAsync()
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace)) return new List<RunManifest>();

        var runsFolder = Path.Combine(workspace, ".ml", "runs");
        if (!Directory.Exists(runsFolder)) return new List<RunManifest>();

        var runs = new List<RunManifest>();

        foreach (var dir in Directory.GetDirectories(runsFolder))
        {
            var manifestPath = RunContract.GetManifestPath(dir);
            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<RunManifest>(json, _jsonOptions);
                    if (manifest != null)
                        runs.Add(manifest);
                }
                catch
                {
                    // Skip malformed manifests
                }
            }
        }

        return runs.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<RunManifest?> GetRunAsync(string runId)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace)) return null;

        var runFolder = RunContract.GetRunFolder(workspace, runId);
        var manifestPath = RunContract.GetManifestPath(runFolder);

        if (!File.Exists(manifestPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<RunManifest>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string[]> TailLogsAsync(string runId, int lines = 100)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace)) return Array.Empty<string>();

        var runFolder = RunContract.GetRunFolder(workspace, runId);
        var stdoutPath = RunContract.GetStdoutPath(runFolder);

        if (!File.Exists(stdoutPath)) return Array.Empty<string>();

        try
        {
            // Read with sharing so we can read while process writes
            using var fs = new FileStream(stdoutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var allLines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return allLines.TakeLast(lines).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<List<MetricsEntry>> GetMetricsAsync(string runId)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace)) return new List<MetricsEntry>();

        var runFolder = RunContract.GetRunFolder(workspace, runId);
        var metricsPath = RunContract.GetMetricsPath(runFolder);

        if (!File.Exists(metricsPath)) return new List<MetricsEntry>();

        var metrics = new List<MetricsEntry>();

        try
        {
            // Read with sharing
            using var fs = new FileStream(metricsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<MetricsEntry>(line);
                    if (entry != null)
                        metrics.Add(entry);
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }
        catch
        {
            // Return what we have
        }

        return metrics;
    }

    public async Task<(List<MetricsEntry> NewEntries, long NewOffset)> TailMetricsAsync(string runId, long fromOffset)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace))
            return (new List<MetricsEntry>(), fromOffset);

        var runFolder = RunContract.GetRunFolder(workspace, runId);
        var metricsPath = RunContract.GetMetricsPath(runFolder);

        if (!File.Exists(metricsPath))
            return (new List<MetricsEntry>(), 0);

        var entries = new List<MetricsEntry>();

        try
        {
            using var fs = new FileStream(metricsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // If file is smaller than offset, it was reset - start from beginning
            if (fs.Length < fromOffset)
                fromOffset = 0;

            fs.Seek(fromOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<MetricsEntry>(line);
                    if (entry != null)
                        entries.Add(entry);
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            return (entries, fs.Position);
        }
        catch
        {
            return (entries, fromOffset);
        }
    }

    public async Task<(string[] NewLines, long NewOffset)> TailLogsIncrementalAsync(string runId, long fromOffset)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace))
            return (Array.Empty<string>(), fromOffset);

        var runFolder = RunContract.GetRunFolder(workspace, runId);
        var stdoutPath = RunContract.GetStdoutPath(runFolder);

        if (!File.Exists(stdoutPath))
            return (Array.Empty<string>(), 0);

        try
        {
            using var fs = new FileStream(stdoutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // If file is smaller than offset, it was reset - start from beginning
            if (fs.Length < fromOffset)
                fromOffset = 0;

            fs.Seek(fromOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            var content = await reader.ReadToEndAsync();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            return (lines, fs.Position);
        }
        catch
        {
            return (Array.Empty<string>(), fromOffset);
        }
    }

    private async Task SaveManifestAsync(RunManifest manifest)
    {
        var workspace = _workspaceService.CurrentWorkspacePath;
        if (string.IsNullOrEmpty(workspace)) return;

        var runFolder = RunContract.GetRunFolder(workspace, manifest.RunId);
        var manifestPath = RunContract.GetManifestPath(runFolder);

        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        await File.WriteAllTextAsync(manifestPath, json);
    }

    private async Task MarkRunFailedAsync(RunManifest manifest, string error)
    {
        manifest.Status = RunStatus.Failed;
        manifest.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        manifest.Error = error;
        await SaveManifestAsync(manifest);
    }

    private async Task CreateSimulatorScriptAsync(string scriptPath)
    {
        // Create a Python script that simulates training
        var script = """
            #!/usr/bin/env python3
            """
            + "\"\"\"Training simulator for RunForge Desktop.\"\"\"\n" +
            """
            import sys
            import json
            import time
            import random
            import os

            def main():
                if len(sys.argv) < 4:
                    print("Usage: runner.py <run_folder> <epochs> <device>")
                    sys.exit(1)

                run_folder = sys.argv[1]
                total_epochs = int(sys.argv[2])
                device = sys.argv[3]

                metrics_path = os.path.join(run_folder, "metrics.jsonl")

                print(f"Starting training simulation on {device}")
                print(f"Run folder: {run_folder}")
                print(f"Total epochs: {total_epochs}")

                loss = 2.5  # Starting loss
                step = 0
                lr = 0.001

                for epoch in range(1, total_epochs + 1):
                    steps_per_epoch = 20  # Fewer steps for faster demo
                    for s in range(steps_per_epoch):
                        step += 1

                        # Simulate loss decreasing with noise
                        loss = loss * 0.98 + random.gauss(0, 0.02)
                        loss = max(0.01, loss)  # Don't go negative

                        # Write metrics every step for smooth chart
                        entry = {
                            "step": step,
                            "epoch": epoch,
                            "loss": round(loss, 4),
                            "lr": lr,
                            "time": int(time.time())
                        }
                        with open(metrics_path, "a") as f:
                            f.write(json.dumps(entry) + "\n")

                        print(f"step {step} epoch {epoch}/{total_epochs} loss={loss:.4f}")

                        # Simulate work - faster for demo (20 steps * 0.1s * 10 epochs = 20s)
                        time.sleep(0.1)

                print("Training complete!")

            if __name__ == "__main__":
                main()
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, script);
    }

    public async Task RecoverOrphanedRunsAsync()
    {
        var runs = await GetAllRunsAsync();

        foreach (var run in runs.Where(r => r.Status == RunStatus.Running))
        {
            // Check if process is still alive
            bool isAlive = false;
            if (run.ProcessId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(run.ProcessId.Value);
                    isAlive = !proc.HasExited;
                }
                catch
                {
                    // Process not found
                    isAlive = false;
                }
            }

            if (!isAlive)
            {
                // Mark as failed - orphaned run
                run.Status = RunStatus.Failed;
                run.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                run.Error = "Process terminated unexpectedly (app crash recovery)";
                await SaveManifestAsync(run);
            }
        }
    }

    public async Task<bool> HasActiveRunAsync()
    {
        // Check in-memory first
        if (_runningProcesses.Count > 0)
            return true;

        // Check manifests
        var runs = await GetAllRunsAsync();
        return runs.Any(r => r.Status == RunStatus.Running || r.Status == RunStatus.Pending);
    }
}
