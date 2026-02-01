"""Request.json parsing and validation.

Implements v1 schema from docs/V1_CONTRACT.md.
Forward-compatible: unknown fields are preserved.
"""

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class DatasetConfig:
    """Dataset configuration."""

    path: str
    label_column: str
    extension_data: dict[str, Any] = field(default_factory=dict)


@dataclass
class ModelConfig:
    """Model configuration."""

    family: str
    hyperparameters: dict[str, Any] = field(default_factory=dict)
    extension_data: dict[str, Any] = field(default_factory=dict)


@dataclass
class DeviceConfig:
    """Device configuration."""

    type: str  # "cpu" or "gpu"
    gpu_reason: str | None = None
    extension_data: dict[str, Any] = field(default_factory=dict)


@dataclass
class RunRequest:
    """Parsed request.json content."""

    version: int
    preset: str
    dataset: DatasetConfig
    model: ModelConfig
    device: DeviceConfig
    created_at: str
    created_by: str
    schema_url: str | None = None
    rerun_from: str | None = None
    name: str | None = None
    tags: list[str] = field(default_factory=list)
    notes: str | None = None
    extension_data: dict[str, Any] = field(default_factory=dict)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "RunRequest":
        """Parse from dictionary (JSON-loaded)."""
        # Extract known fields
        dataset_data = data.get("dataset", {})
        model_data = data.get("model", {})
        device_data = data.get("device", {})

        # Extract extension data (unknown fields)
        known_dataset_keys = {"path", "label_column"}
        known_model_keys = {"family", "hyperparameters"}
        known_device_keys = {"type", "gpu_reason"}
        known_root_keys = {
            "$schema",
            "version",
            "preset",
            "dataset",
            "model",
            "device",
            "created_at",
            "created_by",
            "rerun_from",
            "name",
            "tags",
            "notes",
        }

        dataset_ext = {k: v for k, v in dataset_data.items() if k not in known_dataset_keys}
        model_ext = {k: v for k, v in model_data.items() if k not in known_model_keys}
        device_ext = {k: v for k, v in device_data.items() if k not in known_device_keys}
        root_ext = {k: v for k, v in data.items() if k not in known_root_keys}

        return cls(
            version=data.get("version", 0),
            preset=data.get("preset", ""),
            dataset=DatasetConfig(
                path=dataset_data.get("path", ""),
                label_column=dataset_data.get("label_column", ""),
                extension_data=dataset_ext,
            ),
            model=ModelConfig(
                family=model_data.get("family", ""),
                hyperparameters=model_data.get("hyperparameters", {}),
                extension_data=model_ext,
            ),
            device=DeviceConfig(
                type=device_data.get("type", ""),
                gpu_reason=device_data.get("gpu_reason"),
                extension_data=device_ext,
            ),
            created_at=data.get("created_at", ""),
            created_by=data.get("created_by", ""),
            schema_url=data.get("$schema"),
            rerun_from=data.get("rerun_from"),
            name=data.get("name"),
            tags=data.get("tags", []),
            notes=data.get("notes"),
            extension_data=root_ext,
        )

    @classmethod
    def load(cls, path: Path) -> "RunRequest":
        """Load from request.json file."""
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        return cls.from_dict(data)

    def validate(self) -> list[str]:
        """Validate the request. Returns list of errors (empty if valid)."""
        errors = []

        if self.version < 1:
            errors.append("version must be >= 1")

        if not self.preset:
            errors.append("preset is required")

        if not self.dataset.path:
            errors.append("dataset.path is required")

        if not self.dataset.label_column:
            errors.append("dataset.label_column is required")

        if not self.model.family:
            errors.append("model.family is required")

        if not self.device.type:
            errors.append("device.type is required")

        if not self.created_at:
            errors.append("created_at is required")

        if not self.created_by:
            errors.append("created_by is required")

        return errors

    @property
    def is_valid(self) -> bool:
        """Returns True if request passes validation."""
        return len(self.validate()) == 0
