using System.Reflection;
using System.Text.Json;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of crash recovery service.
/// Persists session state to %LOCALAPPDATA%\RunForge\session.json
/// </summary>
public sealed class CrashRecoveryService : ICrashRecoveryService
{
    private const string SessionFileName = "session.json";
    private const string CrashLogsFolderName = "CrashLogs";
    private const string AppFolderName = "RunForge";

    private SessionState? _currentSession;
    private readonly object _lock = new();

    /// <inheritdoc />
    public SessionState? CurrentSession => _currentSession;

    /// <inheritdoc />
    public string CrashLogsDirectory => GetCrashLogsDirectory();

    /// <inheritdoc />
    public async Task<CrashRecoveryInfo> CheckForCrashRecoveryAsync(CancellationToken cancellationToken = default)
    {
        var sessionPath = GetSessionPath();

        if (!File.Exists(sessionPath))
        {
            return new CrashRecoveryInfo { HasRecoverableSession = false };
        }

        try
        {
            var json = await File.ReadAllTextAsync(sessionPath, cancellationToken);
            var session = JsonSerializer.Deserialize<SessionState>(json);

            if (session is null || session.TerminatedCleanly)
            {
                // Session ended cleanly, nothing to recover
                return new CrashRecoveryInfo { HasRecoverableSession = false };
            }

            // Session didn't terminate cleanly - possible crash
            var recoveryInfo = new CrashRecoveryInfo
            {
                HasRecoverableSession = true,
                SessionState = session,
                CrashTimestamp = session.LastHeartbeat,
                RecoveryDescription = BuildRecoveryDescription(session)
            };

            return recoveryInfo;
        }
        catch
        {
            // Corrupted session file, can't recover
            return new CrashRecoveryInfo { HasRecoverableSession = false };
        }
    }

    /// <inheritdoc />
    public async Task StartSessionAsync(CancellationToken cancellationToken = default)
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        lock (_lock)
        {
            _currentSession = new SessionState
            {
                SessionId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                TerminatedCleanly = false,
                AppVersion = version
            };
        }

        await SaveSessionStateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSessionStateAsync(
        string? currentRoute = null,
        string? workspacePath = null,
        string? activeRunId = null,
        string? unsavedFormData = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentSession is null)
            {
                return;
            }

            _currentSession.LastHeartbeat = DateTime.UtcNow;

            if (currentRoute is not null)
            {
                _currentSession.CurrentRoute = currentRoute;
            }

            if (workspacePath is not null)
            {
                _currentSession.WorkspacePath = workspacePath;
            }

            if (activeRunId is not null)
            {
                _currentSession.ActiveRunId = activeRunId;
            }

            if (unsavedFormData is not null)
            {
                _currentSession.UnsavedFormData = unsavedFormData;
            }
        }

        await SaveSessionStateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task EndSessionCleanlyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentSession is not null)
            {
                _currentSession.TerminatedCleanly = true;
            }
        }

        await SaveSessionStateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DismissRecoveryAsync(CancellationToken cancellationToken = default)
    {
        var sessionPath = GetSessionPath();

        if (File.Exists(sessionPath))
        {
            try
            {
                File.Delete(sessionPath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WriteCrashLogAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var logsDir = GetCrashLogsDirectory();
            Directory.CreateDirectory(logsDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var logFileName = $"crash-{timestamp}.log";
            var logPath = Path.Combine(logsDir, logFileName);

            var logContent = $"""
                ═══════════════════════════════════════════════════════════════════
                RunForge Desktop Crash Log
                ═══════════════════════════════════════════════════════════════════
                Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                Session ID: {_currentSession?.SessionId ?? "unknown"}
                App Version: {_currentSession?.AppVersion ?? "unknown"}

                Message:
                {message}

                {(exception is not null ? $"""
                Exception Type: {exception.GetType().FullName}
                Exception Message: {exception.Message}

                Stack Trace:
                {exception.StackTrace}

                {(exception.InnerException is not null ? $"""
                Inner Exception: {exception.InnerException.GetType().FullName}
                Inner Message: {exception.InnerException.Message}
                """ : "")}
                """ : "No exception details.")}

                Session State:
                - Current Route: {_currentSession?.CurrentRoute ?? "none"}
                - Workspace: {_currentSession?.WorkspacePath ?? "none"}
                - Active Run: {_currentSession?.ActiveRunId ?? "none"}
                ═══════════════════════════════════════════════════════════════════
                """;

            await File.WriteAllTextAsync(logPath, logContent, cancellationToken);

            // Cleanup old logs (keep last 10)
            CleanupOldLogs(logsDir, 10);
        }
        catch
        {
            // Can't log the crash log error, just ignore
        }
    }

    private async Task SaveSessionStateAsync(CancellationToken cancellationToken)
    {
        SessionState? session;
        lock (_lock)
        {
            session = _currentSession;
        }

        if (session is null)
        {
            return;
        }

        try
        {
            var sessionPath = GetSessionPath();
            var sessionDir = Path.GetDirectoryName(sessionPath)!;
            Directory.CreateDirectory(sessionDir);

            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Atomic write via temp file
            var tempPath = sessionPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, sessionPath, overwrite: true);
        }
        catch
        {
            // Best effort - don't crash on session save failure
        }
    }

    private static string BuildRecoveryDescription(SessionState session)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(session.CurrentRoute))
        {
            parts.Add($"Page: {session.CurrentRoute}");
        }

        if (!string.IsNullOrEmpty(session.ActiveRunId))
        {
            parts.Add($"Run: {session.ActiveRunId}");
        }

        if (!string.IsNullOrEmpty(session.UnsavedFormData))
        {
            parts.Add("Unsaved form data");
        }

        var timeSinceCrash = DateTime.UtcNow - session.LastHeartbeat;
        if (timeSinceCrash.TotalMinutes < 60)
        {
            parts.Add($"Crashed {timeSinceCrash.TotalMinutes:F0} minutes ago");
        }
        else if (timeSinceCrash.TotalHours < 24)
        {
            parts.Add($"Crashed {timeSinceCrash.TotalHours:F1} hours ago");
        }
        else
        {
            parts.Add($"Crashed {timeSinceCrash.TotalDays:F0} days ago");
        }

        return parts.Count > 0
            ? string.Join(" • ", parts)
            : "Previous session did not close properly";
    }

    private static void CleanupOldLogs(string logsDir, int keepCount)
    {
        try
        {
            var logFiles = Directory.GetFiles(logsDir, "crash-*.log")
                .OrderByDescending(f => f)
                .Skip(keepCount)
                .ToList();

            foreach (var file in logFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore individual deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static string GetAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppFolderName);
    }

    private static string GetSessionPath()
    {
        return Path.Combine(GetAppDataDirectory(), SessionFileName);
    }

    private static string GetCrashLogsDirectory()
    {
        return Path.Combine(GetAppDataDirectory(), CrashLogsFolderName);
    }
}
