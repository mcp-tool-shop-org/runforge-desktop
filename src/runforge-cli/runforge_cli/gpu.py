"""
GPU detection and capability probing for RunForge.

This module provides:
- GPU detection (CUDA via PyTorch or direct CUDA check)
- Device enumeration with memory and compute capability
- Cached detection (runs once at daemon startup)
- Soft failure if no GPU available

GPU detection happens once and is cached for the process lifetime.
"""

from __future__ import annotations

import os
import subprocess
from dataclasses import dataclass
from functools import lru_cache
from typing import Literal


@dataclass(frozen=True)
class GpuDevice:
    """Information about a single GPU device."""

    index: int
    name: str
    memory_mb: int
    compute_capability: str | None  # e.g., "8.6" for Ampere
    driver_version: str | None


@dataclass(frozen=True)
class GpuInfo:
    """Summary of GPU availability and capabilities."""

    available: bool
    devices: tuple[GpuDevice, ...]
    detection_method: Literal["pytorch", "nvidia-smi", "none"]
    error: str | None = None

    @property
    def device_count(self) -> int:
        return len(self.devices)

    @property
    def total_memory_mb(self) -> int:
        return sum(d.memory_mb for d in self.devices)

    def to_dict(self) -> dict:
        """Convert to JSON-serializable dict."""
        return {
            "available": self.available,
            "device_count": self.device_count,
            "total_memory_mb": self.total_memory_mb,
            "detection_method": self.detection_method,
            "devices": [
                {
                    "index": d.index,
                    "name": d.name,
                    "memory_mb": d.memory_mb,
                    "compute_capability": d.compute_capability,
                    "driver_version": d.driver_version,
                }
                for d in self.devices
            ],
            "error": self.error,
        }


# GPU reason constants
GPU_REASON_NO_GPU = "no_gpu_detected"
GPU_REASON_GPU_BUSY = "gpu_busy"
GPU_REASON_UNSUPPORTED_MODEL = "unsupported_model"
GPU_REASON_DRIVER_MISMATCH = "driver_mismatch"
GPU_REASON_SLOT_UNAVAILABLE = "gpu_slot_unavailable"
GPU_REASON_USER_REQUESTED_CPU = "user_requested_cpu"


@lru_cache(maxsize=1)
def detect_gpu() -> GpuInfo:
    """
    Detect available GPUs.

    Detection order:
    1. PyTorch CUDA (if available)
    2. nvidia-smi (fallback)
    3. No GPU

    Results are cached for the process lifetime.
    """
    # Try PyTorch first (most accurate for ML workloads)
    pytorch_info = _detect_via_pytorch()
    if pytorch_info.available:
        return pytorch_info

    # Fallback to nvidia-smi
    smi_info = _detect_via_nvidia_smi()
    if smi_info.available:
        return smi_info

    # No GPU detected
    return GpuInfo(
        available=False,
        devices=(),
        detection_method="none",
        error=pytorch_info.error or smi_info.error or "No CUDA-capable GPU detected",
    )


def _detect_via_pytorch() -> GpuInfo:
    """Detect GPUs using PyTorch."""
    try:
        import torch

        if not torch.cuda.is_available():
            return GpuInfo(
                available=False,
                devices=(),
                detection_method="pytorch",
                error="CUDA not available in PyTorch",
            )

        devices = []
        for i in range(torch.cuda.device_count()):
            props = torch.cuda.get_device_properties(i)
            devices.append(
                GpuDevice(
                    index=i,
                    name=props.name,
                    memory_mb=props.total_memory // (1024 * 1024),
                    compute_capability=f"{props.major}.{props.minor}",
                    driver_version=None,  # PyTorch doesn't expose this directly
                )
            )

        return GpuInfo(
            available=True,
            devices=tuple(devices),
            detection_method="pytorch",
        )

    except ImportError:
        return GpuInfo(
            available=False,
            devices=(),
            detection_method="pytorch",
            error="PyTorch not installed",
        )
    except Exception as e:
        return GpuInfo(
            available=False,
            devices=(),
            detection_method="pytorch",
            error=f"PyTorch GPU detection failed: {e}",
        )


def _detect_via_nvidia_smi() -> GpuInfo:
    """Detect GPUs using nvidia-smi."""
    try:
        # Check if nvidia-smi exists
        result = subprocess.run(
            [
                "nvidia-smi",
                "--query-gpu=index,name,memory.total,driver_version",
                "--format=csv,noheader,nounits",
            ],
            capture_output=True,
            text=True,
            timeout=10,
        )

        if result.returncode != 0:
            return GpuInfo(
                available=False,
                devices=(),
                detection_method="nvidia-smi",
                error=f"nvidia-smi failed: {result.stderr.strip()}",
            )

        devices = []
        for line in result.stdout.strip().split("\n"):
            if not line.strip():
                continue
            parts = [p.strip() for p in line.split(",")]
            if len(parts) >= 4:
                devices.append(
                    GpuDevice(
                        index=int(parts[0]),
                        name=parts[1],
                        memory_mb=int(float(parts[2])),
                        compute_capability=None,  # nvidia-smi doesn't provide this
                        driver_version=parts[3],
                    )
                )

        if not devices:
            return GpuInfo(
                available=False,
                devices=(),
                detection_method="nvidia-smi",
                error="No GPUs found by nvidia-smi",
            )

        return GpuInfo(
            available=True,
            devices=tuple(devices),
            detection_method="nvidia-smi",
        )

    except FileNotFoundError:
        return GpuInfo(
            available=False,
            devices=(),
            detection_method="nvidia-smi",
            error="nvidia-smi not found",
        )
    except subprocess.TimeoutExpired:
        return GpuInfo(
            available=False,
            devices=(),
            detection_method="nvidia-smi",
            error="nvidia-smi timed out",
        )
    except Exception as e:
        return GpuInfo(
            available=False,
            devices=(),
            detection_method="nvidia-smi",
            error=f"nvidia-smi detection failed: {e}",
        )


def clear_gpu_cache() -> None:
    """Clear the cached GPU detection result. Useful for testing."""
    detect_gpu.cache_clear()


def get_gpu_summary() -> str:
    """Get a human-readable GPU summary for logging."""
    info = detect_gpu()
    if not info.available:
        return f"No GPU available ({info.error})"

    lines = [f"GPU available ({info.detection_method}):"]
    for d in info.devices:
        cc = f", CC {d.compute_capability}" if d.compute_capability else ""
        lines.append(f"  [{d.index}] {d.name} ({d.memory_mb} MB{cc})")
    return "\n".join(lines)


def select_device(requested: str, gpu_slot_granted: bool = True) -> tuple[str, str | None]:
    """
    Select the actual device to use based on request and availability.

    Args:
        requested: "cpu" or "gpu"
        gpu_slot_granted: Whether a GPU slot was granted by the scheduler

    Returns:
        (actual_device, gpu_reason) - gpu_reason is None if GPU was used successfully
    """
    if requested == "cpu":
        return ("cpu", GPU_REASON_USER_REQUESTED_CPU if detect_gpu().available else None)

    # GPU requested
    info = detect_gpu()

    if not info.available:
        return ("cpu", GPU_REASON_NO_GPU)

    if not gpu_slot_granted:
        return ("cpu", GPU_REASON_SLOT_UNAVAILABLE)

    # GPU available and slot granted
    return ("gpu", None)
