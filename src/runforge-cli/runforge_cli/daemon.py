"""Execution daemon for runforge-cli.

The daemon is a long-running process that:
- Watches the queue for new jobs
- Executes jobs up to max_parallel concurrency
- Respects group pause state
- Writes heartbeat for Desktop to monitor
- Handles graceful shutdown

Usage:
    runforge-cli daemon --workspace <path> [--max-parallel 2]
"""

import json
import os
import signal
import subprocess
import sys
import threading
import time
from concurrent.futures import Future, ThreadPoolExecutor
from datetime import datetime
from pathlib import Path
from typing import Any

from . import __version__
from .queue import DaemonState, GroupPauseManager, Job, QueueManager


class DaemonLock:
    """Lock file to ensure only one daemon runs per workspace."""

    def __init__(self, queue_dir: Path):
        self.lock_file = queue_dir / "daemon.lock"
        self._fd: int | None = None

    def acquire(self) -> bool:
        """Try to acquire the lock. Returns True if successful."""
        import msvcrt  # Windows-specific

        try:
            self.lock_file.parent.mkdir(parents=True, exist_ok=True)

            # Open file for writing (create if needed)
            self._fd = os.open(
                str(self.lock_file),
                os.O_RDWR | os.O_CREAT,
                0o644,
            )

            # Try to lock exclusively (non-blocking)
            try:
                msvcrt.locking(self._fd, msvcrt.LK_NBLCK, 1)
                # Write PID to lock file
                os.lseek(self._fd, 0, os.SEEK_SET)
                os.ftruncate(self._fd, 0)
                os.write(self._fd, str(os.getpid()).encode())
                return True
            except OSError:
                # Lock failed - another daemon is running
                os.close(self._fd)
                self._fd = None
                return False

        except Exception:
            if self._fd is not None:
                try:
                    os.close(self._fd)
                except Exception:
                    pass
                self._fd = None
            return False

    def release(self) -> None:
        """Release the lock."""
        if self._fd is not None:
            try:
                import msvcrt
                msvcrt.locking(self._fd, msvcrt.LK_UNLCK, 1)
            except Exception:
                pass
            try:
                os.close(self._fd)
            except Exception:
                pass
            self._fd = None
            try:
                self.lock_file.unlink()
            except Exception:
                pass

    def get_running_pid(self) -> int | None:
        """Get PID of the running daemon, if any."""
        if not self.lock_file.exists():
            return None
        try:
            with open(self.lock_file, "r") as f:
                return int(f.read().strip())
        except Exception:
            return None


class ExecutionDaemon:
    """Background daemon that processes the job queue."""

    def __init__(
        self,
        workspace: Path,
        max_parallel: int = 2,
        gpu_slots: int = 1,
        heartbeat_interval: float = 5.0,
        poll_interval: float = 1.0,
    ):
        self.workspace = workspace
        self.max_parallel = max_parallel
        self.gpu_slots = gpu_slots
        self.heartbeat_interval = heartbeat_interval
        self.poll_interval = poll_interval

        self.queue_mgr = QueueManager(workspace)
        self.pause_mgr = GroupPauseManager(workspace)
        self._daemon_lock = DaemonLock(self.queue_mgr.queue_dir)

        self._shutdown_requested = False
        self._lock = threading.Lock()
        self._active_jobs: dict[str, Future[tuple[bool, str | None]]] = {}
        self._active_gpu_jobs: set[str] = set()  # Track which active jobs are GPU jobs
        self._executor: ThreadPoolExecutor | None = None
        self._heartbeat_thread: threading.Thread | None = None

    def _write_daemon_state(self, state: str = "running") -> None:
        """Write daemon state file."""
        with self._lock:
            active_gpu = len(self._active_gpu_jobs)
        daemon_state = DaemonState(
            version=1,
            pid=os.getpid(),
            started_at=getattr(self, "_started_at", datetime.now().isoformat()),
            last_heartbeat=datetime.now().isoformat(),
            max_parallel=self.max_parallel,
            gpu_slots=self.gpu_slots,
            active_jobs=len(self._active_jobs),
            active_gpu_jobs=active_gpu,
            state=state,
        )
        self.queue_mgr.save_daemon(daemon_state)

    def _heartbeat_loop(self) -> None:
        """Background thread that writes heartbeat."""
        while not self._shutdown_requested:
            try:
                with self._lock:
                    active = len(self._active_jobs)
                    active_gpu = len(self._active_gpu_jobs)
                daemon_state = DaemonState(
                    version=1,
                    pid=os.getpid(),
                    started_at=self._started_at,
                    last_heartbeat=datetime.now().isoformat(),
                    max_parallel=self.max_parallel,
                    gpu_slots=self.gpu_slots,
                    active_jobs=active,
                    active_gpu_jobs=active_gpu,
                    state="stopping" if self._shutdown_requested else "running",
                )
                self.queue_mgr.save_daemon(daemon_state)
            except Exception as e:
                print(f"Heartbeat error: {e}", file=sys.stderr)

            time.sleep(self.heartbeat_interval)

    def _execute_job(self, job: Job) -> tuple[bool, str | None]:
        """Execute a single job. Returns (success, error_message)."""
        run_dir = self.workspace / ".ml" / "runs" / job.run_id

        if not run_dir.exists():
            return False, f"Run directory not found: {run_dir}"

        cmd = [
            sys.executable,
            "-m",
            "runforge_cli",
            "run",
            "--run-dir",
            str(run_dir),
            "--workspace",
            str(self.workspace),
        ]

        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=3600,  # 1 hour max
            )

            if result.returncode == 0:
                return True, None
            else:
                # Extract error from stderr
                error = result.stderr.strip()[-500:] if result.stderr else f"Exit code: {result.returncode}"
                return False, error

        except subprocess.TimeoutExpired:
            return False, "Job timed out after 1 hour"
        except Exception as e:
            return False, str(e)

    def _update_group_on_completion(self, job: Job, success: bool) -> None:
        """Update group.json when a job completes."""
        if not job.group_id:
            return

        group_file = self.workspace / ".runforge" / "groups" / job.group_id / "group.json"
        if not group_file.exists():
            return

        try:
            with open(group_file, "r", encoding="utf-8") as f:
                data = json.load(f)

            # Update run status
            for run in data.get("runs", []):
                if run.get("run_id") == job.run_id:
                    run["status"] = "succeeded" if success else "failed"

                    # Try to read result.json for metrics
                    if success:
                        result_path = self.workspace / ".ml" / "runs" / job.run_id / "result.json"
                        if result_path.exists():
                            try:
                                with open(result_path, "r", encoding="utf-8") as rf:
                                    result_data = json.load(rf)
                                pm = result_data.get("summary", {}).get("primary_metric", {})
                                if pm:
                                    run["primary_metric"] = pm
                                run["result_ref"] = str(result_path.relative_to(self.workspace))
                            except Exception:
                                pass
                    break

            # Update summary counts
            summary = data.get("summary", {})
            runs = data.get("runs", [])
            summary["succeeded"] = sum(1 for r in runs if r.get("status") == "succeeded")
            summary["failed"] = sum(1 for r in runs if r.get("status") == "failed")
            summary["canceled"] = sum(1 for r in runs if r.get("status") == "canceled")

            # Check if all runs are done
            pending = sum(1 for r in runs if r.get("status") in ("pending", "queued", "running"))
            if pending == 0:
                data["status"] = "failed" if summary["failed"] > 0 else "completed"
                data["execution"]["finished_at"] = datetime.now().isoformat()

            # Update best run
            best_value = None
            best_run_id = None
            best_metric = None
            for run in runs:
                pm = run.get("primary_metric")
                if pm and pm.get("value") is not None:
                    if best_value is None or pm["value"] > best_value:
                        best_value = pm["value"]
                        best_run_id = run["run_id"]
                        best_metric = pm

            summary["best_run_id"] = best_run_id
            summary["best_primary_metric"] = best_metric
            data["summary"] = summary

            # Atomic write
            import tempfile
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
            print(f"Error updating group {job.group_id}: {e}", file=sys.stderr)

    def _process_completed_jobs(self) -> None:
        """Check for completed jobs and update queue."""
        with self._lock:
            completed = []
            for job_id, future in list(self._active_jobs.items()):
                if future.done():
                    completed.append((job_id, future))

            for job_id, future in completed:
                del self._active_jobs[job_id]
                # Remove from GPU tracking if applicable
                self._active_gpu_jobs.discard(job_id)

                try:
                    success, error = future.result()
                    self.queue_mgr.complete_job(job_id, success, error)

                    # Get job to update group
                    state = self.queue_mgr.load_queue()
                    for job in state.jobs:
                        if job.job_id == job_id:
                            self._update_group_on_completion(job, success)
                            status = "succeeded" if success else "failed"
                            gpu_tag = " [GPU]" if job.requires_gpu else ""
                            print(f"[DAEMON] Job {job_id} ({job.run_id}){gpu_tag} {status}")
                            break

                except Exception as e:
                    self.queue_mgr.complete_job(job_id, False, str(e))
                    print(f"[DAEMON] Job {job_id} failed: {e}", file=sys.stderr)

    def _schedule_jobs(self) -> None:
        """Schedule new jobs up to max_parallel, respecting GPU slots."""
        with self._lock:
            available_slots = self.max_parallel - len(self._active_jobs)
            gpu_slots_available = self.gpu_slots - len(self._active_gpu_jobs)

        if available_slots <= 0:
            return

        paused_groups = self.pause_mgr.get_paused_groups()

        for _ in range(available_slots):
            # Recalculate GPU slots in case we scheduled a GPU job
            with self._lock:
                gpu_slots_available = self.gpu_slots - len(self._active_gpu_jobs)

            job = self.queue_mgr.dequeue_next(paused_groups, gpu_slots_available)
            if job is None:
                break

            gpu_tag = " [GPU]" if job.requires_gpu else ""
            print(f"[DAEMON] Starting job {job.job_id} ({job.run_id}){gpu_tag}")

            with self._lock:
                future = self._executor.submit(self._execute_job, job)
                self._active_jobs[job.job_id] = future
                if job.requires_gpu:
                    self._active_gpu_jobs.add(job.job_id)

    def request_shutdown(self) -> None:
        """Request graceful shutdown."""
        print("[DAEMON] Shutdown requested")
        self._shutdown_requested = True

    def run(self) -> int:
        """Run the daemon. Returns exit code."""
        from .gpu import detect_gpu, get_gpu_summary

        self._started_at = datetime.now().isoformat()

        print(f"[DAEMON] runforge-cli daemon v{__version__}")
        print(f"[DAEMON] Workspace: {self.workspace}")
        print(f"[DAEMON] Max parallel: {self.max_parallel}")
        print(f"[DAEMON] GPU slots: {self.gpu_slots}")
        print(f"[DAEMON] PID: {os.getpid()}")

        # GPU detection at startup
        gpu_info = detect_gpu()
        if gpu_info.available:
            print(f"[DAEMON] {get_gpu_summary()}")
        else:
            print(f"[DAEMON] No GPU available ({gpu_info.error})")
            if self.gpu_slots > 0:
                print(f"[DAEMON] Warning: gpu_slots={self.gpu_slots} but no GPU detected")

        # Ensure queue directory exists
        self.queue_mgr.ensure_queue_dir()

        # Try to acquire lock
        if not self._daemon_lock.acquire():
            existing_pid = self._daemon_lock.get_running_pid()
            print(f"ERROR: Another daemon is already running (PID: {existing_pid})", file=sys.stderr)
            return 1

        try:
            # Update queue settings
            self.queue_mgr.set_max_parallel(self.max_parallel)
            self.queue_mgr.set_gpu_slots(self.gpu_slots)

            # Write initial state
            self._write_daemon_state("running")

            # Start heartbeat thread
            self._heartbeat_thread = threading.Thread(target=self._heartbeat_loop, daemon=True)
            self._heartbeat_thread.start()

            # Create executor
            self._executor = ThreadPoolExecutor(max_workers=self.max_parallel)

            try:
                while not self._shutdown_requested:
                    self._process_completed_jobs()
                    self._schedule_jobs()
                    time.sleep(self.poll_interval)

                # Graceful shutdown - wait for active jobs
                print("[DAEMON] Waiting for active jobs to complete...")
                self._write_daemon_state("stopping")

                # Wait for all active jobs with timeout
                shutdown_timeout = 60  # 1 minute
                start = time.time()
                while self._active_jobs and (time.time() - start) < shutdown_timeout:
                    self._process_completed_jobs()
                    time.sleep(0.5)

                # Cancel any remaining running jobs in queue
                with self._lock:
                    for job_id in list(self._active_jobs.keys()):
                        self.queue_mgr.complete_job(job_id, False, "Daemon shutdown")

            finally:
                if self._executor:
                    self._executor.shutdown(wait=False)
                self._write_daemon_state("stopped")
                print("[DAEMON] Shutdown complete")

        finally:
            # Always release lock on any exit
            self._daemon_lock.release()

        return 0


def daemon_command(workspace: Path, max_parallel: int = 2, gpu_slots: int = 1) -> int:
    """Run the execution daemon.

    Args:
        workspace: Workspace root path
        max_parallel: Maximum concurrent jobs
        gpu_slots: Maximum concurrent GPU jobs

    Returns:
        Exit code
    """
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return 1

    daemon = ExecutionDaemon(workspace, max_parallel, gpu_slots)

    # Setup signal handlers
    def handle_signal(signum: int, frame: Any) -> None:
        daemon.request_shutdown()

    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)

    return daemon.run()
