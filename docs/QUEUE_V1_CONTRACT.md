# Queue v1 Contract

This document defines the contract for the execution queue system introduced in v0.3.5.

## Overview

The queue system provides:
- **Global execution daemon**: Long-running process that manages job execution
- **Fair scheduling**: Round-robin by group ensures no sweep starves others
- **Group controls**: Pause, resume, retry failed, cancel operations
- **Disk-backed state**: All queue state persisted to JSON files

## File Locations

```
<workspace>/
├── .runforge/
│   ├── queue/
│   │   ├── queue.json      # Queue state
│   │   ├── daemon.json     # Daemon status
│   │   └── daemon.lock     # Lock file (prevents multiple daemons)
│   └── groups/
│       └── <group-id>/
│           └── group.json  # Includes "paused" field
```

## Queue State (`queue.json`)

```json
{
    "version": 1,
    "kind": "execution_queue",
    "max_parallel": 2,
    "last_served_group": "grp_20260201_150000_Test",
    "jobs": [
        {
            "job_id": "job_20260201_150000_0001",
            "kind": "run",
            "run_id": "20260201-150000-sweep-0000",
            "group_id": "grp_20260201_150000_Test",
            "priority": 0,
            "state": "queued",
            "attempt": 1,
            "created_at": "2026-02-01T15:00:00",
            "started_at": null,
            "finished_at": null,
            "error": null
        }
    ]
}
```

### Job States

| State | Description |
|-------|-------------|
| `queued` | Waiting to run |
| `running` | Currently executing |
| `succeeded` | Completed successfully |
| `failed` | Completed with error |
| `canceled` | Canceled by user |

### Job Fields

| Field | Type | Description |
|-------|------|-------------|
| `job_id` | string | Unique job identifier |
| `kind` | string | Always "run" |
| `run_id` | string | Associated run ID |
| `group_id` | string? | Group ID (null for ungrouped runs) |
| `priority` | int | Higher = scheduled first (default 0) |
| `state` | string | Current state |
| `attempt` | int | Attempt number (increments on retry) |
| `created_at` | ISO8601 | When job was created |
| `started_at` | ISO8601? | When execution started |
| `finished_at` | ISO8601? | When execution completed |
| `error` | string? | Error message if failed |

## Daemon State (`daemon.json`)

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

### Daemon States

| State | Description |
|-------|-------------|
| `running` | Daemon is active and processing jobs |
| `stopping` | Daemon is shutting down, waiting for active jobs |
| `stopped` | Daemon has stopped |

### Health Check

The daemon is considered healthy if:
- `state` is "running"
- `last_heartbeat` is within the last 30 seconds

## Group Pause Field

Groups can be paused by setting `"paused": true` in `group.json`. When a group is paused:
- Jobs from that group will not be scheduled
- Running jobs continue to completion
- Resuming unpauses all queued jobs

## CLI Commands

### Daemon Management

```bash
# Start daemon
runforge-cli daemon --workspace <path> [--max-parallel 2]

# Daemon runs until SIGINT/SIGTERM
```

### Enqueue Operations

```bash
# Enqueue a single run
runforge-cli enqueue-run --run-id <id> --workspace <path> [--group-id <gid>] [--priority 0]

# Enqueue all runs from a sweep plan
runforge-cli enqueue-sweep --plan <plan.json> [--workspace <path>]

# Or use --enqueue flag on sweep command
runforge-cli sweep --plan <plan.json> --enqueue
```

### Group Controls

```bash
# Pause a group (stops new jobs from starting)
runforge-cli pause-group --group-id <gid> --workspace <path>

# Resume a paused group
runforge-cli resume-group --group-id <gid> --workspace <path>

# Re-enqueue failed runs in a group
runforge-cli retry-failed --group-id <gid> --workspace <path>

# Cancel all queued runs in a group
runforge-cli cancel-group --group-id <gid> --workspace <path>
```

### Status

```bash
# Show queue status
runforge-cli queue-status --workspace <path>
```

## Scheduling Algorithm

### Round-Robin Fairness

The scheduler uses round-robin by group to ensure fairness:

1. Group jobs by `group_id` (ungrouped runs form their own bucket)
2. Within each group, sort by priority (desc) then created_at (asc)
3. Take the head (best job) from each group
4. Prefer groups other than the last served group
5. Among candidates, pick the one created earliest (longest wait)
6. Track `last_served_group` for next scheduling decision

### Priority

Higher priority jobs are scheduled first within their group. Priority is a simple integer (default 0).

## Exit Codes

| Code | Name | Description |
|------|------|-------------|
| 0 | SUCCESS | Command succeeded |
| 1 | FAILED | General failure |
| 5 | CANCELED | Operation was canceled |
| 6 | INVALID_PLAN | Sweep plan validation failed |

## RF Tokens

New tokens for queue mode:

```
[RF:GROUP=ENQUEUED grp_xxx runs=N]   # Group enqueued to queue
[RF:GROUP=PAUSED grp_xxx]            # Group paused
[RF:GROUP=RESUMED grp_xxx]           # Group resumed
[RF:DAEMON=STARTED pid=N max_parallel=N]  # Daemon started
[RF:DAEMON=STOPPED pid=N]            # Daemon stopped
[RF:QUEUE=JOB_STARTED job_xxx run_id=xxx]  # Job started
[RF:QUEUE=JOB_DONE job_xxx run_id=xxx status=xxx]  # Job completed
```

## Atomic Updates

All state files use atomic updates:
1. Write to temporary file in same directory
2. Rename to target (atomic on most filesystems)
3. Clean up on failure

## Single Instance Lock

The daemon uses a lock file (`daemon.lock`) to prevent multiple instances:
- Lock acquired on startup using file locking (msvcrt on Windows)
- PID written to lock file
- Lock released on shutdown
- Stale locks are detected by checking if PID is still running

## Migration from Direct Execution

v0.3.5 supports both modes:
- **Direct mode** (default): `runforge-cli sweep --plan <file>` executes runs directly
- **Queue mode**: `runforge-cli sweep --plan <file> --enqueue` uses the daemon

Future versions may make queue mode the default.
