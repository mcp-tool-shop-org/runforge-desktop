# RunForge Desktop v0.1 — Acceptance Criteria

> **Platform:** Windows 11
> **UI Framework:** .NET MAUI
> **Packaging:** MSIX
> **Status:** Pre-implementation acceptance criteria
> **Companion to:** https://github.com/mcp-tool-shop-org/runforge-vscode

---

## 1. Scope

RunForge Desktop v0.1 is a **read-only** Windows desktop companion that makes RunForge artifacts discoverable, navigable, and exportable.

### This release includes:
- Workspace selection
- Run listing and filtering
- Run detail views (summary + interpretability index)
- One-click viewing of linked artifacts (metrics / feature importance / coefficients)
- Exporting run bundles (zip)

### This release explicitly excludes:
- Training, re-training, or invoking the ML runner
- Editing any artifact files
- Cloud sync, accounts, or telemetry
- Any mutation of `.runforge` or run output directories
- Any schema migration or rewriting

---

## 2. Non-Negotiable Principles

### 2.1 Read-only by default
The app must not modify RunForge output unless the user explicitly exports a copy.

### 2.2 Truthful rendering
The UI may summarize artifacts, but must not invent data. It must clearly distinguish:
- "present" vs "missing"
- "unsupported" vs "not generated"
- "corrupt" vs "empty"

### 2.3 Artifacts remain source of truth
The app reads artifacts exactly as produced by RunForge.

### 2.4 Stable performance & safety
The app must remain responsive when browsing large run lists (hundreds+).

---

## 3. Inputs and Discovery Rules

### 3.1 Workspace selection

The user can choose a workspace folder.

**Acceptance:**
- The app detects RunForge presence by locating:
  - `.runforge/index.json` (preferred), OR
  - Run folders via configured convention (if index absent)

### 3.2 No hidden assumptions

If `.runforge/index.json` is missing, corrupt, or empty, the app must:
- Show an actionable message
- Allow the user to select a different folder
- **Not** create `.runforge/`

---

## 4. Runs Listing (Browse)

### 4.1 Runs table view

The app displays runs in a list/table with at least:

| Column | Description |
|--------|-------------|
| `created_at` | Human-friendly + raw on hover or details |
| `run_id` | Unique run identifier |
| `model_family` | If present |
| Dataset fingerprint | Short form (first 8 chars) |

**Interpretability available indicators:**
- ✓ metrics v1
- ✓ feature importance
- ✓ linear coefficients
- ✓ interpretability index

**Acceptance:**
- Runs are shown newest first
- The app must not reorder or mutate the underlying append-only index

### 4.2 Filtering & search

**Minimum viable filters:**
- Search by `run_id` substring
- Filter by `model_family` (if present)

**Acceptance:**
- Filtering does not require re-reading all files repeatedly (cache allowed)
- Results update within a reasonable UI response (no freezes on typical datasets)

---

## 5. Run Detail View

### 5.1 Summary panel (run.json)

Selecting a run shows a "Run Summary" view derived from `run.json`, including:
- Dataset path (as recorded)
- Dataset fingerprint
- Label column
- Dropped rows (missing values)
- Phase 2 summary metrics (accuracy / num_samples / num_features)
- `model_family` + hyperparameters + profile info (if present)

**Acceptance:**
- If `run.json` is missing or corrupt, the app shows a clear error state and does not crash

### 5.2 Interpretability index panel (3.6)

If present, the app loads:
- `artifacts/interpretability.index.v1.json`

**Acceptance:**
- Shows which artifacts exist, with schema versions and relative paths
- Provides one-click navigation to each artifact view

**If absent:**
- The app shows "Interpretability index not present for this run"
- Still allows artifact discovery via known conventions where safe

---

## 6. Artifact Viewers (Read-Only)

### 6.1 Metrics v1 viewer

If `metrics.v1.json` is present:
- Show profile ID
- Show key metrics (as provided)
- Link to open raw JSON

**Acceptance:**
- Viewer clearly indicates which metrics profile is active

### 6.2 Feature importance viewer (RF)

If `feature_importance.v1.json` is present:
- Show top-k feature names
- Visual bars allowed
- Link to open raw JSON

**Acceptance:**
- Viewer does not compute new statistics; it only presents

### 6.3 Linear coefficients viewer

If `linear_coefficients.v1.json` is present:
- Show per-class sections
- Show top-k names with bars
- Explicit note: coefficients are in standardized space
- Link to open raw JSON

**Acceptance:**
- Multiclass grouping preserved exactly as artifact defines

### 6.4 Corrupt or schema-invalid artifacts

If an artifact fails validation or parsing:
- Show a "Corrupt/Invalid artifact" state
- Provide:
  - File path
  - Validation error summary (short)
  - "Open raw file" action

---

## 7. Export Run Bundle

### 7.1 Export zip

The user can export a selected run as a zip containing:
- Run folder contents
- `.runforge` context files only if explicitly included (optional switch)

**Acceptance:**
- Export is copy-only (no mutation)
- Export preserves file bytes exactly
- Export includes a manifest file listing included paths and hashes (recommended)

---

## 8. MSIX Packaging Requirements

### 8.1 Install/uninstall integrity

**Acceptance:**
- App installs via MSIX cleanly
- Uninstall removes app cleanly (no leftover app-owned directories)
- App stores user settings in appropriate Windows location

### 8.2 File system access

**Acceptance:**
- Workspace selection and access works with standard Windows permissions
- If a folder is inaccessible, the app shows a permission error with recovery hint

---

## 9. Reliability and Performance

### 9.1 Responsiveness

**Acceptance:**

UI must remain responsive during:
- Loading run list
- Opening large JSON files
- Exporting zip

### 9.2 Caching

**Acceptance:**
- Caching is allowed, but must be invalidated when workspace changes
- The app must not serve stale run lists after a refresh action

---

## 10. Diagnostics & Logging (App-Side)

**Acceptance:**
- User-facing error messages are actionable and non-technical where possible
- A "Copy diagnostic details" action exists for support/debugging
- No telemetry by default

---

## 11. Tests (Minimum Bar)

**Acceptance:**

### Unit tests for:
- Index loading and ordering
- Artifact detection logic
- Schema validation error handling
- Export bundle manifest generation (if included)

### Smoke test:
- Script/instructions for MSIX install and basic flows

---

## 12. Done Definition

RunForge Desktop v0.1 is complete when:

- [ ] A user can select a workspace containing RunForge outputs from https://github.com/mcp-tool-shop-org/runforge-vscode
- [ ] Browse runs reliably
- [ ] Open summary + interpretability index
- [ ] View metrics/importance/coefficients when present
- [ ] Export a run bundle
- [ ] All error cases are handled without crashes
- [ ] App installs/uninstalls cleanly via MSIX

---

## Appendix A: UI Tone Guidelines

| Attribute | Guideline |
|-----------|-----------|
| Visual style | Serious, calm, professional |
| Color scheme | Dark/light mode, neutral colors |
| Labels | Clear, no "AI magic" language |
| Error messages | Explain *why*, not just *what* |

**Target impression:** *"Something an auditor wouldn't flinch at."*

---

## Appendix B: Partner Program Alignment

This app aligns with Microsoft's narrative:
- Local-first
- Native Windows UI
- MSIX clean install/uninstall
- Developer productivity
- No cloud dependency

**Pitch:** *"A Windows-native ML inspection tool that surfaces deterministic artifacts produced by local training."*
