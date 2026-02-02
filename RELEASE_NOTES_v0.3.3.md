# RunForge Desktop v0.3.3 Release Notes

## Multi-Select Compare Mode

This release adds the ability to compare any two runs, not just parent-child relationships.

### New Features

#### Multi-Select Mode in Runs List
- **Select Button**: Toggle multi-select mode from the runs list header
- **Action Bar**: Appears when runs are selected, showing selection count
- **Compare Button**: Enabled only when exactly 2 runs are selected
- **Selection Hints**: Guides users to select the right number of runs

#### Neutral A vs B Comparison
- **Unified Language**: Compare page now uses "Run A" and "Run B" instead of "Parent" and "Child"
- **Lineage Badge**: When comparing runs with a parent-child relationship, a green badge shows the lineage direction
- **Dual Entry Modes**: Navigate to compare from:
  - Multi-select in runs list (new)
  - "Compare with Parent" button in run detail (existing)

### UI Changes

#### Runs List Page
- New Select/Done toggle button
- Floating action bar with selection count
- Two CollectionViews for single-select and multi-select modes
- Visual checkbox indicators in multi-select mode

#### Compare Page
- Header shows lineage badge when detected (e.g., "A → B (A is parent)")
- Page title uses "vs" separator instead of arrow
- Column headers changed from "Parent/Child" to "Run A/Run B"
- Artifact sections show "In Both", "Only in Run A", "Only in Run B"
- Copy Summary uses Run A/B terminology

### Technical Details

- `RunsListViewModel`: Added multi-select state management with `IsMultiSelectMode`, `SelectedCount`, `SelectedRuns`
- `RunCompareViewModel`: Supports two navigation modes (A/B params and legacy runId/runDir)
- `RunComparePage.xaml.cs`: Added `OnMultiSelectionChanged` handler
- Lineage detection reads `request.json` to find `rerun_from` relationships

### Tests

Added 27 new tests for multi-select and compare navigation:
- Selection logic tests
- Navigation parameter tests
- Page title format tests
- Lineage detection tests
- Summary text format tests

### Test Results

- **Total Tests**: 317 (290 existing + 27 new)
- **All Passing**: ✓

### Breaking Changes

None. Existing "Compare with Parent" navigation continues to work.

### Compatibility

- .NET 10 Preview
- Windows 10/11
- Compatible with VS Code extension v0.3.x
