"""Dry-run mode for runforge-cli.

Validates the request and writes result.json with effective_config,
without actually running training. Useful for integration tests.
"""

import time
from pathlib import Path

import pandas as pd

from .logger import RunLogger
from .request import RunRequest
from .result import EffectiveConfig, RunResult, timestamp_now
from .runner import get_default_hyperparameters, MODEL_FAMILIES
from .tokens import (
    STAGE_LOADING_DATASET,
    STAGE_EVALUATING,
    STAGE_WRITING_ARTIFACTS,
)


def run_dry(
    request: RunRequest,
    run_dir: Path,
    workspace_path: Path,
    logger: RunLogger,
    max_rows: int | None = None,
) -> RunResult:
    """Execute a dry run - validate without training.

    Args:
        request: Parsed request.json
        run_dir: Path to the run directory
        workspace_path: Path to the workspace root
        logger: Logger for output
        max_rows: Optional row limit for dataset loading

    Returns:
        RunResult with status, effective_config (no actual metrics/artifacts)
    """
    start_time = time.time()
    started_at = timestamp_now()

    result = RunResult(started_at=started_at)

    try:
        # Validate model family
        family = request.model.family
        if family not in MODEL_FAMILIES:
            raise ValueError(
                f"Unsupported model family: {family}. "
                f"Supported: {', '.join(MODEL_FAMILIES.keys())}"
            )

        # Validate dataset exists
        logger.raw(STAGE_LOADING_DATASET)
        logger.log(f"[DRY-RUN] Checking dataset: {request.dataset.path}")

        dataset_path = workspace_path / request.dataset.path
        if not dataset_path.exists():
            raise FileNotFoundError(f"Dataset not found: {dataset_path}")

        # Load dataset header (or limited rows)
        nrows = max_rows if max_rows else 5  # Just check header + few rows
        df = pd.read_csv(dataset_path, nrows=nrows)
        logger.log(f"[DRY-RUN] Dataset preview: {len(df)} rows, {len(df.columns)} columns")

        # Validate label column exists
        label_col = request.dataset.label_column
        if label_col not in df.columns:
            raise ValueError(
                f"Label column '{label_col}' not found. "
                f"Available: {', '.join(df.columns)}"
            )

        logger.log(f"[DRY-RUN] Label column '{label_col}' found")

        # Merge hyperparameters (same logic as real runner)
        logger.raw(STAGE_EVALUATING)
        logger.log("[DRY-RUN] Validating configuration")

        hyperparams = get_default_hyperparameters(family)
        if request.model.hyperparameters:
            hyperparams.update(request.model.hyperparameters)

        # Capture effective configuration
        result.effective_config = EffectiveConfig(
            model_family=family,
            model_hyperparameters=hyperparams,
            device_type=request.device.type,
            preset=request.preset,
            dataset_path=request.dataset.path,
            label_column=request.dataset.label_column,
        )
        logger.log(f"[DRY-RUN] Effective config: family={family}, device={request.device.type}")

        # Write artifacts stage (no actual artifacts in dry-run)
        logger.raw(STAGE_WRITING_ARTIFACTS)
        logger.log("[DRY-RUN] Dry run: validated request; effective_config written")

        # Set placeholder metrics for dry-run
        result.metrics = {
            "dry_run": 1.0,
        }
        result.primary_metric_name = "dry_run"
        result.primary_metric_value = 1.0

        result.status = "succeeded"

    except Exception as e:
        result.status = "failed"
        result.error_message = str(e)
        result.error_type = type(e).__name__
        logger.error(f"[DRY-RUN] {e}")

    # Finalize timing
    end_time = time.time()
    result.finished_at = timestamp_now()
    result.duration_ms = int((end_time - start_time) * 1000)

    return result
