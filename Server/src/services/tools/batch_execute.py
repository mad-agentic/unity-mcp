"""Server-orchestrated batch execution with guardrails and soft rollback."""

from __future__ import annotations

from typing import Annotated, Any, Optional

from fastmcp import Context

from core.error_codes import ERROR_INVALID_INPUT, ERROR_INTERNAL, WARNING_PARTIAL_SUCCESS
from core.response_contract import error_response, success_response
from services.registry import mcp_for_unity_tool, get_tool


DEFAULT_MAX_COMMANDS_PER_BATCH = 25
HARD_MAX_COMMANDS_PER_BATCH = 100


def _parse_bool(value: Any, *, default: bool) -> bool:
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.lower() in {"true", "1", "yes", "on"}
    return bool(value)


@mcp_for_unity_tool(
    description=(
        "Execute multiple MCP commands in one request with guardrails: "
        "max commands per batch, fail-fast option, and optional soft rollback."
    ),
    group="core",
    maturity="advanced",
)
async def batch_execute(
    ctx: Context,
    commands: Annotated[
        list[dict[str, Any]],
        "Ordered list of commands: [{tool, params?, rollback?}]",
    ],
    max_commands_per_batch: Annotated[
        Optional[int],
        "Soft limit for this request (default 25, hard max 100)",
    ] = None,
    fail_fast: Annotated[
        Optional[bool],
        "Stop execution on first failed command (default true)",
    ] = True,
    rollback_soft: Annotated[
        Optional[bool],
        "Attempt best-effort rollback using command.rollback definitions (default false)",
    ] = False,
) -> dict[str, Any]:
    if not isinstance(commands, list) or not commands:
        return error_response(
            tool="batch_execute",
            code=ERROR_INVALID_INPUT,
            message="commands must be a non-empty list",
            retryable=False,
            details={"commands_type": type(commands).__name__},
            next_actions=["Provide commands as a list of {tool, params} objects."],
        )

    limit = max_commands_per_batch or DEFAULT_MAX_COMMANDS_PER_BATCH
    if limit <= 0 or limit > HARD_MAX_COMMANDS_PER_BATCH:
        return error_response(
            tool="batch_execute",
            code=ERROR_INVALID_INPUT,
            message=f"max_commands_per_batch must be between 1 and {HARD_MAX_COMMANDS_PER_BATCH}",
            retryable=False,
            details={"max_commands_per_batch": max_commands_per_batch},
            next_actions=[f"Use a value between 1 and {HARD_MAX_COMMANDS_PER_BATCH}."],
        )

    if len(commands) > limit:
        return error_response(
            tool="batch_execute",
            code=ERROR_INVALID_INPUT,
            message="Batch exceeds configured command limit",
            retryable=False,
            details={"command_count": len(commands), "limit": limit},
            next_actions=["Reduce batch size.", "Split commands into multiple batches."],
        )

    fail_fast_enabled = _parse_bool(fail_fast, default=True)
    rollback_soft_enabled = _parse_bool(rollback_soft, default=False)

    results: list[dict[str, Any]] = []
    succeeded_indices: list[int] = []
    failed_indices: list[int] = []

    for index, command in enumerate(commands):
        if not isinstance(command, dict):
            results.append(
                {
                    "index": index,
                    "tool": None,
                    "status": "error",
                    "error": {
                        "code": ERROR_INVALID_INPUT,
                        "message": "Command must be an object",
                    },
                }
            )
            failed_indices.append(index)
            if fail_fast_enabled:
                break
            continue

        tool_name = command.get("tool")
        params = command.get("params") or {}

        if not isinstance(tool_name, str) or not tool_name:
            results.append(
                {
                    "index": index,
                    "tool": tool_name,
                    "status": "error",
                    "error": {
                        "code": ERROR_INVALID_INPUT,
                        "message": "Command is missing tool name",
                    },
                }
            )
            failed_indices.append(index)
            if fail_fast_enabled:
                break
            continue

        if tool_name == "batch_execute":
            results.append(
                {
                    "index": index,
                    "tool": tool_name,
                    "status": "error",
                    "error": {
                        "code": ERROR_INVALID_INPUT,
                        "message": "Nested batch_execute is not allowed",
                    },
                }
            )
            failed_indices.append(index)
            if fail_fast_enabled:
                break
            continue

        if not isinstance(params, dict):
            results.append(
                {
                    "index": index,
                    "tool": tool_name,
                    "status": "error",
                    "error": {
                        "code": ERROR_INVALID_INPUT,
                        "message": "params must be an object",
                    },
                }
            )
            failed_indices.append(index)
            if fail_fast_enabled:
                break
            continue

        registered = get_tool(tool_name)
        if not registered:
            results.append(
                {
                    "index": index,
                    "tool": tool_name,
                    "status": "error",
                    "error": {
                        "code": ERROR_INVALID_INPUT,
                        "message": f"Unknown tool: {tool_name}",
                    },
                }
            )
            failed_indices.append(index)
            if fail_fast_enabled:
                break
            continue

        try:
            response = await registered["func"](ctx, **params)
            cmd_status = str(response.get("status") or "error") if isinstance(response, dict) else "error"
            row: dict[str, Any] = {
                "index": index,
                "tool": tool_name,
                "status": cmd_status,
                "response": response,
            }
            if cmd_status == "success":
                succeeded_indices.append(index)
            else:
                failed_indices.append(index)
            results.append(row)

            if cmd_status != "success" and fail_fast_enabled:
                break
        except Exception as exc:  # noqa: BLE001
            failed_indices.append(index)
            results.append(
                {
                    "index": index,
                    "tool": tool_name,
                    "status": "error",
                    "error": {
                        "code": ERROR_INTERNAL,
                        "message": "Unhandled exception during command execution",
                        "details": str(exc),
                    },
                }
            )
            if fail_fast_enabled:
                break

    rollback_results: list[dict[str, Any]] = []
    if failed_indices and rollback_soft_enabled and succeeded_indices:
        for success_index in reversed(succeeded_indices):
            source_command = commands[success_index]
            rollback = source_command.get("rollback")
            if not isinstance(rollback, dict):
                rollback_results.append(
                    {
                        "source_index": success_index,
                        "status": "skipped",
                        "message": "No rollback command provided",
                    }
                )
                continue

            rollback_tool = rollback.get("tool")
            rollback_params = rollback.get("params") or {}
            if not isinstance(rollback_tool, str) or not rollback_tool:
                rollback_results.append(
                    {
                        "source_index": success_index,
                        "status": "error",
                        "error": {
                            "code": ERROR_INVALID_INPUT,
                            "message": "Invalid rollback tool",
                        },
                    }
                )
                continue

            registered_rollback = get_tool(rollback_tool)
            if not registered_rollback or not isinstance(rollback_params, dict):
                rollback_results.append(
                    {
                        "source_index": success_index,
                        "tool": rollback_tool,
                        "status": "error",
                        "error": {
                            "code": ERROR_INVALID_INPUT,
                            "message": "Rollback definition is invalid",
                        },
                    }
                )
                continue

            try:
                rollback_response = await registered_rollback["func"](ctx, **rollback_params)
                rollback_results.append(
                    {
                        "source_index": success_index,
                        "tool": rollback_tool,
                        "status": "success" if isinstance(rollback_response, dict) and rollback_response.get("status") == "success" else "error",
                        "response": rollback_response,
                    }
                )
            except Exception as exc:  # noqa: BLE001
                rollback_results.append(
                    {
                        "source_index": success_index,
                        "tool": rollback_tool,
                        "status": "error",
                        "error": {
                            "code": ERROR_INTERNAL,
                            "message": "Rollback execution failed",
                            "details": str(exc),
                        },
                    }
                )

    data = {
        "summary": {
            "total": len(commands),
            "executed": len(results),
            "succeeded": len(succeeded_indices),
            "failed": len(failed_indices),
            "fail_fast": fail_fast_enabled,
            "rollback_soft": rollback_soft_enabled,
            "limit": limit,
        },
        "results": results,
        "rollback_results": rollback_results,
    }

    if not failed_indices:
        return success_response(
            tool="batch_execute",
            message="Batch executed successfully",
            data=data,
        )

    return error_response(
        tool="batch_execute",
        code=WARNING_PARTIAL_SUCCESS,
        message="Batch completed with failures",
        retryable=False,
        details={
            "failed_indices": failed_indices,
            "rollback_attempted": rollback_soft_enabled,
        },
        next_actions=[
            "Inspect failed command entries and fix invalid inputs.",
            "Retry only failed commands in a new batch.",
        ],
        meta={"summary": data["summary"], "rollback_results": rollback_results},
    )
