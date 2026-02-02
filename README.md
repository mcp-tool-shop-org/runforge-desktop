# RunForge Desktop

**RunForge Desktop** is a Windows-native desktop application for creating, monitoring, and inspecting ML training runs.

It provides a visual control plane for ML experiments—creating runs, monitoring live training progress with real-time charts, and browsing completed runs with full artifact inspection.

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
2. **Select Workspace** - Click "Select Workspace" and choose a folder for your ML experiments
3. **Start Training** - Click "+ New Run" to configure and launch a training run
4. **Monitor Live** - Watch training progress with real-time loss charts and logs
5. **Browse Runs** - View all runs with filtering by status
6. **Inspect Details** - Click any run to view metrics, artifacts, and outputs

---

## Features

### Training Run Creation
- Configure new training runs with presets (SLOAQ, ResNet, BERT, GPT2, etc.)
- GPU/CPU device selection with automatic detection
- Optional custom dataset path
- One-click training launch

### Live Monitoring
- Real-time loss chart with automatic updates
- Live log streaming from training process
- Progress tracking (epoch, step, elapsed time)
- Cancel running training at any time

### Run Browsing
- Browse runs with newest-first ordering
- Filter by status: Pending, Running, Completed, Failed, Cancelled
- View run details and outputs

### Run Inspection
- **Metrics** - Loss curves, accuracy, training statistics
- **Logs** - Full stdout/stderr from training process
- **Artifacts** - Open output folder, copy training command

### Diagnostics
- View app version, framework, and memory usage
- View workspace path and Python configuration
- Copy diagnostics to clipboard for support

---

## What RunForge Desktop Is

RunForge Desktop is a **standalone ML experiment tracker** for Windows.
It focuses on **local execution, transparency, and simplicity**.

With RunForge Desktop you can:

- **Create** training runs with preset configurations
- **Monitor** live training with real-time charts and logs
- **Browse** completed runs and their outputs
- **Inspect** metrics, logs, and artifacts
- **Manage** runs (cancel, view outputs, copy commands)

All training runs locally on your machine using Python.

---

## What It Does Not Do

RunForge Desktop does **not**:

- Upload data to the cloud
- Collect telemetry
- Require accounts or sign-in
- Require internet access (after installation)

---

## Core Principles

### Local-first
All training runs on your machine. No cloud required.

### Transparent
See exactly what's happening: live logs, real-time metrics, full process control.

### Simple
One workspace, clear presets, no configuration files to manage.

### Auditable
All run artifacts saved to disk for inspection and reproducibility.

---

## How It Works

```
RunForge Desktop
  │
  ├── Select Workspace (any folder)
  │
  ├── Create Run (preset + device + optional dataset)
  │
  ├── Spawn Python training process
  │
  ▼
.ml/
  └── runs/
      └── 20240101-123456-myrun-abc1/
          ├── run.json       (manifest)
          ├── metrics.jsonl  (live metrics)
          ├── stdout.log     (live logs)
          └── stderr.log     (errors)
```

RunForge Desktop manages the full lifecycle: creation, execution, monitoring, and inspection.

---

## System Requirements

| Requirement | Value |
|-------------|-------|
| OS | Windows 10 (1809+) or Windows 11 |
| Architecture | x64 |
| Runtime | .NET 10 (bundled in MSIX) |
| Python | 3.10+ (for training) |
| GPU | Optional (CUDA for GPU training) |
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
| Current version | v1.0.0 |
| Scope | ML training, monitoring, and inspection |

See [RELEASE_NOTES_v0.4.0.md](RELEASE_NOTES_v0.4.0.md) for recent changes.

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

- Developers training models locally on Windows
- Researchers who need simple, inspectable experiment tracking
- Anyone who wants a native Windows ML training UI
- Teams that want local-first, no-cloud ML workflows

---

## License

MIT License - See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions welcome. Please respect the core principles:

- Keep it simple and local-first
- No cloud dependencies or telemetry
- Clear, actionable error messages

---

## Reliability Gauntlets

RunForge ships with a repeatable reliability suite you can run locally to validate queueing, pause/resume, cancellation, crash recovery, fairness, disk drift resilience, and Desktop reconnect behavior.

| Gauntlet | Focus |
|----------|-------|
| G1 | max_parallel enforcement |
| G2 | Pause/Resume |
| G3 | Cancel determinism |
| G4 | Crash recovery |
| G5 | Fair scheduling |
| G6 | Disk drift resilience |
| G7 | Desktop reconnect |
| G8-G10 | GPU support (v0.4.0+) |

See: [`docs/GAUNTLETS.md`](docs/GAUNTLETS.md)

---

## Support

- **Issues**: [GitHub Issues](https://github.com/mcp-tool-shop-org/runforge-desktop/issues)
- **Diagnostics**: Use the Diagnostics page to copy system info for bug reports
