# RunForge Desktop — Visual Activity System v1

This document defines the visual language and color rules for RunForge Desktop's Activity System.
These rules are **locked** and should not be changed without updating this document.

---

## Color Rules (LOCKED)

| State | Color | Hex | Usage |
|-------|-------|-----|-------|
| **Active** | Blue | `#2196F3` | Running jobs, active slots |
| **Waiting** | Amber | `#FFA726` | Queued jobs, waiting for slot |
| **Idle** | Gray | `#9E9E9E` | Empty slots, completed items, idle state |
| **Error** | Red | `#F44336` | Failed jobs, daemon errors |
| **Success** | Green | `#4CAF50` | Daemon healthy, completed successfully |

### Slot Indicators

| State | Light Mode | Dark Mode |
|-------|------------|-----------|
| Filled | `#2196F3` | `#64B5F6` |
| Empty | `#E0E0E0` | `#424242` |

### Strip Background

| Theme | Color |
|-------|-------|
| Light | `#F5F5F5` |
| Dark | `#1A1A1A` |

---

## Components

### 1. Activity Strip (Global, 28px height)

**Always visible** under the title bar on every page.

```
CPU ▮▮▯▯   GPU ▮▯   ⏳ 2 queued   ▶ 2 running
```

**Layout:**
- Left: CPU slot indicators (filled/empty blocks)
- Center-left: GPU slot indicators (if configured)
- Center: Queue count (pulses if > 0)
- Right: Daemon state icon + System state text

**Daemon State Icons:**
- `▶` (Green) — Running and healthy
- `⚠` (Amber) — Running but unhealthy (stale heartbeat)
- `⏹` (Gray) — Stopped

**System State Text:**
- "Idle" (Gray) — No jobs running or queued
- "N running" (Blue) — Jobs executing
- "Stalled" (Amber) — Daemon unhealthy
- "Error" (Red) — Daemon not running

---

### 2. System Status Panel

Contextual panel shown on RunsListPage when relevant.

**Idle State:**
```
✓ System idle
Last activity: 5 min ago
```
- Icon: Checkmark (Green)
- Background: Light neutral

**Busy State:**
```
▶ Executing
2 CPU jobs, 1 GPU job
```
- Icon: Play (Blue)
- Shows job breakdown

**Stalled State:**
```
⚠ Stalled
No daemon heartbeat
```
- Icon: Warning (Amber)
- Shows reason

**Error State:**
```
⛔ Error
Daemon not running
```
- Icon: Stop (Red)
- Shows reason

---

### 3. GPU Queue Card

Shown when GPU is configured and active.

```
GPU  1/2 slots
▮▯
3 jobs waiting for GPU
```

- Orange/amber theme for GPU-specific content
- Shows slot usage visually
- Shows waiting count when > 0

---

### 4. Run List Progress Edge

Each run row has a **4px left edge** indicating state.

| State | Edge Color | Notes |
|-------|------------|-------|
| Running | Blue (`#2196F3`) | Animated shimmer (future) |
| Queued | Amber (`#FFA726`) | Solid |
| Completed | Gray (`#9E9E9E`) | Succeeded |
| Failed | Red (`#F44336`) | Error state |
| Stalled | Orange (`#FF9800`) | Pulsing (future) |

**Current Implementation:** Uses `IsSucceeded` to show Green (success) or Red (failed).
**Future Enhancement:** Correlate with queue state for Running/Queued/Stalled.

---

## Data Sources

| Visual Element | Data Source |
|----------------|-------------|
| CPU slots | `QueueStatusSummary.RunningCount` / `MaxParallel` |
| GPU slots | `DaemonStatus.ActiveGpuJobs` / `GpuSlots` |
| Queue count | `QueueStatusSummary.QueuedCount` |
| Daemon health | `DaemonStatus.IsHealthy` (heartbeat < 30s) |
| System state | Computed from above |

---

## Files

### Core Services
- `IActivityMonitorService.cs` — Interface with `ActivitySystemState` enum
- `ActivityMonitorService.cs` — Polls every 2s, calculates state

### UI Components
- `Controls/ActivityStrip.xaml` — Global strip control
- `Controls/SystemStatusPanel.xaml` — Contextual status panel
- `Controls/GpuQueueCard.xaml` — GPU queue card
- `ViewModels/ActivityStripViewModel.cs` — Strip ViewModel

### Color Definitions
- `Resources/Styles/Colors.xaml` — Activity color resources

### Converters
- `SlotFilledToColorConverter` — Boolean to slot color
- `RunDisplayStateToColorConverter` — State enum to color
- `ActivitySystemStateToColorConverter` — System state to color

---

## Design Principles

1. **The UI moves when the system moves** — Progress is felt, not read
2. **Color communicates state** — Consistent colors across all components
3. **No invisible work** — GPU work, queue state, daemon health are all visible
4. **Idle is reassuring** — A calm gray state indicates "ready for work"
5. **Errors are obvious** — Red means something needs attention

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2026-02-02 | Initial Visual Activity System |

---

*This document is part of the RunForge Desktop v0.5.0 release.*
