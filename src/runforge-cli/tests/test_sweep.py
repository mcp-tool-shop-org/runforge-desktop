"""Tests for sweep command functionality."""

import json
import tempfile
from pathlib import Path

import pytest

from runforge_cli.sweep import RunConfig, SweepOrchestrator, SweepPlan


class TestSweepPlan:
    """Tests for SweepPlan parsing and validation."""

    def test_load_valid_plan(self, tmp_path: Path) -> None:
        """Test loading a valid sweep plan."""
        plan_data = {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test@1.0",
            "workspace": str(tmp_path),
            "group": {"name": "Test Sweep", "notes": "Test notes"},
            "base_request": {
                "version": 1,
                "preset": "balanced",
                "dataset": {"path": "data.csv", "label_column": "target"},
                "model": {"family": "random_forest", "hyperparameters": {}},
                "device": {"type": "cpu"},
                "created_at": "2026-02-01T14:00:00Z",
                "created_by": "test@1.0",
            },
            "strategy": {
                "type": "grid",
                "parameters": [
                    {"path": "model.hyperparameters.n_estimators", "values": [50, 100]},
                    {"path": "model.hyperparameters.max_depth", "values": [None, 10]},
                ],
            },
            "execution": {"max_parallel": 2, "fail_fast": False, "stop_on_cancel": True},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)

        assert plan.version == 1
        assert plan.kind == "sweep_plan"
        assert plan.group_name == "Test Sweep"
        assert plan.group_notes == "Test notes"
        assert plan.strategy_type == "grid"
        assert len(plan.parameters) == 2
        assert plan.max_parallel == 2

    def test_validate_invalid_version(self, tmp_path: Path) -> None:
        """Test validation fails for invalid version."""
        plan_data = {
            "version": 2,  # Invalid
            "kind": "sweep_plan",
            "workspace": str(tmp_path),
            "base_request": {"version": 1},
            "strategy": {"type": "grid", "parameters": [{"path": "x", "values": [1]}]},
            "execution": {"max_parallel": 1},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)
        errors = plan.validate()

        assert any("version" in e.lower() for e in errors)

    def test_validate_missing_workspace(self, tmp_path: Path) -> None:
        """Test validation fails for missing workspace."""
        plan_data = {
            "version": 1,
            "kind": "sweep_plan",
            "workspace": "",  # Missing
            "base_request": {"version": 1},
            "strategy": {"type": "grid", "parameters": [{"path": "x", "values": [1]}]},
            "execution": {"max_parallel": 1},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)
        errors = plan.validate()

        assert any("workspace" in e.lower() for e in errors)

    def test_validate_empty_grid(self, tmp_path: Path) -> None:
        """Test validation fails for empty grid parameters."""
        plan_data = {
            "version": 1,
            "kind": "sweep_plan",
            "workspace": str(tmp_path),
            "base_request": {"version": 1},
            "strategy": {"type": "grid", "parameters": []},  # Empty
            "execution": {"max_parallel": 1},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)
        errors = plan.validate()

        assert any("parameter" in e.lower() for e in errors)


class TestSweepOrchestrator:
    """Tests for SweepOrchestrator."""

    def create_plan(self, tmp_path: Path, parameters: list) -> tuple[SweepPlan, Path]:
        """Helper to create a test plan."""
        plan_data = {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test@1.0",
            "workspace": str(tmp_path),
            "group": {"name": "Test Sweep", "notes": None},
            "base_request": {
                "version": 1,
                "preset": "balanced",
                "dataset": {"path": "data.csv", "label_column": "target"},
                "model": {"family": "random_forest", "hyperparameters": {"random_state": 42}},
                "device": {"type": "cpu"},
                "created_at": "2026-02-01T14:00:00Z",
                "created_by": "test@1.0",
            },
            "strategy": {"type": "grid", "parameters": parameters},
            "execution": {"max_parallel": 2, "fail_fast": False, "stop_on_cancel": True},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)
        return plan, plan_path

    def test_expand_grid_single_param(self, tmp_path: Path) -> None:
        """Test grid expansion with single parameter."""
        plan, plan_path = self.create_plan(
            tmp_path,
            [{"path": "model.hyperparameters.n_estimators", "values": [50, 100, 200]}],
        )

        orchestrator = SweepOrchestrator(plan, plan_path)
        runs = orchestrator.expand_grid()

        assert len(runs) == 3
        assert runs[0].overrides == {"model.hyperparameters.n_estimators": 50}
        assert runs[1].overrides == {"model.hyperparameters.n_estimators": 100}
        assert runs[2].overrides == {"model.hyperparameters.n_estimators": 200}

    def test_expand_grid_multiple_params(self, tmp_path: Path) -> None:
        """Test grid expansion with multiple parameters (cartesian product)."""
        plan, plan_path = self.create_plan(
            tmp_path,
            [
                {"path": "model.hyperparameters.n_estimators", "values": [50, 100]},
                {"path": "model.hyperparameters.max_depth", "values": [None, 10]},
            ],
        )

        orchestrator = SweepOrchestrator(plan, plan_path)
        runs = orchestrator.expand_grid()

        # 2 x 2 = 4 runs
        assert len(runs) == 4

        # Check all combinations exist
        overrides_set = {frozenset(r.overrides.items()) for r in runs}
        expected = {
            frozenset({("model.hyperparameters.n_estimators", 50), ("model.hyperparameters.max_depth", None)}),
            frozenset({("model.hyperparameters.n_estimators", 50), ("model.hyperparameters.max_depth", 10)}),
            frozenset({("model.hyperparameters.n_estimators", 100), ("model.hyperparameters.max_depth", None)}),
            frozenset({("model.hyperparameters.n_estimators", 100), ("model.hyperparameters.max_depth", 10)}),
        }
        assert overrides_set == expected

    def test_expand_grid_three_params(self, tmp_path: Path) -> None:
        """Test grid expansion with three parameters."""
        plan, plan_path = self.create_plan(
            tmp_path,
            [
                {"path": "model.family", "values": ["random_forest", "xgboost"]},
                {"path": "model.hyperparameters.n_estimators", "values": [50, 100]},
                {"path": "model.hyperparameters.max_depth", "values": [10, 20, 30]},
            ],
        )

        orchestrator = SweepOrchestrator(plan, plan_path)
        runs = orchestrator.expand_grid()

        # 2 x 2 x 3 = 12 runs
        assert len(runs) == 12

    def test_apply_overrides_nested(self, tmp_path: Path) -> None:
        """Test applying nested overrides."""
        plan, plan_path = self.create_plan(tmp_path, [])

        orchestrator = SweepOrchestrator(plan, plan_path)
        base = {
            "model": {"family": "rf", "hyperparameters": {"n_estimators": 10, "max_depth": 5}}
        }

        result = orchestrator.apply_overrides(
            base, {"model.hyperparameters.n_estimators": 100, "model.hyperparameters.max_depth": 20}
        )

        assert result["model"]["hyperparameters"]["n_estimators"] == 100
        assert result["model"]["hyperparameters"]["max_depth"] == 20
        assert result["model"]["family"] == "rf"  # Unchanged

    def test_apply_overrides_null_removes(self, tmp_path: Path) -> None:
        """Test that null values remove keys."""
        plan, plan_path = self.create_plan(tmp_path, [])

        orchestrator = SweepOrchestrator(plan, plan_path)
        base = {"model": {"hyperparameters": {"n_estimators": 10, "max_depth": 5}}}

        result = orchestrator.apply_overrides(base, {"model.hyperparameters.max_depth": None})

        assert "max_depth" not in result["model"]["hyperparameters"]
        assert result["model"]["hyperparameters"]["n_estimators"] == 10

    def test_apply_overrides_creates_path(self, tmp_path: Path) -> None:
        """Test that missing path segments are created."""
        plan, plan_path = self.create_plan(tmp_path, [])

        orchestrator = SweepOrchestrator(plan, plan_path)
        base = {"model": {}}

        result = orchestrator.apply_overrides(base, {"model.hyperparameters.new_param": 42})

        assert result["model"]["hyperparameters"]["new_param"] == 42

    def test_setup_group_creates_directory(self, tmp_path: Path) -> None:
        """Test that setup_group creates the group directory structure."""
        plan, plan_path = self.create_plan(
            tmp_path,
            [{"path": "model.hyperparameters.n_estimators", "values": [50, 100]}],
        )

        orchestrator = SweepOrchestrator(plan, plan_path)
        runs = orchestrator.expand_grid()
        orchestrator.setup_group(runs)

        # Check directory exists
        assert orchestrator.group_dir.exists()

        # Check plan.json copied
        assert (orchestrator.group_dir / "plan.json").exists()

        # Check group.json created
        group_json = orchestrator.group_dir / "group.json"
        assert group_json.exists()

        # Verify group.json content
        with open(group_json) as f:
            data = json.load(f)

        assert data["version"] == 1
        assert data["kind"] == "run_group"
        assert data["status"] == "running"
        assert len(data["runs"]) == 2
        assert all(r["status"] == "pending" for r in data["runs"])

    def test_run_id_format(self, tmp_path: Path) -> None:
        """Test run IDs have expected format."""
        plan, plan_path = self.create_plan(
            tmp_path,
            [{"path": "model.hyperparameters.n_estimators", "values": [50, 100, 200]}],
        )

        orchestrator = SweepOrchestrator(plan, plan_path)
        runs = orchestrator.expand_grid()

        for run in runs:
            # Format: YYYYMMDD-HHMMSS-sweep-NNNN
            parts = run.run_id.split("-")
            assert len(parts) == 4
            assert parts[2] == "sweep"
            assert len(parts[3]) == 4  # 4-digit index

    def test_group_id_format(self, tmp_path: Path) -> None:
        """Test group ID has expected format."""
        plan, plan_path = self.create_plan(tmp_path, [{"path": "x", "values": [1]}])

        orchestrator = SweepOrchestrator(plan, plan_path)

        # Format: grp_YYYYMMDD_HHMMSS_name
        assert orchestrator.group_id.startswith("grp_")
        parts = orchestrator.group_id.split("_")
        assert len(parts) >= 3


class TestGroupJson:
    """Tests for group.json content and updates."""

    def create_orchestrator(self, tmp_path: Path) -> SweepOrchestrator:
        """Helper to create an orchestrator."""
        plan_data = {
            "version": 1,
            "kind": "sweep_plan",
            "created_at": "2026-02-01T15:00:00Z",
            "created_by": "test@1.0",
            "workspace": str(tmp_path),
            "group": {"name": "Test Sweep", "notes": "Test notes"},
            "base_request": {
                "version": 1,
                "preset": "balanced",
                "dataset": {"path": "data.csv", "label_column": "target"},
                "model": {"family": "random_forest", "hyperparameters": {}},
                "device": {"type": "cpu"},
                "created_at": "2026-02-01T14:00:00Z",
                "created_by": "test@1.0",
            },
            "strategy": {
                "type": "grid",
                "parameters": [{"path": "model.hyperparameters.n_estimators", "values": [50, 100]}],
            },
            "execution": {"max_parallel": 2, "fail_fast": False, "stop_on_cancel": True},
        }

        plan_path = tmp_path / "sweep_plan.json"
        with open(plan_path, "w") as f:
            json.dump(plan_data, f)

        plan = SweepPlan.load(plan_path)
        return SweepOrchestrator(plan, plan_path)

    def test_group_json_initial_state(self, tmp_path: Path) -> None:
        """Test initial group.json state."""
        orchestrator = self.create_orchestrator(tmp_path)
        runs = orchestrator.expand_grid()
        orchestrator.setup_group(runs)

        with open(orchestrator.group_dir / "group.json") as f:
            data = json.load(f)

        assert data["version"] == 1
        assert data["kind"] == "run_group"
        assert data["name"] == "Test Sweep"
        assert data["notes"] == "Test notes"
        assert data["status"] == "running"
        assert data["execution"]["max_parallel"] == 2
        assert data["execution"]["cancelled"] is False
        assert data["summary"]["total"] == 2
        assert data["summary"]["succeeded"] == 0
        assert data["summary"]["failed"] == 0
        assert data["summary"]["canceled"] == 0

    def test_group_json_includes_plan_ref(self, tmp_path: Path) -> None:
        """Test that group.json includes plan reference."""
        orchestrator = self.create_orchestrator(tmp_path)
        runs = orchestrator.expand_grid()
        orchestrator.setup_group(runs)

        with open(orchestrator.group_dir / "group.json") as f:
            data = json.load(f)

        assert data["plan_ref"] == "plan.json"

    def test_group_json_runs_have_overrides(self, tmp_path: Path) -> None:
        """Test that run entries include their overrides."""
        orchestrator = self.create_orchestrator(tmp_path)
        runs = orchestrator.expand_grid()
        orchestrator.setup_group(runs)

        with open(orchestrator.group_dir / "group.json") as f:
            data = json.load(f)

        # Check each run has its overrides
        overrides_50 = {"model.hyperparameters.n_estimators": 50}
        overrides_100 = {"model.hyperparameters.n_estimators": 100}

        run_overrides = [r["request_overrides"] for r in data["runs"]]
        assert overrides_50 in run_overrides
        assert overrides_100 in run_overrides
