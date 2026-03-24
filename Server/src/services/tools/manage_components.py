"""MCP tool for managing Unity components: add, remove, get, set/get properties, list types."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional, Union

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Manage Unity components: add, remove, get info, set/get properties dynamically, "
    "list available types, and check if a component exists on a GameObject.",
    group="core",
)
async def manage_components(
    ctx: Context,
    action: Annotated[
        Literal["add", "remove", "get", "set_property", "get_property", "list_types", "has"],
        "The component operation to perform",
    ],
    gameobject: Annotated[
        Optional[str],
        "Target GameObject identifier (name, path, or instanceID). Used with add, remove, get, "
        "set_property, get_property, has.",
    ] = None,
    component_type: Annotated[
        Optional[str],
        "Component type name (e.g., 'Rigidbody', 'BoxCollider', 'MeshRenderer', 'Light'). "
        "Used with add, remove, list_types, has.",
    ] = None,
    property_name: Annotated[
        Optional[str],
        "Property/field name to get or set. Supports public fields and properties with getters/setters. "
        "Used with set_property, get_property.",
    ] = None,
    property_value: Annotated[
        Optional[Union[str, int, float, bool, list, dict]],
        "Value to set for the property. Used with set_property.",
    ] = None,
    search_method: Annotated[
        Optional[str],
        "How to resolve the GameObject: 'by_id', 'by_name', 'by_path'. "
        "Used when gameobject parameter is ambiguous.",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity components.

    Actions:
    - add: Add a component to a GameObject by type
    - remove: Remove a component from a GameObject by type
    - get: Get all components on a GameObject with their types
    - set_property: Set a property value on a component using reflection
    - get_property: Get a property value from a component using reflection
    - list_types: List all available component types in loaded assemblies
    - has: Check if a GameObject has a specific component type
    """
    params: dict[str, Any] = {"action": action}

    if gameobject is not None:
        params["gameobject"] = gameobject
    if component_type is not None:
        params["component_type"] = component_type
    if property_name is not None:
        params["property_name"] = property_name
    if property_value is not None:
        params["property_value"] = property_value
    if search_method is not None:
        params["search_method"] = search_method

    result = await send_with_unity_instance(None, "manage_components", params)
    return result
