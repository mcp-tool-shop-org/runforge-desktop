using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for editing a run request before execution.
/// Supports Normal Mode (form) and Advanced Mode (raw JSON).
/// </summary>
public partial class RequestEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IRunRequestService _runRequestService;

    private string? _runDir;
    private string? _runId;
    private string? _originalJson;

    #region Observable Properties - Mode Toggle

    [ObservableProperty]
    private bool _isAdvancedMode;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _validationError;

    #endregion

    #region Observable Properties - Form Fields (Normal Mode)

    [ObservableProperty]
    private string? _runName;

    [ObservableProperty]
    private string _preset = "balanced";

    [ObservableProperty]
    private string _modelFamily = "logistic_regression";

    [ObservableProperty]
    private string? _datasetPath;

    [ObservableProperty]
    private string? _labelColumn;

    [ObservableProperty]
    private string _deviceType = "cpu";

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _rerunFrom;

    #endregion

    #region Observable Properties - Advanced Mode

    [ObservableProperty]
    private string? _rawJson;

    #endregion

    #region Options Collections

    public IReadOnlyList<string> PresetOptions { get; } = new[]
    {
        "fast",
        "balanced",
        "thorough",
        "custom"
    };

    public IReadOnlyList<string> ModelFamilyOptions { get; } = new[]
    {
        "logistic_regression",
        "random_forest",
        "linear_svc"
    };

    public IReadOnlyList<string> DeviceTypeOptions { get; } = new[]
    {
        "cpu",
        "gpu"
    };

    #endregion

    public RequestEditorViewModel(
        IWorkspaceService workspaceService,
        IRunRequestService runRequestService)
    {
        _workspaceService = workspaceService;
        _runRequestService = runRequestService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("runDir", out var dirObj) && dirObj is string dir)
        {
            _runDir = dir;
        }

        if (query.TryGetValue("runId", out var idObj) && idObj is string id)
        {
            _runId = id;
        }

        if (query.TryGetValue("runName", out var nameObj) && nameObj is string name)
        {
            RunName = name;
        }

        _ = LoadRequestAsync();
    }

    #region Property Change Handlers

    partial void OnPresetChanged(string value) => MarkChanged();
    partial void OnModelFamilyChanged(string value) => MarkChanged();
    partial void OnDatasetPathChanged(string? value) => MarkChanged();
    partial void OnLabelColumnChanged(string? value) => MarkChanged();
    partial void OnDeviceTypeChanged(string value) => MarkChanged();
    partial void OnRunNameChanged(string? value) => MarkChanged();
    partial void OnNotesChanged(string? value) => MarkChanged();

    partial void OnRawJsonChanged(string? value)
    {
        if (IsAdvancedMode)
        {
            MarkChanged();
            ValidateJson();
        }
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        if (value)
        {
            // Switching to Advanced Mode - sync form to JSON
            SyncFormToJson();
        }
        else
        {
            // Switching to Normal Mode - sync JSON to form
            if (!SyncJsonToForm())
            {
                // Invalid JSON - stay in advanced mode
                IsAdvancedMode = true;
                StatusMessage = "Cannot switch to Normal Mode: JSON is invalid";
            }
        }
    }

    private void MarkChanged()
    {
        if (!IsLoading)
        {
            HasChanges = true;
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task LoadRequestAsync()
    {
        if (string.IsNullOrEmpty(_runDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            StatusMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ValidationError = null;

        try
        {
            var fullRunDir = Path.Combine(
                _workspaceService.CurrentWorkspacePath,
                _runDir.Replace('/', Path.DirectorySeparatorChar));

            var result = await _runRequestService.LoadAsync(fullRunDir);

            if (result.IsSuccess && result.Value is not null)
            {
                var request = result.Value;

                // Populate form fields
                RunName = request.Name;
                Preset = request.Preset;
                ModelFamily = request.Model.Family;
                DatasetPath = request.Dataset.Path;
                LabelColumn = request.Dataset.LabelColumn;
                DeviceType = request.Device.Type;
                Notes = request.Notes;
                RerunFrom = request.RerunFrom;

                // Store original JSON for change detection
                _originalJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                RawJson = _originalJson;
                HasChanges = false;
            }
            else
            {
                StatusMessage = result.Error?.Message ?? "Failed to load request";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading request: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveRequestAsync()
    {
        if (string.IsNullOrEmpty(_runDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            StatusMessage = "Invalid run or workspace";
            return;
        }

        // Validate before saving
        if (IsAdvancedMode)
        {
            if (!ValidateJson())
            {
                StatusMessage = "Cannot save: JSON is invalid";
                return;
            }
        }
        else
        {
            if (!ValidateForm())
            {
                return;
            }
        }

        IsSaving = true;
        ValidationError = null;

        try
        {
            RunRequest request;

            if (IsAdvancedMode)
            {
                // Parse from JSON
                request = JsonSerializer.Deserialize<RunRequest>(RawJson!)!;
            }
            else
            {
                // Build from form
                request = BuildRequestFromForm();
            }

            // Validate the request object
            var errors = request.Validate();
            if (errors.Count > 0)
            {
                ValidationError = string.Join("\n", errors);
                StatusMessage = "Validation failed";
                return;
            }

            // Save to file
            var fullRunDir = Path.Combine(
                _workspaceService.CurrentWorkspacePath,
                _runDir.Replace('/', Path.DirectorySeparatorChar));

            var saveResult = await _runRequestService.SaveAsync(fullRunDir, request);

            if (saveResult.IsSuccess)
            {
                _originalJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                RawJson = _originalJson;
                HasChanges = false;
                StatusMessage = "Request saved";
                _ = ClearStatusAfterDelayAsync();
            }
            else
            {
                StatusMessage = "Failed to save request";
            }
        }
        catch (JsonException ex)
        {
            ValidationError = $"JSON error: {ex.Message}";
            StatusMessage = "Failed to parse JSON";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task SaveAndCloseAsync()
    {
        await SaveRequestAsync();

        if (!HasChanges && string.IsNullOrEmpty(ValidationError))
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (HasChanges)
        {
            // Could show confirmation dialog here
            // For now, just go back
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task BrowseDatasetAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Dataset File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv", ".parquet", ".json" } }
                })
            });

            if (result is not null)
            {
                // Make path relative to workspace if possible
                var workspacePath = _workspaceService.CurrentWorkspacePath;
                var fullPath = result.FullPath;

                if (!string.IsNullOrEmpty(workspacePath) &&
                    fullPath.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    DatasetPath = fullPath[(workspacePath.Length + 1)..]
                        .Replace(Path.DirectorySeparatorChar, '/');
                }
                else
                {
                    DatasetPath = fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Browse failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsAdvancedMode = !IsAdvancedMode;
    }

    [RelayCommand]
    private void RevertChanges()
    {
        if (!string.IsNullOrEmpty(_originalJson))
        {
            RawJson = _originalJson;
            SyncJsonToForm();
            HasChanges = false;
            ValidationError = null;
            StatusMessage = "Changes reverted";
            _ = ClearStatusAfterDelayAsync();
        }
    }

    [RelayCommand]
    private void FormatJson()
    {
        if (string.IsNullOrEmpty(RawJson)) return;

        try
        {
            var obj = JsonSerializer.Deserialize<JsonElement>(RawJson);
            RawJson = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            StatusMessage = "JSON formatted";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (JsonException ex)
        {
            ValidationError = $"Cannot format: {ex.Message}";
        }
    }

    #endregion

    #region Helpers

    private void SyncFormToJson()
    {
        try
        {
            var request = BuildRequestFromForm();
            RawJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ValidationError = null;
        }
        catch (Exception ex)
        {
            ValidationError = $"Error building JSON: {ex.Message}";
        }
    }

    private bool SyncJsonToForm()
    {
        if (string.IsNullOrEmpty(RawJson))
        {
            return false;
        }

        try
        {
            var request = JsonSerializer.Deserialize<RunRequest>(RawJson);
            if (request is null)
            {
                ValidationError = "Failed to parse JSON";
                return false;
            }

            // Update form fields (without triggering change detection)
            var wasLoading = IsLoading;
            IsLoading = true;

            RunName = request.Name;
            Preset = request.Preset;
            ModelFamily = request.Model.Family;
            DatasetPath = request.Dataset.Path;
            LabelColumn = request.Dataset.LabelColumn;
            DeviceType = request.Device.Type;
            Notes = request.Notes;
            RerunFrom = request.RerunFrom;

            IsLoading = wasLoading;
            ValidationError = null;
            return true;
        }
        catch (JsonException ex)
        {
            ValidationError = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    private bool ValidateJson()
    {
        if (string.IsNullOrEmpty(RawJson))
        {
            ValidationError = "JSON is empty";
            return false;
        }

        try
        {
            var request = JsonSerializer.Deserialize<RunRequest>(RawJson);
            if (request is null)
            {
                ValidationError = "Failed to parse JSON";
                return false;
            }

            var errors = request.Validate();
            if (errors.Count > 0)
            {
                ValidationError = string.Join("\n", errors);
                return false;
            }

            ValidationError = null;
            return true;
        }
        catch (JsonException ex)
        {
            ValidationError = $"JSON parse error: {ex.Message}";
            return false;
        }
    }

    private bool ValidateForm()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Preset))
            errors.Add("Preset is required");

        if (string.IsNullOrWhiteSpace(ModelFamily))
            errors.Add("Model family is required");

        if (string.IsNullOrWhiteSpace(DatasetPath))
            errors.Add("Dataset path is required");

        if (string.IsNullOrWhiteSpace(LabelColumn))
            errors.Add("Label column is required");

        if (string.IsNullOrWhiteSpace(DeviceType))
            errors.Add("Device type is required");

        if (errors.Count > 0)
        {
            ValidationError = string.Join("\n", errors);
            return false;
        }

        ValidationError = null;
        return true;
    }

    private RunRequest BuildRequestFromForm()
    {
        // Try to preserve original JSON extension data
        Dictionary<string, JsonElement>? originalExtensionData = null;
        if (!string.IsNullOrEmpty(_originalJson))
        {
            try
            {
                var original = JsonSerializer.Deserialize<RunRequest>(_originalJson);
                originalExtensionData = original?.ExtensionData;
            }
            catch
            {
                // Ignore parse errors
            }
        }

        return new RunRequest
        {
            Version = 1,
            Preset = Preset,
            Model = new RunRequestModel { Family = ModelFamily },
            Dataset = new RunRequestDataset
            {
                Path = DatasetPath ?? "",
                LabelColumn = LabelColumn ?? ""
            },
            Device = new RunRequestDevice { Type = DeviceType },
            CreatedAt = DateTime.UtcNow.ToString("o"),
            CreatedBy = "runforge-desktop@0.3.1",
            Name = string.IsNullOrWhiteSpace(RunName) ? null : RunName,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
            RerunFrom = RerunFrom,
            ExtensionData = originalExtensionData
        };
    }

    private async Task ClearStatusAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (StatusMessage is not null && !StatusMessage.StartsWith("Failed") && !StatusMessage.StartsWith("Error"))
        {
            StatusMessage = null;
        }
    }

    #endregion
}
