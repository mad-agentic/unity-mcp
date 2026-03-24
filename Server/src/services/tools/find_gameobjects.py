"""MCP tool for finding and filtering GameObjects in the Unity scene hierarchy."""

from __future__ import annotations

from typing import Annotated, Any, Optional, Union

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Search and filter GameObjects in the Unity scene hierarchy by name, "
    "tag, layer, path, component type, or instance ID. Supports pagination.",
    group="core",
)
async def find_gameobjects(
    ctx: Context,
    name: Annotated[
        Optional[str],
        "Search by GameObject name. Supports * wildcards (e.g. 'Player*' matches "
        "'Player', 'PlayerController', 'Player_Health')",
    ] = None,
    tag: Annotated[
        Optional[str],
        "Search by Unity tag. Only GameObjects with this exact tag are returned",
    ] = None,
    layer: Annotated[
        Optional[Union[int, str]],
        "Search by layer. Accepts layer number (0-31) or layer name",
    ] = None,
    path: Annotated[
        Optional[str],
        "Search by scene hierarchy path (e.g. 'Game/Camera/MainCamera'). "
        "Supports partial path matching",
    ] = None,
    has_component: Annotated[
        Optional[str],
        "Filter by component type. Only GameObjects with this component are returned "
        "(e.g. 'Rigidbody', 'Collider', 'Light', 'Camera')",
    ] = None,
    instance_id: Annotated[
        Optional[int],
        "Search by Unity instance ID. Returns the specific GameObject if found",
    ] = None,
    page_size: Annotated[
        Optional[int],
        "Maximum number of results to return per page. Default: 50",
    ] = None,
    cursor: Annotated[
        Optional[int],
        "Pagination cursor (instance ID offset). Pass the last seen instance_id "
        "to get the next page of results",
    ] = None,
) -> dict[str, Any]:
    """Search for GameObjects in the Unity scene hierarchy.

    Multiple filters can be combined (AND logic). Returns paginated results
    with optional cursor-based pagination.
    """
    params: dict[str, Any] = {}

    if name is not None:
        params["name"] = name
    if tag is not None:
        params["tag"] = tag
    if layer is not None:
        params["layer"] = layer
    if path is not None:
        params["path"] = path
    if has_component is not None:
        params["has_component"] = has_component
    if instance_id is not None:
        params["instance_id"] = instance_id
    if page_size is not None:
        params["page_size"] = page_size
    if cursor is not None:
        params["cursor"] = cursor

    return await execute_tool_with_contract("find_gameobjects", params)
