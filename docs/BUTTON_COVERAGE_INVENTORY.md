# Button Coverage Inventory

This document tracks all interactive buttons in RunForge Desktop and their test coverage status.

**Last Updated:** Phase 12 Commit 6

---

## Coverage Summary

| Page | Total Buttons | Covered | Coverage % |
|------|---------------|---------|------------|
| WorkspaceDashboardPage | 3 | - | - |
| RunsDashboardPage | 2 | - | - |
| NewRunPage | 5 | - | - |
| LiveRunPage | 6 | - | - |
| MultiRunPage | 4 | - | - |
| SettingsPage | 13 | - | - |
| DiagnosticsPage | 10 | - | - |
| HelpPage | 0 | N/A | N/A |
| **Total** | **43** | **-** | **-** |

---

## Page: WorkspaceDashboardPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Select Workspace / Change | `SelectWorkspaceCommand` | ☐ | Opens folder picker |
| Browse Runs | Navigation to Runs tab | ☐ | Tab navigation |
| Diagnostics | `GoToDiagnosticsCommand` | ☐ | Navigation button |

---

## Page: RunsDashboardPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| MultiRun | `CreateMultiRunCommand` | ☐ | Navigation to MultiRunPage |
| Train | `CreateNewRunCommand` | ☐ | Navigation to NewRunPage |

---

## Page: NewRunPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Back (◀) | `GoBackCommand` | ☐ | Navigation back |
| Browse Dataset | `BrowseDatasetCommand` | ☐ | File picker for CSV |
| Show/Hide Advanced | `ToggleAdvancedCommand` | ☐ | Toggle UI section |
| Open Settings | Navigation to Settings | ☐ | Navigation button |
| Start Training | `CreateRunCommand` | ☐ | Creates and starts run |

---

## Page: LiveRunPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Back (◀) | `GoBackCommand` | ☐ | Navigation back |
| View Logs | `ViewLogsCommand` | ☐ | Opens log file |
| Open Settings | Navigation to Settings | ☐ | Navigation button |
| Cancel Run | `CancelRunCommand` | ☐ | Cancels running training |
| Open Output Folder | `OpenOutputFolderCommand` | ☐ | Opens explorer |
| Open Logs | `OpenLogsCommand` | ☐ | Opens logs folder |
| Copy Command | `CopyCommandCommand` | ☐ | Copies CLI command |

---

## Page: MultiRunPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Back (◀) | `GoBackCommand` | ☐ | Navigation back |
| Open Settings | Navigation to Settings | ☐ | Navigation button |
| Start Sweep | `StartSweepCommand` | ☐ | Starts hyperparameter sweep |
| Cancel | `CancelCommand` | ☐ | Cancels running sweep |

---

## Page: SettingsPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Back (◀) | `GoBackCommand` | ☐ | Navigation back |
| Browse Python | `BrowsePythonCommand` | ☐ | File picker |
| Auto-detect Python | `AutoDetectPythonCommand` | ☐ | Auto-detect logic |
| Validate Python | `ValidatePythonCommand` | ☐ | Runs validation |
| Browse Logs | `BrowseLogsCommand` | ☐ | Folder picker |
| Clear Logs | `ClearLogsCommand` | ☐ | Clears path |
| Browse Artifacts | `BrowseArtifactsCommand` | ☐ | Folder picker |
| Clear Artifacts | `ClearArtifactsCommand` | ☐ | Clears path |
| Open Workspace | `OpenWorkspaceCommand` | ☐ | Opens in explorer |
| Change Workspace | `ChangeWorkspaceCommand` | ☐ | Folder picker |
| Clear Workspace | `ClearWorkspaceCommand` | ☐ | Clears workspace |
| Save All Settings | `SaveCommand` | ☐ | Saves to disk |
| Reset to Defaults | `ResetCommand` | ☐ | Resets all settings |

---

## Page: DiagnosticsPage

| Button | Command | Test Status | Notes |
|--------|---------|-------------|-------|
| Back (◀) | `GoBackCommand` | ☐ | Navigation back |
| Open App Data | `OpenAppDataCommand` | ☐ | Opens folder |
| Refresh | `RefreshCommand` | ☐ | Refreshes diagnostics |
| Copy All | `CopyAllCommand` | ☐ | Copies to clipboard |
| Open Workspace | `OpenWorkspaceCommand` | ☐ | Opens folder |
| Refresh Storage | `RefreshStorageCommand` | ☐ | Refreshes storage info |
| Open Run Folder | Per-run command | ☐ | Opens specific run |
| Delete Run | Per-run command | ☐ | Shows delete confirm |
| Cancel (Delete Dialog) | Dialog dismiss | ☐ | Closes dialog |
| Delete (Confirm) | `ConfirmDeleteCommand` | ☐ | Deletes run |

---

## Page: HelpPage

No interactive buttons - content-only page.

---

## Test Implementation Strategy

### Unit Test Approach
For ViewModel commands:
1. Create test class per ViewModel
2. Test that command can execute (CanExecute)
3. Test command execution produces expected state changes
4. Mock dependencies (ISettingsService, IWorkspaceService, etc.)

### UI Test Approach (Future)
For navigation and visual feedback:
1. Use MAUI UITest framework
2. Verify button visibility and enabled states
3. Test navigation flows
4. Verify error message display

---

## Test File Mapping

| ViewModel | Test File | Status |
|-----------|-----------|--------|
| WorkspaceDashboardViewModel | WorkspaceDashboardViewModelTests.cs | ☐ |
| RunsDashboardViewModel | RunsDashboardViewModelTests.cs | ☐ |
| NewRunViewModel | NewRunViewModelTests.cs | ☐ |
| LiveRunViewModel | LiveRunViewModelTests.cs | ☐ |
| MultiRunViewModel | MultiRunViewModelTests.cs | ☐ |
| SettingsViewModel | SettingsViewModelTests.cs | ☐ |
| DiagnosticsViewModel | DiagnosticsViewModelTests.cs | ☐ |

---

## Coverage Criteria

A button is considered "covered" when:
1. ✅ Unit test exists for the bound command
2. ✅ Test verifies CanExecute condition
3. ✅ Test verifies Execute produces expected state
4. ✅ Test covers error/edge cases

---

**Note:** This inventory serves as the test coverage tracking document for Phase 12 Commit 6.
Full test implementation requires dedicated test time beyond initial Phase 12 scope.
