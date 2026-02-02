# RunForge Gauntlet Results

This document records the results of running the RunForge Reliability Gauntlets
(see [docs/GAUNTLETS.md](GAUNTLETS.md)).

**Use this when:**
- Validating a new release
- Testing a new environment (machine / OS / Python version)
- Filing a bug or regression report
- Contributing changes to queue, daemon, sweeps, or GPU scheduling

---

## Environment

Fill this out once per run.

| Item | Value |
|------|-------|
| Date | YYYY-MM-DD |
| RunForge Desktop Version | vX.Y.Z |
| runforge-cli Version | vX.Y.Z |
| OS | Windows 10 / Windows 11 |
| CPU | |
| RAM | |
| GPU (if any) | |
| Python Version | |
| Workspace Path | |

**Optional:**
- Notes about environment quirks (VM, WSL, network drives, etc.)

---

## Execution Mode

- [ ] Queued (daemon)
- [ ] Direct (legacy)

**Daemon settings (if queued):**
- `max_parallel` = ___

Daemon started via:
```bash
python -m runforge_cli daemon --workspace <path> --max-parallel ___
```

---

## Gauntlet Results

Mark each test and add notes if anything unexpected occurred.

### G1 — Basic Queue Correctness

**Goal:** Global `max_parallel` respected.

- [ ] PASS
- [ ] FAIL

**Notes:**
- Observed max concurrent jobs:
- Evidence checked (queue.json, group.json):

---

### G2 — Pause / Resume

**Goal:** Pause prevents new jobs; resume continues.

- [ ] PASS
- [ ] FAIL

**Notes:**
- Did running jobs finish or cancel?
- Did queued jobs remain queued while paused?

---

### G3 — Cancel Group

**Goal:** Deterministic cancellation.

- [ ] PASS
- [ ] FAIL

**Notes:**
- Queued jobs marked canceled?
- Running jobs resolved cleanly?
- Final group status:

---

### G4 — Crash Recovery (Daemon Killed)

**Goal:** Recovery after scheduler death.

- [ ] PASS
- [ ] FAIL

**Notes:**
- How was daemon killed?
- Orphaned jobs resolved as:
  - [ ] canceled
  - [ ] failed
  - [ ] requeued
- Stale lock detected correctly?

---

### G5 — Fairness Smoke

**Goal:** Single run interleaves with sweep.

- [ ] PASS
- [ ] FAIL

**Notes:**
- When did single run start relative to sweep?
- Any starvation observed?

---

### G6 — Disk Drift (Missing Run Folder)

**Goal:** Missing folders fail safely.

- [ ] PASS
- [ ] FAIL

**Notes:**
- Error message observed:
- Did daemon continue processing other jobs?

---

### G7 — Desktop Reconnect

**Goal:** Desktop reattaches to live state.

- [ ] PASS
- [ ] FAIL

**Notes:**
- Queue/group state rendered correctly?
- Stale heartbeat warning shown (if applicable)?

---

## Optional Gauntlets

### G8 — GPU Fallback (if applicable)

- [ ] PASS
- [ ] FAIL
- [ ] SKIPPED (no GPU)

**Notes:**
- Requested device:
- Effective device:
- `gpu_reason`:

---

### G9 — GPU Exclusivity (if applicable)

- [ ] PASS
- [ ] FAIL
- [ ] SKIPPED

**Notes:**
- `gpu_slots`:
- Concurrent GPU jobs observed:

---

### G10 — Mixed CPU/GPU Workload (if applicable)

- [ ] PASS
- [ ] FAIL
- [ ] SKIPPED

**Notes:**
- CPU jobs progressed alongside GPU jobs?
- Any starvation observed?

---

## Overall Assessment

- [ ] All required gauntlets passed
- [ ] Some failures (details below)
- [ ] Blocking issues found

**Summary Notes:**

(Anything surprising, brittle, or worth improving)

---

## Attachments / Evidence

If filing an issue, attach or link to:

- `.runforge/queue/queue.json`
- `.runforge/queue/daemon.json`
- `.runforge/groups/<gid>/group.json`
- Relevant `logs.txt` excerpts
- Screenshots (Desktop UI)

---

## Maintainer Notes (optional)

- Regression suspected from version:
- Suggested fix / hypothesis:
- Follow-up action:

---

## Why This Document Exists

RunForge's queue, daemon, and sweep systems are designed to be **provable**, not just "seem to work."

These gauntlets ensure failures are:
- **Visible**
- **Recoverable**
- **Explainable**

If a gauntlet fails, that's actionable signal — not user error.
