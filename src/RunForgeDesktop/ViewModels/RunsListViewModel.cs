using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the runs list page.
/// </summary>
public partial class RunsListViewModel : ObservableObject
{
    private readonly IRunIndexService _runIndexService;
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private bool _isLoading;

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

    public ObservableCollection<RunIndexEntry> Runs { get; } = [];

    public RunsListViewModel(IRunIndexService runIndexService, IWorkspaceService workspaceService)
    {
        _runIndexService = runIndexService;
        _workspaceService = workspaceService;

        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;
    }

    partial void OnSelectedRunChanged(RunIndexEntry? value)
    {
        if (value is not null)
        {
            _ = NavigateToRunDetailAsync(value);
        }
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

        try
        {
            var result = await _runIndexService.LoadIndexAsync(WorkspacePath);

            if (result.IsSuccess)
            {
                TotalRunCount = result.Runs.Count;
                ApplyFilters();
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
                ClearRuns();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load runs: {ex.Message}";
            ClearRuns();
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

        try
        {
            var result = await _runIndexService.LoadIndexAsync(WorkspacePath, forceRefresh: true);

            if (result.IsSuccess)
            {
                TotalRunCount = result.Runs.Count;
                ApplyFilters();
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh runs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilters();
    }

    partial void OnStatusFilterChanged(RunStatusFilter value)
    {
        ApplyFilters();
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
}
