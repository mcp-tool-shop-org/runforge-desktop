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

    public DiagnosticsViewModel(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        LoadDiagnostics();
    }

    [RelayCommand]
    private void LoadDiagnostics()
    {
        // App version
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        AppVersion = version?.ToString() ?? "Unknown";

        // Framework version
        FrameworkVersion = Environment.Version.ToString();

        // OS version
        OsVersion = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";

        // Architecture
        Architecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";

        // Working directory
        WorkingDirectory = Environment.CurrentDirectory;

        // Workspace info
        CurrentWorkspace = _workspaceService.CurrentWorkspacePath ?? "Not set";

        var discovery = _workspaceService.CurrentDiscoveryResult;
        if (discovery is not null && discovery.IsValid)
        {
            WorkspaceType = discovery.Method.ToString();
            IndexPath = discovery.IndexPath;
            // RunCount will be updated if we load the index
            RunCount = 0;
        }
        else
        {
            WorkspaceType = "None";
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
            Type: {WorkspaceType}
            Index: {IndexPath ?? "N/A"}
            Runs: {RunCount}
            """;

        await Clipboard.Default.SetTextAsync(diagnostics);
    }
}
