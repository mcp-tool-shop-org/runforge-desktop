# Release Notes v0.3.1

## Request Editor & Diff View

This release adds the ability to edit run configurations before execution and compare changes between parent/child runs.

### New Features

#### Request Editor
Full configuration editing before execution:
- **Normal Mode**: Edit key fields (preset, model family, device type, dataset path, label column)
- **Advanced Mode**: Full JSON editing with syntax validation
- **GPU Warning**: Shows message when GPU is selected (not yet supported)
- **Validation**: Blocks execution if required fields are missing
- Edit button on draft runs opens editor directly
- Edit button on completed runs creates a rerun copy first

#### Diff-from-Parent View
Compare changes between parent and child runs:
- **View Changes button** appears on runs with `rerun_from` lineage
- **Key-field diff table**: Shows only fields that changed with red (parent) / green (current) highlighting
- **Side-by-side JSON**: Full request comparison in modal
- Compared fields: preset, dataset.path, dataset.label_column, model.family, model.hyperparameters, device.type, name, notes

#### effective_config in result.json
CLI now captures the actual configuration used for training:
- Merged hyperparameters (defaults + overrides)
- Resolved device type, preset, dataset info
- Written to `result.json` after successful execution

#### CLI Dry-Run Mode
Fast validation without training:
```bash
runforge-cli run --run-dir <path> --dry-run [--max-rows N]
```
- Validates request.json, dataset existence, label column
- Writes result.json with `status: "succeeded"` and effective_config
- Emits RF stage tokens for timeline integration
- Used by integration tests for deterministic testing

#### Python Discovery Override
- Settings page now allows manual Python path configuration
- Useful when automatic discovery fails or multiple Python versions exist

### Technical Details

#### New Services
- `IRunRequestComparer` / `RunRequestComparer` - Compare run requests and identify differences
- `RunRequestService.SaveAsync()` - Atomic save with validation

#### New Models
- `DiffItem` - Single difference between parent/current (Field, ParentValue, CurrentValue, DisplayName)
- `RunRequestDiffResult` - Comparison result with differences list and both requests
- `EffectiveConfig` - Captured configuration in result.json

#### CLI Additions
- `--dry-run` flag: Validate without training
- `--max-rows N` flag: Limit dataset rows for testing
- `dry_run.py` module: Dry-run execution logic
- `EffectiveConfig` dataclass in result.py

#### ViewModel Additions
- `RequestEditorViewModel` - Full request editing with Normal/Advanced modes
- `RunDetailViewModel.ViewDiffFromParentCommand` - Load and show diff modal
- `RunDetailViewModel.HasParent` computed property
- `RunDetailViewModel.CanEdit` - True when request exists (regardless of result)

### Tests
- 254 .NET tests passing (13 new for RunRequestComparer)
- Integration tests using dry-run mode:
  - Edit → Save → Execute workflow
  - Validation blocking
  - Unknown field preservation
  - Multiple edits persistence

### Files Changed

#### New Files
- `DiffItem.cs` - Diff model classes
- `RunRequestComparer.cs` - Comparison service
- `RequestEditorPage.xaml/cs` - Editor UI
- `RequestEditorViewModel.cs` - Editor logic
- `dry_run.py` - CLI dry-run module
- `RunRequestComparerTests.cs` - Comparer unit tests
- `EditSaveExecuteIntegrationTests.cs` - Integration tests
- `CliDryRunTests.cs` - CLI dry-run tests

#### Updated Files
- `cli.py` - Added --dry-run and --max-rows arguments
- `result.py` - Added EffectiveConfig dataclass
- `runner.py` - Populate effective_config
- `RunDetailViewModel.cs` - Diff modal, CanEdit, HasParent
- `RunDetailPage.xaml` - View Changes button, diff modal overlay
- `MauiProgram.cs` - Service registration
- `ValueConverters.cs` - StringEqualsConverter for GPU warning
- `App.xaml` - Converter registration

## Upgrading

**Requirements:**
- Python 3.10+ with `pip install -e src/runforge-cli`
- No breaking changes from v0.3.0

**New CLI features:**
```bash
# Dry-run validation (no training)
runforge-cli run --run-dir <path> --dry-run

# Limit dataset rows for testing
runforge-cli run --run-dir <path> --dry-run --max-rows 100
```

## Smoke Test Checklist
- [ ] Open workspace with existing runs
- [ ] View run detail page
- [ ] Create rerun from completed run
- [ ] Edit request in Normal mode
- [ ] Switch to Advanced mode, validate JSON
- [ ] Execute dry-run (requires CLI installed)
- [ ] View diff from parent on rerun
- [ ] Verify diff table shows changes
- [ ] Verify side-by-side JSON loads

## Roadmap

- v0.3.2: Parent vs child comparison view (results + metrics delta)
- v0.4.0: GPU support with RTX 5080 optimizations
- v1.0.0: Stable release with full training loop

## Full Changelog

- feat(desktop): add diff-from-parent view with key-field table and side-by-side JSON
- feat(desktop): add request editor with Normal/Advanced modes
- feat(cli): add --dry-run mode for validation without training
- feat(cli): add effective_config to result.json
- feat(cli): add --max-rows flag for dataset limiting
- feat(desktop): add GPU warning when gpu device selected
- fix(desktop): CanEdit now true for any run with request (not just drafts)
- test: add RunRequestComparer unit tests
- test: add CLI dry-run integration tests
- test: add edit-save-execute integration tests
