# RunForge Desktop Demo Script

**Duration:** ~5 minutes
**Audience:** ML practitioners, potential users, stakeholders

---

## Setup (Before Demo)

1. Have RunForge Desktop installed
2. Have a workspace with 3-5 training runs (mix of completed and failed)
3. At least one run should have interpretability artifacts (metrics, feature importance, or linear coefficients)

---

## Demo Flow

### 1. First Launch (30 seconds)

> "Let me show you RunForge Desktop, the Windows companion for inspecting your ML training runs."

- Launch the app (show splash screen briefly)
- Point out the clean, native Windows interface
- Mention it's built with .NET MAUI for Windows 10/11

### 2. Setting Up the Workspace (1 minute)

> "First, we point it at our RunForge workspace - that's where all our training runs live."

- Navigate to **Settings** (gear icon)
- Click **Browse** and select your workspace folder
- Show the workspace validation (green checkmark when valid)
- Click **Apply** and return to Runs list

### 3. Browsing Runs (1.5 minutes)

> "Now we can see all our training runs at a glance."

- Point out the run cards: name, date, status, model family
- Show the status badges (Completed in green, Failed in red)
- Demonstrate **search filtering**:
  - Type a partial run name
  - Point out the debounced search (no lag)
  - Clear the search
- Mention the filter persistence: "If I navigate away and come back, my filters are still here"

### 4. Inspecting a Run (1.5 minutes)

> "Let's dive into a specific run."

- Click on a run card to open Run Detail view
- Walk through the sections:
  - **Run Summary** - ID, model family, timestamps
  - **Request** - What was requested (parameters, data paths)
  - **Result** - Final status, any error messages
- Click **View Interpretability Artifacts**
- Show the artifact cards:
  - **Available artifacts** - green status, clickable
  - **Unavailable artifacts** - gray/orange status with reason
- Click on an available artifact (e.g., metrics)
- Show the raw JSON view
- Demonstrate **Copy to Clipboard** button

### 5. Diagnostics (30 seconds)

> "If something's not working, there's a built-in diagnostics page."

- Navigate to **Diagnostics** (wrench icon or from menu)
- Show the workspace status, app version, OS info
- Click **Copy All** - "I can share this with support or include in a bug report"

### 6. Wrap-Up (30 seconds)

> "That's RunForge Desktop - a focused, read-only tool for inspecting your training runs."

Key points to reiterate:
- **Read-only** - It never modifies your data
- **Local only** - No cloud, no sync, your data stays on your machine
- **Windows native** - Fast, responsive, feels like a real Windows app
- **Works with RunForge** - Companion to the VS Code extension

---

## Troubleshooting During Demo

| Issue | Quick Fix |
|-------|-----------|
| No runs showing | Check workspace path in Settings |
| Artifacts not loading | Verify the run has an `interpretability/index.json` |
| App won't launch | Check Windows 10 version (1809+ required) |

---

## Demo Assets

If you need sample data for demos, create a workspace with:
- `index.json` - Run index
- `runs/[run-id]/run.json` - Run metadata
- `runs/[run-id]/request.json` - Training request
- `runs/[run-id]/result.json` - Training result
- `runs/[run-id]/interpretability/index.json` - Artifact index
- `runs/[run-id]/interpretability/artifacts/metrics.v1.json` - Sample metrics

See the test fixtures in `tests/RunForgeDesktop.Core.Tests/` for JSON examples.
