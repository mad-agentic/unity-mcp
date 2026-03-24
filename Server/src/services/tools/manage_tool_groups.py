"""MCP tool for runtime inspection and toggling of server-side tool groups."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from core.response_contract import error_response, success_response
from services.registry import (
    disable_group,
    enable_group,
    get_all_groups,
    get_enabled_groups,
    mcp_for_unity_tool,
    set_enabled_groups,
)


def _sync_guidance() -> list[str]:
    return [
        "Reconnect the MCP client to refresh the advertised tool list.",
        "Restart the Unity MCP server if the client caches tool schemas aggressively.",
    ]


@mcp_for_unity_tool(
    description="Inspect and update enabled server-side tool groups. Changes apply immediately to registry state and require client refresh to resync tool discovery.",
    group="core",
)
async def manage_tool_groups(
    ctx: Context,
    action: Annotated[
        Literal["list", "enable", "disable", "set"],
        "Tool group action to perform",
    ],
    group: Annotated[
        Optional[str],
        "Single tool group name for enable/disable actions.",
    ] = None,
    groups: Annotated[
        Optional[list[str]],
        "Full list of enabled tool groups for set action.",
    ] = None,
) -> dict[str, Any]:
    """Inspect and modify runtime tool-group state."""
    all_groups = sorted(get_all_groups())

    if action == "list":
        return success_response(
            tool="manage_tool_groups",
            message="Tool groups listed.",
            data={
                "all_groups": all_groups,
                "enabled_groups": sorted(get_enabled_groups()),
                "sync_required": False,
            },
        )

    if action in {"enable", "disable"} and not group:
        return error_response(
            tool="manage_tool_groups",
            code="E_INVALID_INPUT",
            message="group is required for enable/disable actions.",
            next_actions=["Provide a valid tool group name."],
        )

    if action == "enable":
        changed = enable_group(str(group))
        if not changed and str(group) not in get_enabled_groups():
            return error_response(
                tool="manage_tool_groups",
                code="E_INVALID_INPUT",
                message=f"Unknown tool group '{group}'.",
                next_actions=["Use action='list' to inspect valid groups."],
            )
        return success_response(
            tool="manage_tool_groups",
            message=f"Tool group '{group}' enabled.",
            data={
                "enabled_groups": sorted(get_enabled_groups()),
                "changed": changed,
                "sync_required": True,
            },
            next_actions=_sync_guidance(),
        )

    if action == "disable":
        if str(group) == "core":
            return error_response(
                tool="manage_tool_groups",
                code="E_INVALID_INPUT",
                message="The core group cannot be disabled.",
                next_actions=["Disable a non-core group or use action='list' to inspect current state."],
            )
        changed = disable_group(str(group))
        if not changed:
            return error_response(
                tool="manage_tool_groups",
                code="E_INVALID_INPUT",
                message=f"Unknown or already disabled tool group '{group}'.",
                next_actions=["Use action='list' to inspect valid groups and current state."],
            )
        return success_response(
            tool="manage_tool_groups",
            message=f"Tool group '{group}' disabled.",
            data={
                "enabled_groups": sorted(get_enabled_groups()),
                "changed": changed,
                "sync_required": True,
            },
            next_actions=_sync_guidance(),
        )

    if action == "set":
        if groups is None:
            return error_response(
                tool="manage_tool_groups",
                code="E_INVALID_INPUT",
                message="groups is required for set action.",
                next_actions=["Provide a full list of enabled groups."],
            )
        requested_groups = {str(item) for item in groups}
        invalid_groups = sorted(requested_groups - set(all_groups))
        if invalid_groups:
            return error_response(
                tool="manage_tool_groups",
                code="E_INVALID_INPUT",
                message="One or more requested groups are invalid.",
                next_actions=["Remove invalid group names and retry.", "Use action='list' to inspect valid groups."],
                meta={"invalid_groups": invalid_groups},
            )
        set_enabled_groups(requested_groups)
        return success_response(
            tool="manage_tool_groups",
            message="Tool groups updated.",
            data={
                "enabled_groups": sorted(get_enabled_groups()),
                "sync_required": True,
            },
            next_actions=_sync_guidance(),
        )

    return error_response(
        tool="manage_tool_groups",
        code="E_INVALID_INPUT",
        message=f"Unsupported action '{action}'.",
        next_actions=["Use one of: list, enable, disable, set."],
    )