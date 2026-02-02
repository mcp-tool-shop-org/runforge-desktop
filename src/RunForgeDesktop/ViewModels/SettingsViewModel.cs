using System.Diagnostics;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for application settings.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IPythonDiscoveryService _pythonDiscovery;
    private readonly IWorkspaceService _workspaceService;

    // Python settings
    [ObservableProperty]
    private string? _pythonPathOverride;

    [ObservableProperty]
    private bool _isPythonValid;

    [ObservableProperty]
    private string? _pythonVersion;

    [ObservableProperty]
    private string? _pythonDiscoveryInfo;

    // Output directories
    [ObservableProperty]
    private string? _customLogsDirectory;

    [ObservableProperty]
    private string? _customArtifactsDirectory;

    // Training defaults
    [ObservableProperty]
    private bool _autoOpenOutputFolder;

    [ObservableProperty]
    private string _defaultDevice = "GPU";

    [ObservableProperty]
    private int _defaultEpochs = 10;

    [ObservableProperty]
    private int _defaultBatchSize = 64;

    [ObservableProperty]
    private double _defaultLearningRate = 0.001;

    [ObservableProperty]
    private bool _verboseLogging;

    // Appearance
    [ObservableProperty]
    private string _selectedTheme = "Dark";

    partial void OnSelectedThemeChanged(string value)
    {
        // Apply theme immediately when changed
        ThemeChanged?.Invoke(value);

        // Also save to settings (fire and forget)
        _settings.AppTheme = value;
        _ = _settings.SaveAsync();
    }

    // UI state
    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private string? _settingsFilePath;

    [ObservableProperty]
    private string? _appDataDirectory;

    [ObservableProperty]
    private string? _currentWorkspace;

    public List<string> DeviceOptions { get; } = ["GPU", "CPU"];

    public List<string> ThemeOptions { get; } = ["Dark", "Light", "System"];

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    public event Action<string>? ThemeChanged;

    public SettingsViewModel(
        ISettingsService settings,
        IPythonDiscoveryService pythonDiscovery,
        IWorkspaceService workspaceService)
    {
        _settings = settings;
        _pythonDiscovery = pythonDiscovery;
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Load settings and validate current Python.
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        await _settings.LoadAsync();

        // Python
        PythonPathOverride = _settings.PythonPathOverride;

        // Output directories
        CustomLogsDirectory = _settings.CustomLogsDirectory;
        CustomArtifactsDirectory = _settings.CustomArtifactsDirectory;

        // Training defaults
        AutoOpenOutputFolder = _settings.AutoOpenOutputFolder;
        DefaultDevice = _settings.DefaultDevice;
        DefaultEpochs = _settings.DefaultEpochs;
        DefaultBatchSize = _settings.DefaultBatchSize;
        DefaultLearningRate = _settings.DefaultLearningRate;
        VerboseLogging = _settings.VerboseLogging;

        // Appearance
        SelectedTheme = _settings.AppTheme;

        // Paths info
        SettingsFilePath = _settings.SettingsFilePath;
        AppDataDirectory = _settings.AppDataDirectory;
        CurrentWorkspace = _workspaceService.CurrentWorkspacePath ?? "Not selected";

        // Validate current Python
        await ValidatePythonAsync();
    }

    /// <summary>
    /// Save all settings.
    /// </summary>
    [RelayCommand]
    public async Task SaveAllSettingsAsync()
    {
        // Python
        _settings.PythonPathOverride = string.IsNullOrWhiteSpace(PythonPathOverride)
            ? null
            : PythonPathOverride.Trim();

        // Output directories
        _settings.CustomLogsDirectory = string.IsNullOrWhiteSpace(CustomLogsDirectory)
            ? null
            : CustomLogsDirectory.Trim();
        _settings.CustomArtifactsDirectory = string.IsNullOrWhiteSpace(CustomArtifactsDirectory)
            ? null
            : CustomArtifactsDirectory.Trim();

        // Training defaults
        _settings.AutoOpenOutputFolder = AutoOpenOutputFolder;
        _settings.DefaultDevice = DefaultDevice;
        _settings.DefaultEpochs = DefaultEpochs;
        _settings.DefaultBatchSize = DefaultBatchSize;
        _settings.DefaultLearningRate = DefaultLearningRate;
        _settings.VerboseLogging = VerboseLogging;

        // Appearance
        var themeChanged = _settings.AppTheme != SelectedTheme;
        _settings.AppTheme = SelectedTheme;

        await _settings.SaveAsync();

        // Apply theme if changed
        if (themeChanged)
        {
            ThemeChanged?.Invoke(SelectedTheme);
        }

        // Re-validate Python if path changed
        await ValidatePythonAsync();

        StatusMessage = "All settings saved";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Save the Python path override.
    /// </summary>
    [RelayCommand]
    public async Task SavePythonPathAsync()
    {
        _settings.PythonPathOverride = string.IsNullOrWhiteSpace(PythonPathOverride)
            ? null
            : PythonPathOverride.Trim();

        await _settings.SaveAsync();

        // Re-validate with new path
        await ValidatePythonAsync();

        StatusMessage = "Python settings saved";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Clear the Python path override and use auto-discovery.
    /// </summary>
    [RelayCommand]
    public async Task ClearPythonPathAsync()
    {
        PythonPathOverride = null;
        _settings.ClearPythonPathOverride();

        await ValidatePythonAsync();

        StatusMessage = "Python path cleared, using auto-discovery";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Browse for Python executable.
    /// </summary>
    [RelayCommand]
    public async Task BrowsePythonPathAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Python Executable",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".exe" } }
                })
            });

            if (result is not null)
            {
                PythonPathOverride = result.FullPath;
                await SavePythonPathAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.FromException(ex, "file selection");
        }
    }

    /// <summary>
    /// Browse for custom logs directory.
    /// </summary>
    [RelayCommand]
    public async Task BrowseLogsDirectoryAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(default);

            if (result.IsSuccessful && result.Folder is not null)
            {
                CustomLogsDirectory = result.Folder.Path;
                StatusMessage = "Logs directory selected";
                _ = ClearStatusAfterDelayAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.FromException(ex, "folder selection");
        }
    }

    /// <summary>
    /// Browse for custom artifacts directory.
    /// </summary>
    [RelayCommand]
    public async Task BrowseArtifactsDirectoryAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(default);

            if (result.IsSuccessful && result.Folder is not null)
            {
                CustomArtifactsDirectory = result.Folder.Path;
                StatusMessage = "Artifacts directory selected";
                _ = ClearStatusAfterDelayAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.FromException(ex, "folder selection");
        }
    }

    /// <summary>
    /// Clear custom logs directory.
    /// </summary>
    [RelayCommand]
    public void ClearLogsDirectory()
    {
        CustomLogsDirectory = null;
        StatusMessage = "Logs directory cleared (using workspace default)";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Clear custom artifacts directory.
    /// </summary>
    [RelayCommand]
    public void ClearArtifactsDirectory()
    {
        CustomArtifactsDirectory = null;
        StatusMessage = "Artifacts directory cleared (using workspace default)";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Open the settings file in the default editor.
    /// Creates the file with defaults if it doesn't exist.
    /// </summary>
    [RelayCommand]
    public async Task OpenSettingsFileAsync()
    {
        try
        {
            var settingsPath = _settings.SettingsFilePath;

            // Create the file if it doesn't exist
            if (!File.Exists(settingsPath))
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Save current settings to create the file
                await _settings.SaveAsync();
                StatusMessage = "Created settings.json";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = settingsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.File.OpenFailed("settings file");
        }
    }

    /// <summary>
    /// Open the app data folder in Explorer.
    /// Creates the folder if it doesn't exist.
    /// </summary>
    [RelayCommand]
    public void OpenAppDataFolder()
    {
        try
        {
            var path = _settings.AppDataDirectory;

            // Create the folder if it doesn't exist
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                StatusMessage = "Created app data folder";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.File.OpenFailed("app data folder");
        }
    }

    /// <summary>
    /// Open the current workspace folder in Explorer.
    /// </summary>
    [RelayCommand]
    public void OpenWorkspaceFolder()
    {
        try
        {
            var path = _workspaceService.CurrentWorkspacePath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
            }
            else
            {
                StatusMessage = ErrorMessages.Workspace.NotSelected;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ErrorMessages.File.OpenFailed("workspace folder");
        }
    }

    /// <summary>
    /// Reset all settings to defaults.
    /// </summary>
    [RelayCommand]
    public async Task ResetToDefaultsAsync()
    {
        await _settings.ResetToDefaultsAsync();
        await LoadAsync();

        StatusMessage = "Settings reset to defaults";
        _ = ClearStatusAfterDelayAsync();
    }

    /// <summary>
    /// Validate the current Python configuration.
    /// </summary>
    [RelayCommand]
    public async Task ValidatePythonAsync()
    {
        IsValidating = true;

        try
        {
            var found = await _pythonDiscovery.DiscoverAsync();

            IsPythonValid = found;
            PythonVersion = _pythonDiscovery.PythonVersion;
            PythonDiscoveryInfo = found
                ? _pythonDiscovery.DiscoveryInfo
                : _pythonDiscovery.UnavailableReason;
        }
        finally
        {
            IsValidating = false;
        }
    }

    private async Task ClearStatusAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (StatusMessage is not null && !StatusMessage.StartsWith("Failed"))
        {
            StatusMessage = null;
        }
    }
}
