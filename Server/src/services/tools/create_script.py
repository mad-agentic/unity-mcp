"""MCP tool for creating Unity C# scripts with templates and method stubs."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Create Unity C# scripts from templates or from scratch, with optional method stubs. "
    "More focused than manage_script for script creation scenarios.",
    group="core",
)
async def create_script(
    ctx: Context,
    action: Annotated[
        Literal["from_template", "from_scratch", "with_methods"],
        "The script creation mode",
    ],
    name: Annotated[
        Optional[str],
        "Script file name (without .cs extension). Used with all actions.",
    ] = None,
    class_name: Annotated[
        Optional[str],
        "Class name for the script. Defaults to the file name. Used with all actions.",
    ] = None,
    template: Annotated[
        Optional[str],
        "Template type: 'MonoBehaviour', 'NetworkBehaviour', 'EditorWindow', "
        "'ScriptableObject', 'StateMachine', 'Singleton'. Used with from_template.",
    ] = None,
    namespace: Annotated[
        Optional[str],
        "Namespace for the script class. Used with all actions.",
    ] = None,
    methods: Annotated[
        Optional[list[str]],
        "List of method signatures to stub out (e.g., ['void Update()', 'public void MyMethod(string arg)']). "
        "Used with with_methods.",
    ] = None,
    output_path: Annotated[
        Optional[str],
        "Output path relative to Assets folder (e.g., 'Scripts/MyFolder'). "
        "Used with all actions.",
    ] = None,
) -> dict[str, Any]:
    """Create Unity C# scripts.

    Actions:
    - from_template: Create a script from a built-in template (MonoBehaviour, NetworkBehaviour, etc.)
    - from_scratch: Create a bare minimum script with only usings and a class declaration
    - with_methods: Create a script with specified method stubs
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
    if methods is not None:
        params["methods"] = methods
    if output_path is not None:
        params["output_path"] = output_path

    result = await send_with_unity_instance(None, "create_script", params)
    return result
