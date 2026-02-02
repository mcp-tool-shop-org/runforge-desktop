using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

public partial class NewRunViewModel : ObservableObject
{
    private readonly IRunnerService _runnerService;
    private readonly IWorkspaceService _workspaceService;

    public NewRunViewModel(IRunnerService runnerService, IWorkspaceService workspaceService)
    {
        _runnerService = runnerService;
        _workspaceService = workspaceService;

        // Detect GPU availability
        DetectGpu();
    }

    private void DetectGpu()
    {
        // Simple GPU detection - in production, would use CUDA/DirectX queries
        // Check for NVIDIA GPU via environment or registry
        try
        {
            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            GpuAvailable = !string.IsNullOrEmpty(cudaPath);
            if (GpuAvailable)
            {
                // Extract version hint from CUDA path
                var cudaVersion = Path.GetFileName(cudaPath ?? "");
                GpuName = $"CUDA GPU ({cudaVersion})";
            }
            else
            {
                GpuName = "";
                GpuUnavailableReason = "GPU unavailable. Install NVIDIA drivers and CUDA toolkit, or use CPU.";
            }
        }
        catch
        {
            GpuAvailable = false;
            GpuName = "";
            GpuUnavailableReason = "GPU detection failed. Using CPU instead.";
        }

        UseGpu = GpuAvailable;
    }

    [ObservableProperty]
    private string _runName = "";

    [ObservableProperty]
    private string _selectedPreset = "SLOAQ (Adaptive)";

    [ObservableProperty]
    private string _datasetPath = "";

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private bool _gpuAvailable = true;

    [ObservableProperty]
    private string _gpuName = "";

    [ObservableProperty]
    private string _gpuUnavailableReason = "";

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Helper text for device selection based on availability.
    /// </summary>
    public string DeviceHelperText => GpuAvailable
        ? $"GPU available: {GpuName}"
        : GpuUnavailableReason;

    /// <summary>
    /// Helper text for dataset field - always visible to clarify optional behavior.
    /// </summary>
    public string DatasetHelperText => string.IsNullOrEmpty(DatasetPath)
        ? "Optional. Uses built-in simulation data if empty."
        : "Custom dataset selected";

    /// <summary>
    /// Path to current workspace for display.
    /// </summary>
    public string? WorkspacePath => _workspaceService.CurrentWorkspacePath;

    /// <summary>
    /// Short workspace name for display.
    /// </summary>
    public string WorkspaceDisplayName => HasWorkspace
        ? Path.GetFileName(_workspaceService.CurrentWorkspacePath!) ?? "Workspace"
        : "";

    /// <summary>
    /// Can the form be submitted?
    /// </summary>
    public bool CanSubmit => HasWorkspace && !IsCreating && !string.IsNullOrWhiteSpace(RunName);

    public string[] AvailablePresets { get; } =
    [
        "SLOAQ (Adaptive)",
        "ResNet50",
        "ResNet18",
        "VGG16",
        "BERT-base",
        "GPT2-small",
        "Custom"
    ];

    public bool HasWorkspace => !string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath);

    partial void OnDatasetPathChanged(string value)
    {
        OnPropertyChanged(nameof(DatasetHelperText));
    }

    [RelayCommand]
    private async Task BrowseDataset()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Dataset"
            });

            if (result != null)
            {
                DatasetPath = result.FullPath;
            }
        }
        catch
        {
            // User cancelled
        }
    }

    [RelayCommand]
    private async Task StartTraining()
    {
        if (string.IsNullOrWhiteSpace(RunName))
        {
            ErrorMessage = "Run name required. Enter a name to identify this run.";
            return;
        }

        if (!HasWorkspace)
        {
            ErrorMessage = "No workspace selected. Go to Dashboard → Select Workspace.";
            return;
        }

        // Check for existing active run
        if (await _runnerService.HasActiveRunAsync())
        {
            ErrorMessage = "A run is already active. Wait for it to complete or cancel it first.";
            return;
        }

        // Disable button immediately for feedback
        IsCreating = true;
        ErrorMessage = null;

        try
        {
            var device = UseGpu ? Core.Models.DeviceType.GPU : Core.Models.DeviceType.CPU;

            // Create the run manifest
            var manifest = await _runnerService.CreateRunAsync(
                RunName,
                SelectedPreset,
                DatasetPath,
                device);

            // Navigate immediately - motion = confidence
            // The live view will show "Starting..." state while runner spawns
            await Shell.Current.GoToAsync($"rundetail?runId={manifest.RunId}");

            // Start training in background after navigation
            _ = _runnerService.StartRunAsync(manifest.RunId);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsCreating = false;
        }
        // Note: Don't reset IsCreating on success - we've navigated away
    }
}
