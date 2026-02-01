"""Logging utilities for runforge-cli.

Writes to both stdout and logs.txt in the run directory.
All output is line-buffered for real-time streaming.
"""

import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import TextIO


class RunLogger:
    """Logger that writes to stdout and logs.txt simultaneously."""

    def __init__(self, run_dir: Path):
        self.run_dir = run_dir
        self.log_path = run_dir / "logs.txt"
        self._file: TextIO | None = None

    def open(self) -> None:
        """Open the log file for writing."""
        self._file = open(self.log_path, "w", encoding="utf-8", buffering=1)

    def close(self) -> None:
        """Close the log file."""
        if self._file:
            self._file.close()
            self._file = None

    def log(self, message: str) -> None:
        """Write a line to both stdout and logs.txt."""
        timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
        line = f"[{timestamp}] {message}"

        # Write to stdout (line-buffered)
        print(line, flush=True)

        # Write to log file
        if self._file:
            self._file.write(line + "\n")
            self._file.flush()

    def raw(self, line: str) -> None:
        """Write a raw line (no timestamp) to both outputs."""
        print(line, flush=True)
        if self._file:
            self._file.write(line + "\n")
            self._file.flush()

    def error(self, message: str) -> None:
        """Write an error message."""
        timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
        line = f"[{timestamp}] ERROR: {message}"

        print(line, file=sys.stderr, flush=True)
        if self._file:
            self._file.write(line + "\n")
            self._file.flush()

    def __enter__(self) -> "RunLogger":
        self.open()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()
