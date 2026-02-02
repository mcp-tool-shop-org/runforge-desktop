# RunForge Desktop - Marketing & Store Assets

> Ready-to-use copy for Microsoft Store, Partner Center, README, and press.

---

## 1. Taglines

### One-liner (Store tagline, ≤80 chars)
```
Run and monitor ML training locally with live metrics, logs, and reproducible runs.
```

### Alternate one-liner
```
Local ML training interface with real-time charts and full artifact inspection.
```

---

## 2. Short Description (≤200 chars)

```
A desktop ML training tool that lets you start runs, monitor progress live, and inspect results — all locally, with no cloud dependency.
```

---

## 3. Long Description (Store / Partner / README)

```
RunForge Desktop is a local ML training interface for developers and researchers.

Start training runs from a clean desktop UI, monitor progress with live loss charts and streaming logs, and inspect completed runs with full artifact access.

WHAT YOU CAN DO:
• Configure and start training runs with GPU or CPU
• Watch live metrics: loss curves, epoch progress, elapsed time
• Stream logs in real-time during training
• Browse all runs with status filtering
• Open output folders and copy training commands
• Run hyperparameter sweeps with MultiRun

INTENTIONALLY LOCAL:
• No cloud services — everything runs on your machine
• No accounts, no sign-in, no telemetry
• All artifacts saved to disk for inspection and reproducibility
• One active run at a time for predictable resource usage

REQUIREMENTS:
• Windows 10 (1809+) or Windows 11
• Python 3.10+ (for training execution)
• Optional: CUDA-capable GPU for accelerated training

Built for developers who want a simple, transparent ML workflow without cloud overhead.
```

---

## 4. Keywords (for Store)

```
machine learning, ml training, experiment tracking, local ai, developer tools, training monitor, pytorch, tensorflow, ml experiments, ai desktop, training dashboard, loss curve, ml visualization
```

---

## 5. System Requirements (Store format)

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| OS | Windows 10 (1809) | Windows 11 |
| Architecture | x64 | x64 |
| RAM | 4 GB | 8 GB |
| Disk Space | 100 MB | 500 MB (for run artifacts) |
| Python | 3.10+ | 3.11+ |
| GPU | Not required | CUDA 11.8+ with 8GB+ VRAM |

---

## 6. Store Categories

**Primary Category:** Developer Tools
**Secondary Category:** Productivity
**Tags:** Machine Learning, AI, Training, Local Development

---

## 7. Compliance Statements (Partner Center)

### Data Collection
```
This application does not collect, transmit, or store any user data remotely. All training data, logs, metrics, and artifacts remain on the local machine. No telemetry, analytics, or usage tracking.
```

### Privacy
```
RunForge Desktop operates entirely offline after installation. No account creation, no sign-in, no network requests during normal operation. The app only accesses folders explicitly selected by the user.
```

### Internet Connectivity
```
Internet connection is NOT required for app operation. All training runs execute locally using the user's Python environment.
```

---

## 8. Launch Blurb (Reddit / Hacker News / X)

### Short (Twitter/X, ≤280 chars)
```
Built a local-first ML training UI for Windows. Start runs, watch live loss charts, inspect artifacts — no cloud, no accounts. Looking for early feedback from ML/dev folks.

github.com/mcp-tool-shop-org/runforge-desktop
```

### Medium (Reddit / HN)
```
I built a local-first ML training UI that lets you start runs, watch metrics live, and inspect results without any cloud setup.

It's intentionally simple:
- One run at a time
- CPU or GPU execution
- Reproducible outputs saved to disk
- Real-time loss charts and log streaming

No accounts, no telemetry, no cloud dependencies.

Looking for early feedback from ML/dev folks who want a simple way to run experiments locally.

GitHub: github.com/mcp-tool-shop-org/runforge-desktop
```

---

## 9. Screenshot Requirements

### Required Screenshots (minimum 4)

| # | Filename | Page | Requirements |
|---|----------|------|--------------|
| 1 | `screenshot-01-live-run.png` | Live Run | Progress bar visible, loss chart with data, Status = Running, run name in title |
| 2 | `screenshot-02-new-run.png` | New Run | Workspace selected (green checkmark), preset picker visible, GPU/CPU visible, Start Training enabled |
| 3 | `screenshot-03-runs-dashboard.png` | Runs Dashboard | Multiple runs visible, mixed states (Running/Completed/Failed), Train + MultiRun buttons |
| 4 | `screenshot-04-completed-run.png` | Live Run (finished) | Loss curve complete, Status = Completed, "Open Output Folder" + "Open Logs" buttons visible |

### Optional Screenshots

| # | Filename | Page | Notes |
|---|----------|------|-------|
| 5 | `screenshot-05-multirun.png` | MultiRun | Sweep configuration, run preview list |
| 6 | `screenshot-06-settings.png` | Settings | Python path, output directories, training defaults |
| 7 | `screenshot-07-help.png` | Help | Tutorial content visible |
| 8 | `screenshot-08-diagnostics.png` | Diagnostics | System info, storage breakdown |

### Screenshot Standards

- **Theme:** Dark mode only
- **Resolution:** 1440×900 or 1600×1000
- **Content:** Real data (no placeholders, no debug labels)
- **Paths:** Clean workspace paths (not `C:\Users\Mike\Desktop\test`)
- **Format:** PNG, high quality

---

## 10. Store Icon Requirements

| Asset | Size | Format |
|-------|------|--------|
| App icon | 150×150 | PNG |
| Store listing | 300×300 | PNG |
| Wide tile | 310×150 | PNG (optional) |

---

## 11. What's Next Section (for README / launch posts)

```
## Roadmap

- [ ] Run comparison view
- [ ] Export metrics to CSV
- [ ] Custom training script support
- [ ] Multi-run parallel execution

Feedback welcome via GitHub Issues.
```

---

## 12. Feature Comparison (for positioning)

| Feature | RunForge Desktop | MLflow | W&B |
|---------|------------------|--------|-----|
| Local execution | Yes | Yes | No (cloud) |
| No account required | Yes | Yes | No |
| Real-time charts | Yes | Limited | Yes |
| Windows-native | Yes | No | No |
| Zero config | Yes | No | No |
| Free | Yes | Yes | Freemium |

---

## 13. Limitations (explicitly state)

```
CURRENT LIMITATIONS:
• One active training run at a time
• Windows only (no macOS/Linux)
• Requires Python 3.10+ installed locally
• No remote/distributed training support
• No model versioning or registry
```

---

## 14. Version Information

| Field | Value |
|-------|-------|
| Version | 1.0.0 |
| Build | Release |
| Target | net10.0-windows10.0.19041.0 |
| Framework | .NET MAUI |
| Package | MSIX |

---

## 15. Support Links

| Purpose | URL |
|---------|-----|
| GitHub | https://github.com/mcp-tool-shop-org/runforge-desktop |
| Issues | https://github.com/mcp-tool-shop-org/runforge-desktop/issues |
| Releases | https://github.com/mcp-tool-shop-org/runforge-desktop/releases |

---

## Usage Checklist

- [ ] Screenshots captured with correct filenames
- [ ] All copy reviewed for accuracy
- [ ] Version numbers consistent
- [ ] Limitations explicitly stated
- [ ] No outdated terminology
- [ ] README matches current app state
