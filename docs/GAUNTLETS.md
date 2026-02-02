# RunForge Reliability Gauntlets

Repeatable reliability suite for validating queue, scheduling, and recovery behavior.

## Prerequisites

- Python 3.10+
- Workspace at `<path>` contains `data/train.csv` (small CSV)
- `.runforge/` will be created as needed
- Use `--dry-run` for deterministic, fast runs
- Replace `<path>` with your workspace path and `<gid>` with the group ID printed in output

---

## G1 — Basic Queue Correctness (max_parallel respected)

**Goal**: 4 jobs queued; at most 2 running concurrently.

Start daemon:
```bash
python -m runforge_cli daemon --workspace <path> --max-parallel 2
```

Enqueue a sweep that produces 4 runs:
```bash
python -m runforge_cli enqueue-sweep --plan <plan.json> --workspace <path>
```

Observe:
```bash
python -m runforge_cli queue-status --workspace <path>
```

**Pass criteria**:
- Never more than 2 running jobs
- Group progresses to terminal `completed` (or expected terminal state)
- Each run produces `logs.txt` and `result.json`

---

## G2 — Pause/Resume

**Goal**: Pause stops new jobs; resume continues.

Enqueue a sweep (6+ runs recommended):
```bash
python -m runforge_cli enqueue-sweep --plan <plan.json> --workspace <path>
```

Pause:
```bash
python -m runforge_cli pause-group --group-id <gid> --workspace <path>
```

Confirm no new jobs start:
```bash
python -m runforge_cli queue-status --workspace <path>
```

Resume:
```bash
python -m runforge_cli resume-group --group-id <gid> --workspace <path>
```

**Pass criteria**:
- While paused: `queued` remains queued; running may finish but nothing new starts
- After resume: queued runs start

---

## G3 — Cancel Group

**Goal**: Cancel marks queued jobs canceled; running jobs complete or cancel deterministically.

```bash
python -m runforge_cli cancel-group --group-id <gid> --workspace <path>
python -m runforge_cli queue-status --workspace <path>
```

**Pass criteria**:
- Group ends in `canceled`
- No jobs remain stuck in `running` forever
- State is consistent in `group.json` and queue

---

## G4 — Crash Recovery (kill daemon mid-run)

**Goal**: Restart recovers queue and resolves stale lock.

Ensure jobs are active, then kill daemon process (Task Manager / `kill`).

Restart daemon:
```bash
python -m runforge_cli daemon --workspace <path> --max-parallel 2
```

Check:
```bash
python -m runforge_cli queue-status --workspace <path>
```

**Pass criteria**:
- Daemon detects stale lock/heartbeat and takes over
- Orphaned running jobs are resolved per policy (failed/canceled/requeued)
- Remaining jobs continue

---

## G5 — Fairness Smoke

**Goal**: A single run interleaves with a big sweep.

Enqueue a 10-run sweep:
```bash
python -m runforge_cli enqueue-sweep --plan <plan_big.json> --workspace <path>
```

Enqueue a single run:
```bash
python -m runforge_cli enqueue-run --run-id <id> --workspace <path>
```

Watch:
```bash
python -m runforge_cli queue-status --workspace <path>
```

**Pass criteria**:
- The single run starts early (not after all sweep runs), consistent with round-robin fairness

---

## G6 — Disk Drift (missing run folder)

**Goal**: Missing folder fails job; daemon continues.

1. Enqueue jobs
2. Delete one queued run folder manually before it starts
3. Observe:

```bash
python -m runforge_cli queue-status --workspace <path>
```

**Pass criteria**:
- That job becomes `failed` with a clear reason
- Other jobs proceed normally

---

## G7 — Desktop Reconnect

**Goal**: Desktop reattaches to live state and detects stale heartbeat.

1. With daemon running and jobs active, close Desktop
2. Reopen Desktop

**Pass criteria**:
- Desktop correctly renders:
  - Queue status (or daemon status)
  - Group progress
  - Stale heartbeat warning if daemon stopped

---

## G8 — GPU Fallback (v0.4.0+)

**Goal**: GPU fallback is explicit and explained.

On a machine without GPU (or with GPU detection disabled), create request with `device.type = "gpu"`:

```bash
python -m runforge_cli run --request gpu_request.json --workspace <path>
```

**Pass criteria**:
- Execution completes on CPU
- `result.json` → `effective_config.device.type` = `"cpu"`
- `result.json` → `effective_config.device.gpu_reason` = `"no_gpu_detected"`
- RF token: `[RF:DEVICE=CPU gpu_reason=no_gpu_detected]`

---

## G9 — GPU Exclusivity (v0.4.0+)

**Goal**: GPU jobs respect `gpu_slots`.

Start daemon with gpu_slots=1:
```bash
python -m runforge_cli daemon --workspace <path> --max-parallel 4 --gpu-slots 1
```

Enqueue 2 GPU jobs:
```bash
python -m runforge_cli enqueue-sweep --plan gpu_sweep_2.json --workspace <path>
```

**Pass criteria**:
- At most 1 GPU job running at any time
- Second GPU job waits until first completes
- CPU jobs unaffected by GPU slot

---

## G10 — Mixed CPU/GPU Workload (v0.4.0+)

**Goal**: CPU jobs progress alongside GPU jobs.

```bash
python -m runforge_cli daemon --workspace <path> --max-parallel 4 --gpu-slots 1

# Enqueue GPU sweep (4 runs)
python -m runforge_cli enqueue-sweep --plan gpu_sweep_4.json --workspace <path>

# Enqueue CPU sweep (4 runs)
python -m runforge_cli enqueue-sweep --plan cpu_sweep_4.json --workspace <path>
```

**Pass criteria**:
- CPU jobs start immediately (up to max_parallel - gpu_in_use)
- GPU jobs run one at a time (gpu_slots=1)
- Both sweeps make progress concurrently
- No starvation: CPU jobs don't wait for all GPU jobs

---

## Optional: Direct Mode (non-queue) Smoke

```bash
python -m runforge_cli sweep --plan <plan.json> --dry-run
python -m runforge_cli run --request <request.json> --workspace <path> --dry-run
```

---

## Evidence Files (for debugging)

| File | Purpose |
|------|---------|
| `.runforge/queue/queue.json` | Job states and scheduling |
| `.runforge/queue/daemon.json` | Daemon heartbeat and status |
| `.runforge/groups/<gid>/group.json` | Group summary and run entries |
| `.runforge/runs/<run-id>/logs.txt` | Execution logs |
| `.runforge/runs/<run-id>/result.json` | Run outcome and effective config |

---

## Summary

| Gauntlet | Focus | Version |
|----------|-------|---------|
| G1 | max_parallel enforcement | v0.3.5+ |
| G2 | Pause/Resume | v0.3.5+ |
| G3 | Cancel determinism | v0.3.5+ |
| G4 | Crash recovery | v0.3.5+ |
| G5 | Fair scheduling | v0.3.5+ |
| G6 | Disk drift resilience | v0.3.5+ |
| G7 | Desktop reconnect | v0.3.5+ |
| G8 | GPU fallback | v0.4.0+ |
| G9 | GPU exclusivity | v0.4.0+ |
| G10 | Mixed workload | v0.4.0+ |
