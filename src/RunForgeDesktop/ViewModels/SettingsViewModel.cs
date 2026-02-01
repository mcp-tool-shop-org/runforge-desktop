using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for application settings.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IPythonDiscoveryService _pythonDiscovery;

    [ObservableProperty]
    private string? _pythonPathOverride;

    [ObservableProperty]
    private bool _isPythonValid;

    [ObservableProperty]
    private string? _pythonVersion;

    [ObservableProperty]
    private string? _pythonDiscoveryInfo;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isValidating;

    public SettingsViewModel(ISettingsService settings, IPythonDiscoveryService pythonDiscovery)
    {
        _settings = settings;
        _pythonDiscovery = pythonDiscovery;
    }

    /// <summary>
    /// Load settings and validate current Python.
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        await _settings.LoadAsync();

        PythonPathOverride = _settings.PythonPathOverride;

        // Validate current Python
        await ValidatePythonAsync();
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

        StatusMessage = "Settings saved";
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
            StatusMessage = $"Browse failed: {ex.Message}";
        }
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
