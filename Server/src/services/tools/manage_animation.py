"""MCP tool for managing Unity Animator controllers, animation clips, states, and keyframes."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Create and manage Unity Animator controllers, animation clips, states, "
    "transitions, and keyframes. Group: animation.",
    group="animation",
)
async def manage_animation(
    ctx: Context,
    action: Annotated[
        Literal[
            "create_controller",
            "add_state",
            "add_transition",
            "set_keyframe",
            "create_clip",
        ],
        "The animation operation to perform",
    ],
    controller_path: Annotated[
        Optional[str],
        "Path to the Animator Controller asset "
        "(e.g. 'Assets/Animators/MyController.controller')",
    ] = None,
    state_name: Annotated[
        Optional[str],
        "Name of an animation state within the controller",
    ] = None,
    clip_path: Annotated[
        Optional[str],
        "Path to an Animation Clip asset (e.g. 'Assets/Animations/idle.anim')",
    ] = None,
    property_path: Annotated[
        Optional[str],
        "Property path for keyframe animation (e.g. 'm_LocalPosition.x', 'm_Color.r')",
    ] = None,
    value: Annotated[
        Optional[float],
        "Numeric value for the keyframe",
    ] = None,
    time: Annotated[
        Optional[float],
        "Time in seconds for the keyframe within the animation clip",
    ] = None,
    gameobject: Annotated[
        Optional[str],
        "GameObject identifier for keyframe operations (to determine the clip)",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Animator controllers and animation clips.

    Actions:
    - create_controller: Create a new Animator Controller asset
    - add_state: Add a new state to an existing controller
    - add_transition: Create a transition between two states
    - set_keyframe: Add a keyframe to an animation clip on a specific object
    - create_clip: Create a new Animation Clip asset
    """
    params: dict[str, Any] = {"action": action}

    if controller_path is not None:
        params["controller_path"] = controller_path
    if state_name is not None:
        params["state_name"] = state_name
    if clip_path is not None:
        params["clip_path"] = clip_path
    if property_path is not None:
        params["property_path"] = property_path
    if value is not None:
        params["value"] = value
    if time is not None:
        params["time"] = time
    if gameobject is not None:
        params["gameobject"] = gameobject

    return await execute_tool_with_contract("manage_animation", params)
