"""Result.json writing.

Implements the result schema for CLI output.
Written atomically at the end of training.
"""

import json
import os
import tempfile
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


@dataclass
class EffectiveConfig:
    """Effective configuration used for training (post-merge with defaults)."""

    model_family: str = ""
    model_hyperparameters: dict[str, Any] = field(default_factory=dict)
    device_type: str = "cpu"
    gpu_reason: str | None = None  # Reason if GPU was requested but CPU used
    preset: str = "balanced"
    dataset_path: str = ""
    label_column: str = ""

    def to_dict(self) -> dict[str, Any]:
        device_dict: dict[str, Any] = {"type": self.device_type}
        if self.gpu_reason:
            device_dict["gpu_reason"] = self.gpu_reason
        return {
            "model": {
                "family": self.model_family,
                "hyperparameters": self.model_hyperparameters,
            },
            "device": device_dict,
            "preset": self.preset,
            "dataset": {
                "path": self.dataset_path,
                "label_column": self.label_column,
            },
        }


@dataclass
class ArtifactInfo:
    """Information about a generated artifact."""

    path: str  # Relative to run dir
    type: str  # "model", "metrics", etc.
    bytes: int

    def to_dict(self) -> dict[str, Any]:
        return {"path": self.path, "type": self.type, "bytes": self.bytes}


@dataclass
class RunResult:
    """Result of a training run."""

    version: int = 1
    status: str = "succeeded"  # "succeeded" or "failed"
    started_at: str = ""
    finished_at: str = ""
    duration_ms: int = 0
    primary_metric_name: str | None = None
    primary_metric_value: float | None = None
    metrics: dict[str, float] = field(default_factory=dict)
    artifacts: list[ArtifactInfo] = field(default_factory=list)
    error_message: str | None = None
    error_type: str | None = None
    effective_config: EffectiveConfig | None = None

    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result: dict[str, Any] = {
            "version": self.version,
            "status": self.status,
            "started_at": self.started_at,
            "finished_at": self.finished_at,
            "duration_ms": self.duration_ms,
            "summary": {
                "metrics": self.metrics,
            },
            "artifacts": [a.to_dict() for a in self.artifacts],
        }

        # Add primary metric if present
        if self.primary_metric_name and self.primary_metric_value is not None:
            result["summary"]["primary_metric"] = {
                "name": self.primary_metric_name,
                "value": self.primary_metric_value,
            }

        # Add effective config if present
        if self.effective_config is not None:
            result["effective_config"] = self.effective_config.to_dict()

        # Add error if failed
        if self.status == "failed" and self.error_message:
            result["error"] = {
                "message": self.error_message,
                "type": self.error_type or "TrainingError",
            }

        return result

    def save(self, run_dir: Path) -> None:
        """Save result.json atomically."""
        result_path = run_dir / "result.json"
        data = self.to_dict()

        # Write to temp file first, then rename (atomic on Windows NTFS)
        fd, temp_path = tempfile.mkstemp(
            suffix=".json", prefix="result_", dir=run_dir
        )
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)

            # Atomic rename
            os.replace(temp_path, result_path)
        except Exception:
            # Clean up temp file on error
            try:
                os.unlink(temp_path)
            except OSError:
                pass
            raise


def timestamp_now() -> str:
    """Get current UTC timestamp in ISO-8601 format."""
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
