"""Tests for RF token generation."""

from runforge_cli.tokens import (
    STAGE_COMPLETED,
    STAGE_FAILED,
    STAGE_LOADING_DATASET,
    STAGE_STARTING,
    STAGE_TRAINING,
    epoch,
    progress,
    stage,
)


def test_stage_token_format():
    """Stage tokens should have correct format."""
    assert stage("STARTING") == "[RF:STAGE=STARTING]"
    assert stage("TRAINING") == "[RF:STAGE=TRAINING]"
    assert stage("COMPLETED") == "[RF:STAGE=COMPLETED]"


def test_stage_constants():
    """Stage constants should match expected values."""
    assert STAGE_STARTING == "[RF:STAGE=STARTING]"
    assert STAGE_LOADING_DATASET == "[RF:STAGE=LOADING_DATASET]"
    assert STAGE_TRAINING == "[RF:STAGE=TRAINING]"
    assert STAGE_COMPLETED == "[RF:STAGE=COMPLETED]"
    assert STAGE_FAILED == "[RF:STAGE=FAILED]"


def test_epoch_token_format():
    """Epoch tokens should have correct format."""
    assert epoch(1, 10) == "[RF:EPOCH=1/10]"
    assert epoch(5, 5) == "[RF:EPOCH=5/5]"
    assert epoch(100, 1000) == "[RF:EPOCH=100/1000]"


def test_progress_token_format():
    """Progress tokens should have correct format."""
    assert progress(50, 100) == "[RF:PROGRESS=50/100 samples]"
    assert progress(10, 50, "batches") == "[RF:PROGRESS=10/50 batches]"
