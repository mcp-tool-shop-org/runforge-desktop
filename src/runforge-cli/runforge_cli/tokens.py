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


# Group/sweep tokens
def group_start(group_id: str, total_runs: int) -> str:
    """Emit group start token."""
    return f"[RF:GROUP=START {group_id} runs={total_runs}]"


def group_run(run_id: str, index: int, total: int) -> str:
    """Emit group run start token."""
    return f"[RF:GROUP=RUN {run_id} {index}/{total}]"


def group_run_complete(run_id: str, status: str) -> str:
    """Emit group run complete token."""
    return f"[RF:GROUP=RUN_DONE {run_id} status={status}]"


def group_complete(group_id: str, succeeded: int, failed: int, canceled: int) -> str:
    """Emit group complete token."""
    return f"[RF:GROUP=COMPLETE {group_id} succeeded={succeeded} failed={failed} canceled={canceled}]"


def group_canceled(group_id: str) -> str:
    """Emit group canceled token."""
    return f"[RF:GROUP=CANCELED {group_id}]"
