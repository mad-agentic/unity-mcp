"""MCP tool for managing Unity C# scripts: create, get, rename, delete, list methods/properties."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Manage Unity C# scripts: create, get, rename, delete, and introspect "
    "methods/properties. Supports templates like MonoBehaviour, NetworkBehaviour, EditorWindow.",
    group="core",
)
async def manage_script(
    ctx: Context,
    action: Annotated[
        Literal["create", "get", "rename", "delete", "get_methods", "get_properties"],
        "The script operation to perform",
    ],
    name: Annotated[
        Optional[str],
        "Script file name (without .cs extension). Used with create, rename, delete, get.",
    ] = None,
    class_name: Annotated[
        Optional[str],
        "Class name for the script. Defaults to the file name if not provided. Used with create.",
    ] = None,
    template: Annotated[
        Optional[str],
        "Script template: 'MonoBehaviour', 'NetworkBehaviour', 'EditorWindow', "
        "'StateMachine', 'Singleton', 'ScriptableObject'. Used with create.",
    ] = None,
    namespace: Annotated[
        Optional[str],
        "Namespace for the script class. Used with create.",
    ] = None,
    script_path: Annotated[
        Optional[str],
        "Full path to the script file (relative to Assets). Used with get, rename, delete, "
        "get_methods, get_properties.",
    ] = None,
    target: Annotated[
        Optional[str],
        "Target identifier for rename/delete (by path or name). Used with rename, delete.",
    ] = None,
    include_methods: Annotated[
        Optional[bool],
        "Include method signatures in the output. Used with get_methods.",
    ] = True,
    include_properties: Annotated[
        Optional[bool],
        "Include property info in the output. Used with get_properties.",
    ] = True,
) -> dict[str, Any]:
    """Manage Unity C# scripts.

    Actions:
    - create: Create a new C# script from a template
    - get: Get script file info (path, class name, namespace, template)
    - rename: Rename a script file and update the class name inside
    - delete: Delete a script file from Assets
    - get_methods: List all method declarations in a script
    - get_properties: List all property declarations in a script
    """
    params: dict[str, Any] = {"action": action}

    if name is not None:
        params["name"] = name
    if class_name is not None:
        params["class_name"] = class_name
    if template is not None:
        params["template"] = template
    if namespace is not None:
        params["namespace"] = namespace
    if script_path is not None:
        params["script_path"] = script_path
    if target is not None:
        params["target"] = target
    if include_methods is not None:
        params["include_methods"] = include_methods
    if include_properties is not None:
        params["include_properties"] = include_properties

    result = await send_with_unity_instance(None, "manage_script", params)
    return result
