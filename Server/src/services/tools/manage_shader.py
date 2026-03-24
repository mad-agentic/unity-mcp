"""MCP tool for inspecting Unity Shaders (list, get properties, get keywords)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Inspect Unity Shaders: list available shaders, get shader properties, "
    "and get shader keywords.",
    group="core",
)
async def manage_shader(
    ctx: Context,
    action: Annotated[
        Literal["list", "get_properties", "get_keywords"],
        "The shader operation to perform",
    ],
    shader_name: Annotated[
        Optional[str],
        "Name or path of the shader (e.g., 'Standard', 'Unlit/Color'). "
        "Required for get_properties and get_keywords actions.",
    ] = None,
    shader_path: Annotated[
        Optional[str],
        "Asset path to a shader file (.shader). "
        "Alternative to shader_name for get_properties and get_keywords.",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Shaders.

    Actions:
    - list: List all available shaders in the project
    - get_properties: Get all properties exposed by a shader
    - get_keywords: Get all shader keywords for a shader
    """
    params: dict[str, Any] = {"action": action}

    if shader_name is not None:
        params["shader_name"] = shader_name
    if shader_path is not None:
        params["shader_path"] = shader_path

    return await execute_tool_with_contract("manage_shader", params)
