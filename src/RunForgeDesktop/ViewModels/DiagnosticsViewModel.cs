using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the diagnostics page.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IRunIndexService _runIndexService;
    private readonly IStorageService _storageService;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _frameworkVersion = string.Empty;

    [ObservableProperty]
    private string _osVersion = string.Empty;

    [ObservableProperty]
    private string _architecture = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string? _currentWorkspace;

    [ObservableProperty]
    private string? _workspaceType;

    [ObservableProperty]
    private string? _indexPath;

    [ObservableProperty]
    private int _runCount;

    [ObservableProperty]
    private string _memoryUsage = string.Empty;

    [ObservableProperty]
    private string _lastRefresh = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    // Storage properties
    [ObservableProperty]
    private bool _isLoadingStorage;

    [ObservableProperty]
    private string _totalStorageSize = "—";

    [ObservableProperty]
    private string _totalLogsSize = "—";

    [ObservableProperty]
    private string _totalArtifactsSize = "—";

    [ObservableProperty]
    private ObservableCollection<RunStorageInfo> _topRuns = [];

    [ObservableProperty]
    private RunStorageInfo? _selectedRun;

    [ObservableProperty]
    private bool _showDeleteConfirmation;

    [ObservableProperty]
    private string? _deleteConfirmRunName;

    public DiagnosticsViewModel(
        IWorkspaceService workspaceService,
        IRunIndexService runIndexService,
        IStorageService storageService)
    {
        _workspaceService = workspaceService;
        _runIndexService = runIndexService;
        _storageService = storageService;
        LoadDiagnostics();
        _ = LoadStorageAsync();
    }

    [RelayCommand]
    private void LoadDiagnostics()
    {
        StatusMessage = null;

        // App version
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        AppVersion = version is not null
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : "Not available";

        // Framework version
        FrameworkVersion = $".NET {Environment.Version}";

        // OS version - more detailed
        var osVersion = Environment.OSVersion;
        var windowsBuild = osVersion.Version.Build;
        var windowsVersion = windowsBuild switch
        {
            >= 22000 => "Windows 11",
            >= 10240 => "Windows 10",
            _ => $"Windows {osVersion.Version.Major}"
        };
        OsVersion = $"{windowsVersion} (Build {windowsBuild})";

        // Architecture
        Architecture = Environment.Is64BitProcess ? "x64" : "x86";

        // Working directory
        WorkingDirectory = Environment.CurrentDirectory;

        // Workspace info
        CurrentWorkspace = _workspaceService.CurrentWorkspacePath ?? "Not set";

        var discovery = _workspaceService.CurrentDiscoveryResult;
        if (discovery is not null && discovery.IsValid)
        {
            WorkspaceType = discovery.Method.ToString();
            IndexPath = discovery.IndexPath;
            // Get run count from index service if loaded
            RunCount = _runIndexService.CurrentRuns.Count;
        }
        else
        {
            WorkspaceType = "Not configured";
            IndexPath = null;
            RunCount = 0;
        }

        // Memory usage
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
        MemoryUsage = $"{memoryMb:F1} MB";

        // Timestamp
        LastRefresh = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [RelayCommand]
    private async Task OpenWorkspaceFolderAsync()
    {
        if (string.IsNullOrEmpty(CurrentWorkspace) || CurrentWorkspace == "Not set")
        {
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(CurrentWorkspace)
            });
        }
        catch
        {
            // Try opening in explorer
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = CurrentWorkspace,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore
            }
        }
    }

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        var diagnostics = $"""
            RunForge Desktop Diagnostics
            ============================
            Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            Application
            -----------
            Version: {AppVersion}
            Framework: {FrameworkVersion}
            Architecture: {Architecture}

            System
            ------
            OS: {OsVersion}
            Working Directory: {WorkingDirectory}
            Memory Usage: {MemoryUsage}

            Workspace
            ---------
            Path: {CurrentWorkspace}
            Discovery Method: {WorkspaceType}
            Index Location: {IndexPath ?? "(not configured)"}
            Run Count: {RunCount}

            Storage
            -------
            Total Size: {TotalStorageSize}
            Logs: {TotalLogsSize}
            Artifacts: {TotalArtifactsSize}
            """;

        await Clipboard.Default.SetTextAsync(diagnostics);

        // Show status message
        StatusMessage = "Diagnostics copied to clipboard";

        // Clear message after 3 seconds
        await Task.Delay(3000);
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        await Shell.Current.GoToAsync("settings");
    }

    [RelayCommand]
    private async Task LoadStorageAsync()
    {
        if (string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            TotalStorageSize = "—";
            TotalLogsSize = "—";
            TotalArtifactsSize = "—";
            TopRuns.Clear();
            return;
        }

        IsLoadingStorage = true;
        try
        {
            var summary = await _storageService.CalculateStorageAsync(
                _workspaceService.CurrentWorkspacePath,
                topN: 10);

            TotalStorageSize = summary.TotalSizeDisplay;
            TotalLogsSize = FormatSize(summary.TotalLogsBytes);
            TotalArtifactsSize = FormatSize(summary.TotalArtifactsBytes);

            TopRuns.Clear();
            foreach (var run in summary.TopRunsBySize)
            {
                TopRuns.Add(run);
            }
        }
        catch
        {
            TotalStorageSize = "Error";
            TotalLogsSize = "—";
            TotalArtifactsSize = "—";
        }
        finally
        {
            IsLoadingStorage = false;
        }
    }

    [RelayCommand]
    private async Task OpenRunFolderAsync(RunStorageInfo? run)
    {
        if (run is null || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var runPath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            run.RunDir.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(runPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = runPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore
            }
        }
    }

    [RelayCommand]
    private void RequestDeleteRun(RunStorageInfo? run)
    {
        if (run is null)
        {
            return;
        }

        SelectedRun = run;
        DeleteConfirmRunName = run.Name;
        ShowDeleteConfirmation = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirmation = false;
        SelectedRun = null;
        DeleteConfirmRunName = null;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (SelectedRun is null || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ShowDeleteConfirmation = false;
            return;
        }

        var runToDelete = SelectedRun;
        ShowDeleteConfirmation = false;
        SelectedRun = null;
        DeleteConfirmRunName = null;

        var success = await _storageService.DeleteRunAsync(
            _workspaceService.CurrentWorkspacePath,
            runToDelete.RunDir);

        if (success)
        {
            StatusMessage = $"Deleted run: {runToDelete.Name}";
            TopRuns.Remove(runToDelete);

            // Reload storage to update totals
            await LoadStorageAsync();

            // Reload the index
            await _runIndexService.LoadIndexAsync(_workspaceService.CurrentWorkspacePath);
            RunCount = _runIndexService.CurrentRuns.Count;
        }
        else
        {
            StatusMessage = $"Failed to delete run: {runToDelete.Name}";
        }

        // Clear message after 3 seconds
        await Task.Delay(3000);
        StatusMessage = null;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
