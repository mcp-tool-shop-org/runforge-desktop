"""Tests for exit codes contract."""

from runforge_cli.exit_codes import (
    FAILED,
    INTERNAL_ERROR,
    INVALID_REQUEST,
    MISSING_FILES,
    SUCCESS,
)


def test_exit_codes_are_distinct():
    """All exit codes should be unique."""
    codes = [SUCCESS, FAILED, INVALID_REQUEST, MISSING_FILES, INTERNAL_ERROR]
    assert len(codes) == len(set(codes))


def test_exit_code_values():
    """Exit codes should match contract."""
    assert SUCCESS == 0
    assert FAILED == 1
    assert INVALID_REQUEST == 2
    assert MISSING_FILES == 3
    assert INTERNAL_ERROR == 4
