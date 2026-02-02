using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the runs list page with multi-select support.
/// </summary>
public partial class RunsListViewModel : ObservableObject
{
    private readonly IRunIndexService _runIndexService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IActivityMonitorService _activityMonitor;

    /// <summary>
    /// Debounce delay for search/filter operations (milliseconds).
    /// </summary>
    private const int FilterDebounceMs = 150;
    private CancellationTokenSource? _filterDebounceTokenSource;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isFiltering;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private RunStatusFilter _statusFilter = RunStatusFilter.All;

    [ObservableProperty]
    private RunIndexEntry? _selectedRun;

    [ObservableProperty]
    private string? _workspacePath;

    [ObservableProperty]
    private int _totalRunCount;

    [ObservableProperty]
    private int _filteredRunCount;

    [ObservableProperty]
    private bool _isFromCache;

    [ObservableProperty]
    private bool _isMultiSelectMode;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string? _selectionHint;

    public ObservableCollection<RunIndexEntry> Runs { get; } = [];

    /// <summary>
    /// Currently selected runs in multi-select mode.
    /// </summary>
    public ObservableCollection<object> SelectedRuns { get; } = [];

    /// <summary>
    /// Whether exactly 2 runs are selected (Compare enabled).
    /// </summary>
    public bool CanCompareSelected => SelectedCount == 2;

    /// <summary>
    /// Whether the action bar should be visible.
    /// </summary>
    public bool ShowActionBar => IsMultiSelectMode && SelectedCount > 0;

    // === Activity System Properties (for SystemStatusPanel and GpuQueueCard) ===

    /// <summary>
    /// Current system-wide activity state.
    /// </summary>
    public ActivitySystemState SystemState => _activityMonitor.SystemState;

    /// <summary>
    /// Reason for stalled/error state.
    /// </summary>
    public string? StatusReason => _activityMonitor.StatusReason;

    /// <summary>
    /// Number of jobs currently running (CPU + GPU).
    /// </summary>
    public int RunningCount => _activityMonitor.ActiveCpuSlots + _activityMonitor.ActiveGpuSlots;

    /// <summary>
    /// Number of jobs queued.
    /// </summary>
    public int ActivityQueuedCount => _activityMonitor.QueuedCount;

    /// <summary>
    /// Number of GPU jobs currently running.
    /// </summary>
    public int GpuRunningCount => _activityMonitor.ActiveGpuSlots;

    /// <summary>
    /// Number of GPU slots configured.
    /// </summary>
    public int TotalGpuSlots => _activityMonitor.TotalGpuSlots;

    /// <summary>
    /// Number of jobs waiting for GPU.
    /// </summary>
    public int QueuedGpuCount => _activityMonitor.QueuedGpuCount;

    /// <summary>
    /// Whether to show the GPU queue card.
    /// </summary>
    public bool ShowGpuQueueCard => _activityMonitor.HasGpuSlots &&
        (_activityMonitor.ActiveGpuSlots > 0 || _activityMonitor.QueuedGpuCount > 0);

    public RunsListViewModel(
        IRunIndexService runIndexService,
        IWorkspaceService workspaceService,
        IActivityMonitorService activityMonitor)
    {
        _runIndexService = runIndexService;
        _workspaceService = workspaceService;
        _activityMonitor = activityMonitor;

        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;

        // Forward activity monitor property changes
        _activityMonitor.PropertyChanged += OnActivityMonitorPropertyChanged;

        // Track selection changes
        SelectedRuns.CollectionChanged += (s, e) =>
        {
            SelectedCount = SelectedRuns.Count;
            UpdateSelectionHint();
            OnPropertyChanged(nameof(CanCompareSelected));
            OnPropertyChanged(nameof(ShowActionBar));
        };
    }

    private void OnActivityMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward relevant property changes to the UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(SystemState));
            OnPropertyChanged(nameof(StatusReason));
            OnPropertyChanged(nameof(RunningCount));
            OnPropertyChanged(nameof(ActivityQueuedCount));
            OnPropertyChanged(nameof(GpuRunningCount));
            OnPropertyChanged(nameof(TotalGpuSlots));
            OnPropertyChanged(nameof(QueuedGpuCount));
            OnPropertyChanged(nameof(ShowGpuQueueCard));
        });
    }

    partial void OnSelectedRunChanged(RunIndexEntry? value)
    {
        // Only navigate in single-select mode
        if (!IsMultiSelectMode && value is not null)
        {
            _ = NavigateToRunDetailAsync(value);
        }
    }

    partial void OnIsMultiSelectModeChanged(bool value)
    {
        if (!value)
        {
            // Exiting multi-select mode - clear selections
            SelectedRuns.Clear();
        }
        OnPropertyChanged(nameof(ShowActionBar));
    }

    partial void OnSelectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanCompareSelected));
        OnPropertyChanged(nameof(ShowActionBar));
    }

    private void UpdateSelectionHint()
    {
        SelectionHint = SelectedCount switch
        {
            0 => null,
            1 => "Select one more run to compare",
            2 => null,
            _ => "Select exactly 2 runs to compare"
        };
    }

    private async Task NavigateToRunDetailAsync(RunIndexEntry run)
    {
        var parameters = new Dictionary<string, object>
        {
            { "runId", run.RunId },
            { "runName", run.Name },
            { "runDir", run.RunDir }
        };

        await Shell.Current.GoToAsync(nameof(RunDetailPage), parameters);

        // Clear selection so the same run can be selected again
        SelectedRun = null;
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        WorkspacePath = e.NewPath;
        if (e.DiscoveryResult?.IsValid == true)
        {
            _ = LoadRunsAsync();
        }
        else
        {
            ClearRuns();
        }
    }

    [RelayCommand]
    private async Task LoadRunsAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        IsFromCache = false;

        try
        {
            var result = await _runIndexService.LoadIndexAsync(WorkspacePath).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (result.IsSuccess)
                {
                    TotalRunCount = result.Runs.Count;
                    IsFromCache = result.FromCache;
                    ApplyFilters();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    ClearRuns();
                }
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorMessage = $"Failed to load runs: {ex.Message}";
                ClearRuns();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshRunsAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        IsFromCache = false;

        try
        {
            var result = await _runIndexService.LoadIndexAsync(WorkspacePath, forceRefresh: true).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (result.IsSuccess)
                {
                    TotalRunCount = result.Runs.Count;
                    IsFromCache = false; // Force refresh never uses cache
                    ApplyFilters();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorMessage = $"Failed to refresh runs: {ex.Message}";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string? value)
    {
        _ = ApplyFiltersWithDebounceAsync();
    }

    partial void OnStatusFilterChanged(RunStatusFilter value)
    {
        // Status filter changes should be immediate (user clicked a button)
        ApplyFilters();
    }

    private async Task ApplyFiltersWithDebounceAsync()
    {
        // Cancel any pending filter operation
        _filterDebounceTokenSource?.Cancel();
        _filterDebounceTokenSource = new CancellationTokenSource();

        try
        {
            IsFiltering = true;
            await Task.Delay(FilterDebounceMs, _filterDebounceTokenSource.Token).ConfigureAwait(false);

            // Run filtering on UI thread
            await MainThread.InvokeOnMainThreadAsync(ApplyFilters);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled - newer filter operation is pending
        }
        finally
        {
            IsFiltering = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = _runIndexService.FilterRuns(
            runIdSubstring: SearchText,
            statusFilter: StatusFilter);

        Runs.Clear();
        foreach (var run in filtered)
        {
            Runs.Add(run);
        }

        FilteredRunCount = Runs.Count;
    }

    private void ClearRuns()
    {
        Runs.Clear();
        TotalRunCount = 0;
        FilteredRunCount = 0;
    }

    [RelayCommand]
    private async Task OpenWorkspaceFolderAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = WorkspacePath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently ignore if we can't open the folder
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedRuns.Clear();
    }

    [RelayCommand]
    private void ExitMultiSelectMode()
    {
        IsMultiSelectMode = false;
    }

    [RelayCommand(CanExecute = nameof(CanCompareSelected))]
    private async Task CompareSelectedRunsAsync()
    {
        if (SelectedRuns.Count != 2)
        {
            return;
        }

        var runA = SelectedRuns[0] as RunIndexEntry;
        var runB = SelectedRuns[1] as RunIndexEntry;

        if (runA is null || runB is null)
        {
            return;
        }

        // Navigate to compare page with both runs
        var parameters = new Dictionary<string, object>
        {
            { "runIdA", runA.RunId },
            { "runNameA", runA.Name },
            { "runDirA", runA.RunDir },
            { "runIdB", runB.RunId },
            { "runNameB", runB.Name },
            { "runDirB", runB.RunDir }
        };

        await Shell.Current.GoToAsync(nameof(RunComparePage), parameters);

        // Exit multi-select mode after navigation
        IsMultiSelectMode = false;
    }

    /// <summary>
    /// Called when selection changes in the CollectionView.
    /// </summary>
    public void OnSelectionChanged(IList<object> currentSelection)
    {
        // Sync with our observable collection
        SelectedRuns.Clear();
        foreach (var item in currentSelection)
        {
            SelectedRuns.Add(item);
        }
    }
}
