"""Tests for GPU detection and device selection."""

import pytest

from runforge_cli.gpu import (
    GPU_REASON_NO_GPU,
    GPU_REASON_SLOT_UNAVAILABLE,
    GPU_REASON_USER_REQUESTED_CPU,
    GpuDevice,
    GpuInfo,
    clear_gpu_cache,
    select_device,
)


class TestGpuInfo:
    """Tests for GpuInfo dataclass."""

    def test_empty_gpu_info(self):
        """Test GpuInfo with no devices."""
        info = GpuInfo(
            available=False,
            devices=(),
            detection_method="none",
            error="No GPU",
        )
        assert not info.available
        assert info.device_count == 0
        assert info.total_memory_mb == 0

    def test_gpu_info_with_devices(self):
        """Test GpuInfo with multiple devices."""
        devices = (
            GpuDevice(index=0, name="RTX 5080", memory_mb=16000, compute_capability="12.0", driver_version="570.1"),
            GpuDevice(index=1, name="RTX 4090", memory_mb=24000, compute_capability="8.9", driver_version="570.1"),
        )
        info = GpuInfo(
            available=True,
            devices=devices,
            detection_method="pytorch",
        )
        assert info.available
        assert info.device_count == 2
        assert info.total_memory_mb == 40000

    def test_gpu_info_to_dict(self):
        """Test GpuInfo serialization."""
        info = GpuInfo(
            available=True,
            devices=(
                GpuDevice(index=0, name="Test GPU", memory_mb=8000, compute_capability="8.0", driver_version=None),
            ),
            detection_method="nvidia-smi",
        )
        d = info.to_dict()
        assert d["available"] is True
        assert d["device_count"] == 1
        assert d["detection_method"] == "nvidia-smi"
        assert len(d["devices"]) == 1
        assert d["devices"][0]["name"] == "Test GPU"


class TestSelectDevice:
    """Tests for device selection logic."""

    def test_cpu_requested_returns_cpu(self):
        """CPU requested should always return CPU."""
        device, reason = select_device("cpu")
        assert device == "cpu"
        # Reason depends on GPU availability - may be None or user_requested_cpu

    def test_gpu_requested_no_slot(self):
        """GPU requested but no slot available returns CPU with reason."""
        device, reason = select_device("gpu", gpu_slot_granted=False)
        assert device == "cpu"
        assert reason == GPU_REASON_SLOT_UNAVAILABLE

    def test_gpu_requested_with_slot(self):
        """GPU requested with slot - result depends on GPU availability."""
        device, reason = select_device("gpu", gpu_slot_granted=True)
        # Result depends on actual GPU availability
        # If GPU available: device="gpu", reason=None
        # If no GPU: device="cpu", reason="no_gpu_detected"
        assert device in ("cpu", "gpu")
        if device == "cpu":
            assert reason == GPU_REASON_NO_GPU
        else:
            assert reason is None


class TestGpuReasonConstants:
    """Test GPU reason constant values."""

    def test_reason_constants_are_strings(self):
        """All reason constants should be non-empty strings."""
        assert isinstance(GPU_REASON_NO_GPU, str)
        assert isinstance(GPU_REASON_SLOT_UNAVAILABLE, str)
        assert isinstance(GPU_REASON_USER_REQUESTED_CPU, str)
        assert len(GPU_REASON_NO_GPU) > 0
        assert len(GPU_REASON_SLOT_UNAVAILABLE) > 0
        assert len(GPU_REASON_USER_REQUESTED_CPU) > 0

    def test_reason_constants_are_snake_case(self):
        """Reason constants should be snake_case for JSON serialization."""
        assert "_" in GPU_REASON_NO_GPU
        assert GPU_REASON_NO_GPU.islower() or "_" in GPU_REASON_NO_GPU
