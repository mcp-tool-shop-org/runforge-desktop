# RunForge v0.4.0 Release Notes

**Release Date**: 2026-02-01
**Theme**: GPU Support

## Summary

v0.4.0 adds GPU scheduling and device management to RunForge. GPU is treated as a constrained resource, with explicit fallback and clear reporting when GPU is unavailable.

## Key Features

### GPU Detection

- Automatic GPU detection at daemon startup via PyTorch or nvidia-smi
- Reports device count, memory, and compute capability
- Cached detection (runs once per daemon lifecycle)
- Graceful fallback if GPU detection fails

### GPU Scheduling

- New `--gpu-slots` flag for daemon (default: 1)
- GPU jobs require a slot to run
- CPU jobs unaffected by GPU slot availability
- No starvation: CPU jobs progress while GPU jobs wait

### Device Fallback

- Explicit fallback from GPU to CPU with reason
- `gpu_reason` field in `result.json` effective_config
- Supported reasons:
  - `no_gpu_detected` - No GPU available
  - `gpu_slot_unavailable` - All GPU slots in use
  - `user_requested_cpu` - Explicit CPU request

### RF Tokens

New device token:
```
[RF:DEVICE=GPU]
[RF:DEVICE=CPU gpu_reason=no_gpu_detected]
```

Updated daemon token:
```
[RF:DAEMON=STARTED pid=N max_parallel=N gpu_slots=N]
```

## CLI Changes

### Daemon

```bash
# Start with GPU slots
python -m runforge_cli daemon --workspace <path> --max-parallel 4 --gpu-slots 1
```

### Queue Status

Now shows GPU-specific counts:
- Running GPU jobs
- Queued GPU jobs
- Available GPU slots

## Schema Changes

### queue.json

Added fields:
- `gpu_slots` (int): Maximum concurrent GPU jobs
- `jobs[].requires_gpu` (bool): Whether job needs GPU slot

### daemon.json

Added fields:
- `gpu_slots` (int): Configured GPU slot count
- `active_gpu_jobs` (int): Currently running GPU jobs

### result.json

Added field to `effective_config.device`:
- `gpu_reason` (string?): Reason if GPU requested but CPU used

## Desktop Changes

### Models

- `ExecutionQueue.GpuSlots` - GPU slot configuration
- `ExecutionQueue.QueuedGpuJobs` - Queued GPU job filter
- `ExecutionQueue.RunningGpuJobs` - Running GPU job filter
- `QueueJob.RequiresGpu` - GPU requirement flag
- `DaemonStatus.GpuSlots` - Daemon GPU slot config
- `DaemonStatus.ActiveGpuJobs` - Active GPU job count
- `DaemonStatus.AvailableGpuSlots` - Available slots
- `EffectiveDeviceConfig.GpuReason` - Fallback reason
- `EffectiveDeviceConfig.IsGpu` - GPU usage check
- `EffectiveDeviceConfig.IsFallback` - Fallback detection

## Gauntlets

New GPU gauntlets added:

| Gauntlet | Focus |
|----------|-------|
| G8 | GPU fallback - CPU execution with reason |
| G9 | GPU exclusivity - gpu_slots respected |
| G10 | Mixed workload - CPU/GPU interleaving |

## Breaking Changes

None. All new fields have defaults for backward compatibility.

## Migration

No migration required. Existing workspaces work without changes.

## Known Limitations

1. GPU slots are per-daemon, not per-device (multi-GPU support planned)
2. GPU detection uses PyTorch first; requires PyTorch with CUDA for best results
3. Compute capability check not enforced (may fail at training time on incompatible GPUs)

## Files Changed

### CLI (runforge-cli)

- `gpu.py` (new) - GPU detection and device selection
- `queue.py` - Added `requires_gpu`, `gpu_slots` fields
- `daemon.py` - GPU slot tracking and scheduling
- `runner.py` - Device selection and fallback
- `enqueue.py` - Pass `requires_gpu` from request
- `cli.py` - Added `--gpu-slots` argument
- `tokens.py` - Added `device_selected` token
- `result.py` - Added `gpu_reason` to EffectiveConfig

### Desktop (RunForgeDesktop.Core)

- `Models/ExecutionQueue.cs` - GPU fields in queue/job/daemon models
- `Models/RunResult.cs` - GPU reason in effective device config

### Documentation

- `docs/QUEUE_V1_CONTRACT.md` - Updated with GPU schema
- `docs/GAUNTLETS.md` - Added G8-G10 GPU gauntlets

## Test Summary

- Python: All imports pass, GPU module tested
- C#: 339 tests passing
- Gauntlets: G1-G7 validated, G8-G10 ready

## Next Steps

- v0.5.0: Multi-GPU device selection
- v0.5.0: Per-device GPU slots
- v0.6.0: GPU memory monitoring
