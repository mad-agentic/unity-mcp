"""MCP tool for retrieving Unity project information and configuration."""

from __future__ import annotations

from typing import Annotated, Any, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Get Unity project information including tags, layers, scenes, and assets. "
    "Use flags to control which information is included.",
    group="core",
)
async def get_project_info(
    ctx: Context,
    include_tags: Annotated[
        Optional[bool],
        "Include all defined Unity tags (TagManager.tags)",
    ] = None,
    include_layers: Annotated[
        Optional[bool],
        "Include all defined layers (TagManager.layers, 0-31)",
    ] = None,
    include_scenes: Annotated[
        Optional[bool],
        "Include the list of scenes in the project (from EditorBuildSettings)",
    ] = None,
    include_assets: Annotated[
        Optional[bool],
        "Include a paginated list of assets in the project. "
        "Use page_size and cursor for pagination",
    ] = None,
    page_size: Annotated[
        Optional[int],
        "Number of assets to return per page when include_assets is True. Default: 50",
    ] = None,
    cursor: Annotated[
        Optional[int],
        "Pagination cursor for assets (asset index). Pass the last seen index "
        "to get the next page",
    ] = None,
) -> dict[str, Any]:
    """Retrieve Unity project configuration and asset information.

    This tool provides an overview of the Unity project's configuration
    including tags, layers, scenes, and optionally a paginated asset listing.
    By default returns project name, Unity version, and platform.
    """
    params: dict[str, Any] = {}

    if include_tags is not None:
        params["include_tags"] = include_tags
    if include_layers is not None:
        params["include_layers"] = include_layers
    if include_scenes is not None:
        params["include_scenes"] = include_scenes
    if include_assets is not None:
        params["include_assets"] = include_assets
    if page_size is not None:
        params["page_size"] = page_size
    if cursor is not None:
        params["cursor"] = cursor

    return await execute_tool_with_contract("get_project_info", params)
