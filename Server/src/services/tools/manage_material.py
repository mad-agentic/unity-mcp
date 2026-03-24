"""MCP tool for managing Unity Materials (create, get, set properties, duplicate, delete)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional, Union

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity Materials: create, get, set color, set texture, set shader, "
    "set float, set vector, set keyword, duplicate, and delete.",
    group="core",
)
async def manage_material(
    ctx: Context,
    action: Annotated[
        Literal[
            "create", "get", "set_color", "set_texture", "set_shader",
            "set_float", "set_vector", "set_keyword", "duplicate", "delete",
        ],
        "The Material operation to perform",
    ],
    name: Annotated[
        Optional[str],
        "Name for create, duplicate, or display name for the target material",
    ] = None,
    target: Annotated[
        Optional[str],
        "Identifier for the target material (asset path or instance ID). "
        "Used by get, set_color, set_texture, set_shader, set_float, set_vector, set_keyword, duplicate, delete",
    ] = None,
    shader_name: Annotated[
        Optional[str],
        "Shader name or path for create or set_shader action (e.g., 'Standard', 'Unlit/Color')",
    ] = None,
    color: Annotated[
        Optional[Union[list[float], dict[str, float]]],
        "Color as [r, g, b, a] or {r, g, b, a}. Used with create (via _Color) or set_color",
    ] = None,
    texture_path: Annotated[
        Optional[str],
        "Path to a texture asset. Used with set_texture",
    ] = None,
    property_name: Annotated[
        Optional[str],
        "Shader property name (e.g., '_Color', '_MainTex', '_Smoothness'). Used with set_float, set_vector",
    ] = None,
    property_value: Annotated[
        Optional[Union[float, list[float], dict[str, float]]],
        "Property value. Float for set_float, vector [x,y,z,w] or {x,y,z,w} for set_vector",
    ] = None,
    keyword: Annotated[
        Optional[str],
        "Shader keyword name (e.g., '_ALPHATEST_ON', '_EMISSION'). Used with set_keyword",
    ] = None,
    keyword_enabled: Annotated[
        Optional[bool],
        "Enable (true) or disable (false) the keyword. Used with set_keyword",
    ] = True,
) -> dict[str, Any]:
    """Manage Unity Materials.

    Actions:
    - create: Create a new material asset
    - get: Get material properties (shader, color, textures, keywords)
    - set_color: Set the material color (_Color)
    - set_texture: Set a texture property on the material
    - set_shader: Change the material's shader
    - set_float: Set a float property on the material
    - set_vector: Set a vector property on the material
    - set_keyword: Enable or disable a shader keyword
    - duplicate: Duplicate a material
    - delete: Delete a material asset
    """
    params: dict[str, Any] = {"action": action}

    if name is not None:
        params["name"] = name
    if target is not None:
        params["target"] = target
    if shader_name is not None:
        params["shader_name"] = shader_name
    if color is not None:
        params["color"] = color
    if texture_path is not None:
        params["texture_path"] = texture_path
    if property_name is not None:
        params["property_name"] = property_name
    if property_value is not None:
        params["property_value"] = property_value
    if keyword is not None:
        params["keyword"] = keyword
    if keyword_enabled is not None:
        params["keyword_enabled"] = keyword_enabled

    return await execute_tool_with_contract("manage_material", params)
