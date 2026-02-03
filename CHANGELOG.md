# Changelog

All notable changes to RunForge Desktop will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Phase 12: Project hygiene files (SECURITY.md, issue templates, PR template)
- Phase 12: PHASE12_AUDIT.md for tracking release readiness

## [0.9.0-rc.1] - TBD

### Changed
- Version bump to 0.9.0-rc.1 for Release Candidate phase
- Formalized release candidate versioning

### Added
- CHANGELOG.md following Keep a Changelog format

## [0.5.0] - 2026-02-01

### Added
- Theme support (Light/Dark modes)
- Enhanced error messages throughout the app
- Settings persistence
- Marketing assets for Microsoft Store

### Changed
- Improved overall stability

## [0.3.2] - 2026-02-01

### Added
- **Run Comparison View**: Side-by-side comparison of parent/child runs
  - Results overview with status badges and duration delta
  - Configuration changes table
  - All metrics comparison with severity indicators
  - Artifacts comparison (common/added/removed)
- Enhanced result.json parsing (summary, effective_config, artifacts, error)

### Changed
- 26 new tests for RunComparisonService (280 total tests)

## [0.3.1] - 2026-02-01

### Added
- **Request Editor**: Edit run configurations before execution
  - Normal mode for key fields (preset, model, device, dataset)
  - Advanced mode for full JSON editing with validation
  - GPU warning when GPU device selected
- **Diff-from-Parent View**: Compare changes between parent/child runs
  - Key-field diff table with color highlighting
  - Side-by-side JSON comparison modal
- effective_config in result.json from CLI

## [0.3.0] - 2026-02-01

### Added
- **runforge-cli**: Python CLI for executing ML training runs
  - Supports logistic_regression, random_forest, linear_svc
  - Real-time RF token streaming for timeline updates
  - Atomic file writes for crash safety
- **Execute Draft Runs**: Run training directly from Desktop
  - Real-time log streaming
  - Timeline and milestone updates
  - Cancel button for aborting runs
- **Rerun Workflow**: Create child runs from completed runs
  - Preserves lineage with rerun_from field
  - Copies configuration with editable overrides

## [0.2.3] - 2026-02-01

### Added
- Queue management improvements
- Enhanced run status tracking

## [0.2.2] - 2026-02-01

### Added
- Sweep parameter grid support
- Hyperparameter configuration UI

## [0.2.1] - 2026-02-01

### Added
- Live monitoring enhancements
- Real-time log streaming improvements

## [0.2.0] - 2026-02-01

### Added
- **V1 Contract Implementation**: Shared schema between Desktop and VS Code
  - Forward compatibility for unknown fields
  - Atomic writes with temp file + rename pattern
- **Run Request Panel**: Full request display on Run Detail page
  - Core fields, metadata, optional fields
- **JSON Operations**: View, Copy, Open File, Open Folder buttons

## [0.1.1] - 2026-02-01

### Added
- **Welcome Screen**: First-run experience guiding users to Settings
- **Empty State Messaging**: Context-aware messages for empty run lists
- **Artifact Status Badges**: Present, Not Available, Unsupported, Corrupt indicators
- **Diagnostics Page**: Copy All button, improved layout
- New app icon with RF logo and metrics bars

### Changed
- Debounced filtering (150ms) for better performance
- Updated terminology ("Artifacts Not Present" instead of "Not Found")
- Loading indicators during filter operations

### Fixed
- Filter state persistence when navigating between views

## [0.1.0] - 2026-01-15

### Added
- Initial release of RunForge Desktop
- Windows-native ML experiment tracker
- Run list with filtering and sorting
- Run detail view with artifacts
- Settings page with workspace configuration
- MSIX packaging for Windows 10/11

[Unreleased]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.9.0-rc.1...HEAD
[0.9.0-rc.1]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.5.0...v0.9.0-rc.1
[0.5.0]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.3.2...v0.5.0
[0.3.2]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.2.3...v0.3.0
[0.2.3]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/mcp-tool-shop-org/runforge-desktop/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/mcp-tool-shop-org/runforge-desktop/releases/tag/v0.1.0
