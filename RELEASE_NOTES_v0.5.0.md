# RunForge v0.5.0 Release Notes

**Release Date**: 2026-02-02
**Theme**: User Experience & Customization

## Summary

v0.5.0 focuses on user experience improvements: theme customization (dark/light/system), enhanced error messaging with actionable guidance, expanded settings, and hyperparameter sweeps via MultiRun.

## Key Features

### Theme Support

- **Dark mode** (default) - Optimized for extended use
- **Light mode** - Clean, bright interface
- **System mode** - Follows Windows theme preference
- Instant theme switching without restart
- Theme persisted across sessions

### Hyperparameter Sweeps (MultiRun)

- Run multiple experiments with different hyperparameter combinations
- Configure learning rates, batch sizes, and optimizers as comma-separated lists
- Automatic grid search across all combinations
- Track best-performing configuration by final loss
- Progress tracking across all runs in sweep

### Enhanced Error Messaging

- Centralized `ErrorMessages.cs` with user-friendly error templates
- Clear explanations: what happened, why, and what to do next
- Action buttons in error banners (Open Settings, View Logs)
- No blame, no panic - just clear guidance

### Expanded Settings

- **Python Configuration** - Custom path override with validation
- **Output Directories** - Custom logs and artifacts paths
- **Training Defaults** - Device, epochs, batch size, learning rate
- **Appearance** - Theme selection
- Quick actions: Open workspace, app data, settings file

## Desktop Changes

### New Files

- `Core/ErrorMessages.cs` - Centralized error message templates
- `Views/HelpPage.xaml` - Help page placeholder
- `docs/MARKETING_ASSETS.md` - Store copy and launch blurbs

### Modified Files

- `App.xaml.cs` - Theme loading and application
- `SettingsViewModel.cs` - Theme selection, expanded settings
- `SettingsPage.xaml` - New appearance section, training defaults
- `ISettingsService.cs` / `SettingsService.cs` - AppTheme property
- `NewRunPage.xaml` - Updated placeholder, enhanced error display
- `MultiRunPage.xaml` - Enhanced error display with action buttons
- `LiveRunPage.xaml` - Enhanced error display
- `README.md` - Updated button labels, MultiRun documentation

### Settings Schema

New field in `settings.json`:
```json
{
  "AppTheme": "Dark"  // "Dark", "Light", or "System"
}
```

## UI/UX Improvements

- Consistent error styling across all pages
- Action buttons in error banners for quick resolution
- Theme picker with instant preview
- Updated preset names: Quick, Standard, Extended, Custom

## Breaking Changes

None. All new fields have defaults for backward compatibility.

## Migration

No migration required. Existing settings files are automatically extended with new defaults.

## Screenshots

New screenshots added to `docs/screenshots/`:
- `01-dashboard-dark.png` - Main dashboard
- `02-new-run-dark.png` - Training configuration
- `03-settings-dark.png` - Settings with theme picker
- `04-runs-list-dark.png` - Runs list view

## Test Summary

- Build: 0 errors, 109 warnings (AOT compatibility warnings, safe to ignore)
- All existing functionality preserved

## Next Steps

- v0.6.0: Live run chart improvements
- v0.6.0: Run comparison view
- v0.7.0: Export/import settings
