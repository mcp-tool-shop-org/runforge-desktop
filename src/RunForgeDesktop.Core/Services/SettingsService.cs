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
        set
        {
            _settings.PythonPathOverride = value;
            _ = SaveAsync();
        }
    }

    /// <inheritdoc />
    public bool HasPythonPathOverride =>
        !string.IsNullOrWhiteSpace(_settings.PythonPathOverride);

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

    private static string GetSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppFolderName, SettingsFileName);
    }

    /// <summary>
    /// Internal settings data structure.
    /// </summary>
    private sealed class SettingsData
    {
        public string? PythonPathOverride { get; set; }
    }
}
