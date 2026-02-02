# RunForge Desktop v0.3.2 Release Notes

**Release Date:** 2026-02-01

## Overview

v0.3.2 introduces the **Run Comparison View** - a powerful feature for analyzing differences between parent and child runs. When you rerun a training job with modified parameters, you can now see exactly what changed and how it affected results.

## New Features

### Run Comparison View

Navigate to any child run (created via "Rerun") and click **"Compare with Parent"** to open a comprehensive comparison:

#### Results Overview
- Side-by-side status badges (succeeded/failed)
- Duration comparison with delta (e.g., "10.0s → 8.0s (-2.0s)")
- Primary metric with severity highlighting:
  - **Green (↑)**: Improved
  - **Red (↓)**: Degraded
  - **Gray (─)**: Unchanged

#### Configuration Changes
- Shows effective config differences (what was actually used at runtime)
- Clear parent → child visualization with color-coded values
- Fields compared: preset, model.family, device.type, dataset.path, and more

#### All Metrics Comparison
- Full table of all metrics from both runs
- Parent value, child value, delta, and severity indicator
- Primary metric highlighted with bold text

#### Artifacts Comparison
- **Common**: Artifacts in both runs (with size delta)
- **Added in Child**: New artifacts not in parent
- **Removed**: Artifacts in parent but not in child

### Enhanced Result Parsing

RunForge Desktop now fully parses `result.json` structure:
- `summary.primary_metric` and `summary.metrics`
- `effective_config` (merged runtime configuration)
- `artifacts[]` with path, type, and size
- `error` object for failed runs

## Technical Changes

### New Files
| File | Purpose |
|------|---------|
| `RunComparison.cs` | Models: `RunComparisonResult`, `MetricDelta`, `ArtifactComparison` |
| `RunComparisonService.cs` | `IRunComparisonService` with `CompareWithParentAsync` |
| `RunComparePage.xaml` | Full comparison UI |
| `RunCompareViewModel.cs` | Page ViewModel |

### Modified Files
| File | Changes |
|------|---------|
| `RunResult.cs` | Parse full result.json structure |
| `ValueConverters.cs` | +6 converters for status/severity display |
| `RunDetailPage.xaml` | "Compare with Parent" button |
| `RunDetailViewModel.cs` | Navigation command |

### New Converters
- `StatusToColorConverter` - Run status to color
- `SeverityToColorConverter` - Metric severity to background color
- `SeverityToTextColorConverter` - Metric severity to text color
- `SeverityToIconConverter` - Severity to arrow icon (↑/↓/─)
- `BoolToFontAttributeConverter` - Boolean to Bold/None
- `IntToBoolConverter` - Int > 0 to boolean

## Test Coverage

- **26 new tests** for `RunComparisonService`
- **280 total tests** passing
- Coverage includes:
  - Metric delta calculations and severity
  - Duration formatting
  - Artifact set operations (common/added/removed)
  - Effective config diffing
  - Edge cases (missing parent, incomplete runs)

## Primary Metric Selection Policy

When comparing runs, the primary metric is selected as follows:
1. Use `summary.primary_metric` if present
2. Fall back to first metric in preferred order: `accuracy`, `f1_score`, `loss`
3. Display "No primary metric" if none available

## Upgrade Notes

- No breaking changes
- Backward compatible with existing run directories
- Desktop gracefully handles runs without `effective_config` or `summary`

## Known Limitations

- Comparison requires both runs to have `result.json`
- Draft runs (no result) cannot be compared
- Currently only supports parent→child comparison (v0.3.3 will add arbitrary run comparison)

## Screenshots

### Compare Button on Run Detail Page
The "Compare with Parent" button appears for any run that has a parent (created via rerun).

### Results Overview
Status badges, duration delta, and primary metric with severity highlighting.

### Configuration Changes Table
Side-by-side view of what changed in the effective configuration.

### Metrics Comparison
Full table of all metrics with deltas and severity indicators.

---

**Full Changelog:** [v0.3.1...v0.3.2](https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.3.1...v0.3.2)
