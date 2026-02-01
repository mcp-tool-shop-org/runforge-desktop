# What RunForge Desktop Is

**RunForge Desktop** is the Windows-native companion for inspecting and exporting ML training runs created by [RunForge](https://github.com/mcp-tool-shop-org/runforge-vscode).

## Desktop Edition

RunForge Desktop is the **read-only inspection and export tool** for the RunForge ecosystem. It focuses on one thing: making it easy to browse, filter, and export your training runs on Windows.

## What It Does

- **Browse runs** - Navigate your local RunForge workspace with a native Windows UI
- **Filter and search** - Find runs by name, date, status, or model family
- **Inspect artifacts** - View metrics, feature importance, linear coefficients, and more
- **Export data** - Copy metrics, export to CSV, or open raw JSON
- **Diagnose issues** - Built-in diagnostics page for troubleshooting workspace problems

## What It Doesn't Do

RunForge Desktop is intentionally limited in scope:

- **No training** - It doesn't run or schedule training jobs
- **No mutation** - It never modifies your run data or artifacts
- **No cloud sync** - All data stays local on your machine
- **No Python runtime** - Pure .NET MAUI, no dependencies on Python or ML frameworks

## Who It's For

- **ML practitioners** who use RunForge in VS Code and want a dedicated Windows app for browsing results
- **Teams** who want to quickly review training outcomes without opening an IDE
- **Anyone** who prefers a native desktop experience over browser-based tools

## The RunForge Ecosystem

| Component | Purpose | Repository |
|-----------|---------|------------|
| **RunForge** (VS Code) | Create and manage training runs | [runforge-vscode](https://github.com/mcp-tool-shop-org/runforge-vscode) |
| **RunForge Desktop** | Inspect and export runs on Windows | [runforge-desktop](https://github.com/mcp-tool-shop-org/runforge-desktop) |

## Getting Started

1. Install RunForge Desktop from the [Releases](https://github.com/mcp-tool-shop-org/runforge-desktop/releases) page
2. Point it at your RunForge workspace directory
3. Start browsing your training runs

---

*RunForge Desktop is part of the [mcp-tool-shop](https://github.com/mcp-tool-shop) ecosystem.*
