# RunForge Product Map

This document defines the canonical repositories for each RunForge product.

## Products

| Product | Description | Repository |
|---------|-------------|------------|
| **RunForge VS Code Extension** | VS Code extension that runs ML training jobs | [mcp-tool-shop-org/runforge-vscode](https://github.com/mcp-tool-shop-org/runforge-vscode) |
| **RunForge Desktop** | Windows app for browsing/inspecting run artifacts (read-only) | [mcp-tool-shop-org/runforge-desktop](https://github.com/mcp-tool-shop-org/runforge-desktop) |
| **RunForge Companion** | Mobile app for monitoring runs from phone | [mcp-tool-shop-org/runforge-companion](https://github.com/mcp-tool-shop-org/runforge-companion) |

## Relationship

```
┌─────────────────────────────────────────────────────────────────┐
│                    RunForge VS Code Extension                    │
│                   (creates runs, writes artifacts)               │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │  Workspace Dir  │
                    │  (run folders)  │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
     ┌────────────┐  ┌────────────┐  ┌────────────┐
     │  Desktop   │  │ Companion  │  │   Other    │
     │  (browse)  │  │ (monitor)  │  │  viewers   │
     └────────────┘  └────────────┘  └────────────┘
```

- **VS Code Extension** is the **write surface** (creates runs, produces artifacts)
- **Desktop** and **Companion** are **read surfaces** (view/export artifacts, never modify)
- All consumers share the same artifact schemas defined in the VS Code extension repo

## This Repository

**This is `runforge-desktop`** — the Windows desktop application for browsing and inspecting RunForge artifacts.

It does NOT:
- Create or modify runs
- Execute training jobs
- Write to the workspace

It DOES:
- Browse run history
- Display metrics and interpretability artifacts
- Export data to CSV
- Provide diagnostics for troubleshooting
