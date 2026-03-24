"""MCP tool for managing the Unity Editor: play mode, pause, stop, step, selection, and window focus."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Manage Unity Editor state: play, pause, stop, step, get play mode status, "
    "get/set selection, focus windows, and execute menu items.",
    group="core",
)
async def manage_editor(
    ctx: Context,
    action: Annotated[
        Literal[
            "play",
            "pause",
            "stop",
            "step",
            "is_playing",
            "set_playmode_toggle",
            "get_selection",
            "set_selection",
            "focus_window",
            "execute_menu_item",
        ],
        "The editor operation to perform",
    ],
    gameobjects: Annotated[
        Optional[list[str]],
        "List of GameObject identifiers (names or instance IDs) for selection operations",
    ] = None,
    window_type: Annotated[
        Optional[str],
        "EditorWindow class name to focus (e.g. 'SceneView', 'GameView', 'ProjectBrowser')",
    ] = None,
    menu_path: Annotated[
        Optional[str],
        "Menu item path for execute_menu_item action (e.g. 'File/Save', 'Edit/Redo')",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Editor state and operations.

    Actions:
    - play: Start play mode
    - pause: Pause play mode
    - stop: Stop play mode
    - step: Advance one frame in play mode
    - is_playing: Check if currently in play mode and paused state
    - set_playmode_toggle: Enable/disable the play mode button
    - get_selection: Get currently selected objects in the editor
    - set_selection: Set the active selection in the editor
    - focus_window: Focus an EditorWindow by type name
    - execute_menu_item: Execute a Unity menu item by path
    """
    params: dict[str, Any] = {"action": action}

    if gameobjects is not None:
        params["gameobjects"] = gameobjects
    if window_type is not None:
        params["window_type"] = window_type
    if menu_path is not None:
        params["menu_path"] = menu_path

    result = await send_with_unity_instance(None, "manage_editor", params)
    return result
