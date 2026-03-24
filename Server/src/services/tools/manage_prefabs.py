"""MCP tool for managing Unity Prefabs: create, instantiate, unpack, get info, apply, and revert."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity Prefabs: create new prefabs, instantiate prefabs in scenes, "
    "unpack prefab instances, get prefab info, apply overrides, and revert to original prefab.",
    group="core",
)
async def manage_prefab(
    ctx: Context,
    action: Annotated[
        Literal["create", "instantiate", "unpack", "get_info", "apply", "revert"],
        "The prefab operation to perform",
    ],
    prefab_path: Annotated[
        Optional[str],
        "Path to the prefab asset (e.g. 'Assets/Prefabs/MyPrefab.prefab')",
    ] = None,
    parent: Annotated[
        Optional[str],
        "Parent GameObject identifier (name or instance ID) for instantiate action",
    ] = None,
    position: Annotated[
        Optional[list[float]],
        "World position [x, y, z] for instantiation",
    ] = None,
    scene_name: Annotated[
        Optional[str],
        "Target scene name for instantiation (defaults to active scene)",
    ] = None,
    target: Annotated[
        Optional[str],
        "GameObject identifier for apply/revert/unpack operations",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Prefab operations.

    Actions:
    - create: Create a new prefab from a GameObject
    - instantiate: Instantiate a prefab into a scene
    - unpack: Unpack a prefab instance into regular scene GameObjects
    - get_info: Get information about a prefab asset or instance
    - apply: Apply overrides from a prefab instance back to the prefab
    - revert: Revert a prefab instance to the original prefab state
    """
    params: dict[str, Any] = {"action": action}

    if prefab_path is not None:
        params["prefab_path"] = prefab_path
    if parent is not None:
        params["parent"] = parent
    if position is not None:
        params["position"] = position
    if scene_name is not None:
        params["scene_name"] = scene_name
    if target is not None:
        params["target"] = target

    return await execute_tool_with_contract("manage_prefab", params)
