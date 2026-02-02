"""Exit codes for runforge-cli.

These codes are part of the CLI contract and must remain stable.
Desktop uses these to determine run status and show appropriate messages.
"""

# Success
SUCCESS = 0

# Training/runtime error (model failed, exception during training)
FAILED = 1

# Invalid request.json (schema/validation error)
INVALID_REQUEST = 2

# Missing files (request.json missing, dataset not found, etc.)
MISSING_FILES = 3

# Internal/tooling error (unexpected CLI bug)
INTERNAL_ERROR = 4

# Sweep/group canceled by user
CANCELED = 5

# Invalid sweep plan
INVALID_PLAN = 6
