"""MCP tool for executing Unity menu items and searching menu paths."""

from __future__ import annotations

from typing import Annotated, Any, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Execute Unity menu items by path, get recently executed items, "
    "and search menu paths. Common paths: 'File/Save', 'Edit/Redo', 'Edit/Undo', "
    "'Edit/Redo', 'Edit/Cut', 'Edit/Copy', 'Edit/Paste', 'GameObject/Create Empty'.",
    group="core",
)
async def execute_menu_item(
    ctx: Context,
    action: Annotated[
        Literal["execute", "get_recent", "search"],
        "The menu item operation to perform",
    ],
    menu_path: Annotated[
        Optional[str],
        "Menu item path to execute or search for. "
        "Examples: 'File/Save', 'Edit/Redo', 'Assets/Create/Prefab', 'GameObject/Create Empty Child'",
    ] = None,
    search_query: Annotated[
        Optional[str],
        "Search query to find matching menu items (for 'search' action). "
        "Returns all menu paths containing this text.",
    ] = None,
) -> dict[str, Any]:
    """Execute Unity menu items or search menu paths.

    Actions:
    - execute: Execute a specific menu item by its full path
    - get_recent: Return recently executed menu items (from editor history)
    - search: Search for menu items containing the query text
    """
    params: dict[str, Any] = {"action": action}

    if menu_path is not None:
        params["menu_path"] = menu_path
    if search_query is not None:
        params["search_query"] = search_query

    result = await send_with_unity_instance(None, "execute_menu_item", params)
    return result
