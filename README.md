# RunForge Desktop

**RunForge Desktop** is a Windows-native, read-only desktop application for browsing, inspecting, and exporting deterministic ML runs produced by RunForge.

It provides a visual control plane for RunForge artifacts—without modifying them—making ML runs auditable, inspectable, and exportable from a native Windows app.

> **Canonical upstream (artifacts, schemas, guarantees):**
> https://github.com/mcp-tool-shop-org/runforge-vscode

---

## What RunForge Desktop Is

RunForge Desktop is a **companion application** to RunForge tooling.
It focuses on **inspection, transparency, and trust**, not execution.

With RunForge Desktop you can:

- **Select** a local workspace containing RunForge outputs
- **Browse** runs safely (newest first)
- **View** run summaries derived from `run.json`
- **Navigate** model-aware interpretability artifacts:
  - Metrics v1
  - Feature importance (RandomForest)
  - Linear model coefficients (LogisticRegression, LinearSVC)
- **Open** the unified interpretability index (Phase 3.6)
- **Export** complete run bundles as zip files (copy-only)

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
run.json
  ├── metrics.v1.json
  ├── feature_importance.v1.json   (if supported)
  ├── linear_coefficients.v1.json  (if supported)
  └── interpretability.index.v1.json
  │
  ▼
RunForge Desktop (this app)
```

RunForge Desktop does not replace VS Code integration.
It complements it with a Windows-native browsing and inspection experience.

---

## Platform & Packaging

| Attribute | Value |
|-----------|-------|
| Platform | Windows 11 |
| UI framework | .NET MAUI |
| Packaging | MSIX |
| Install/uninstall | Clean, isolated, reversible |

The app follows standard Windows permission models for file access.

---

## Project Status

| Attribute | Value |
|-----------|-------|
| Current version | v0.1 (in development) |
| Scope | Read-only inspection and export |
| Acceptance criteria | [docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md](docs/PHASE-DESKTOP-0.1-ACCEPTANCE.md) |

Phase-style acceptance criteria are used to prevent scope drift.

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
