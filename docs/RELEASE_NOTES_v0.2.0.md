# RunForge Desktop v0.2.0 Release Notes

**Release Date:** 2026-02-01

## Overview

v0.2.0 introduces the **V1 Contract** for RunRequest, enabling interoperability between RunForge Desktop and the VS Code extension. This release focuses on displaying, viewing, and exporting run request data.

## New Features

### V1 Contract Implementation

- **Shared Schema**: Both Desktop and VS Code now use the same `request.json` schema (v1)
- **Forward Compatibility**: Unknown fields from newer versions are preserved on load/save
- **Atomic Writes**: File operations use temp file + rename pattern for crash safety

### Run Request Panel

The Run Detail page now displays the full run request:

- **Core Fields**: Preset, model family, dataset path, label column, device type
- **Metadata**: Created timestamp (localized), created by client
- **Optional Fields**: User name, tags, notes, rerun source, GPU reason

### JSON Operations

- **View JSON**: Opens a modal with formatted, syntax-highlighted JSON
- **Copy JSON**: Copies request to clipboard (with success toast)
- **Open File**: Opens `request.json` in your default editor
- **Open Folder**: Opens the run folder in file explorer

### Error States

- Missing `request.json` shows a helpful warning with action buttons
- Parse errors display the error message with fallback options

## Breaking Changes

None. This release maintains compatibility with all v0.1.x workspaces.

## Technical Details

### V1 Contract Schema

See `docs/V1_CONTRACT.md` for the complete specification.

Required fields:
- `version` (integer, must be >= 1)
- `preset` (fast | balanced | thorough | custom)
- `dataset.path`, `dataset.label_column`
- `model.family` (logistic_regression | random_forest | linear_svc)
- `device.type` (cpu | gpu)
- `created_at` (ISO-8601 timestamp)
- `created_by` (format: `client@version`)

Optional fields:
- `name`, `tags`, `notes`, `rerun_from`
- `device.gpu_reason`
- `model.hyperparameters`

### Test Coverage

- 95 total tests (13 contract compliance, 14 service tests, 15 model tests)
- All test vectors in `docs/test-vectors/` validate correctly

## Upgrade Instructions

1. Update to v0.2.0
2. Open any existing workspace
3. Runs created by VS Code v0.3.6+ will show the full request panel

No migration needed. Older runs without `request.json` will show a "Request Not Available" message.

## Contributors

- Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
