"""MCP tool for refreshing Unity's asset database and recompiling scripts."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


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

    result = await send_with_unity_instance(None, "refresh_unity", params)
    return result
