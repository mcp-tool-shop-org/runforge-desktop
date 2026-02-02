using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Windows Python discovery service.
///
/// Discovery order:
/// 1. User-specified path (settings override)
/// 2. py launcher (Windows Python Launcher)
/// 3. python in PATH
/// 4. Common installation paths
/// 5. Windows Store Python
/// </summary>
public sealed partial class PythonDiscoveryService : IPythonDiscoveryService
{
    private const string MinPythonVersion = "3.10";

    private readonly ISettingsService _settings;

    public PythonDiscoveryService(ISettingsService settings)
    {
        _settings = settings;
    }

    /// <inheritdoc />
    public string? PythonPath { get; private set; }

    /// <inheritdoc />
    public bool IsPythonAvailable => PythonPath is not null;

    /// <inheritdoc />
    public string? PythonVersion { get; private set; }

    /// <inheritdoc />
    public string? DiscoveryInfo { get; private set; }

    /// <inheritdoc />
    public string? UnavailableReason { get; private set; }

    /// <inheritdoc />
    public async Task<bool> DiscoverAsync(string? preferredPath = null, CancellationToken cancellationToken = default)
    {
        // Reset state
        PythonPath = null;
        PythonVersion = null;
        DiscoveryInfo = null;
        UnavailableReason = null;

        // 1. Try user-specified path first (parameter takes priority over settings)
        var overridePath = preferredPath ?? _settings.PythonPathOverride;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (await TryPythonAsync(overridePath, "user-specified", cancellationToken))
            {
                return true;
            }
        }

        // 2. Try py launcher (most reliable on Windows)
        if (await TryPythonAsync("py", "py launcher", cancellationToken))
        {
            return true;
        }

        // 3. Try python in PATH
        if (await TryPythonAsync("python", "PATH", cancellationToken))
        {
            return true;
        }

        // 4. Try python3 in PATH (less common on Windows but possible)
        if (await TryPythonAsync("python3", "PATH (python3)", cancellationToken))
        {
            return true;
        }

        // 5. Try common installation paths
        var commonPaths = GetCommonPythonPaths();
        foreach (var path in commonPaths)
        {
            if (await TryPythonAsync(path, $"common path ({path})", cancellationToken))
            {
                return true;
            }
        }

        // 6. Try Windows Store Python
        var storePath = GetWindowsStorePythonPath();
        if (storePath is not null)
        {
            if (await TryPythonAsync(storePath, "Windows Store", cancellationToken))
            {
                return true;
            }
        }

        UnavailableReason = $"Python {MinPythonVersion}+ not found. Install from python.org or Microsoft Store.";
        return false;
    }

    private async Task<bool> TryPythonAsync(string pythonPath, string source, CancellationToken cancellationToken)
    {
        try
        {
            var (exitCode, output) = await RunProcessAsync(pythonPath, "--version", cancellationToken);
            if (exitCode != 0)
            {
                return false;
            }

            // Parse version from "Python 3.12.0"
            var match = VersionRegex().Match(output);
            if (!match.Success)
            {
                return false;
            }

            var version = match.Groups[1].Value;
            if (!IsVersionSufficient(version))
            {
                return false;
            }

            // If using py launcher, resolve to actual python.exe path
            // This is critical for MSIX packaged apps where py launcher
            // may not work correctly due to sandboxing
            var resolvedPath = pythonPath;
            if (pythonPath.Equals("py", StringComparison.OrdinalIgnoreCase))
            {
                var (resolveExitCode, resolveOutput) = await RunProcessAsync(
                    pythonPath,
                    "-c \"import sys; print(sys.executable)\"",
                    cancellationToken);

                if (resolveExitCode == 0 && !string.IsNullOrWhiteSpace(resolveOutput))
                {
                    resolvedPath = resolveOutput.Trim();
                    source = $"py launcher → {resolvedPath}";
                }
            }

            PythonPath = resolvedPath;
            PythonVersion = version;
            DiscoveryInfo = $"Found Python {version} via {source}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVersionSufficient(string version)
    {
        // Parse major.minor from version string
        var parts = version.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return false;
        }

        // Require 3.10+
        return major > 3 || (major == 3 && minor >= 10);
    }

    private static List<string> GetCommonPythonPaths()
    {
        var paths = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // Common Python installation locations on Windows
        for (var minor = 14; minor >= 10; minor--)
        {
            paths.Add(Path.Combine(programFiles, $"Python3{minor}", "python.exe"));
            paths.Add(Path.Combine(userProfile, "AppData", "Local", "Programs", "Python", $"Python3{minor}", "python.exe"));
        }

        return paths.Where(File.Exists).ToList();
    }

    private static string? GetWindowsStorePythonPath()
    {
        // Windows Store Python is installed in WindowsApps
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsApps = Path.Combine(localAppData, "Microsoft", "WindowsApps");

        if (Directory.Exists(windowsApps))
        {
            // Look for python3.exe (Windows Store uses this)
            var python3 = Path.Combine(windowsApps, "python3.exe");
            if (File.Exists(python3))
            {
                return python3;
            }

            var python = Path.Combine(windowsApps, "python.exe");
            if (File.Exists(python))
            {
                return python;
            }
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        // Python --version outputs to stdout or stderr depending on version
        var combined = string.IsNullOrEmpty(output) ? error : output;
        return (process.ExitCode, combined.Trim());
    }

    [GeneratedRegex(@"Python\s+(\d+\.\d+\.\d+)")]
    private static partial Regex VersionRegex();
}
