"""Sweep command implementation.

Orchestrates multiple runs from a sweep plan with:
- Grid strategy expansion
- Concurrent execution with max_parallel cap
- Atomic group.json updates
- Cancel handling
"""

import copy
import itertools
import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any

from . import __version__
from .exit_codes import CANCELED, INTERNAL_ERROR, INVALID_PLAN, MISSING_FILES, SUCCESS
from .tokens import group_canceled, group_complete, group_run, group_run_complete, group_start


@dataclass
class SweepPlan:
    """Parsed sweep plan."""

    version: int
    kind: str
    created_at: str
    created_by: str
    workspace: str
    group_name: str
    group_notes: str | None
    base_request: dict[str, Any]
    strategy_type: str
    parameters: list[dict[str, Any]]
    max_parallel: int
    fail_fast: bool
    stop_on_cancel: bool

    @classmethod
    def load(cls, path: Path) -> "SweepPlan":
        """Load from sweep_plan.json file."""
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)

        return cls(
            version=data.get("version", 0),
            kind=data.get("kind", ""),
            created_at=data.get("created_at", ""),
            created_by=data.get("created_by", ""),
            workspace=data.get("workspace", ""),
            group_name=data.get("group", {}).get("name", "Unnamed Sweep"),
            group_notes=data.get("group", {}).get("notes"),
            base_request=data.get("base_request", {}),
            strategy_type=data.get("strategy", {}).get("type", "grid"),
            parameters=data.get("strategy", {}).get("parameters", []),
            max_parallel=data.get("execution", {}).get("max_parallel", 1),
            fail_fast=data.get("execution", {}).get("fail_fast", False),
            stop_on_cancel=data.get("execution", {}).get("stop_on_cancel", True),
        )

    def validate(self) -> list[str]:
        """Validate the plan. Returns list of errors (empty if valid)."""
        errors = []

        if self.version != 1:
            errors.append(f"Unsupported plan version: {self.version}")

        if self.kind != "sweep_plan":
            errors.append(f"Invalid kind: {self.kind}, expected 'sweep_plan'")

        if not self.workspace:
            errors.append("workspace is required")

        if not self.base_request:
            errors.append("base_request is required")

        if self.strategy_type not in ("grid", "list"):
            errors.append(f"Unsupported strategy type: {self.strategy_type}")

        if self.strategy_type == "grid" and not self.parameters:
            errors.append("grid strategy requires at least one parameter")

        if self.max_parallel < 1:
            errors.append("max_parallel must be >= 1")

        return errors


@dataclass
class RunConfig:
    """Configuration for a single run in the sweep."""

    run_id: str
    overrides: dict[str, Any]


@dataclass
class RunResult:
    """Result from executing a single run."""

    run_id: str
    status: str  # "succeeded", "failed", "canceled"
    primary_metric_name: str | None = None
    primary_metric_value: float | None = None
    result_ref: str | None = None


@dataclass
class GroupState:
    """Mutable state for the sweep group."""

    group_id: str
    status: str  # "running", "completed", "failed", "canceled"
    runs: list[dict[str, Any]] = field(default_factory=list)
    succeeded: int = 0
    failed: int = 0
    canceled: int = 0
    best_run_id: str | None = None
    best_metric_name: str | None = None
    best_metric_value: float | None = None
    started_at: str = ""
    finished_at: str | None = None
    cancelled: bool = False


class SweepOrchestrator:
    """Orchestrates a sweep of runs."""

    def __init__(self, plan: SweepPlan, plan_path: Path):
        self.plan = plan
        self.plan_path = plan_path
        self.workspace = Path(plan.workspace)
        self.group_id = self._generate_group_id()
        self.group_dir = self.workspace / ".runforge" / "groups" / self.group_id
        self.state: GroupState | None = None
        self._cancel_requested = False
        self._lock = threading.Lock()

    def _generate_group_id(self) -> str:
        """Generate a unique group ID."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        # Sanitize group name for use in ID
        name_part = "".join(c if c.isalnum() else "_" for c in self.plan.group_name[:20])
        return f"grp_{timestamp}_{name_part}"

    def expand_grid(self) -> list[RunConfig]:
        """Expand grid parameters into individual run configs."""
        if self.plan.strategy_type != "grid":
            raise ValueError(f"expand_grid only supports grid strategy, got {self.plan.strategy_type}")

        if not self.plan.parameters:
            return []

        # Extract parameter paths and values
        param_paths = []
        param_values = []
        for param in self.plan.parameters:
            param_paths.append(param["path"])
            # Values can be a JSON array
            values = param["values"]
            if isinstance(values, list):
                param_values.append(values)
            else:
                param_values.append([values])

        # Generate cartesian product
        runs = []
        timestamp_base = datetime.now().strftime("%Y%m%d-%H%M%S")

        for idx, combo in enumerate(itertools.product(*param_values)):
            run_id = f"{timestamp_base}-sweep-{idx:04d}"
            overrides = {}
            for path, value in zip(param_paths, combo):
                overrides[path] = value
            runs.append(RunConfig(run_id=run_id, overrides=overrides))

        return runs

    def apply_overrides(self, base: dict[str, Any], overrides: dict[str, Any]) -> dict[str, Any]:
        """Apply dot-path overrides to a base request."""
        result = copy.deepcopy(base)

        for path, value in overrides.items():
            parts = path.split(".")
            current = result

            # Navigate to the parent of the target field
            for part in parts[:-1]:
                if part not in current:
                    current[part] = {}
                current = current[part]

            # Set the value (None removes the key)
            if value is None:
                current.pop(parts[-1], None)
            else:
                current[parts[-1]] = value

        return result

    def setup_group(self, run_configs: list[RunConfig]) -> None:
        """Create group directory and initial group.json."""
        self.group_dir.mkdir(parents=True, exist_ok=True)

        # Copy plan to group folder
        plan_copy = self.group_dir / "plan.json"
        shutil.copy2(self.plan_path, plan_copy)

        # Initialize state
        now = datetime.now().isoformat()
        self.state = GroupState(
            group_id=self.group_id,
            status="running",
            started_at=now,
            runs=[
                {
                    "run_id": rc.run_id,
                    "status": "pending",
                    "request_overrides": rc.overrides,
                    "result_ref": None,
                    "primary_metric": None,
                }
                for rc in run_configs
            ],
        )

        self._write_group_json()

    def _write_group_json(self) -> None:
        """Write group.json atomically."""
        if self.state is None:
            return

        group_data = {
            "version": 1,
            "kind": "run_group",
            "group_id": self.state.group_id,
            "created_at": self.state.started_at,
            "created_by": f"runforge-cli@{__version__}",
            "name": self.plan.group_name,
            "notes": self.plan.group_notes,
            "plan_ref": "plan.json",
            "status": self.state.status,
            "execution": {
                "max_parallel": self.plan.max_parallel,
                "started_at": self.state.started_at,
                "finished_at": self.state.finished_at,
                "cancelled": self.state.cancelled,
            },
            "runs": self.state.runs,
            "summary": {
                "total": len(self.state.runs),
                "succeeded": self.state.succeeded,
                "failed": self.state.failed,
                "canceled": self.state.canceled,
                "best_run_id": self.state.best_run_id,
                "best_primary_metric": (
                    {"name": self.state.best_metric_name, "value": self.state.best_metric_value}
                    if self.state.best_run_id
                    else None
                ),
            },
        }

        # Atomic write via temp file + rename
        target = self.group_dir / "group.json"
        fd, temp_path = tempfile.mkstemp(suffix=".json", dir=self.group_dir)
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                json.dump(group_data, f, indent=2)
            # On Windows, need to remove target first if it exists
            if target.exists():
                target.unlink()
            Path(temp_path).rename(target)
        except Exception:
            # Clean up temp file on error
            try:
                Path(temp_path).unlink()
            except Exception:
                pass
            raise

    def create_run_directory(self, run_config: RunConfig) -> Path:
        """Create run directory with request.json."""
        run_dir = self.workspace / ".ml" / "runs" / run_config.run_id
        run_dir.mkdir(parents=True, exist_ok=True)

        # Apply overrides to base request
        request = self.apply_overrides(self.plan.base_request, run_config.overrides)

        # Update created_at and created_by
        request["created_at"] = datetime.now().isoformat()
        request["created_by"] = f"runforge-cli@{__version__}"

        # Add sweep metadata
        request["sweep_group_id"] = self.group_id

        # Write request.json
        request_path = run_dir / "request.json"
        with open(request_path, "w", encoding="utf-8") as f:
            json.dump(request, f, indent=2)

        return run_dir

    def execute_run(self, run_config: RunConfig, index: int, total: int) -> RunResult:
        """Execute a single run using the CLI."""
        print(group_run(run_config.run_id, index, total), flush=True)

        # Create run directory
        run_dir = self.create_run_directory(run_config)

        # Execute using CLI run command
        # We call ourselves to avoid reimplementing the run logic
        cmd = [
            sys.executable,
            "-m",
            "runforge_cli",
            "run",
            "--run-dir",
            str(run_dir),
        ]

        try:
            # Run the subprocess
            result = subprocess.run(
                cmd,
                capture_output=False,  # Let output flow through
                text=True,
                timeout=3600,  # 1 hour max per run
            )

            status = "succeeded" if result.returncode == 0 else "failed"
        except subprocess.TimeoutExpired:
            status = "failed"
        except Exception as e:
            print(f"Error executing run {run_config.run_id}: {e}", file=sys.stderr)
            status = "failed"

        # Read result.json to get primary metric
        primary_name = None
        primary_value = None
        result_ref = None

        result_path = run_dir / "result.json"
        if result_path.exists():
            result_ref = str(result_path.relative_to(self.workspace))
            try:
                with open(result_path, "r", encoding="utf-8") as f:
                    result_data = json.load(f)
                summary = result_data.get("summary", {})
                pm = summary.get("primary_metric", {})
                if pm:
                    primary_name = pm.get("name")
                    primary_value = pm.get("value")
            except Exception:
                pass

        print(group_run_complete(run_config.run_id, status), flush=True)

        return RunResult(
            run_id=run_config.run_id,
            status=status,
            primary_metric_name=primary_name,
            primary_metric_value=primary_value,
            result_ref=result_ref,
        )

    def update_run_result(self, result: RunResult) -> None:
        """Update group state with run result."""
        with self._lock:
            if self.state is None:
                return

            # Find and update the run entry
            for run_entry in self.state.runs:
                if run_entry["run_id"] == result.run_id:
                    run_entry["status"] = result.status
                    run_entry["result_ref"] = result.result_ref
                    if result.primary_metric_name and result.primary_metric_value is not None:
                        run_entry["primary_metric"] = {
                            "name": result.primary_metric_name,
                            "value": result.primary_metric_value,
                        }
                    break

            # Update counters
            if result.status == "succeeded":
                self.state.succeeded += 1
                # Update best run if this is better
                if result.primary_metric_value is not None:
                    if (
                        self.state.best_metric_value is None
                        or result.primary_metric_value > self.state.best_metric_value
                    ):
                        self.state.best_run_id = result.run_id
                        self.state.best_metric_name = result.primary_metric_name
                        self.state.best_metric_value = result.primary_metric_value
            elif result.status == "failed":
                self.state.failed += 1
            elif result.status == "canceled":
                self.state.canceled += 1

            self._write_group_json()

    def request_cancel(self) -> None:
        """Request cancellation of remaining runs."""
        self._cancel_requested = True

    def is_cancel_requested(self) -> bool:
        """Check if cancellation has been requested."""
        return self._cancel_requested

    def execute(self) -> int:
        """Execute the full sweep. Returns exit code."""
        # Expand grid
        run_configs = self.expand_grid()
        if not run_configs:
            print("ERROR: No runs to execute (empty grid)", file=sys.stderr)
            return INVALID_PLAN

        total = len(run_configs)
        print(f"Sweep plan: {total} runs, max_parallel={self.plan.max_parallel}")
        print(group_start(self.group_id, total), flush=True)

        # Setup group directory
        self.setup_group(run_configs)
        print(f"Group directory: {self.group_dir}")

        # Execute runs with concurrency
        completed_runs = []
        try:
            with ThreadPoolExecutor(max_workers=self.plan.max_parallel) as executor:
                # Submit all runs
                futures = {}
                for idx, rc in enumerate(run_configs):
                    if self.is_cancel_requested():
                        break
                    future = executor.submit(self.execute_run, rc, idx + 1, total)
                    futures[future] = rc

                # Process completions
                for future in as_completed(futures):
                    try:
                        result = future.result()
                        completed_runs.append(result)
                        self.update_run_result(result)

                        # Check fail_fast
                        if self.plan.fail_fast and result.status == "failed":
                            print("Fail-fast triggered, canceling remaining runs")
                            self.request_cancel()
                            break

                    except Exception as e:
                        rc = futures[future]
                        print(f"Error in run {rc.run_id}: {e}", file=sys.stderr)
                        result = RunResult(run_id=rc.run_id, status="failed")
                        completed_runs.append(result)
                        self.update_run_result(result)

        except KeyboardInterrupt:
            self.request_cancel()

        # Finalize state
        with self._lock:
            if self.state:
                self.state.finished_at = datetime.now().isoformat()

                # Mark any pending runs as canceled
                for run_entry in self.state.runs:
                    if run_entry["status"] == "pending":
                        run_entry["status"] = "canceled"
                        self.state.canceled += 1

                # Determine final status
                if self._cancel_requested:
                    self.state.status = "canceled"
                    self.state.cancelled = True
                    print(group_canceled(self.group_id), flush=True)
                elif self.state.failed > 0:
                    self.state.status = "failed"
                else:
                    self.state.status = "completed"

                self._write_group_json()

                print(
                    group_complete(
                        self.group_id,
                        self.state.succeeded,
                        self.state.failed,
                        self.state.canceled,
                    ),
                    flush=True,
                )

        # Return appropriate exit code
        if self._cancel_requested:
            return CANCELED
        elif self.state and self.state.failed > 0:
            return 1  # FAILED
        return SUCCESS


def sweep_command(plan_path: Path) -> int:
    """Execute a sweep from a plan file.

    Args:
        plan_path: Path to sweep_plan.json

    Returns:
        Exit code
    """
    # Validate plan file exists
    if not plan_path.exists():
        print(f"ERROR: Plan file not found: {plan_path}", file=sys.stderr)
        return MISSING_FILES

    # Load and validate plan
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

    # Validate workspace
    workspace = Path(plan.workspace)
    if not workspace.exists():
        print(f"ERROR: Workspace not found: {workspace}", file=sys.stderr)
        return MISSING_FILES

    print(f"runforge-cli sweep v{__version__}")
    print(f"Plan: {plan_path}")
    print(f"Workspace: {workspace}")
    print(f"Group: {plan.group_name}")

    # Create orchestrator and execute
    orchestrator = SweepOrchestrator(plan, plan_path)

    # Setup signal handler for graceful cancel
    def handle_signal(signum: int, frame: Any) -> None:
        print("\nCancel requested, stopping remaining runs...")
        orchestrator.request_cancel()

    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)

    try:
        return orchestrator.execute()
    except Exception as e:
        print(f"ERROR: Sweep failed: {e}", file=sys.stderr)
        import traceback

        traceback.print_exc()
        return INTERNAL_ERROR
