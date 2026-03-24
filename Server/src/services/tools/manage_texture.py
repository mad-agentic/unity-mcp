"""MCP tool for managing Unity Textures (create, get, set pixels, resize, apply)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional, Union

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity Textures: create, get, set pixels, resize, and apply changes to Texture2D assets.",
    group="core",
)
async def manage_texture(
    ctx: Context,
    action: Annotated[
        Literal["create", "get", "set_pixels", "resize", "apply"],
        "The texture operation to perform",
    ],
    name: Annotated[
        Optional[str],
        "Name for create, or display name for the target texture",
    ] = None,
    width: Annotated[
        Optional[int],
        "Texture width in pixels. Used with create",
    ] = 256,
    height: Annotated[
        Optional[int],
        "Texture height in pixels. Used with create",
    ] = 256,
    format: Annotated[
        Optional[str],
        "Texture format: 'RGB', 'RGBA', 'ARGB', 'Alpha', 'R8', 'R16', 'DXT1', 'DXT5'. Used with create",
    ] = "RGBA",
    color: Annotated[
        Optional[Union[list[float], dict[str, float]]],
        "Fill color as [r, g, b, a] or {r, g, b, a}. Used with create for solid-color textures",
    ] = None,
    pattern: Annotated[
        Optional[str],
        "Fill pattern: 'gradient', 'noise', 'checker'. Used with create to fill the texture",
    ] = None,
    target: Annotated[
        Optional[str],
        "Identifier for the target texture (asset path or instance ID). "
        "Used by get, set_pixels, resize, apply",
    ] = None,
    pixels: Annotated[
        Optional[list],
        "Pixel data as flat list [r,g,b,a, r,g,b,a, ...] or list of {r,g,b,a} dicts. Used with set_pixels",
    ] = None,
    offset_x: Annotated[
        Optional[int],
        "X offset for set_pixels. Defaults to 0",
    ] = 0,
    offset_y: Annotated[
        Optional[int],
        "Y offset for set_pixels. Defaults to 0",
    ] = 0,
) -> dict[str, Any]:
    """Manage Unity Textures.

    Actions:
    - create: Create a new Texture2D asset
    - get: Get texture properties (dimensions, format, mipmap count)
    - set_pixels: Set pixel data on a texture (call apply to save)
    - resize: Resize a texture
    - apply: Apply pending pixel changes to the texture
    """
    params: dict[str, Any] = {"action": action}

    if name is not None:
        params["name"] = name
    if width is not None:
        params["width"] = width
    if height is not None:
        params["height"] = height
    if format is not None:
        params["format"] = format
    if color is not None:
        params["color"] = color
    if pattern is not None:
        params["pattern"] = pattern
    if target is not None:
        params["target"] = target
    if pixels is not None:
        params["pixels"] = pixels
    if offset_x is not None:
        params["offset_x"] = offset_x
    if offset_y is not None:
        params["offset_y"] = offset_y

    return await execute_tool_with_contract("manage_texture", params)
