"""Unified response envelope helpers for Phase 1 foundation."""

from __future__ import annotations

from typing import Any

from core.error_codes import ERROR_INTERNAL


def success_response(
    *,
    tool: str,
    data: Any = None,
    message: str = "OK",
    next_actions: list[str] | None = None,
    meta: dict[str, Any] | None = None,
) -> dict[str, Any]:
    return {
        "status": "success",
        "tool": tool,
        "message": message,
        "data": data,
        "error": None,
        "next_actions": next_actions or [],
        "meta": meta or {},
    }


def error_response(
    *,
    tool: str,
    code: str = ERROR_INTERNAL,
    message: str = "Unexpected error",
    retryable: bool = False,
    details: Any = None,
    next_actions: list[str] | None = None,
    meta: dict[str, Any] | None = None,
) -> dict[str, Any]:
    return {
        "status": "error",
        "tool": tool,
        "message": message,
        "data": None,
        "error": {
            "code": code,
            "retryable": retryable,
            "details": details,
        },
        "next_actions": next_actions or [],
        "meta": meta or {},
    }


def pending_response(
    *,
    tool: str,
    job_id: str,
    message: str = "Job pending",
    poll_after_seconds: float = 1.0,
    data: Any = None,
    next_actions: list[str] | None = None,
    meta: dict[str, Any] | None = None,
) -> dict[str, Any]:
    return {
        "status": "pending",
        "tool": tool,
        "message": message,
        "data": data,
        "error": None,
        "next_actions": next_actions or [f"Poll job status with job_id={job_id}"],
        "meta": {
            "job_id": job_id,
            "poll_after_seconds": poll_after_seconds,
            **(meta or {}),
        },
    }
