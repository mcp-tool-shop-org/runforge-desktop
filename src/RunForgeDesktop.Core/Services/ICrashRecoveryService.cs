namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Session state information saved for crash recovery.
/// </summary>
public sealed class SessionState
{
    /// <summary>Session identifier.</summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>When the session started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last heartbeat timestamp.</summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>Current page/route being viewed.</summary>
    public string? CurrentRoute { get; set; }

    /// <summary>Current workspace path.</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>ID of run being viewed or edited.</summary>
    public string? ActiveRunId { get; set; }

    /// <summary>Any unsaved form data (JSON serialized).</summary>
    public string? UnsavedFormData { get; set; }

    /// <summary>Whether the session ended cleanly.</summary>
    public bool TerminatedCleanly { get; set; }

    /// <summary>App version at time of session.</summary>
    public string? AppVersion { get; set; }
}

/// <summary>
/// Crash recovery information for display to user.
/// </summary>
public sealed class CrashRecoveryInfo
{
    /// <summary>Whether there is a session to recover.</summary>
    public bool HasRecoverableSession { get; set; }

    /// <summary>The session state to recover.</summary>
    public SessionState? SessionState { get; set; }

    /// <summary>User-friendly description of what can be recovered.</summary>
    public string? RecoveryDescription { get; set; }

    /// <summary>When the crash occurred (estimated).</summary>
    public DateTime? CrashTimestamp { get; set; }
}

/// <summary>
/// Service for managing crash recovery and session state.
/// </summary>
public interface ICrashRecoveryService
{
    /// <summary>
    /// Checks if there is a previous session that didn't terminate cleanly.
    /// </summary>
    Task<CrashRecoveryInfo> CheckForCrashRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new session and begins heartbeat monitoring.
    /// </summary>
    Task StartSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current session state (call periodically and on navigation).
    /// </summary>
    Task UpdateSessionStateAsync(
        string? currentRoute = null,
        string? workspacePath = null,
        string? activeRunId = null,
        string? unsavedFormData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the current session as cleanly terminated.
    /// </summary>
    Task EndSessionCleanlyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses the crash recovery (user chose not to restore).
    /// </summary>
    Task DismissRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    SessionState? CurrentSession { get; }

    /// <summary>
    /// Path to the crash logs directory.
    /// </summary>
    string CrashLogsDirectory { get; }

    /// <summary>
    /// Writes a crash log entry.
    /// </summary>
    Task WriteCrashLogAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default);
}
