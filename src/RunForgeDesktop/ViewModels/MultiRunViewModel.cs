using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the MultiRun (hyperparameter sweep) page.
/// Allows users to configure and run multiple training experiments.
/// </summary>
public partial class MultiRunViewModel : ObservableObject
{
    private readonly IRunnerService _runnerService;
    private readonly IWorkspaceService _workspaceService;

    public MultiRunViewModel(IRunnerService runnerService, IWorkspaceService workspaceService)
    {
        _runnerService = runnerService;
        _workspaceService = workspaceService;
        DetectGpu();
        GeneratePreview();
    }

    // === BASIC SETTINGS ===

    [ObservableProperty]
    private string _sweepName = "sweep";

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private bool _gpuAvailable = true;

    [ObservableProperty]
    private string _gpuName = "";

    // === HYPERPARAMETER RANGES ===

    [ObservableProperty]
    private string _learningRates = "0.001, 0.0001";

    [ObservableProperty]
    private string _batchSizes = "32, 64";

    [ObservableProperty]
    private string _optimizers = "Adam, AdamW";

    [ObservableProperty]
    private int _epochsPerRun = 10;

    [ObservableProperty]
    private int _samplesPerRun = 5000;

    [ObservableProperty]
    private string _selectedScheduler = "StepLR";

    // === EXECUTION ===

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _currentRunIndex;

    [ObservableProperty]
    private int _totalRuns;

    [ObservableProperty]
    private string? _currentRunName;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    // === PREVIEW ===

    public ObservableCollection<RunPreviewItem> RunPreviews { get; } = new();

    public string[] AvailableSchedulers { get; } = ["None", "StepLR", "CosineAnnealing", "OneCycleLR"];

    public bool HasWorkspace => !string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath);

    public string WorkspaceDisplayName => HasWorkspace
        ? Path.GetFileName(_workspaceService.CurrentWorkspacePath!) ?? "Workspace"
        : "";

    public bool CanStart => HasWorkspace && !IsRunning && TotalRuns > 0;

    private void DetectGpu()
    {
        try
        {
            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            GpuAvailable = !string.IsNullOrEmpty(cudaPath);
            if (GpuAvailable)
            {
                var cudaVersion = Path.GetFileName(cudaPath ?? "");
                GpuName = $"CUDA GPU ({cudaVersion})";
            }
        }
        catch
        {
            GpuAvailable = false;
        }
        UseGpu = GpuAvailable;
    }

    partial void OnLearningRatesChanged(string value) => GeneratePreview();
    partial void OnBatchSizesChanged(string value) => GeneratePreview();
    partial void OnOptimizersChanged(string value) => GeneratePreview();
    partial void OnEpochsPerRunChanged(int value) => GeneratePreview();
    partial void OnSamplesPerRunChanged(int value) => GeneratePreview();
    partial void OnSelectedSchedulerChanged(string value) => GeneratePreview();

    [RelayCommand]
    private void GeneratePreview()
    {
        RunPreviews.Clear();

        var lrs = ParseDoubles(LearningRates);
        var batches = ParseInts(BatchSizes);
        var opts = ParseStrings(Optimizers);

        if (lrs.Count == 0) lrs.Add(0.001);
        if (batches.Count == 0) batches.Add(64);
        if (opts.Count == 0) opts.Add("Adam");

        int runNum = 1;
        foreach (var lr in lrs)
        {
            foreach (var batch in batches)
            {
                foreach (var opt in opts)
                {
                    RunPreviews.Add(new RunPreviewItem
                    {
                        RunNumber = runNum++,
                        LearningRate = lr,
                        BatchSize = batch,
                        Optimizer = opt,
                        Epochs = EpochsPerRun,
                        Status = "Pending"
                    });
                }
            }
        }

        TotalRuns = RunPreviews.Count;
        OnPropertyChanged(nameof(CanStart));
    }

    [RelayCommand]
    private async Task StartSweep()
    {
        if (!HasWorkspace || IsRunning) return;

        IsRunning = true;
        CurrentRunIndex = 0;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CanStart));

        var device = UseGpu ? Core.Models.DeviceType.GPU : Core.Models.DeviceType.CPU;

        try
        {
            foreach (var preview in RunPreviews)
            {
                CurrentRunIndex++;
                CurrentRunName = $"{SweepName}_{CurrentRunIndex:D2}";
                preview.Status = "Running";
                StatusMessage = $"Running {CurrentRunIndex}/{TotalRuns}: {CurrentRunName}";

                var config = new TrainingConfig
                {
                    Epochs = preview.Epochs,
                    BatchSize = preview.BatchSize,
                    LearningRate = preview.LearningRate,
                    NumSamples = SamplesPerRun,
                    Optimizer = preview.Optimizer,
                    Scheduler = SelectedScheduler
                };

                // Create and start the run
                var manifest = await _runnerService.CreateRunAsync(
                    CurrentRunName,
                    "MultiRun",
                    "",
                    device,
                    config);

                var result = await _runnerService.StartRunAsync(manifest.RunId);

                if (!result.Success)
                {
                    preview.Status = "Failed";
                    preview.Error = result.ErrorMessage;
                    continue;
                }

                // Wait for completion by polling
                while (true)
                {
                    await Task.Delay(1000);
                    var currentManifest = await _runnerService.GetRunAsync(manifest.RunId);
                    if (currentManifest == null) break;

                    if (currentManifest.Status == RunStatus.Completed)
                    {
                        preview.Status = "Completed";
                        // Get final loss from metrics
                        var metrics = await _runnerService.GetMetricsAsync(manifest.RunId);
                        if (metrics.Count > 0)
                        {
                            preview.FinalLoss = metrics.Last().Loss;
                        }
                        break;
                    }
                    else if (currentManifest.Status == RunStatus.Failed)
                    {
                        preview.Status = "Failed";
                        preview.Error = currentManifest.Error;
                        break;
                    }
                    else if (currentManifest.Status == RunStatus.Cancelled)
                    {
                        preview.Status = "Cancelled";
                        break;
                    }
                }
            }

            // Find best run
            var completedRuns = RunPreviews.Where(r => r.Status == "Completed" && r.FinalLoss.HasValue).ToList();
            if (completedRuns.Any())
            {
                var best = completedRuns.OrderBy(r => r.FinalLoss).First();
                StatusMessage = ErrorMessages.Sweep.BestRunFound(best.RunNumber, best.FinalLoss!.Value);
            }
            else
            {
                StatusMessage = ErrorMessages.Sweep.NoSuccessfulRuns;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorMessages.FromException(ex, "hyperparameter sweep");
            StatusMessage = null;
        }
        finally
        {
            IsRunning = false;
            CurrentRunName = null;
            OnPropertyChanged(nameof(CanStart));
        }
    }

    [RelayCommand]
    private void CancelSweep()
    {
        // Mark remaining as cancelled
        foreach (var preview in RunPreviews.Where(p => p.Status == "Pending"))
        {
            preview.Status = "Cancelled";
        }
        IsRunning = false;
        StatusMessage = ErrorMessages.Sweep.CancelledMidway;
        OnPropertyChanged(nameof(CanStart));
    }

    private static List<double> ParseDoubles(string input)
    {
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, out var v) ? v : (double?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }

    private static List<int> ParseInts(string input)
    {
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }

    private static List<string> ParseStrings(string input)
    {
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}

/// <summary>
/// Preview item for a single run in the sweep grid.
/// </summary>
public partial class RunPreviewItem : ObservableObject
{
    [ObservableProperty]
    private int _runNumber;

    [ObservableProperty]
    private double _learningRate;

    [ObservableProperty]
    private int _batchSize;

    [ObservableProperty]
    private string _optimizer = "Adam";

    [ObservableProperty]
    private int _epochs;

    [ObservableProperty]
    private string _status = "Pending";

    [ObservableProperty]
    private double? _finalLoss;

    [ObservableProperty]
    private string? _error;

    public string DisplayName => $"lr={LearningRate}, bs={BatchSize}, opt={Optimizer}";
}
