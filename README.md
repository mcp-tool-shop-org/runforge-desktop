# RunForge Desktop

**RunForge Desktop** is a Windows-native, read-only desktop application for browsing, inspecting, and exporting deterministic ML runs produced by RunForge.

It provides a visual control plane for RunForge artifacts—without modifying them—making ML runs auditable, inspectable, and exportable from a native Windows app.

> **Canonical upstream (artifacts, schemas, guarantees):**
> https://github.com/mcp-tool-shop-org/runforge-vscode

---

## Quick Start

### Installation

**Option 1: MSIX Package (Recommended)**
1. Download the `.msix` file from [Releases](https://github.com/mcp-tool-shop-org/runforge-desktop/releases)
2. Double-click to install
3. Launch from Start Menu

**Option 2: Build from Source**
```powershell
git clone https://github.com/mcp-tool-shop-org/runforge-desktop
cd runforge-desktop
dotnet run --project src/RunForgeDesktop/RunForgeDesktop.csproj
```

See [docs/INSTALL.md](docs/INSTALL.md) for detailed installation options.

### Usage

1. **Launch** RunForge Desktop
2. **Select Workspace** - Click "Select Workspace" and choose a folder containing RunForge outputs
3. **Browse Runs** - View all runs with filtering by status or search
4. **Inspect Details** - Click any run to view request, result, and metrics
5. **View Interpretability** - Navigate to interpretability artifacts for model insights
6. **Export** - Export metrics, feature importance, or coefficients to CSV

---

## Features

### Run Browsing
- Browse runs with newest-first ordering
- Filter by status: All, Succeeded, Failed, In-progress
- Search by run ID

### Run Detail View
- **Request** - Training parameters (preset, device, GPU reason)
- **Result** - Status, exit code, duration, errors
- **Metrics** - Accuracy and sample/feature counts
- **Open JSON** - View raw artifact files

### Interpretability Index
- **Metrics v1** - Categorized metrics with formatted values
- **Feature Importance v1** - Ranked features with visual bars (RandomForest)
- **Linear Coefficients v1** - Per-class coefficients with class selector (LogisticRegression)

### Export
- Export feature importance to CSV (Rank, Feature, Importance)
- Export linear coefficients to CSV (Class, Feature, Coefficient)
- Export metrics to CSV (Category, Metric, Value)

### Diagnostics
- View app version, framework, and memory usage
- View workspace path, discovery method, and index location
- Copy diagnostics to clipboard for support

---

## What RunForge Desktop Is

RunForge Desktop is a **companion application** to RunForge tooling.
It focuses on **inspection, transparency, and trust**, not execution.

With RunForge Desktop you can:

- **Select** a local workspace containing RunForge outputs
- **Browse** runs safely (newest first)
- **View** run summaries derived from `run.json`
- **Navigate** model-aware interpretability artifacts
- **Export** data to CSV for further analysis

All data is read directly from artifacts on disk.

---

## What It Explicitly Does Not Do

RunForge Desktop does **not**:

- Train or re-train models
- Modify any RunForge output
- Rewrite schemas or artifacts
- Upload data to the cloud
- Collect telemetry
- Require accounts or sign-in

**This app is read-only by design.**

---

## Core Principles

RunForge Desktop follows the same principles as RunForge itself:

### Artifacts are the source of truth
The app renders what exists; it does not invent or infer data.

### Truthful rendering
The UI distinguishes clearly between:
- "present" vs "missing"
- "unsupported" vs "not generated"
- "corrupt" vs "empty"

### Determinism preserved
Exported artifacts preserve original bytes exactly.

### Auditor-safe UX
Calm, explicit UI with clear diagnostics and no hidden behavior.

---

## How It Fits in the RunForge Ecosystem

```
Dataset
  │
  ▼
RunForge training (CLI / VS Code)
  │
  ▼
.runforge/
  ├── index.json
  └── runs/
      └── run_20240101_123456/
          ├── run.json
          ├── request.json
          ├── result.json
          ├── metrics.json
          └── interpretability/
              ├── interpretability.index.v1.json
              ├── metrics.v1.json
              ├── feature_importance.v1.json   (if supported)
              └── linear_coefficients.v1.json  (if supported)
  │
  ▼
RunForge Desktop (this app)
```

RunForge Desktop does not replace VS Code integration.
It complements it with a Windows-native browsing and inspection experience.

---

## System Requirements

| Requirement | Value |
|-------------|-------|
| OS | Windows 10 (1809+) or Windows 11 |
| Architecture | x64 |
| Runtime | .NET 10 (bundled in MSIX) |
| Disk Space | ~100 MB |

---

## Platform & Packaging

| Attribute | Value |
|-----------|-------|
| Platform | Windows 10/11 |
| UI framework | .NET MAUI |
| Packaging | MSIX (self-contained) |
| Install/uninstall | Clean, isolated, reversible |

The app follows standard Windows permission models for file access.

---

## Project Status

| Attribute | Value |
|-----------|-------|
| Current version | v0.1.1 |
| Scope | Read-only inspection and export |
| Acceptance criteria | [docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md](docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md) |

Phase-style acceptance criteria are used to prevent scope drift.

---

## Development

### Prerequisites

- .NET 10 SDK
- Windows 10/11
- Visual Studio 2022 (17.12+) with MAUI workload, OR VS Code with .NET MAUI extension

### Build

```powershell
# Debug build
dotnet build

# Run tests
dotnet test

# Release build
.\scripts\build-release.cmd
```

### Project Structure

```
runforge-desktop/
├── src/
│   ├── RunForgeDesktop/          # MAUI app (UI, ViewModels)
│   └── RunForgeDesktop.Core/     # Core services, models
├── tests/
│   └── RunForgeDesktop.Core.Tests/
├── docs/
│   ├── PHASE-DESKTOP-0.1-ACCEPTANCE.md
│   └── INSTALL.md
└── scripts/
    ├── build-msix.ps1
    └── build-release.cmd
```

---

## Relationship to RunForge Core

All schemas, guarantees, and artifact formats are defined and frozen in:

> https://github.com/mcp-tool-shop-org/runforge-vscode

This repository contains:
- No training logic
- No schema definitions
- No contract ownership

RunForge Desktop **consumes** those artifacts faithfully.

---

## Intended Audience

- Developers training models locally
- Researchers who need inspectable results
- Auditors and reviewers
- Teams that value determinism and provenance
- Windows users who prefer native tooling

---

## License

MIT License - See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions should respect the core constraint:

> **No feature may compromise read-only guarantees or artifact integrity.**

Before proposing changes, please review:
- [docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md](docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md)
- [TRUST_MODEL.md](https://github.com/mcp-tool-shop-org/runforge-vscode/blob/main/docs/TRUST_MODEL.md) (in the RunForge repo)

---

## Support

- **Issues**: [GitHub Issues](https://github.com/mcp-tool-shop-org/runforge-desktop/issues)
- **Diagnostics**: Use the Diagnostics page to copy system info for bug reports
