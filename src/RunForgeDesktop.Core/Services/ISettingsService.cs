namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for managing application settings.
/// Settings are persisted to user's local app data.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the user-specified Python path override.
    /// If set and valid, this is used instead of auto-discovery.
    /// </summary>
    string? PythonPathOverride { get; set; }

    /// <summary>
    /// Gets whether a Python path override is configured.
    /// </summary>
    bool HasPythonPathOverride { get; }

    /// <summary>
    /// Gets or sets a custom logs output directory.
    /// If null, logs are stored in the workspace's runs folder.
    /// </summary>
    string? CustomLogsDirectory { get; set; }

    /// <summary>
    /// Gets or sets a custom artifacts/checkpoints output directory.
    /// If null, artifacts are stored in the workspace's runs folder.
    /// </summary>
    string? CustomArtifactsDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically open the output folder after a run completes.
    /// </summary>
    bool AutoOpenOutputFolder { get; set; }

    /// <summary>
    /// Gets or sets the default device preference (GPU/CPU).
    /// </summary>
    string DefaultDevice { get; set; }

    /// <summary>
    /// Gets or sets the default number of epochs for new runs.
    /// </summary>
    int DefaultEpochs { get; set; }

    /// <summary>
    /// Gets or sets the default batch size for new runs.
    /// </summary>
    int DefaultBatchSize { get; set; }

    /// <summary>
    /// Gets or sets the default learning rate for new runs.
    /// </summary>
    double DefaultLearningRate { get; set; }

    /// <summary>
    /// Gets or sets whether verbose logging is enabled.
    /// </summary>
    bool VerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets the app theme preference.
    /// Values: "Dark" (default), "Light", "System"
    /// </summary>
    string AppTheme { get; set; }

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Gets the RunForge app data directory.
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves settings to persistent storage.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the Python path override.
    /// </summary>
    void ClearPythonPathOverride();

    /// <summary>
    /// Resets all settings to default values.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
