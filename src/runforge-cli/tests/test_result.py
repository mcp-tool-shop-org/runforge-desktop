"""Tests for result.json generation."""

import json
import tempfile
from pathlib import Path

from runforge_cli.result import ArtifactInfo, RunResult, timestamp_now


def test_success_result_to_dict():
    """Success result should have correct structure."""
    result = RunResult(
        status="succeeded",
        started_at="2026-02-01T12:00:00Z",
        finished_at="2026-02-01T12:00:05Z",
        duration_ms=5000,
        primary_metric_name="accuracy",
        primary_metric_value=0.95,
        metrics={"accuracy": 0.95, "f1_score": 0.94},
        artifacts=[
            ArtifactInfo(path="artifacts/model.pkl", type="model", bytes=12345)
        ],
    )

    data = result.to_dict()

    assert data["version"] == 1
    assert data["status"] == "succeeded"
    assert data["started_at"] == "2026-02-01T12:00:00Z"
    assert data["finished_at"] == "2026-02-01T12:00:05Z"
    assert data["duration_ms"] == 5000
    assert data["summary"]["primary_metric"]["name"] == "accuracy"
    assert data["summary"]["primary_metric"]["value"] == 0.95
    assert data["summary"]["metrics"]["accuracy"] == 0.95
    assert data["summary"]["metrics"]["f1_score"] == 0.94
    assert len(data["artifacts"]) == 1
    assert data["artifacts"][0]["path"] == "artifacts/model.pkl"
    assert "error" not in data


def test_failed_result_to_dict():
    """Failed result should include error info."""
    result = RunResult(
        status="failed",
        started_at="2026-02-01T12:00:00Z",
        finished_at="2026-02-01T12:00:01Z",
        duration_ms=1000,
        error_message="Dataset not found",
        error_type="FileNotFoundError",
    )

    data = result.to_dict()

    assert data["status"] == "failed"
    assert data["error"]["message"] == "Dataset not found"
    assert data["error"]["type"] == "FileNotFoundError"


def test_save_result_atomic():
    """Result should be saved atomically."""
    with tempfile.TemporaryDirectory() as tmpdir:
        run_dir = Path(tmpdir)

        result = RunResult(
            status="succeeded",
            started_at="2026-02-01T12:00:00Z",
            finished_at="2026-02-01T12:00:05Z",
            duration_ms=5000,
            metrics={"accuracy": 0.95},
        )

        result.save(run_dir)

        result_path = run_dir / "result.json"
        assert result_path.exists()

        with open(result_path, "r", encoding="utf-8") as f:
            data = json.load(f)

        assert data["status"] == "succeeded"
        assert data["summary"]["metrics"]["accuracy"] == 0.95


def test_timestamp_now_format():
    """Timestamp should be ISO-8601 format."""
    ts = timestamp_now()

    # Should match pattern: YYYY-MM-DDTHH:MM:SSZ
    assert len(ts) == 20
    assert ts[4] == "-"
    assert ts[7] == "-"
    assert ts[10] == "T"
    assert ts[13] == ":"
    assert ts[16] == ":"
    assert ts.endswith("Z")
