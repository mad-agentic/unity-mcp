#!/usr/bin/env python3
"""Main entry point for the Unity MCP server.

Supports both stdio and HTTP transport modes.
"""

from __future__ import annotations

import asyncio
import logging
import sys
import os

# Windows event loop fix for IPV4 issues
if sys.platform == "win32":
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

import click
from fastmcp import FastMCP

from core.config import get_config, reset_config
from core.constants import DEFAULT_HTTP_HOST, DEFAULT_HTTP_PORT, MCP_URI_SCHEME
from services.registry import (
    auto_discover_tools,
    auto_discover_resources,
    get_enabled_tools,
    get_all_resources,
)
from transport.plugin_hub import get_plugin_hub, reset_plugin_hub
from transport.legacy.unity_connection import get_unity_connection_pool

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[logging.StreamHandler(sys.stderr)],
)
logger = logging.getLogger("unity-mcp")


def _build_mcp_server() -> FastMCP:
    """Build and configure the FastMCP server instance."""
    cfg = get_config()
    mcp = FastMCP(
        name="Unity MCP",
        description="AI-powered Unity Editor automation via MCP",
        dependencies=[],
    )

    # Set URI scheme
    mcp._uri_scheme = MCP_URI_SCHEME

    # Auto-discover tools
    auto_discover_tools("services.tools")
    auto_discover_resources("services.resources")

    # Register all discovered tools
    for name, tool in get_enabled_tools().items():
        func = tool["func"]
        tool_mcp = FastMCP(
            name=f"unity-mcp-{name}",
            description=tool["description"],
        )
        try:
            tool_mcp.add_tool(func)
            mcp._tool_manager.add_tool(tool_mcp._tool_manager._tools[name])
        except Exception:
            pass  # Skip tools that fail to register

    return mcp


async def _run_stdio(mcp: FastMCP) -> None:
    """Run the server in stdio mode."""
    logger.info("Starting Unity MCP server in stdio mode")
    await mcp.run_stdio()


async def _run_http(mcp: FastMCP) -> None:
    """Run the server in HTTP mode with WebSocket hub."""
    cfg = get_config()
    logger.info(f"Starting Unity MCP server in HTTP mode on {cfg.http_host}:{cfg.http_port}")

    # Connect to Unity plugin hub
    hub = get_plugin_hub()
    if not cfg.skip_startup_connect:
        try:
            await hub.connect()
        except Exception as e:
            logger.warning(f"Could not connect to Unity hub on startup: {e}")
            logger.info("The Unity editor plugin will connect when it starts")

    # Run with uvicorn/FastAPI
    from fastapi import FastAPI
    from starlette.routing import WebSocketRoute, Route
    from starlette.responses import JSONResponse
    import uvicorn

    app = FastAPI(title="Unity MCP Server", version="0.1.0")

    # Health check
    @app.get("/health")
    async def health():
        return {"status": "ok", "transport": "http"}

    # MCP endpoint
    @app.post("/mcp")
    async def mcp_endpoint(request: dict):
        # Handle MCP JSON-RPC requests
        method = request.get("method", "")
        params = request.get("params", {})
        req_id = request.get("id")

        # Route tool calls through hub
        if method.startswith("tools/"):
            tool_name = method.replace("tools/", "")
            result = await hub.send_command(tool_name, params, timeout=30.0)
            return JSONResponse({"jsonrpc": "2.0", "id": req_id, "result": result})

        return JSONResponse(
            {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": "Method not found"}}
        )

    # WebSocket hub endpoint
    @app.websocket("/hub/plugin")
    async def websocket_hub(websocket):
        """WebSocket endpoint for Unity plugin connections."""
        await websocket.accept()
        try:
            async for message in websocket:
                # Forward to hub receive loop
                pass
        except Exception:
            pass

    config = uvicorn.Config(
        app,
        host=cfg.http_host,
        port=cfg.http_port,
        log_level="info",
    )
    server = uvicorn.Server(config)
    await server.serve()


@click.command()
@click.option(
    "--transport",
    type=click.Choice(["stdio", "http"], case_sensitive=False),
    default=None,
    help="Transport protocol (default: stdio)",
)
@click.option("--http-host", default=None, help="HTTP bind host")
@click.option("--http-port", type=int, default=None, help="HTTP bind port")
@click.option("--http-url", default=None, help="HTTP base URL")
@click.option(
    "--http-remote-hosted",
    is_flag=True,
    default=None,
    help="Enable remote-hosted HTTP mode with API key auth",
)
@click.option("--default-instance", default=None, help="Default Unity instance to target")
@click.option("--project-scoped-tools", is_flag=True, default=None, help="Enable project-scoped custom tools")
@click.option("--dev-mode", is_flag=True, default=None, help="Enable development mode")
def main(
    transport: str | None,
    http_host: str | None,
    http_port: int | None,
    http_url: str | None,
    http_remote_hosted: bool | None,
    default_instance: str | None,
    project_scoped_tools: bool | None,
    dev_mode: bool | None,
) -> None:
    """Unity MCP Server — Bridge AI assistants with the Unity Editor."""
    # Reset config to pick up CLI args
    reset_config()
    cfg = get_config()

    # Override from CLI args
    if transport:
        cfg.transport_mode = transport
    if http_host:
        cfg.http_host = http_host
    if http_port:
        cfg.http_port = http_port
    if http_url:
        cfg.http_url = http_url
    if http_remote_hosted is not None:
        cfg.http_remote_hosted = http_remote_hosted
    if default_instance:
        cfg.default_instance = default_instance
    if project_scoped_tools is not None:
        cfg.project_scoped_tools = project_scoped_tools
    if dev_mode is not None:
        cfg.dev_mode = dev_mode

    logger.info(f"Unity MCP Server v0.1.0 starting (transport={cfg.transport_mode})")

    # Build MCP server
    mcp = _build_mcp_server()

    # Write PID file if configured
    if cfg.pidfile:
        with open(cfg.pidfile, "w") as f:
            f.write(str(os.getpid()))
        logger.info(f"Wrote PID {os.getpid()} to {cfg.pidfile}")

    # Run in appropriate mode
    if cfg.transport_mode.lower() == "http":
        asyncio.run(_run_http(mcp))
    else:
        asyncio.run(_run_stdio(mcp))


if __name__ == "__main__":
    main()
