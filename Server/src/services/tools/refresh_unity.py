"""MCP tool for refreshing Unity's asset database and recompiling scripts."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Refresh Unity's asset database, recompile scripts, and refresh import settings. "
    "This is a long-running async operation that may take time depending on project size.",
    group="core",
)
async def refresh_unity(
    ctx: Context,
    action: Annotated[
        Literal["refresh", "recompile", "import_settings"],
        "The refresh operation to perform",
    ],
    path: Annotated[
        Optional[str],
        "Optional asset path for import_settings action (e.g. 'Assets/Textures/mytex.png'). "
        "If omitted, refreshes all import settings.",
    ] = None,
) -> dict[str, Any]:
    """Refresh Unity's asset database or recompile scripts.

    This tool triggers editor operations that may take significant time to complete.
    The operation runs asynchronously and returns immediately while Unity processes in the background.

    Actions:
    - refresh: Full asset database refresh (equivalent to AssetDatabase.Refresh)
    - recompile: Force recompilation of all scripts
    - import_settings: Reimport specific asset or all assets if path is omitted
    """
    params: dict[str, Any] = {"action": action}

    if path is not None:
        params["path"] = path

    return await execute_tool_with_contract("refresh_unity", params)
