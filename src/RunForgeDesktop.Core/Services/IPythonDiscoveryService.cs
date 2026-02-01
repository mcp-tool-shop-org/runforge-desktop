namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for discovering Python installation on Windows.
/// </summary>
public interface IPythonDiscoveryService
{
    /// <summary>
    /// Gets the discovered Python executable path.
    /// </summary>
    string? PythonPath { get; }

    /// <summary>
    /// Gets whether Python was found.
    /// </summary>
    bool IsPythonAvailable { get; }

    /// <summary>
    /// Gets the Python version string (e.g., "3.12.0").
    /// </summary>
    string? PythonVersion { get; }

    /// <summary>
    /// Gets detailed info about how Python was discovered.
    /// </summary>
    string? DiscoveryInfo { get; }

    /// <summary>
    /// Gets the reason Python is not available (if applicable).
    /// </summary>
    string? UnavailableReason { get; }

    /// <summary>
    /// Discovers Python installation.
    /// </summary>
    /// <param name="preferredPath">Optional user-specified path to use first.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if Python was found.</returns>
    Task<bool> DiscoverAsync(string? preferredPath = null, CancellationToken cancellationToken = default);
}
