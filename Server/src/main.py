#!/usr/bin/env python3
"""Main entry point for the Unity MCP server.

Supports both stdio and HTTP transport modes.
"""

from __future__ import annotations

import asyncio
import json
import logging
import sys
import os
import warnings

# Suppress websockets deprecation warnings
warnings.filterwarnings("ignore", category=DeprecationWarning, module="websockets")

# Windows event loop fix for IPV4 issues
if sys.platform == "win32":
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

import click
import httpx
from fastmcp import FastMCP

from cli.utils.bootstrap import (
    build_bootstrap_report,
    write_client_config,
    sync_skills_for_client,
)
from cli.utils.setup_doctor import build_client_config, build_doctor_report
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
        instructions="AI-powered Unity Editor automation via MCP",
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
            instructions=tool["description"],
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

    from fastapi import FastAPI, WebSocket
    from starlette.routing import WebSocketRoute
    import uvicorn

    # Get FastMCP's native ASGI app — handles full MCP protocol (initialize, tools/list, tools/call, etc.)
    mcp_asgi = mcp.http_app(transport="streamable-http")

    # Build outer FastAPI app that hosts both MCP and the Unity WebSocket hub
    app = FastAPI(title="Unity MCP Server", version="0.1.0")

    # Unity WebSocket hub — Unity editor connects here and listens for commands
    hub = get_plugin_hub()

    async def websocket_hub(websocket: WebSocket):
        logger.info("Incoming websocket request: /hub/plugin")
        await websocket.accept()
        logger.info("Accepted websocket request: /hub/plugin")
        await hub.handle_connection(websocket)

    app.router.routes.append(WebSocketRoute("/hub/plugin", websocket_hub))

    # Health check
    @app.get("/health")
    async def health():
        return {"status": "ok", "transport": "streamable-http", "unity_connected": hub.is_connected}

    # Mount FastMCP at /mcp — VS Code Copilot and other MCP clients connect here
    app.mount("/mcp", mcp_asgi)

    try:
        route_dump: list[str] = []
        for route in app.routes:
            path = getattr(route, "path", "<no-path>")
            name = getattr(route, "name", route.__class__.__name__)
            route_dump.append(f"{route.__class__.__name__}:{path}:{name}")
        logger.info("HTTP app routes: %s", " | ".join(route_dump))
    except Exception as exc:  # noqa: BLE001
        logger.warning("Failed to dump route table: %s", exc)

    for route in app.routes:
        route_path = getattr(route, "path", "<unknown>")
        route_name = getattr(route, "name", "<unnamed>")
        logger.info("Route registered: %s (%s)", route_path, route_name)

    config = uvicorn.Config(
        app,
        host=cfg.http_host,
        port=cfg.http_port,
        log_level="debug",
    )
    server = uvicorn.Server(config)
    await server.serve()


async def _fetch_health(base_url: str) -> dict[str, object]:
    """Fetch server health JSON for doctor mode."""
    async with httpx.AsyncClient(timeout=3.0) as client:
        response = await client.get(f"{base_url.rstrip('/')}/health")
        response.raise_for_status()
        payload = response.json()
        return payload if isinstance(payload, dict) else {}


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
@click.option("--doctor", is_flag=True, default=False, help="Run setup doctor and print client config instead of starting the server")
@click.option("--bootstrap", is_flag=True, default=False, help="Run Week 4 bootstrap scope: env checks + config generation + optional health check")
@click.option("--client", default="other", help="Target MCP client for doctor output (claude-desktop, claude-code, cursor, vscode-copilot, other)")
@click.option("--check-connection", is_flag=True, default=False, help="In doctor mode, check the configured HTTP health endpoint")
@click.option("--write-config", is_flag=True, default=False, help="Write generated client config to disk")
@click.option("--config-output", default=None, help="Optional output path for generated client config JSON")
@click.option("--sync-skills", is_flag=True, default=False, help="Generate per-client skills snapshot and mismatch report")
@click.option("--skills-manifest", default=None, help="Optional manifest path for skill sync input")
@click.option("--skills-output", default=None, help="Optional output path for synced skills JSON")
@click.option("--expected-skill-version", default=None, help="Optional expected runtime version for skill sync mismatch warning")
def main(
    transport: str | None,
    http_host: str | None,
    http_port: int | None,
    http_url: str | None,
    http_remote_hosted: bool | None,
    default_instance: str | None,
    project_scoped_tools: bool | None,
    dev_mode: bool | None,
    doctor: bool,
    bootstrap: bool,
    client: str,
    check_connection: bool,
    write_config: bool,
    config_output: str | None,
    sync_skills: bool,
    skills_manifest: str | None,
    skills_output: str | None,
    expected_skill_version: str | None,
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

    if doctor:
        base_url = cfg.http_url.rstrip("/") if cfg.http_url else cfg.http_base_url
        if not base_url.startswith("http://") and not base_url.startswith("https://"):
            base_url = cfg.http_base_url
        endpoint_url = f"{base_url.rstrip('/')}/mcp"
        health_payload: dict[str, object] = {}

        if check_connection:
            try:
                health_payload = asyncio.run(_fetch_health(base_url))
            except Exception as exc:  # noqa: BLE001
                health_payload = {"status": "unreachable", "error": str(exc)}

        report = build_doctor_report(url=endpoint_url, client=client, health=health_payload)
        click.echo(json.dumps(report, indent=2))
        click.echo("")
        click.echo(json.dumps(build_client_config(endpoint_url, client), indent=2))
        return

    if bootstrap:
        base_url = cfg.http_url.rstrip("/") if cfg.http_url else cfg.http_base_url
        if not base_url.startswith("http://") and not base_url.startswith("https://"):
            base_url = cfg.http_base_url
        endpoint_url = f"{base_url.rstrip('/')}/mcp"

        health_payload: dict[str, object] = {}
        if check_connection:
            try:
                health_payload = asyncio.run(_fetch_health(base_url))
            except Exception as exc:  # noqa: BLE001
                health_payload = {"status": "unreachable", "error": str(exc)}

        report = build_bootstrap_report(
            endpoint_url=endpoint_url,
            client=client,
            health=health_payload,
        )

        config_payload = report.get("config") if isinstance(report.get("config"), dict) else build_client_config(endpoint_url, client)
        if write_config:
            path = write_client_config(config_payload, config_output)
            report.setdefault("artifacts", {})["client_config_path"] = path

        if sync_skills:
            sync_result = sync_skills_for_client(
                client=client,
                endpoint_url=endpoint_url,
                manifest_path=skills_manifest,
                output_path=skills_output,
                expected_version=expected_skill_version,
            )
            report.setdefault("artifacts", {})["skill_sync"] = sync_result

        click.echo(json.dumps(report, indent=2))
        return

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
