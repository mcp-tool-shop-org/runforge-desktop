using System.Collections.ObjectModel;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Controls;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using Timer = System.Timers.Timer;

namespace RunForgeDesktop.ViewModels;

[QueryProperty(nameof(RunId), "runId")]
public partial class LiveRunViewModel : ObservableObject, IDisposable
{
    private readonly IRunnerService _runnerService;
    private readonly Timer _refreshTimer;
    private bool _disposed;
    private readonly List<MetricPoint> _metricPoints = new();

    public LiveRunViewModel(IRunnerService runnerService)
    {
        _runnerService = runnerService;
        ChartDrawable = new LossChartDrawable();

        // Refresh every 500ms for smooth updates
        _refreshTimer = new Timer(500);
        _refreshTimer.Elapsed += async (s, e) => await RefreshAsync();
    }

    [ObservableProperty]
    private string _runId = "";

    [ObservableProperty]
    private string _runName = "";

    [ObservableProperty]
    private RunStatus _status;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private Color _statusColor = Colors.Gray;

    [ObservableProperty]
    private string _device = "";

    [ObservableProperty]
    private string _elapsed = "";

    [ObservableProperty]
    private int _currentEpoch;

    [ObservableProperty]
    private int _totalEpochs = 50;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _currentLoss;

    [ObservableProperty]
    private string _lossText = "";

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _copyCommandText = "";

    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>
    /// The chart drawable - bind GraphicsView.Drawable to this.
    /// </summary>
    public LossChartDrawable ChartDrawable { get; }

    /// <summary>
    /// Event fired when chart needs redraw.
    /// </summary>
    public event Action? ChartInvalidated;

    public bool IsPending => Status == RunStatus.Pending;
    public bool IsRunning => Status == RunStatus.Running;
    public bool IsCompleted => Status == RunStatus.Completed;
    public bool IsFailed => Status == RunStatus.Failed;
    public bool CanCancel => Status == RunStatus.Running || Status == RunStatus.Pending;
    public bool ShowError => IsFailed && !string.IsNullOrEmpty(ErrorMessage);

    partial void OnRunIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadRunAsync();
            _refreshTimer.Start();
        }
    }

    private async Task LoadRunAsync()
    {
        var manifest = await _runnerService.GetRunAsync(RunId);
        if (manifest == null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            UpdateFromManifest(manifest);
        });

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(RunId)) return;

        try
        {
            // Refresh manifest
            var manifest = await _runnerService.GetRunAsync(RunId);
            if (manifest != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() => UpdateFromManifest(manifest));
            }

            // Refresh logs
            var logs = await _runnerService.TailLogsAsync(RunId, 50);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Logs.Clear();
                foreach (var line in logs)
                    Logs.Add(line);
            });

            // Refresh metrics
            var metrics = await _runnerService.GetMetricsAsync(RunId);
            if (metrics.Count > 0)
            {
                var latest = metrics.Last();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CurrentStep = latest.Step;
                    CurrentEpoch = latest.Epoch;
                    CurrentLoss = latest.Loss;
                    LossText = $"Loss: {latest.Loss:F4}";
                    Progress = (double)latest.Epoch / TotalEpochs;
                    ProgressText = $"Epoch {latest.Epoch} / {TotalEpochs}";

                    // Update chart data - only keep last 200 points for performance
                    _metricPoints.Clear();
                    foreach (var m in metrics.TakeLast(200))
                    {
                        _metricPoints.Add(new MetricPoint(m.Step, m.Epoch, (float)m.Loss));
                    }
                    ChartDrawable.Points = _metricPoints;

                    // Signal chart needs redraw
                    ChartInvalidated?.Invoke();
                });
            }

            // Stop polling if completed
            if (Status != RunStatus.Running && Status != RunStatus.Pending)
            {
                _refreshTimer.Stop();
            }
        }
        catch
        {
            // Ignore refresh errors
        }
    }

    private void UpdateFromManifest(RunManifest manifest)
    {
        RunName = manifest.Name;
        Status = manifest.Status;
        Device = manifest.Device.ToString();
        TotalEpochs = manifest.TotalEpochs;

        StatusText = manifest.Status switch
        {
            RunStatus.Pending => "PENDING",
            RunStatus.Running => "RUNNING",
            RunStatus.Completed => "COMPLETED",
            RunStatus.Failed => "FAILED",
            _ => "UNKNOWN"
        };

        StatusColor = manifest.Status switch
        {
            RunStatus.Pending => Color.FromArgb("#9CA3AF"),
            RunStatus.Running => Color.FromArgb("#3B82F6"),
            RunStatus.Completed => Color.FromArgb("#22C55E"),
            RunStatus.Failed => Color.FromArgb("#EF4444"),
            _ => Color.FromArgb("#9CA3AF")
        };

        // Error message
        ErrorMessage = manifest.Error;

        // Build copy command
        CopyCommandText = $"python runner.py \"{manifest.OutputPath}\" {manifest.TotalEpochs} {manifest.Device}";

        // Elapsed time
        if (manifest.StartedAt.HasValue)
        {
            var start = DateTimeOffset.FromUnixTimeSeconds(manifest.StartedAt.Value);
            var end = manifest.CompletedAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(manifest.CompletedAt.Value)
                : DateTimeOffset.UtcNow;
            var duration = end - start;
            Elapsed = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
        }

        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(ShowError));
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (!CanCancel) return;

        var cancelled = await _runnerService.CancelRunAsync(RunId);
        if (cancelled)
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task OpenArtifacts()
    {
        // Open the run folder in file explorer
        var manifest = await _runnerService.GetRunAsync(RunId);
        if (manifest != null && !string.IsNullOrEmpty(manifest.OutputPath))
        {
            try
            {
                // Open folder in explorer
                System.Diagnostics.Process.Start("explorer.exe", manifest.OutputPath);
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    [RelayCommand]
    private async Task CopyCommand()
    {
        if (!string.IsNullOrEmpty(CopyCommandText))
        {
            await Clipboard.SetTextAsync(CopyCommandText);
        }
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        var manifest = await _runnerService.GetRunAsync(RunId);
        if (manifest != null && !string.IsNullOrEmpty(manifest.OutputPath))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", manifest.OutputPath);
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    [RelayCommand]
    private async Task OpenLogs()
    {
        var manifest = await _runnerService.GetRunAsync(RunId);
        if (manifest != null && !string.IsNullOrEmpty(manifest.OutputPath))
        {
            try
            {
                var logPath = Path.Combine(manifest.OutputPath, "stdout.log");
                if (File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start("notepad.exe", logPath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}
