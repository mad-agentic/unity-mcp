"""Job state schema helpers for long-running operations."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class JobState:
    job_id: str
    tool: str
    status: str
    progress: float = 0.0
    message: str = ""
    result: Any = None
    error: dict[str, Any] | None = None
    meta: dict[str, Any] = field(default_factory=dict)


VALID_JOB_STATES = {"queued", "running", "pending", "succeeded", "failed", "cancelled"}


def serialize_job_state(state: JobState) -> dict[str, Any]:
    return {
        "job_id": state.job_id,
        "tool": state.tool,
        "status": state.status,
        "progress": state.progress,
        "message": state.message,
        "result": state.result,
        "error": state.error,
        "meta": state.meta,
    }


def is_terminal_job_state(status: str) -> bool:
    return status in {"succeeded", "failed", "cancelled"}
