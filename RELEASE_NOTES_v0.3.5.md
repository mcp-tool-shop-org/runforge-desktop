# RunForge Desktop v0.3.5 Release Notes

## Queue & Scheduling Discipline

v0.3.5 introduces a global execution daemon with a disk-backed queue, providing disciplined execution across all runs and sweeps.

### Key Features

#### 1. Execution Daemon
- Long-running background process manages all job execution
- Single instance per workspace (enforced by lock file)
- Graceful shutdown on SIGINT/SIGTERM
- Heartbeat for health monitoring

```bash
runforge-cli daemon --workspace . --max-parallel 2
```

#### 2. Global max_parallel
- One setting controls concurrency across everything
- No more per-sweep limits - global fairness

#### 3. Fair Scheduling (Round-Robin)
- Round-robin by group ensures no sweep starves others
- Priority support within groups
- FIFO as tiebreaker

#### 4. Group Controls
- **Pause**: Stop new jobs from starting
- **Resume**: Continue paused groups
- **Retry Failed**: Re-enqueue failed runs (increments attempt count)
- **Cancel**: Mark queued runs as canceled

### CLI Commands

```bash
# Daemon
runforge-cli daemon --workspace <path> [--max-parallel 2]

# Enqueue (sweeps add to queue instead of executing directly)
runforge-cli sweep --plan <file> --enqueue
runforge-cli enqueue-sweep --plan <file>
runforge-cli enqueue-run --run-id <id> --workspace <path>

# Group controls
runforge-cli pause-group --group-id <gid> --workspace <path>
runforge-cli resume-group --group-id <gid> --workspace <path>
runforge-cli retry-failed --group-id <gid> --workspace <path>
runforge-cli cancel-group --group-id <gid> --workspace <path>

# Status
runforge-cli queue-status --workspace <path>
```

### Desktop Integration

New service `IExecutionQueueService` provides:
- Queue and daemon status loading
- Daemon start/stop
- Enqueue operations
- Group controls (pause/resume/retry/cancel)

Models added:
- `ExecutionQueue` - Queue state
- `QueueJob` - Individual job in queue
- `DaemonStatus` - Daemon health state

### File Format

**Queue state**: `.runforge/queue/queue.json`
```json
{
    "version": 1,
    "kind": "execution_queue",
    "max_parallel": 2,
    "jobs": [...],
    "last_served_group": "grp_xxx"
}
```

**Daemon state**: `.runforge/queue/daemon.json`
```json
{
    "version": 1,
    "pid": 12345,
    "started_at": "2026-02-01T15:00:00",
    "last_heartbeat": "2026-02-01T15:05:00",
    "max_parallel": 2,
    "active_jobs": 1,
    "state": "running"
}
```

**Group pause**: `paused: true` field in `group.json`

### Migration Notes

- Both modes work side by side:
  - `runforge-cli sweep --plan <file>` - Direct execution (v0.3.4 behavior)
  - `runforge-cli sweep --plan <file> --enqueue` - Queue mode (new)
- Consider using queue mode for better resource management

### Tests

- 59 Python tests (27 new for queue/scheduling)
- 333 C# tests (6 new for queue models)

### Documentation

- `docs/QUEUE_V1_CONTRACT.md` - Full schema and API documentation

---

## What This Unlocks

v0.3.5 makes execution predictable:
- **Bounded concurrency** across all workloads
- **Fair scheduling** - sweeps share resources
- **Controllable** - pause, resume, retry at will
- **Observable** - queue status shows everything

Next: v0.4.0 will add GPU scheduling, building on this queue infrastructure.
