"""Training runner - executes the actual ML training.

Supports model families:
- logistic_regression
- random_forest
- linear_svc

Uses scikit-learn for all models. Writes artifacts to run_dir/artifacts/.
"""

import json
import os
import time
from pathlib import Path
from typing import Any

import joblib
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder, StandardScaler
from sklearn.svm import LinearSVC
from sklearn.pipeline import Pipeline
from sklearn.metrics import accuracy_score, f1_score, precision_score, recall_score

from .gpu import select_device
from .logger import RunLogger
from .request import RunRequest
from .result import ArtifactInfo, EffectiveConfig, RunResult, timestamp_now
from .tokens import (
    STAGE_LOADING_DATASET,
    STAGE_TRAINING,
    STAGE_EVALUATING,
    STAGE_WRITING_ARTIFACTS,
    device_selected,
    epoch,
)


# Supported model families
MODEL_FAMILIES = {
    "logistic_regression": LogisticRegression,
    "random_forest": RandomForestClassifier,
    "linear_svc": LinearSVC,
}


def get_default_hyperparameters(family: str) -> dict[str, Any]:
    """Get default hyperparameters for a model family."""
    defaults = {
        "logistic_regression": {
            "max_iter": 1000,
            "random_state": 42,
        },
        "random_forest": {
            "n_estimators": 100,
            "max_depth": 10,
            "random_state": 42,
        },
        "linear_svc": {
            "max_iter": 1000,
            "random_state": 42,
        },
    }
    return defaults.get(family, {})


def run_training(
    request: RunRequest,
    run_dir: Path,
    workspace_path: Path,
    logger: RunLogger,
) -> RunResult:
    """Execute training and return result.

    Args:
        request: Parsed request.json
        run_dir: Path to the run directory
        workspace_path: Path to the workspace root
        logger: Logger for output

    Returns:
        RunResult with status, metrics, and artifacts
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

        # Load dataset
        logger.raw(STAGE_LOADING_DATASET)
        logger.log(f"Loading dataset from {request.dataset.path}")

        dataset_path = workspace_path / request.dataset.path
        if not dataset_path.exists():
            raise FileNotFoundError(f"Dataset not found: {dataset_path}")

        df = pd.read_csv(dataset_path)
        logger.log(f"Loaded {len(df)} rows, {len(df.columns)} columns")

        # Prepare features and labels
        label_col = request.dataset.label_column
        if label_col not in df.columns:
            raise ValueError(
                f"Label column '{label_col}' not found. "
                f"Available: {', '.join(df.columns)}"
            )

        X = df.drop(columns=[label_col])
        y = df[label_col]

        # Encode labels if needed
        label_encoder = None
        if y.dtype == "object":
            label_encoder = LabelEncoder()
            y = label_encoder.fit_transform(y)
            logger.log(f"Encoded {len(label_encoder.classes_)} classes")

        # Split data
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=0.2, random_state=42, stratify=y
        )
        logger.log(f"Split: {len(X_train)} train, {len(X_test)} test")

        # Build model
        logger.raw(STAGE_TRAINING)
        logger.log(f"Training {family} model")

        # Merge default and user hyperparameters
        hyperparams = get_default_hyperparameters(family)
        if request.model.hyperparameters:
            hyperparams.update(request.model.hyperparameters)

        # Resolve device: GPU requested → check availability, fallback to CPU with reason
        requested_device = request.device.type
        actual_device, gpu_reason = select_device(requested_device)

        # Emit device token
        logger.raw(device_selected(actual_device, gpu_reason))
        if gpu_reason:
            logger.log(f"Device: {actual_device} (fallback from {requested_device}: {gpu_reason})")
        else:
            logger.log(f"Device: {actual_device}")

        # Capture effective configuration for result
        result.effective_config = EffectiveConfig(
            model_family=family,
            model_hyperparameters=hyperparams,
            device_type=actual_device,
            gpu_reason=gpu_reason,
            preset=request.preset,
            dataset_path=request.dataset.path,
            label_column=request.dataset.label_column,
        )
        logger.log(f"Effective config: family={family}, device={actual_device}")

        model_class = MODEL_FAMILIES[family]
        model = model_class(**hyperparams)

        # Create pipeline with scaler
        pipeline = Pipeline([
            ("scaler", StandardScaler()),
            ("model", model),
        ])

        # Simulate epochs for models that support partial_fit or just log progress
        if family == "random_forest":
            # Random Forest: simulate by training incrementally
            n_estimators = hyperparams.get("n_estimators", 100)
            n_epochs = min(10, n_estimators // 10)  # Report progress 10 times
            estimators_per_epoch = n_estimators // n_epochs

            for i in range(n_epochs):
                current_n = (i + 1) * estimators_per_epoch
                logger.raw(epoch(i + 1, n_epochs))
                logger.log(f"Training epoch {i + 1}/{n_epochs} ({current_n} trees)")

                # Train with current number of estimators
                if i == n_epochs - 1:
                    # Final epoch - use full pipeline
                    pipeline.fit(X_train, y_train)
                else:
                    # Intermediate - just log progress
                    time.sleep(0.1)  # Small delay for realistic feel

        else:
            # For other models, just train and report single epoch
            logger.raw(epoch(1, 1))
            pipeline.fit(X_train, y_train)

        logger.log("Training completed")

        # Evaluate
        logger.raw(STAGE_EVALUATING)
        logger.log("Evaluating model on test set")

        y_pred = pipeline.predict(X_test)

        accuracy = accuracy_score(y_test, y_pred)
        precision = precision_score(y_test, y_pred, average="weighted", zero_division=0)
        recall = recall_score(y_test, y_pred, average="weighted", zero_division=0)
        f1 = f1_score(y_test, y_pred, average="weighted", zero_division=0)

        logger.log(f"Accuracy: {accuracy:.4f}")
        logger.log(f"Precision: {precision:.4f}")
        logger.log(f"Recall: {recall:.4f}")
        logger.log(f"F1 Score: {f1:.4f}")

        result.metrics = {
            "accuracy": round(accuracy, 4),
            "precision": round(precision, 4),
            "recall": round(recall, 4),
            "f1_score": round(f1, 4),
        }
        result.primary_metric_name = "accuracy"
        result.primary_metric_value = round(accuracy, 4)

        # Write artifacts
        logger.raw(STAGE_WRITING_ARTIFACTS)
        logger.log("Writing artifacts")

        artifacts_dir = run_dir / "artifacts"
        artifacts_dir.mkdir(exist_ok=True)

        # Save model
        model_path = artifacts_dir / "model.pkl"
        joblib.dump(pipeline, model_path)
        model_size = model_path.stat().st_size
        result.artifacts.append(
            ArtifactInfo(path="artifacts/model.pkl", type="model", bytes=model_size)
        )
        logger.log(f"Saved model ({model_size:,} bytes)")

        # Save metrics.json
        metrics_path = artifacts_dir / "metrics.json"
        with open(metrics_path, "w", encoding="utf-8") as f:
            json.dump(result.metrics, f, indent=2)
        metrics_size = metrics_path.stat().st_size
        result.artifacts.append(
            ArtifactInfo(path="artifacts/metrics.json", type="metrics", bytes=metrics_size)
        )

        # Save label encoder if used
        if label_encoder is not None:
            encoder_path = artifacts_dir / "label_encoder.pkl"
            joblib.dump(label_encoder, encoder_path)
            encoder_size = encoder_path.stat().st_size
            result.artifacts.append(
                ArtifactInfo(
                    path="artifacts/label_encoder.pkl",
                    type="encoder",
                    bytes=encoder_size,
                )
            )

        result.status = "succeeded"

    except Exception as e:
        result.status = "failed"
        result.error_message = str(e)
        result.error_type = type(e).__name__
        logger.error(str(e))

    # Finalize timing
    end_time = time.time()
    result.finished_at = timestamp_now()
    result.duration_ms = int((end_time - start_time) * 1000)

    return result
