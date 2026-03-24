"""MCP tool for managing Unity Assets (create, get, rename, delete, move, copy, find, get metadata)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity Assets: create, get, rename, delete, move, copy, find, "
    "and get metadata for project assets.",
    group="core",
)
async def manage_asset(
    ctx: Context,
    action: Annotated[
        Literal["create", "get", "rename", "delete", "move", "copy", "find", "get_metadata"],
        "The asset operation to perform",
    ],
    asset_path: Annotated[
        Optional[str],
        "Source asset path (relative to Assets). Used by get, rename, delete, move, copy, find, get_metadata",
    ] = None,
    new_name: Annotated[
        Optional[str],
        "New name for rename action, or name for create action",
    ] = None,
    target_path: Annotated[
        Optional[str],
        "Destination path for move or copy action. Path relative to Assets/.",
    ] = None,
    type_filter: Annotated[
        Optional[str],
        "Asset type filter (e.g., 'Material', 'Shader', 'Texture', 'GameObject', 'AudioClip'). "
        "Used with find and create actions.",
    ] = None,
    search_query: Annotated[
        Optional[str],
        "Search query string. Used with find action.",
    ] = None,
    include_metadata: Annotated[
        Optional[bool],
        "Include full metadata (GUID, import settings). Used with get action. Default: false",
    ] = False,
) -> dict[str, Any]:
    """Manage Unity Assets.

    Actions:
    - create: Create a new asset of the specified type
    - get: Get asset info and properties
    - rename: Rename an asset
    - delete: Delete an asset
    - move: Move an asset to a new path
    - copy: Copy an asset to a new path
    - find: Find assets by type and/or search query
    - get_metadata: Get detailed metadata for an asset
    """
    params: dict[str, Any] = {"action": action}

    if asset_path is not None:
        params["asset_path"] = asset_path
    if new_name is not None:
        params["new_name"] = new_name
    if target_path is not None:
        params["target_path"] = target_path
    if type_filter is not None:
        params["type_filter"] = type_filter
    if search_query is not None:
        params["search_query"] = search_query
    if include_metadata is not None:
        params["include_metadata"] = include_metadata

    return await execute_tool_with_contract("manage_asset", params)
