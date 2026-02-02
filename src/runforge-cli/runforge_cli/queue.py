"""Queue management for the execution daemon.

Provides:
- Queue state file (.runforge/queue/queue.json)
- Daemon state file (.runforge/queue/daemon.json)
- Job management (enqueue, dequeue, update status)
- Atomic file updates

Queue Schema (queue.json):
{
    "version": 1,
    "kind": "execution_queue",
    "max_parallel": 2,
    "jobs": [
        {
            "job_id": "job_20260201_120000_0001",
            "kind": "run",
            "run_id": "20260201-120000-test",
            "group_id": "grp_20260201_120000_Test" | null,
            "priority": 0,
            "state": "queued" | "running" | "succeeded" | "failed" | "canceled",
            "attempt": 1,
            "created_at": "2026-02-01T12:00:00",
            "started_at": null,
            "finished_at": null,
            "error": null
        }
    ]
}

Daemon Schema (daemon.json):
{
    "version": 1,
    "pid": 12345,
    "started_at": "2026-02-01T12:00:00",
    "last_heartbeat": "2026-02-01T12:05:00",
    "max_parallel": 2,
    "active_jobs": 1,
    "state": "running" | "stopping" | "stopped"
}
"""

import json
import os
import tempfile
import threading
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any


@dataclass
class Job:
    """A single job in the queue."""

    job_id: str
    kind: str  # "run"
    run_id: str
    group_id: str | None
    priority: int
    state: str  # "queued", "running", "succeeded", "failed", "canceled"
    attempt: int
    created_at: str
    started_at: str | None = None
    finished_at: str | None = None
    error: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "job_id": self.job_id,
            "kind": self.kind,
            "run_id": self.run_id,
            "group_id": self.group_id,
            "priority": self.priority,
            "state": self.state,
            "attempt": self.attempt,
            "created_at": self.created_at,
            "started_at": self.started_at,
            "finished_at": self.finished_at,
            "error": self.error,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "Job":
        return cls(
            job_id=data["job_id"],
            kind=data.get("kind", "run"),
            run_id=data["run_id"],
            group_id=data.get("group_id"),
            priority=data.get("priority", 0),
            state=data.get("state", "queued"),
            attempt=data.get("attempt", 1),
            created_at=data.get("created_at", datetime.now().isoformat()),
            started_at=data.get("started_at"),
            finished_at=data.get("finished_at"),
            error=data.get("error"),
        )


@dataclass
class QueueState:
    """State of the execution queue."""

    version: int = 1
    kind: str = "execution_queue"
    max_parallel: int = 2
    jobs: list[Job] = field(default_factory=list)
    last_served_group: str | None = None  # For round-robin fairness

    def to_dict(self) -> dict[str, Any]:
        return {
            "version": self.version,
            "kind": self.kind,
            "max_parallel": self.max_parallel,
            "jobs": [job.to_dict() for job in self.jobs],
            "last_served_group": self.last_served_group,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "QueueState":
        jobs = [Job.from_dict(j) for j in data.get("jobs", [])]
        return cls(
            version=data.get("version", 1),
            kind=data.get("kind", "execution_queue"),
            max_parallel=data.get("max_parallel", 2),
            jobs=jobs,
            last_served_group=data.get("last_served_group"),
        )


@dataclass
class DaemonState:
    """State of the daemon process."""

    version: int = 1
    pid: int = 0
    started_at: str = ""
    last_heartbeat: str = ""
    max_parallel: int = 2
    active_jobs: int = 0
    state: str = "stopped"  # "running", "stopping", "stopped"

    def to_dict(self) -> dict[str, Any]:
        return {
            "version": self.version,
            "pid": self.pid,
            "started_at": self.started_at,
            "last_heartbeat": self.last_heartbeat,
            "max_parallel": self.max_parallel,
            "active_jobs": self.active_jobs,
            "state": self.state,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "DaemonState":
        return cls(
            version=data.get("version", 1),
            pid=data.get("pid", 0),
            started_at=data.get("started_at", ""),
            last_heartbeat=data.get("last_heartbeat", ""),
            max_parallel=data.get("max_parallel", 2),
            active_jobs=data.get("active_jobs", 0),
            state=data.get("state", "stopped"),
        )


class QueueManager:
    """Manages the execution queue with atomic file operations."""

    def __init__(self, workspace: Path):
        self.workspace = workspace
        self.queue_dir = workspace / ".runforge" / "queue"
        self.queue_file = self.queue_dir / "queue.json"
        self.daemon_file = self.queue_dir / "daemon.json"
        self._lock = threading.Lock()
        self._job_counter = 0

    def ensure_queue_dir(self) -> None:
        """Ensure queue directory exists."""
        self.queue_dir.mkdir(parents=True, exist_ok=True)

    def _atomic_write(self, path: Path, data: dict[str, Any]) -> None:
        """Write JSON file atomically using temp + rename."""
        self.ensure_queue_dir()
        fd, temp_path = tempfile.mkstemp(suffix=".json", dir=self.queue_dir)
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
            # On Windows, remove target first if exists
            if path.exists():
                path.unlink()
            Path(temp_path).rename(path)
        except Exception:
            try:
                Path(temp_path).unlink()
            except Exception:
                pass
            raise

    def load_queue(self) -> QueueState:
        """Load queue state from disk."""
        if not self.queue_file.exists():
            return QueueState()
        try:
            with open(self.queue_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            return QueueState.from_dict(data)
        except Exception:
            return QueueState()

    def save_queue(self, state: QueueState) -> None:
        """Save queue state atomically."""
        self._atomic_write(self.queue_file, state.to_dict())

    def load_daemon(self) -> DaemonState:
        """Load daemon state from disk."""
        if not self.daemon_file.exists():
            return DaemonState()
        try:
            with open(self.daemon_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            return DaemonState.from_dict(data)
        except Exception:
            return DaemonState()

    def save_daemon(self, state: DaemonState) -> None:
        """Save daemon state atomically."""
        self._atomic_write(self.daemon_file, state.to_dict())

    def _generate_job_id_unlocked(self) -> str:
        """Generate a unique job ID. Must be called with lock held."""
        self._job_counter += 1
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        return f"job_{timestamp}_{self._job_counter:04d}"

    def generate_job_id(self) -> str:
        """Generate a unique job ID (thread-safe)."""
        with self._lock:
            return self._generate_job_id_unlocked()

    def enqueue(
        self,
        run_id: str,
        group_id: str | None = None,
        priority: int = 0,
    ) -> Job:
        """Add a job to the queue. Returns the created job."""
        with self._lock:
            state = self.load_queue()

            # Check for duplicate run_id
            for job in state.jobs:
                if job.run_id == run_id and job.state in ("queued", "running"):
                    raise ValueError(f"Run {run_id} is already queued or running")

            job = Job(
                job_id=self._generate_job_id_unlocked(),
                kind="run",
                run_id=run_id,
                group_id=group_id,
                priority=priority,
                state="queued",
                attempt=1,
                created_at=datetime.now().isoformat(),
            )
            state.jobs.append(job)
            self.save_queue(state)
            return job

    def dequeue_next(self, paused_groups: set[str] | None = None) -> Job | None:
        """Get the next job to run using round-robin by group.

        Round-robin ensures fairness: after serving a group, we try to
        serve a different group next. Within a group, priority then FIFO.

        Args:
            paused_groups: Set of group IDs that are paused.

        Returns:
            Next job to run, or None if no jobs available.
        """
        if paused_groups is None:
            paused_groups = set()

        with self._lock:
            state = self.load_queue()

            # Get queued jobs not in paused groups
            queued = [
                j for j in state.jobs
                if j.state == "queued"
                and (j.group_id is None or j.group_id not in paused_groups)
            ]

            if not queued:
                return None

            # Group jobs by group_id (None = ungrouped, treated as "__ungrouped__")
            groups: dict[str, list[Job]] = {}
            for job in queued:
                gid = job.group_id if job.group_id else "__ungrouped__"
                if gid not in groups:
                    groups[gid] = []
                groups[gid].append(job)

            # Sort each group by (priority desc, created_at asc)
            for gid in groups:
                groups[gid].sort(key=lambda j: (-j.priority, j.created_at))

            # Get heads from each group (best job from each)
            candidates: list[tuple[str, Job]] = []
            for gid, jobs in groups.items():
                if jobs:
                    candidates.append((gid, jobs[0]))

            if not candidates:
                return None

            # Round-robin: prefer groups other than last_served_group
            last_served = state.last_served_group
            selected_gid: str
            selected: Job

            if last_served and len(candidates) > 1:
                # Try to find a different group
                other_candidates = [(g, j) for g, j in candidates if g != last_served]
                if other_candidates:
                    # Pick the one created earliest among others
                    other_candidates.sort(key=lambda x: x[1].created_at)
                    selected_gid, selected = other_candidates[0]
                else:
                    # All remaining are from last_served group
                    candidates.sort(key=lambda x: x[1].created_at)
                    selected_gid, selected = candidates[0]
            else:
                # First time or only one group - pick earliest
                candidates.sort(key=lambda x: x[1].created_at)
                selected_gid, selected = candidates[0]

            # Mark as running
            for job in state.jobs:
                if job.job_id == selected.job_id:
                    job.state = "running"
                    job.started_at = datetime.now().isoformat()
                    break

            # Update last_served_group for next round-robin
            state.last_served_group = selected_gid
            self.save_queue(state)
            return selected

    def complete_job(self, job_id: str, success: bool, error: str | None = None) -> None:
        """Mark a job as completed."""
        with self._lock:
            state = self.load_queue()
            for job in state.jobs:
                if job.job_id == job_id:
                    job.state = "succeeded" if success else "failed"
                    job.finished_at = datetime.now().isoformat()
                    job.error = error
                    break
            self.save_queue(state)

    def cancel_job(self, job_id: str) -> bool:
        """Cancel a queued job. Returns True if canceled."""
        with self._lock:
            state = self.load_queue()
            for job in state.jobs:
                if job.job_id == job_id:
                    if job.state == "queued":
                        job.state = "canceled"
                        job.finished_at = datetime.now().isoformat()
                        self.save_queue(state)
                        return True
                    return False
            return False

    def cancel_group(self, group_id: str) -> int:
        """Cancel all queued jobs in a group. Returns count canceled."""
        with self._lock:
            state = self.load_queue()
            count = 0
            for job in state.jobs:
                if job.group_id == group_id and job.state == "queued":
                    job.state = "canceled"
                    job.finished_at = datetime.now().isoformat()
                    count += 1
            if count > 0:
                self.save_queue(state)
            return count

    def retry_failed(self, group_id: str) -> list[Job]:
        """Re-enqueue failed jobs in a group. Returns new jobs."""
        with self._lock:
            state = self.load_queue()
            new_jobs = []

            for job in state.jobs:
                if job.group_id == group_id and job.state == "failed":
                    # Create new job for retry
                    new_job = Job(
                        job_id=self._generate_job_id_unlocked(),
                        kind=job.kind,
                        run_id=job.run_id,
                        group_id=job.group_id,
                        priority=job.priority,
                        state="queued",
                        attempt=job.attempt + 1,
                        created_at=datetime.now().isoformat(),
                    )
                    new_jobs.append(new_job)

            state.jobs.extend(new_jobs)
            if new_jobs:
                self.save_queue(state)
            return new_jobs

    def get_running_count(self) -> int:
        """Get count of currently running jobs."""
        state = self.load_queue()
        return sum(1 for j in state.jobs if j.state == "running")

    def get_queued_count(self) -> int:
        """Get count of queued jobs."""
        state = self.load_queue()
        return sum(1 for j in state.jobs if j.state == "queued")

    def set_max_parallel(self, max_parallel: int) -> None:
        """Update the max_parallel setting."""
        with self._lock:
            state = self.load_queue()
            state.max_parallel = max_parallel
            self.save_queue(state)

    def cleanup_old_jobs(self, max_age_days: int = 7) -> int:
        """Remove completed/failed/canceled jobs older than max_age_days."""
        from datetime import timedelta

        cutoff = datetime.now() - timedelta(days=max_age_days)
        cutoff_str = cutoff.isoformat()

        with self._lock:
            state = self.load_queue()
            original_count = len(state.jobs)

            state.jobs = [
                j for j in state.jobs
                if j.state in ("queued", "running")
                or (j.finished_at and j.finished_at > cutoff_str)
            ]

            removed = original_count - len(state.jobs)
            if removed > 0:
                self.save_queue(state)
            return removed


class GroupPauseManager:
    """Manages pause state for groups."""

    def __init__(self, workspace: Path):
        self.workspace = workspace
        self.groups_dir = workspace / ".runforge" / "groups"

    def is_paused(self, group_id: str) -> bool:
        """Check if a group is paused."""
        group_file = self.groups_dir / group_id / "group.json"
        if not group_file.exists():
            return False
        try:
            with open(group_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            return data.get("paused", False)
        except Exception:
            return False

    def set_paused(self, group_id: str, paused: bool) -> bool:
        """Set the paused state for a group. Returns True if successful."""
        group_file = self.groups_dir / group_id / "group.json"
        if not group_file.exists():
            return False

        try:
            with open(group_file, "r", encoding="utf-8") as f:
                data = json.load(f)

            data["paused"] = paused

            # Atomic write
            fd, temp_path = tempfile.mkstemp(
                suffix=".json", dir=self.groups_dir / group_id
            )
            try:
                with os.fdopen(fd, "w", encoding="utf-8") as f:
                    json.dump(data, f, indent=2)
                if group_file.exists():
                    group_file.unlink()
                Path(temp_path).rename(group_file)
                return True
            except Exception:
                try:
                    Path(temp_path).unlink()
                except Exception:
                    pass
                raise
        except Exception:
            return False

    def get_paused_groups(self) -> set[str]:
        """Get set of all paused group IDs."""
        paused = set()
        if not self.groups_dir.exists():
            return paused

        for group_dir in self.groups_dir.iterdir():
            if group_dir.is_dir():
                group_file = group_dir / "group.json"
                if group_file.exists():
                    try:
                        with open(group_file, "r", encoding="utf-8") as f:
                            data = json.load(f)
                        if data.get("paused", False):
                            paused.add(group_dir.name)
                    except Exception:
                        pass
        return paused
