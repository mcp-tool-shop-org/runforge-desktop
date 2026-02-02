using System.Collections.ObjectModel;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using Timer = System.Timers.Timer;

namespace RunForgeDesktop.ViewModels;

public partial class RunsDashboardViewModel : ObservableObject, IDisposable
{
    private readonly IRunnerService _runnerService;
    private readonly IWorkspaceService _workspaceService;
    private readonly Timer _refreshTimer;
    private bool _disposed;

    public RunsDashboardViewModel(IRunnerService runnerService, IWorkspaceService workspaceService)
    {
        _runnerService = runnerService;
        _workspaceService = workspaceService;

        // Refresh every 2 seconds for live status
        _refreshTimer = new Timer(2000);
        _refreshTimer.Elapsed += async (s, e) => await RefreshRunsAsync();

        // Listen for workspace changes
        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(WorkspacePath));
            if (HasWorkspace)
            {
                _ = RefreshRunsAsync();
            }
        });
    }

    public ObservableCollection<RunDisplayItem> Runs { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasWorkspace => !string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath);

    public string? WorkspacePath => _workspaceService.CurrentWorkspacePath;

    public void StartPolling()
    {
        _refreshTimer.Start();
        _ = RefreshRunsAsync();
    }

    public void StopPolling()
    {
        _refreshTimer.Stop();
    }

    [RelayCommand]
    private async Task RefreshRuns()
    {
        await RefreshRunsAsync();
    }

    private async Task RefreshRunsAsync()
    {
        if (!HasWorkspace) return;

        try
        {
            var manifests = await _runnerService.GetAllRunsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Update existing items or add new ones
                var existingIds = Runs.Select(r => r.RunId).ToHashSet();
                var newIds = manifests.Select(m => m.RunId).ToHashSet();

                // Remove deleted runs
                var toRemove = Runs.Where(r => !newIds.Contains(r.RunId)).ToList();
                foreach (var item in toRemove)
                    Runs.Remove(item);

                // Update or add runs
                foreach (var manifest in manifests)
                {
                    var existing = Runs.FirstOrDefault(r => r.RunId == manifest.RunId);
                    if (existing != null)
                    {
                        existing.Update(manifest);
                    }
                    else
                    {
                        var index = 0;
                        // Insert in order (most recent first)
                        while (index < Runs.Count && Runs[index].CreatedAt > manifest.CreatedAt)
                            index++;
                        Runs.Insert(index, new RunDisplayItem(manifest));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task NewRun()
    {
        await Shell.Current.GoToAsync("newrun");
    }

    [RelayCommand]
    private async Task MultiRun()
    {
        await Shell.Current.GoToAsync("multirun");
    }

    [RelayCommand]
    private async Task OpenRun(RunDisplayItem? run)
    {
        if (run == null) return;
        await Shell.Current.GoToAsync($"rundetail?runId={run.RunId}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}

/// <summary>
/// Display item for a run in the list.
/// </summary>
public partial class RunDisplayItem : ObservableObject
{
    public RunDisplayItem(RunManifest manifest)
    {
        Update(manifest);
    }

    public string RunId { get; private set; } = "";

    [ObservableProperty]
    private string _name = "";

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

    public long CreatedAt { get; private set; }

    public void Update(RunManifest manifest)
    {
        RunId = manifest.RunId;
        Name = manifest.Name;
        Status = manifest.Status;
        Device = manifest.Device.ToString();
        CreatedAt = manifest.CreatedAt;

        StatusText = manifest.Status switch
        {
            RunStatus.Pending => "PENDING",
            RunStatus.Running => "RUNNING",
            RunStatus.Completed => "DONE",
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

        // Calculate elapsed time
        if (manifest.StartedAt.HasValue)
        {
            var start = DateTimeOffset.FromUnixTimeSeconds(manifest.StartedAt.Value);
            var end = manifest.CompletedAt.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(manifest.CompletedAt.Value)
                : DateTimeOffset.UtcNow;
            var duration = end - start;
            Elapsed = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
        }
        else
        {
            Elapsed = "--:--";
        }
    }
}
