"""Tests for the execution queue system."""

import json
import tempfile
from pathlib import Path

import pytest

from runforge_cli.queue import (
    DaemonState,
    GroupPauseManager,
    Job,
    QueueManager,
    QueueState,
)


class TestJob:
    """Tests for Job dataclass."""

    def test_to_dict(self) -> None:
        job = Job(
            job_id="job_001",
            kind="run",
            run_id="20260201-120000-test",
            group_id="grp_001",
            priority=5,
            state="queued",
            attempt=1,
            created_at="2026-02-01T12:00:00",
        )
        d = job.to_dict()
        assert d["job_id"] == "job_001"
        assert d["run_id"] == "20260201-120000-test"
        assert d["group_id"] == "grp_001"
        assert d["priority"] == 5
        assert d["state"] == "queued"

    def test_from_dict(self) -> None:
        d = {
            "job_id": "job_002",
            "run_id": "20260201-120001-test",
            "group_id": None,
            "priority": 0,
            "state": "running",
            "attempt": 2,
            "created_at": "2026-02-01T12:00:01",
            "started_at": "2026-02-01T12:00:05",
        }
        job = Job.from_dict(d)
        assert job.job_id == "job_002"
        assert job.group_id is None
        assert job.attempt == 2
        assert job.started_at == "2026-02-01T12:00:05"


class TestQueueState:
    """Tests for QueueState dataclass."""

    def test_to_dict_empty(self) -> None:
        state = QueueState()
        d = state.to_dict()
        assert d["version"] == 1
        assert d["kind"] == "execution_queue"
        assert d["max_parallel"] == 2
        assert d["jobs"] == []

    def test_from_dict_with_jobs(self) -> None:
        d = {
            "version": 1,
            "kind": "execution_queue",
            "max_parallel": 4,
            "jobs": [
                {
                    "job_id": "job_001",
                    "run_id": "run1",
                    "state": "queued",
                    "attempt": 1,
                    "created_at": "2026-02-01T12:00:00",
                }
            ],
        }
        state = QueueState.from_dict(d)
        assert state.max_parallel == 4
        assert len(state.jobs) == 1
        assert state.jobs[0].run_id == "run1"


class TestQueueManager:
    """Tests for QueueManager."""

    @pytest.fixture
    def workspace(self) -> Path:
        with tempfile.TemporaryDirectory() as tmp:
            yield Path(tmp)

    def test_ensure_queue_dir(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.ensure_queue_dir()
        assert mgr.queue_dir.exists()

    def test_load_empty_queue(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        state = mgr.load_queue()
        assert state.max_parallel == 2
        assert len(state.jobs) == 0

    def test_enqueue_creates_job(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001", group_id="grp-001", priority=5)

        assert job.run_id == "run-001"
        assert job.group_id == "grp-001"
        assert job.priority == 5
        assert job.state == "queued"

        # Verify persisted
        state = mgr.load_queue()
        assert len(state.jobs) == 1
        assert state.jobs[0].run_id == "run-001"

    def test_enqueue_duplicate_fails(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-001")

        with pytest.raises(ValueError, match="already queued"):
            mgr.enqueue("run-001")

    def test_dequeue_next_fifo(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-001")
        mgr.enqueue("run-002")
        mgr.enqueue("run-003")

        job1 = mgr.dequeue_next()
        assert job1 is not None
        assert job1.run_id == "run-001"
        assert job1.state == "running"

        job2 = mgr.dequeue_next()
        assert job2 is not None
        assert job2.run_id == "run-002"

    def test_dequeue_respects_priority(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-low", priority=0)
        mgr.enqueue("run-high", priority=10)
        mgr.enqueue("run-med", priority=5)

        job = mgr.dequeue_next()
        assert job is not None
        assert job.run_id == "run-high"

    def test_dequeue_respects_paused_groups(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-001", group_id="grp-paused")
        mgr.enqueue("run-002", group_id="grp-active")

        # Dequeue with grp-paused in paused set
        job = mgr.dequeue_next(paused_groups={"grp-paused"})
        assert job is not None
        assert job.run_id == "run-002"

    def test_dequeue_round_robin_by_group(self, workspace: Path) -> None:
        """Test fair scheduling across groups."""
        mgr = QueueManager(workspace)

        # Add jobs from two groups
        mgr.enqueue("grp1-run1", group_id="grp1")
        mgr.enqueue("grp1-run2", group_id="grp1")
        mgr.enqueue("grp2-run1", group_id="grp2")
        mgr.enqueue("grp2-run2", group_id="grp2")

        # Should interleave: first from each group by creation order
        # Since grp1-run1 was created first, it goes first
        # Then grp2-run1 (first from grp2)
        # Then grp1-run2 (next from grp1)
        # Then grp2-run2 (next from grp2)
        job1 = mgr.dequeue_next()
        assert job1.run_id == "grp1-run1"

        job2 = mgr.dequeue_next()
        assert job2.run_id == "grp2-run1"

        job3 = mgr.dequeue_next()
        assert job3.run_id == "grp1-run2"

        job4 = mgr.dequeue_next()
        assert job4.run_id == "grp2-run2"

    def test_complete_job_success(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001")
        job = mgr.dequeue_next()

        mgr.complete_job(job.job_id, success=True)

        state = mgr.load_queue()
        assert state.jobs[0].state == "succeeded"
        assert state.jobs[0].finished_at is not None

    def test_complete_job_failure(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001")
        job = mgr.dequeue_next()

        mgr.complete_job(job.job_id, success=False, error="Something went wrong")

        state = mgr.load_queue()
        assert state.jobs[0].state == "failed"
        assert state.jobs[0].error == "Something went wrong"

    def test_cancel_job(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001")

        result = mgr.cancel_job(job.job_id)
        assert result is True

        state = mgr.load_queue()
        assert state.jobs[0].state == "canceled"

    def test_cancel_running_job_fails(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001")
        mgr.dequeue_next()  # Now running

        result = mgr.cancel_job(job.job_id)
        assert result is False

    def test_cancel_group(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-001", group_id="grp-001")
        mgr.enqueue("run-002", group_id="grp-001")
        mgr.enqueue("run-003", group_id="grp-002")

        count = mgr.cancel_group("grp-001")
        assert count == 2

        state = mgr.load_queue()
        grp1_jobs = [j for j in state.jobs if j.group_id == "grp-001"]
        assert all(j.state == "canceled" for j in grp1_jobs)

        grp2_jobs = [j for j in state.jobs if j.group_id == "grp-002"]
        assert all(j.state == "queued" for j in grp2_jobs)

    def test_retry_failed(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        job = mgr.enqueue("run-001", group_id="grp-001")
        mgr.dequeue_next()
        mgr.complete_job(job.job_id, success=False, error="test error")

        new_jobs = mgr.retry_failed("grp-001")
        assert len(new_jobs) == 1
        assert new_jobs[0].run_id == "run-001"
        assert new_jobs[0].attempt == 2
        assert new_jobs[0].state == "queued"

    def test_get_running_count(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.enqueue("run-001")
        mgr.enqueue("run-002")

        assert mgr.get_running_count() == 0

        mgr.dequeue_next()
        assert mgr.get_running_count() == 1

        mgr.dequeue_next()
        assert mgr.get_running_count() == 2

    def test_set_max_parallel(self, workspace: Path) -> None:
        mgr = QueueManager(workspace)
        mgr.set_max_parallel(8)

        state = mgr.load_queue()
        assert state.max_parallel == 8


class TestDaemonState:
    """Tests for DaemonState."""

    def test_to_dict(self) -> None:
        state = DaemonState(
            pid=12345,
            started_at="2026-02-01T12:00:00",
            last_heartbeat="2026-02-01T12:05:00",
            max_parallel=4,
            active_jobs=2,
            state="running",
        )
        d = state.to_dict()
        assert d["pid"] == 12345
        assert d["max_parallel"] == 4
        assert d["state"] == "running"

    def test_from_dict(self) -> None:
        d = {
            "version": 1,
            "pid": 54321,
            "started_at": "2026-02-01T10:00:00",
            "last_heartbeat": "2026-02-01T10:30:00",
            "max_parallel": 2,
            "active_jobs": 1,
            "state": "stopping",
        }
        state = DaemonState.from_dict(d)
        assert state.pid == 54321
        assert state.state == "stopping"


class TestGroupPauseManager:
    """Tests for GroupPauseManager."""

    @pytest.fixture
    def workspace(self) -> Path:
        with tempfile.TemporaryDirectory() as tmp:
            ws = Path(tmp)
            # Create a group directory with group.json
            grp_dir = ws / ".runforge" / "groups" / "grp-001"
            grp_dir.mkdir(parents=True)
            (grp_dir / "group.json").write_text(
                json.dumps({"name": "Test Group", "paused": False})
            )
            yield ws

    def test_is_paused_returns_false_by_default(self, workspace: Path) -> None:
        mgr = GroupPauseManager(workspace)
        assert mgr.is_paused("grp-001") is False

    def test_set_paused_true(self, workspace: Path) -> None:
        mgr = GroupPauseManager(workspace)
        result = mgr.set_paused("grp-001", True)
        assert result is True
        assert mgr.is_paused("grp-001") is True

    def test_set_paused_false(self, workspace: Path) -> None:
        mgr = GroupPauseManager(workspace)
        mgr.set_paused("grp-001", True)
        mgr.set_paused("grp-001", False)
        assert mgr.is_paused("grp-001") is False

    def test_set_paused_nonexistent_group(self, workspace: Path) -> None:
        mgr = GroupPauseManager(workspace)
        result = mgr.set_paused("grp-nonexistent", True)
        assert result is False

    def test_get_paused_groups(self, workspace: Path) -> None:
        # Create another group
        grp2_dir = workspace / ".runforge" / "groups" / "grp-002"
        grp2_dir.mkdir(parents=True)
        (grp2_dir / "group.json").write_text(
            json.dumps({"name": "Group 2", "paused": True})
        )

        mgr = GroupPauseManager(workspace)
        paused = mgr.get_paused_groups()

        assert "grp-002" in paused
        assert "grp-001" not in paused
