"""Tests for request.json parsing and validation."""

import json
import tempfile
from pathlib import Path

import pytest

from runforge_cli.request import RunRequest


def test_parse_minimal_request():
    """Parse a minimal valid request."""
    data = {
        "version": 1,
        "preset": "balanced",
        "dataset": {"path": "data/iris.csv", "label_column": "species"},
        "model": {"family": "logistic_regression"},
        "device": {"type": "cpu"},
        "created_at": "2026-02-01T12:00:00Z",
        "created_by": "test@1.0.0",
    }

    request = RunRequest.from_dict(data)

    assert request.version == 1
    assert request.preset == "balanced"
    assert request.dataset.path == "data/iris.csv"
    assert request.dataset.label_column == "species"
    assert request.model.family == "logistic_regression"
    assert request.device.type == "cpu"
    assert request.is_valid


def test_parse_full_request():
    """Parse a request with all fields."""
    data = {
        "$schema": "https://runforge.dev/schemas/request.v1.json",
        "version": 1,
        "preset": "thorough",
        "dataset": {"path": "data/train.csv", "label_column": "target"},
        "model": {
            "family": "random_forest",
            "hyperparameters": {"n_estimators": 200, "max_depth": 15},
        },
        "device": {"type": "cpu", "gpu_reason": "GPU not available"},
        "created_at": "2026-02-01T12:00:00Z",
        "created_by": "runforge-desktop@0.3.0",
        "rerun_from": "20260101-000000-original-a1b2",
        "name": "My Training Run",
        "tags": ["experiment", "v2"],
        "notes": "Testing new hyperparameters",
    }

    request = RunRequest.from_dict(data)

    assert request.version == 1
    assert request.preset == "thorough"
    assert request.model.hyperparameters == {"n_estimators": 200, "max_depth": 15}
    assert request.device.gpu_reason == "GPU not available"
    assert request.rerun_from == "20260101-000000-original-a1b2"
    assert request.name == "My Training Run"
    assert request.tags == ["experiment", "v2"]
    assert request.notes == "Testing new hyperparameters"
    assert request.is_valid


def test_preserve_unknown_fields():
    """Unknown fields should be preserved in extension_data."""
    data = {
        "version": 1,
        "preset": "balanced",
        "dataset": {
            "path": "data/iris.csv",
            "label_column": "species",
            "future_field": "preserved",
        },
        "model": {"family": "logistic_regression", "future_option": True},
        "device": {"type": "cpu"},
        "created_at": "2026-02-01T12:00:00Z",
        "created_by": "test@1.0.0",
        "new_root_field": {"nested": "value"},
    }

    request = RunRequest.from_dict(data)

    assert request.dataset.extension_data == {"future_field": "preserved"}
    assert request.model.extension_data == {"future_option": True}
    assert "new_root_field" in request.extension_data
    assert request.is_valid


def test_validation_missing_version():
    """Validation should fail without version."""
    data = {
        "preset": "balanced",
        "dataset": {"path": "data/iris.csv", "label_column": "species"},
        "model": {"family": "logistic_regression"},
        "device": {"type": "cpu"},
        "created_at": "2026-02-01T12:00:00Z",
        "created_by": "test@1.0.0",
    }

    request = RunRequest.from_dict(data)
    errors = request.validate()

    assert "version must be >= 1" in errors
    assert not request.is_valid


def test_validation_missing_required_fields():
    """Validation should catch all missing required fields."""
    data = {"version": 1}

    request = RunRequest.from_dict(data)
    errors = request.validate()

    assert "preset is required" in errors
    assert "dataset.path is required" in errors
    assert "dataset.label_column is required" in errors
    assert "model.family is required" in errors
    assert "device.type is required" in errors
    assert "created_at is required" in errors
    assert "created_by is required" in errors
    assert not request.is_valid


def test_load_from_file():
    """Load request from a JSON file."""
    data = {
        "version": 1,
        "preset": "fast",
        "dataset": {"path": "data/test.csv", "label_column": "label"},
        "model": {"family": "linear_svc"},
        "device": {"type": "cpu"},
        "created_at": "2026-02-01T12:00:00Z",
        "created_by": "test@1.0.0",
    }

    with tempfile.NamedTemporaryFile(
        mode="w", suffix=".json", delete=False
    ) as f:
        json.dump(data, f)
        f.flush()

        request = RunRequest.load(Path(f.name))

        assert request.preset == "fast"
        assert request.model.family == "linear_svc"
        assert request.is_valid
