"""MCP tool for managing GameObjects (create, delete, transform, hierarchy)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional, Union

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity GameObjects: create, delete, rename, duplicate, query, "
    "set transform, set parent, set tag, set layer, find, and list.",
    group="core",
)
async def manage_gameobject(
    ctx: Context,
    action: Annotated[
        Literal[
            "create", "delete", "rename", "duplicate", "get",
            "set_transform", "set_parent", "set_tag", "set_layer",
            "find", "list",
        ],
        "The GameObject operation to perform",
    ],
    name: Annotated[
        Optional[str],
        "Name for create, rename, or display name for the target object",
    ] = None,
    target: Annotated[
        Optional[str],
        "Identifier for the target object (by name, path, or instanceID). "
        "Used by delete, get, duplicate, set_transform, set_parent, set_tag, set_layer",
    ] = None,
    search_method: Annotated[
        Optional[str],
        "How to resolve the target: 'by_id', 'by_name', 'by_path', 'by_tag', 'by_layer'",
    ] = None,
    position: Annotated[
        Optional[Union[list[float], dict[str, float]]],
        "Position as [x, y, z] or {x, y, z}. Used with create and set_transform",
    ] = None,
    rotation: Annotated[
        Optional[Union[list[float], dict[str, float]]],
        "Rotation as euler angles [x, y, z] or {x, y, z}. Used with create and set_transform",
    ] = None,
    scale: Annotated[
        Optional[Union[list[float], dict[str, float]]],
        "Scale as [x, y, z] or {x, y, z}. Used with create and set_transform",
    ] = None,
    primitive_type: Annotated[
        Optional[str],
        "Primitive type to create: 'Cube', 'Sphere', 'Plane', 'Capsule', "
        "'Cylinder', 'Quad'. Used with create action",
    ] = None,
    tag: Annotated[
        Optional[str],
        "Tag name. Used with create, set_tag, or find",
    ] = None,
    layer: Annotated[
        Optional[Union[int, str]],
        "Layer as integer or name. Used with create, set_layer, or find",
    ] = None,
    parent: Annotated[
        Optional[str],
        "Parent GameObject reference (name, path, or instanceID). "
        "Used with create or set_parent",
    ] = None,
    components_to_add: Annotated[
        Optional[list[str]],
        "Component type names to add on creation. Used with create action",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity GameObjects.

    Actions:
    - create: Create a new GameObject (optionally from a primitive type)
    - delete: Delete a GameObject by target
    - rename: Rename a GameObject
    - duplicate: Duplicate a GameObject
    - get: Get GameObject properties (name, transform, components, tag, layer)
    - set_transform: Set position, rotation, and/or scale of a GameObject
    - set_parent: Set the parent of a GameObject
    - set_tag: Set the tag of a GameObject
    - set_layer: Set the layer of a GameObject
    - find: Find GameObjects matching criteria
    - list: List top-level GameObjects in the current scene
    """
    params: dict[str, Any] = {"action": action}

    if name is not None:
        params["name"] = name
    if target is not None:
        params["target"] = target
    if search_method is not None:
        params["search_method"] = search_method
    if position is not None:
        params["position"] = position
    if rotation is not None:
        params["rotation"] = rotation
    if scale is not None:
        params["scale"] = scale
    if primitive_type is not None:
        params["primitive_type"] = primitive_type
    if tag is not None:
        params["tag"] = tag
    if layer is not None:
        params["layer"] = layer
    if parent is not None:
        params["parent"] = parent
    if components_to_add is not None:
        params["components_to_add"] = components_to_add

    return await execute_tool_with_contract("manage_gameobject", params)
