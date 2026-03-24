"""MCP tool for reading, clearing, and filtering Unity's console output."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Read, clear, and filter Unity's console log. Supports reading errors, "
    "warnings, logs, and clearing the console. Useful for debugging and CI workflows.",
    group="core",
)
async def read_console(
    ctx: Context,
    action: Annotated[
        Literal["read", "clear", "get_errors", "get_warnings", "get_logs"],
        "The console operation to perform",
    ],
    count: Annotated[
        Optional[int],
        "Maximum number of log entries to return (default: 100, max: 1000)",
    ] = None,
    filter: Annotated[
        Optional[str],
        "Optional text filter to match in log messages",
    ] = None,
) -> dict[str, Any]:
    """Read and filter Unity's console output.

    Actions:
    - read: Read recent console entries (errors, warnings, and logs combined)
    - clear: Clear the Unity console
    - get_errors: Read only error entries
    - get_warnings: Read only warning entries
    - get_logs: Read only regular log entries
    """
    params: dict[str, Any] = {"action": action}

    if count is not None:
        params["count"] = count
    if filter is not None:
        params["filter"] = filter

    result = await send_with_unity_instance(None, "read_console", params)
    return result
