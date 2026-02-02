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

    public async Task<RunManifest> CreateRunAsync(string name, string preset, string datasetPath, DeviceType device, TrainingConfig? config = null)
    {
        var workspace = _workspaceService.CurrentWorkspacePath
            ?? throw new InvalidOperationException("No workspace selected");

        config ??= new TrainingConfig();

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
            TotalEpochs = config.Epochs,
            BatchSize = config.BatchSize,
            LearningRate = config.LearningRate,
            NumSamples = config.NumSamples,
            Optimizer = config.Optimizer,
            Scheduler = config.Scheduler
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
            Arguments = $"\"{runnerScript}\" \"{runFolder}\"",
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
        // Real PyTorch training script - reads config from run.json
        const string script = """
            #!/usr/bin/env python3
            '''RunForge Desktop - PyTorch Training Runner'''
            import sys
            import json
            import time
            import os

            def main():
                if len(sys.argv) < 2:
                    print("Usage: runner.py <run_folder>")
                    sys.exit(1)

                run_folder = sys.argv[1]

                # Load config from run.json
                manifest_path = os.path.join(run_folder, "run.json")
                with open(manifest_path) as f:
                    config = json.load(f)

                total_epochs = config.get("total_epochs", 10)
                batch_size = config.get("batch_size", 64)
                learning_rate = config.get("learning_rate", 0.001)
                num_samples = config.get("num_samples", 5000)
                optimizer_name = config.get("optimizer", "Adam")
                scheduler_name = config.get("scheduler", "StepLR")
                device_arg = config.get("device", "CPU").upper()

                # Import PyTorch
                try:
                    import torch
                    import torch.nn as nn
                    import torch.optim as optim
                    from torch.utils.data import DataLoader, TensorDataset
                except ImportError:
                    print("ERROR: PyTorch not installed. Run: pip install torch")
                    sys.exit(1)

                # Device selection
                if device_arg == "GPU" and torch.cuda.is_available():
                    device = torch.device("cuda")
                    print(f"Using GPU: {torch.cuda.get_device_name(0)}")
                    print(f"CUDA version: {torch.version.cuda}")
                else:
                    device = torch.device("cpu")
                    print(f"Using CPU (CUDA available: {torch.cuda.is_available()})")

                metrics_path = os.path.join(run_folder, "metrics.jsonl")

                print(f"═══════════════════════════════════════════════")
                print(f"RunForge Training Session")
                print(f"═══════════════════════════════════════════════")
                print(f"Run folder: {run_folder}")
                print(f"Epochs: {total_epochs}")
                print(f"Batch size: {batch_size}")
                print(f"Learning rate: {learning_rate}")
                print(f"Samples: {num_samples}")
                print(f"Optimizer: {optimizer_name}")
                print(f"Scheduler: {scheduler_name}")
                print(f"═══════════════════════════════════════════════")

                # Simple CNN for image classification
                class SimpleCNN(nn.Module):
                    def __init__(self):
                        super().__init__()
                        self.conv1 = nn.Conv2d(3, 32, 3, padding=1)
                        self.conv2 = nn.Conv2d(32, 64, 3, padding=1)
                        self.conv3 = nn.Conv2d(64, 128, 3, padding=1)
                        self.pool = nn.MaxPool2d(2, 2)
                        self.fc1 = nn.Linear(128 * 4 * 4, 256)
                        self.fc2 = nn.Linear(256, 10)
                        self.relu = nn.ReLU()
                        self.dropout = nn.Dropout(0.5)

                    def forward(self, x):
                        x = self.pool(self.relu(self.conv1(x)))
                        x = self.pool(self.relu(self.conv2(x)))
                        x = self.pool(self.relu(self.conv3(x)))
                        x = x.view(-1, 128 * 4 * 4)
                        x = self.dropout(self.relu(self.fc1(x)))
                        return self.fc2(x)

                # Generate synthetic dataset
                print("Generating training data...")
                X = torch.randn(num_samples, 3, 32, 32)
                y = torch.randint(0, 10, (num_samples,))
                dataset = TensorDataset(X, y)
                loader = DataLoader(dataset, batch_size=batch_size, shuffle=True, num_workers=0, pin_memory=(device.type == "cuda"))

                # Model
                model = SimpleCNN().to(device)
                criterion = nn.CrossEntropyLoss()

                # Optimizer selection
                if optimizer_name == "Adam":
                    optimizer = optim.Adam(model.parameters(), lr=learning_rate)
                elif optimizer_name == "AdamW":
                    optimizer = optim.AdamW(model.parameters(), lr=learning_rate)
                elif optimizer_name == "SGD":
                    optimizer = optim.SGD(model.parameters(), lr=learning_rate, momentum=0.9)
                elif optimizer_name == "RMSprop":
                    optimizer = optim.RMSprop(model.parameters(), lr=learning_rate)
                else:
                    optimizer = optim.Adam(model.parameters(), lr=learning_rate)

                # Scheduler selection
                if scheduler_name == "StepLR":
                    scheduler = optim.lr_scheduler.StepLR(optimizer, step_size=max(1, total_epochs // 3), gamma=0.5)
                elif scheduler_name == "CosineAnnealing":
                    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=total_epochs)
                elif scheduler_name == "OneCycleLR":
                    scheduler = optim.lr_scheduler.OneCycleLR(optimizer, max_lr=learning_rate * 10, epochs=total_epochs, steps_per_epoch=len(loader))
                else:
                    scheduler = None

                print(f"Model parameters: {sum(p.numel() for p in model.parameters()):,}")
                print(f"Batches per epoch: {len(loader)}")
                print("Starting training...")
                print()

                step = 0
                for epoch in range(1, total_epochs + 1):
                    model.train()
                    epoch_loss = 0.0
                    num_batches = 0

                    for batch_idx, (data, target) in enumerate(loader):
                        data, target = data.to(device), target.to(device)
                        step += 1

                        optimizer.zero_grad()
                        output = model(data)
                        loss = criterion(output, target)
                        loss.backward()
                        optimizer.step()

                        if scheduler_name == "OneCycleLR" and scheduler:
                            scheduler.step()

                        epoch_loss += loss.item()
                        num_batches += 1

                        # Log every batch
                        entry = {
                            "step": step,
                            "epoch": epoch,
                            "loss": round(loss.item(), 4),
                            "lr": optimizer.param_groups[0]['lr'],
                            "time": int(time.time())
                        }
                        with open(metrics_path, "a") as f:
                            f.write(json.dumps(entry) + "\n")

                        print(f"step {step} epoch {epoch}/{total_epochs} loss={loss.item():.4f} lr={optimizer.param_groups[0]['lr']:.6f}")

                    if scheduler and scheduler_name != "OneCycleLR":
                        scheduler.step()

                    avg_loss = epoch_loss / num_batches
                    print(f">>> Epoch {epoch} complete - avg loss: {avg_loss:.4f}")
                    print()

                print("═══════════════════════════════════════════════")
                print("Training complete!")
                print(f"Final loss: {avg_loss:.4f}")
                print(f"Total steps: {step}")
                print("═══════════════════════════════════════════════")

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
