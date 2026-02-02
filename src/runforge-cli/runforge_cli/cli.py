"""Command-line interface for runforge-cli.

Usage:
    runforge-cli run --run-dir <path>
    runforge-cli run --run-dir <path> --dry-run
    runforge-cli run --run-dir <path> --dry-run --max-rows 200
    runforge-cli sweep --plan <path>

Exit codes:
    0 - Success
    1 - Training failed
    2 - Invalid request.json
    3 - Missing files
    4 - Internal error
    5 - Sweep canceled
    6 - Invalid sweep plan
"""

import argparse
import sys
import traceback
from pathlib import Path

from . import __version__
from .dry_run import run_dry
from .exit_codes import FAILED, INTERNAL_ERROR, INVALID_REQUEST, MISSING_FILES, SUCCESS
from .logger import RunLogger
from .request import RunRequest
from .runner import run_training
from .sweep import sweep_command
from .tokens import STAGE_COMPLETED, STAGE_FAILED, STAGE_STARTING


def run_command(
    run_dir: Path,
    workspace: Path | None = None,
    dry_run: bool = False,
    max_rows: int | None = None,
) -> int:
    """Execute a training run.

    Args:
        run_dir: Path to the run directory containing request.json
        workspace: Optional workspace path. If not provided, inferred from run_dir.
        dry_run: If True, validate only without training.
        max_rows: Optional row limit for dataset loading.

    Returns:
        Exit code (see exit_codes.py)
    """
    # Validate run directory exists
    if not run_dir.exists():
        print(f"ERROR: Run directory not found: {run_dir}", file=sys.stderr)
        return MISSING_FILES

    if not run_dir.is_dir():
        print(f"ERROR: Not a directory: {run_dir}", file=sys.stderr)
        return MISSING_FILES

    # Check for request.json
    request_path = run_dir / "request.json"
    if not request_path.exists():
        print(f"ERROR: request.json not found in {run_dir}", file=sys.stderr)
        return MISSING_FILES

    # Infer workspace from run_dir if not provided
    # Expected structure: <workspace>/.ml/runs/<run-id>/
    if workspace is None:
        # Navigate up to find workspace
        parent = run_dir.parent  # runs/
        if parent.name == "runs":
            parent = parent.parent  # .ml/
            if parent.name == ".ml":
                workspace = parent.parent
            else:
                workspace = run_dir.parent.parent.parent
        else:
            workspace = run_dir.parent.parent.parent

    # Start logging
    with RunLogger(run_dir) as logger:
        try:
            logger.raw(STAGE_STARTING)
            logger.log(f"runforge-cli v{__version__}")
            if dry_run:
                logger.log("[DRY-RUN MODE]")
            logger.log(f"Run directory: {run_dir}")
            logger.log(f"Workspace: {workspace}")

            # Load and validate request
            logger.log("Loading request.json")
            try:
                request = RunRequest.load(request_path)
            except Exception as e:
                logger.error(f"Failed to parse request.json: {e}")
                logger.raw(STAGE_FAILED)
                return INVALID_REQUEST

            # Validate request
            errors = request.validate()
            if errors:
                for error in errors:
                    logger.error(f"Validation error: {error}")
                logger.raw(STAGE_FAILED)
                return INVALID_REQUEST

            logger.log(f"Preset: {request.preset}")
            logger.log(f"Model: {request.model.family}")
            logger.log(f"Dataset: {request.dataset.path}")
            logger.log(f"Device: {request.device.type}")

            if request.name:
                logger.log(f"Run name: {request.name}")
            if request.rerun_from:
                logger.log(f"Rerun from: {request.rerun_from}")

            # Execute training or dry-run
            if dry_run:
                result = run_dry(request, run_dir, workspace, logger, max_rows)
            else:
                result = run_training(request, run_dir, workspace, logger)

            # Save result
            result.save(run_dir)
            logger.log(f"Saved result.json (status: {result.status})")

            # Final status
            if result.status == "succeeded":
                logger.raw(STAGE_COMPLETED)
                logger.log(f"Run completed in {result.duration_ms}ms")
                return SUCCESS
            else:
                logger.raw(STAGE_FAILED)
                logger.log(f"Run failed after {result.duration_ms}ms")
                return FAILED

        except Exception as e:
            logger.error(f"Internal error: {e}")
            logger.error(traceback.format_exc())
            logger.raw(STAGE_FAILED)
            return INTERNAL_ERROR


def main() -> None:
    """Main entry point."""
    parser = argparse.ArgumentParser(
        prog="runforge-cli",
        description="RunForge CLI for executing ML training runs",
    )
    parser.add_argument(
        "--version",
        action="version",
        version=f"runforge-cli {__version__}",
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    # run command
    run_parser = subparsers.add_parser(
        "run",
        help="Execute a training run",
        description="Execute a training run from a run directory containing request.json",
    )
    run_parser.add_argument(
        "--run-dir",
        type=Path,
        required=True,
        help="Path to the run directory containing request.json",
    )
    run_parser.add_argument(
        "--workspace",
        type=Path,
        default=None,
        help="Workspace root path (optional, inferred from run-dir if not provided)",
    )
    run_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate request and write result.json without training",
    )
    run_parser.add_argument(
        "--max-rows",
        type=int,
        default=None,
        help="Maximum rows to load from dataset (useful for testing)",
    )

    # sweep command
    sweep_parser = subparsers.add_parser(
        "sweep",
        help="Execute a sweep from a plan file",
        description="Execute multiple runs from a sweep plan (grid/list strategy)",
    )
    sweep_parser.add_argument(
        "--plan",
        type=Path,
        required=True,
        help="Path to the sweep_plan.json file",
    )

    args = parser.parse_args()

    if args.command == "run":
        exit_code = run_command(
            args.run_dir,
            args.workspace,
            dry_run=args.dry_run,
            max_rows=args.max_rows,
        )
        sys.exit(exit_code)
    elif args.command == "sweep":
        exit_code = sweep_command(args.plan)
        sys.exit(exit_code)


if __name__ == "__main__":
    main()
