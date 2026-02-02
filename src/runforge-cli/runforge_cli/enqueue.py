"""Enqueue commands for the execution queue.

Commands:
- enqueue-run: Add a single run to the queue
- enqueue-sweep: Expand a sweep plan and enqueue all runs
- pause-group: Pause execution of a group
- resume-group: Resume execution of a group
- retry-failed: Re-enqueue failed runs in a group
- cancel-group: Cancel queued runs in a group
- queue-status: Show queue status
"""

import json
import sys
from datetime import datetime
from pathlib import Path

from . import __version__
from .queue import GroupPauseManager, QueueManager
from .sweep import RunConfig, SweepOrchestrator, SweepPlan


def enqueue_run_command(
    run_id: str,
    workspace: Path,
    group_id: str | None = None,
    priority: int = 0,
) -> int:
    """Enqueue a single run.

    Args:
        run_id: The run ID to enqueue
        workspace: Workspace root path
        group_id: Optional group ID
        priority: Job priority (higher = first)

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    run_dir = workspace / ".ml" / "runs" / run_id
    if not run_dir.exists():
        print(f"ERROR: Run directory not found: {run_dir}", file=sys.stderr)
        return 1

    request_file = run_dir / "request.json"
    if not request_file.exists():
        print(f"ERROR: request.json not found in {run_dir}", file=sys.stderr)
        return 1

    # Read request to determine if GPU is required
    requires_gpu = False
    try:
        with open(request_file, "r", encoding="utf-8") as f:
            request_data = json.load(f)
        device_type = request_data.get("device", {}).get("type", "cpu")
        requires_gpu = device_type == "gpu"
    except Exception:
        pass  # Default to CPU if can't read

    queue_mgr = QueueManager(workspace)

    try:
        job = queue_mgr.enqueue(run_id, group_id, priority, requires_gpu)
        gpu_tag = " [GPU]" if requires_gpu else ""
        print(f"Enqueued run {run_id} as job {job.job_id}{gpu_tag}")
        return 0
    except ValueError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1
    except Exception as e:
        print(f"ERROR: Failed to enqueue: {e}", file=sys.stderr)
        return 1


def enqueue_sweep_command(plan_path: Path, workspace: Path | None = None) -> int:
    """Enqueue all runs from a sweep plan.

    This creates the group folder and run folders with request.json,
    then enqueues all runs to the global queue instead of executing directly.

    Args:
        plan_path: Path to sweep_plan.json
        workspace: Optional workspace override

    Returns:
        Exit code
    """
    from .exit_codes import INVALID_PLAN, MISSING_FILES

    if not plan_path.exists():
        print(f"ERROR: Plan file not found: {plan_path}", file=sys.stderr)
        return MISSING_FILES

    try:
        plan = SweepPlan.load(plan_path)
    except Exception as e:
        print(f"ERROR: Failed to parse plan: {e}", file=sys.stderr)
        return INVALID_PLAN

    errors = plan.validate()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return INVALID_PLAN

    # Use workspace from plan if not overridden
    ws = workspace if workspace else Path(plan.workspace)
    if not ws.exists():
        print(f"ERROR: Workspace not found: {ws}", file=sys.stderr)
        return MISSING_FILES

    print(f"runforge-cli enqueue-sweep v{__version__}")
    print(f"Plan: {plan_path}")
    print(f"Workspace: {ws}")
    print(f"Group: {plan.group_name}")

    # Create orchestrator (reuses v0.3.4 code)
    orchestrator = SweepOrchestrator(plan, plan_path)
    orchestrator.workspace = ws  # Override if needed

    # Expand grid
    run_configs = orchestrator.expand_grid()
    if not run_configs:
        print("ERROR: No runs to execute (empty grid)", file=sys.stderr)
        return INVALID_PLAN

    total = len(run_configs)
    print(f"Sweep plan: {total} runs")

    # Setup group directory (creates group.json)
    orchestrator.setup_group(run_configs)
    group_id = orchestrator.group_id
    print(f"Group ID: {group_id}")
    print(f"Group directory: {orchestrator.group_dir}")

    # Create run directories with request.json
    for rc in run_configs:
        orchestrator.create_run_directory(rc)

    # Enqueue all runs
    queue_mgr = QueueManager(ws)
    enqueued = 0
    gpu_count = 0

    # Check if base request requires GPU
    base_device = plan.base_request.get("device", {}).get("type", "cpu")
    requires_gpu = base_device == "gpu"

    for rc in run_configs:
        try:
            job = queue_mgr.enqueue(rc.run_id, group_id, priority=0, requires_gpu=requires_gpu)
            enqueued += 1
            if requires_gpu:
                gpu_count += 1
        except ValueError as e:
            print(f"Warning: Could not enqueue {rc.run_id}: {e}", file=sys.stderr)

    # Update group.json to show runs as queued
    _update_group_runs_queued(orchestrator.group_dir / "group.json", run_configs)

    gpu_info = f" ({gpu_count} GPU)" if gpu_count > 0 else ""
    print(f"Enqueued {enqueued}/{total} runs{gpu_info}")
    print(f"[RF:GROUP=ENQUEUED {group_id} runs={enqueued}]")

    return 0


def _update_group_runs_queued(group_file: Path, run_configs: list[RunConfig]) -> None:
    """Update group.json to mark runs as queued."""
    import os
    import tempfile

    if not group_file.exists():
        return

    try:
        with open(group_file, "r", encoding="utf-8") as f:
            data = json.load(f)

        # Update run statuses from pending to queued
        run_ids = {rc.run_id for rc in run_configs}
        for run in data.get("runs", []):
            if run.get("run_id") in run_ids and run.get("status") == "pending":
                run["status"] = "queued"

        # Atomic write
        fd, temp_path = tempfile.mkstemp(suffix=".json", dir=group_file.parent)
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
            if group_file.exists():
                group_file.unlink()
            Path(temp_path).rename(group_file)
        except Exception:
            try:
                Path(temp_path).unlink()
            except Exception:
                pass
            raise
    except Exception as e:
        print(f"Warning: Could not update group status: {e}", file=sys.stderr)


def pause_group_command(group_id: str, workspace: Path) -> int:
    """Pause a group. Prevents new jobs from starting.

    Args:
        group_id: The group ID to pause
        workspace: Workspace root path

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    pause_mgr = GroupPauseManager(workspace)

    if pause_mgr.is_paused(group_id):
        print(f"Group {group_id} is already paused")
        return 0

    if pause_mgr.set_paused(group_id, True):
        print(f"Paused group {group_id}")
        return 0
    else:
        print(f"ERROR: Group not found: {group_id}", file=sys.stderr)
        return 1


def resume_group_command(group_id: str, workspace: Path) -> int:
    """Resume a paused group.

    Args:
        group_id: The group ID to resume
        workspace: Workspace root path

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    pause_mgr = GroupPauseManager(workspace)

    if not pause_mgr.is_paused(group_id):
        print(f"Group {group_id} is not paused")
        return 0

    if pause_mgr.set_paused(group_id, False):
        print(f"Resumed group {group_id}")
        return 0
    else:
        print(f"ERROR: Group not found: {group_id}", file=sys.stderr)
        return 1


def retry_failed_command(group_id: str, workspace: Path) -> int:
    """Re-enqueue failed runs in a group.

    Args:
        group_id: The group ID
        workspace: Workspace root path

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    queue_mgr = QueueManager(workspace)
    new_jobs = queue_mgr.retry_failed(group_id)

    if new_jobs:
        print(f"Re-enqueued {len(new_jobs)} failed runs in group {group_id}")
        for job in new_jobs:
            print(f"  {job.run_id} -> {job.job_id} (attempt {job.attempt})")
    else:
        print(f"No failed runs to retry in group {group_id}")

    return 0


def cancel_group_command(group_id: str, workspace: Path) -> int:
    """Cancel queued runs in a group.

    Args:
        group_id: The group ID
        workspace: Workspace root path

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    queue_mgr = QueueManager(workspace)
    count = queue_mgr.cancel_group(group_id)

    if count > 0:
        print(f"Canceled {count} queued runs in group {group_id}")
    else:
        print(f"No queued runs to cancel in group {group_id}")

    return 0


def queue_status_command(workspace: Path) -> int:
    """Show queue status.

    Args:
        workspace: Workspace root path

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    queue_mgr = QueueManager(workspace)
    state = queue_mgr.load_queue()

    daemon_state = queue_mgr.load_daemon()

    print(f"Queue Status")
    print(f"  Max parallel: {state.max_parallel}")
    print(f"  Total jobs: {len(state.jobs)}")

    # Count by state
    by_state = {}
    for job in state.jobs:
        by_state[job.state] = by_state.get(job.state, 0) + 1

    for s in ["queued", "running", "succeeded", "failed", "canceled"]:
        if s in by_state:
            print(f"  {s.capitalize()}: {by_state[s]}")

    print()
    print(f"Daemon Status")
    if daemon_state.state == "running":
        print(f"  State: {daemon_state.state}")
        print(f"  PID: {daemon_state.pid}")
        print(f"  Active jobs: {daemon_state.active_jobs}")
        print(f"  Last heartbeat: {daemon_state.last_heartbeat}")
    else:
        print(f"  State: {daemon_state.state}")

    # Show paused groups
    pause_mgr = GroupPauseManager(workspace)
    paused = pause_mgr.get_paused_groups()
    if paused:
        print()
        print(f"Paused Groups")
        for gid in sorted(paused):
            print(f"  {gid}")

    return 0
