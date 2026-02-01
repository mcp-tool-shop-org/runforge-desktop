"""RF tokens for stage and progress reporting.

These tokens are written to logs.txt and parsed by Desktop for timeline display.
Format is fixed - do not change without updating Desktop parser.
"""


def stage(name: str) -> str:
    """Emit a stage token.

    Valid stages:
        STARTING, LOADING_DATASET, TRAINING, EVALUATING,
        WRITING_ARTIFACTS, COMPLETED, FAILED
    """
    return f"[RF:STAGE={name}]"


def epoch(current: int, total: int) -> str:
    """Emit an epoch progress token."""
    return f"[RF:EPOCH={current}/{total}]"


def progress(current: int, total: int, unit: str = "samples") -> str:
    """Emit a progress token (generic)."""
    return f"[RF:PROGRESS={current}/{total} {unit}]"


# Stage constants for convenience
STAGE_STARTING = stage("STARTING")
STAGE_LOADING_DATASET = stage("LOADING_DATASET")
STAGE_TRAINING = stage("TRAINING")
STAGE_EVALUATING = stage("EVALUATING")
STAGE_WRITING_ARTIFACTS = stage("WRITING_ARTIFACTS")
STAGE_COMPLETED = stage("COMPLETED")
STAGE_FAILED = stage("FAILED")
