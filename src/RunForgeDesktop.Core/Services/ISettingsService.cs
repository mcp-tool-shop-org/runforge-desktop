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
}
