"""Utility functions for tool implementations."""

from __future__ import annotations

import json
import logging
from typing import Any

from core.error_codes import (
    ERROR_INVALID_INPUT,
    ERROR_UNITY_UNAVAILABLE,
    ERROR_TIMEOUT,
    ERROR_TRANSPORT_FAILURE,
    ERROR_INTERNAL,
)
from core.response_contract import success_response, error_response, pending_response
from transport.unity_transport import send_with_unity_instance


logger = logging.getLogger(__name__)


_SENSITIVE_ACTIONS: dict[str, set[str]] = {
    "script_apply_edits": {"replace_text", "replace_method", "insert_after", "add_using"},
    "manage_script": {"create", "rename", "delete"},
    "manage_scene": {"load", "create", "create_with_objects", "remove_from_build"},
    "manage_packages": {"install", "remove", "embed"},
}


def _log_verification(
    *,
    tool_name: str,
    action: str,
    stage: str,
    result: str,
    error_code: str | None = None,
) -> None:
    logger.info(
        "verification tool=%s action=%s stage=%s result=%s error_code=%s",
        tool_name,
        action,
        stage,
        result,
        error_code,
    )


async def run_verification_preflight(tool_name: str, params: dict[str, Any]) -> dict[str, Any] | None:
    """Run verification loop for sensitive actions before transport call.

    Returns a canonical error response when verification fails, otherwise None.
    """
    action = str(params.get("action") or "")
    if action not in _SENSITIVE_ACTIONS.get(tool_name, set()):
        _log_verification(tool_name=tool_name, action=action or "<none>", stage="gate", result="skip")
        return None

    _log_verification(tool_name=tool_name, action=action, stage="gate", result="trigger")

    # Stage 1: Input validation
    if tool_name == "script_apply_edits":
        if not params.get("script_path"):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="Missing required field: script_path",
                retryable=False,
                details={"action": action},
                next_actions=["Provide script_path (relative to Assets)."],
            )
        if action == "replace_text" and (not params.get("old_text") or params.get("new_text") is None):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="replace_text requires old_text and new_text",
                retryable=False,
                details={"action": action},
                next_actions=["Provide both old_text and new_text."],
            )
        if action == "replace_method" and (not params.get("method_name") or (params.get("new_method_body") is None and params.get("new_text") is None)):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="replace_method requires method_name and new_method_body/new_text",
                retryable=False,
                details={"action": action},
                next_actions=["Provide method_name and replacement method body."],
            )
        if action == "insert_after" and (not params.get("after_method") or params.get("new_text") is None):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="insert_after requires after_method and new_text",
                retryable=False,
                details={"action": action},
                next_actions=["Provide after_method and new_text."],
            )
        if action == "add_using" and not params.get("using_statement"):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="add_using requires using_statement",
                retryable=False,
                details={"action": action},
                next_actions=["Provide using_statement (e.g. using System.Linq;)."],
            )

    if tool_name == "manage_script":
        if action == "create" and not params.get("name"):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="create requires script name",
                retryable=False,
                details={"action": action},
                next_actions=["Provide name for script creation."],
            )
        if action in {"rename", "delete"} and not (params.get("target") or params.get("script_path") or params.get("name")):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message=f"{action} requires target, script_path, or name",
                retryable=False,
                details={"action": action},
                next_actions=["Provide a target reference for script operation."],
            )

    if tool_name == "manage_scene":
        if action in {"load", "remove_from_build", "create", "create_with_objects"} and not (params.get("scene_name") or params.get("scene_path")):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message=f"{action} requires scene_name or scene_path",
                retryable=False,
                details={"action": action},
                next_actions=["Provide scene_name or scene_path."],
            )

    if tool_name == "manage_packages":
        if action == "install" and not (params.get("package_name") or params.get("git_url")):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message="install requires package_name or git_url",
                retryable=False,
                details={"action": action},
                next_actions=["Provide package_name or git_url."],
            )
        if action in {"remove", "embed"} and not params.get("package_name"):
            _log_verification(tool_name=tool_name, action=action, stage="input", result="fail", error_code=ERROR_INVALID_INPUT)
            return error_response(
                tool=tool_name,
                code=ERROR_INVALID_INPUT,
                message=f"{action} requires package_name",
                retryable=False,
                details={"action": action},
                next_actions=["Provide package_name for package operation."],
            )

    _log_verification(tool_name=tool_name, action=action, stage="input", result="pass")

    # Stage 2: Unity transport readiness
    try:
        await send_with_unity_instance(None, "get_project_info", {})
    except TimeoutError as exc:
        _log_verification(tool_name=tool_name, action=action, stage="transport", result="fail", error_code=ERROR_TIMEOUT)
        return error_response(
            tool=tool_name,
            code=ERROR_TIMEOUT,
            message="Verification timed out while checking Unity readiness",
            retryable=True,
            details=str(exc),
            next_actions=["Retry the operation.", "Check Unity responsiveness and connection health."],
        )
    except ConnectionError as exc:
        _log_verification(tool_name=tool_name, action=action, stage="transport", result="fail", error_code=ERROR_UNITY_UNAVAILABLE)
        return error_response(
            tool=tool_name,
            code=ERROR_UNITY_UNAVAILABLE,
            message="Unity is unavailable for sensitive operation",
            retryable=True,
            details=str(exc),
            next_actions=["Ensure Unity Editor is connected.", "Restart bridge/server and retry."],
        )
    except Exception as exc:  # noqa: BLE001
        _log_verification(tool_name=tool_name, action=action, stage="transport", result="fail", error_code=ERROR_TRANSPORT_FAILURE)
        return error_response(
            tool=tool_name,
            code=ERROR_TRANSPORT_FAILURE,
            message="Failed to verify Unity transport readiness",
            retryable=True,
            details=str(exc),
            next_actions=["Check transport health endpoint.", "Reconnect Unity and retry."],
        )

    _log_verification(tool_name=tool_name, action=action, stage="transport", result="pass")

    # Stage 3: lightweight context checks
    scene_path = params.get("scene_path")
    if isinstance(scene_path, str) and scene_path and not scene_path.endswith(".unity"):
        _log_verification(tool_name=tool_name, action=action, stage="context", result="fail", error_code=ERROR_INVALID_INPUT)
        return error_response(
            tool=tool_name,
            code=ERROR_INVALID_INPUT,
            message="scene_path must end with .unity",
            retryable=False,
            details={"scene_path": scene_path},
            next_actions=["Provide a valid .unity scene path."],
        )

    script_path = params.get("script_path")
    if isinstance(script_path, str) and script_path and not script_path.endswith(".cs"):
        _log_verification(tool_name=tool_name, action=action, stage="context", result="fail", error_code=ERROR_INVALID_INPUT)
        return error_response(
            tool=tool_name,
            code=ERROR_INVALID_INPUT,
            message="script_path must end with .cs",
            retryable=False,
            details={"script_path": script_path},
            next_actions=["Provide a valid C# script path ending with .cs."],
        )

    _log_verification(tool_name=tool_name, action=action, stage="context", result="pass")
    return None


def parse_json_payload(value: Any) -> Any:
    """Parse a JSON string payload, returning the parsed object."""
    if isinstance(value, str):
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value
    return value


def coerce_bool(value: Any, default: bool = False) -> bool:
    """Coerce a value to a boolean."""
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.lower() in ("true", "1", "yes", "on")
    return bool(value)


def normalize_string_list(value: Any) -> list[str]:
    """Normalize a value to a list of strings."""
    if value is None:
        return []
    if isinstance(value, str):
        # Try parsing as JSON array
        try:
            parsed = json.loads(value)
            if isinstance(parsed, list):
                return [str(v) for v in parsed]
        except json.JSONDecodeError:
            # Split by comma
            return [s.strip() for s in value.split(",") if s.strip()]
    if isinstance(value, list):
        return [str(v) for v in value]
    return [str(value)]


def _looks_like_standard_envelope(value: Any) -> bool:
    if not isinstance(value, dict):
        return False
    return {"status", "tool", "message", "data", "error", "next_actions", "meta"}.issubset(value.keys())


def _is_pending_payload(value: Any) -> bool:
    if not isinstance(value, dict):
        return False
    status = str(value.get("status") or value.get("_mcp_status") or "").lower()
    return status == "pending"


def _extract_job_id(value: dict[str, Any]) -> str:
    return str(
        value.get("job_id")
        or value.get("_mcp_job_id")
        or value.get("id")
        or "unknown-job"
    )


async def execute_tool_with_contract(tool_name: str, params: dict[str, Any]) -> dict[str, Any]:
    """Execute a Unity tool call and normalize response to the Phase 1 contract."""
    try:
        preflight_error = await run_verification_preflight(tool_name, params)
        if preflight_error is not None:
            return preflight_error

        raw = await send_with_unity_instance(None, tool_name, params)

        if _looks_like_standard_envelope(raw):
            return raw

        if _is_pending_payload(raw):
            assert isinstance(raw, dict)
            poll_after = raw.get("poll_after_seconds") or raw.get("pollIntervalSeconds") or 1.0
            return pending_response(
                tool=tool_name,
                job_id=_extract_job_id(raw),
                message=str(raw.get("message") or "Job pending"),
                poll_after_seconds=float(poll_after),
                data=raw,
            )

        return success_response(
            tool=tool_name,
            data=raw,
            message="Operation completed",
        )
    except (ValueError, TypeError) as exc:
        return error_response(
            tool=tool_name,
            code=ERROR_INVALID_INPUT,
            message="Invalid request parameters",
            retryable=False,
            details=str(exc),
            next_actions=["Review request payload and required fields."],
        )
    except TimeoutError as exc:
        return error_response(
            tool=tool_name,
            code=ERROR_TIMEOUT,
            message="Unity operation timed out",
            retryable=True,
            details=str(exc),
            next_actions=["Retry the operation.", "Check Unity responsiveness and connection health."],
        )
    except ConnectionError as exc:
        return error_response(
            tool=tool_name,
            code=ERROR_TRANSPORT_FAILURE,
            message="Unable to communicate with Unity transport",
            retryable=True,
            details=str(exc),
            next_actions=["Ensure Unity is connected.", "Restart bridge/server and retry."],
        )
    except Exception as exc:  # noqa: BLE001
        return error_response(
            tool=tool_name,
            code=ERROR_INTERNAL,
            message="Unexpected internal error",
            retryable=False,
            details=str(exc),
            next_actions=["Check server logs for stack trace."],
        )
