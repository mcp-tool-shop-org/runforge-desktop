using System.Text.Json;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of settings service.
/// Persists settings to %LOCALAPPDATA%\RunForge\settings.json
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private const string AppFolderName = "RunForge";

    private SettingsData _settings = new();
    private bool _isLoaded;

    /// <inheritdoc />
    public string? PythonPathOverride
    {
        get => _settings.PythonPathOverride;
        set => _settings.PythonPathOverride = value;
    }

    /// <inheritdoc />
    public bool HasPythonPathOverride =>
        !string.IsNullOrWhiteSpace(_settings.PythonPathOverride);

    /// <inheritdoc />
    public string? CustomLogsDirectory
    {
        get => _settings.CustomLogsDirectory;
        set => _settings.CustomLogsDirectory = value;
    }

    /// <inheritdoc />
    public string? CustomArtifactsDirectory
    {
        get => _settings.CustomArtifactsDirectory;
        set => _settings.CustomArtifactsDirectory = value;
    }

    /// <inheritdoc />
    public bool AutoOpenOutputFolder
    {
        get => _settings.AutoOpenOutputFolder;
        set => _settings.AutoOpenOutputFolder = value;
    }

    /// <inheritdoc />
    public string DefaultDevice
    {
        get => _settings.DefaultDevice;
        set => _settings.DefaultDevice = value;
    }

    /// <inheritdoc />
    public int DefaultEpochs
    {
        get => _settings.DefaultEpochs;
        set => _settings.DefaultEpochs = value;
    }

    /// <inheritdoc />
    public int DefaultBatchSize
    {
        get => _settings.DefaultBatchSize;
        set => _settings.DefaultBatchSize = value;
    }

    /// <inheritdoc />
    public double DefaultLearningRate
    {
        get => _settings.DefaultLearningRate;
        set => _settings.DefaultLearningRate = value;
    }

    /// <inheritdoc />
    public bool VerboseLogging
    {
        get => _settings.VerboseLogging;
        set => _settings.VerboseLogging = value;
    }

    /// <inheritdoc />
    public string AppTheme
    {
        get => _settings.AppTheme;
        set => _settings.AppTheme = value;
    }

    /// <inheritdoc />
    public string SettingsFilePath => GetSettingsPath();

    /// <inheritdoc />
    public string AppDataDirectory => GetAppDataDirectory();

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        var settingsPath = GetSettingsPath();

        if (File.Exists(settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
                var loaded = JsonSerializer.Deserialize<SettingsData>(json);
                if (loaded is not null)
                {
                    _settings = loaded;
                }
            }
            catch
            {
                // If settings are corrupted, start fresh
                _settings = new SettingsData();
            }
        }

        _isLoaded = true;
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settingsPath = GetSettingsPath();
        var settingsDir = Path.GetDirectoryName(settingsPath)!;

        // Ensure directory exists
        Directory.CreateDirectory(settingsDir);

        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }

    /// <inheritdoc />
    public void ClearPythonPathOverride()
    {
        _settings.PythonPathOverride = null;
        _ = SaveAsync();
    }

    /// <inheritdoc />
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        _settings = new SettingsData();
        await SaveAsync(cancellationToken);
    }

    private static string GetAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppFolderName);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(GetAppDataDirectory(), SettingsFileName);
    }

    /// <summary>
    /// Internal settings data structure.
    /// </summary>
    private sealed class SettingsData
    {
        public string? PythonPathOverride { get; set; }
        public string? CustomLogsDirectory { get; set; }
        public string? CustomArtifactsDirectory { get; set; }
        public bool AutoOpenOutputFolder { get; set; } = false;
        public string DefaultDevice { get; set; } = "GPU";
        public int DefaultEpochs { get; set; } = 10;
        public int DefaultBatchSize { get; set; } = 64;
        public double DefaultLearningRate { get; set; } = 0.001;
        public bool VerboseLogging { get; set; } = false;
        public string AppTheme { get; set; } = "Dark";
    }
}
