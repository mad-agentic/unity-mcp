"""Placeholder ping tool to verify server connectivity."""

from __future__ import annotations

from typing import Any

from fastmcp import Context

from services.registry import mcp_for_unity_tool


@mcp_for_unity_tool(
    description="Ping the Unity MCP server to verify connectivity. Returns server info.",
    group="core",
)
async def ping(ctx: Context) -> dict[str, Any]:
    """Ping the server and return status info."""
    return {
        "status": "ok",
        "message": "Unity MCP Server is running",
        "version": "0.1.0",
    }
