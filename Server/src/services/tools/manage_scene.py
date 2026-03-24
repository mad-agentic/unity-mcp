"""MCP tool for managing Unity scenes (list, save, load, create, build settings)."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity scenes: list all scenes, get current scene, save, save_all, "
    "load, create new scenes, and manage build settings.",
    group="core",
)
async def manage_scene(
    ctx: Context,
    action: Annotated[
        Literal["list", "get_current", "save", "save_all", "load", "create", "create_with_objects", "add_to_build", "remove_from_build"],
        "The scene operation to perform",
    ],
    scene_name: Annotated[
        Optional[str],
        "Name for the scene (used by create, load, remove_from_build actions)",
    ] = None,
    scene_path: Annotated[
        Optional[str],
        "Path for load/create (e.g. 'Assets/Scenes/MyScene.unity')",
    ] = None,
    force: Annotated[
        Optional[bool],
        "Force load in additive mode (LoadSceneMode.Additive). "
        "If False or absent, loads in Single mode",
    ] = None,
    objects: Annotated[
        Optional[list[dict[str, Any]]],
        "List of objects to create in a new scene (for create_with_objects action). "
        "Each dict may contain 'name', 'type', 'position', 'rotation', 'scale', 'components'",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity scenes.

    Actions:
    - list: Return all scenes in the project (from EditorBuildSettings)
    - get_current: Return the currently active scene name and path
    - save: Save the current scene
    - save_all: Save all open scenes
    - load: Load a scene by name or path
    - create: Create a new empty scene
    - create_with_objects: Create a new scene with specified objects
    - add_to_build: Add a scene to the build settings
    - remove_from_build: Remove a scene from the build settings
    """
    params: dict[str, Any] = {"action": action}

    if scene_name is not None:
        params["scene_name"] = scene_name
    if scene_path is not None:
        params["scene_path"] = scene_path
    if force is not None:
        params["force"] = force
    if objects is not None:
        params["objects"] = objects

    return await execute_tool_with_contract("manage_scene", params)
