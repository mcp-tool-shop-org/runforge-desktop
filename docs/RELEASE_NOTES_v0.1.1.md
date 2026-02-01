# RunForge Desktop v0.1.1 Release Notes

**Release Date:** February 2026
**Tag:** [v0.1.1](https://github.com/mcp-tool-shop-org/runforge-desktop/releases/tag/v0.1.1)

## Overview

v0.1.1 is a polish release focused on improving the first-run experience, artifact availability messaging, and overall UI responsiveness. No breaking changes from v0.1.0.

## What's New

### First-Run & Empty States

- **Welcome screen** when no workspace is configured - guides new users to Settings
- **Empty run list messaging** when workspace has no runs - clear "No runs found" with helpful context
- **Filter-aware empty states** - distinguishes between "no runs exist" and "no runs match your filter"

### Artifact Availability Messaging

- **Status badges** on artifact cards showing Present, Not Available, Unsupported, or Corrupt
- **Contextual reasons** explaining why an artifact isn't available (e.g., "This model type does not support this artifact")
- **Color-coded indicators** - green for present, gray for not available, orange for unsupported, red for corrupt

### Performance & Responsiveness

- **Debounced filtering** (150ms) prevents UI jank during rapid typing in search
- **Loading indicators** show when filters are being applied
- **Cache indicators** display when results come from cached data
- **Filter state persistence** - search text and filters persist when navigating between views

### Diagnostics Page Polish

- **Copy All button** for easy sharing of diagnostic information
- **Status feedback** when diagnostics are copied
- **Improved OS version display** with build number
- **Cleaner layout** with better visual hierarchy

### Language & Tone

- Replaced "Artifacts Not Found" with "Artifacts Not Present" (less alarming)
- Updated tooltips to use consistent terminology
- Improved empty state messaging throughout

### Visual Updates

- **New app icon** featuring RF logo with ascending metrics bars
- **Updated splash screen** matching the new branding

## Technical Details

- **53 unit tests** covering core functionality
- **Windows 10 1809+** minimum requirement unchanged
- **Self-contained MSIX** - no runtime dependencies

## Upgrade Path

Direct upgrade from v0.1.0. Settings and workspace configuration are preserved.

## Known Issues

- MVVMTK0045 AOT compatibility warnings during build (cosmetic, does not affect functionality)

## Download

Get the latest MSIX from the [Releases](https://github.com/mcp-tool-shop-org/runforge-desktop/releases/tag/v0.1.1) page.

---

*See [WHAT_RUNFORGE_DESKTOP_IS.md](./WHAT_RUNFORGE_DESKTOP_IS.md) for product positioning.*
