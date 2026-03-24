"""Helpers for Week 2 setup and connectivity doctor output."""

from __future__ import annotations

import platform
import sys
from typing import Any


SUPPORTED_CLIENTS = {
    "claude-desktop": "Claude Desktop",
    "claude-code": "Claude Code",
    "cursor": "Cursor",
    "vscode-copilot": "VS Code Copilot",
    "other": "Other",
}


def normalize_client_name(client: str | None) -> str:
    if not client:
        return "other"
    normalized = client.strip().lower().replace("_", "-")
    return normalized if normalized in SUPPORTED_CLIENTS else "other"


def build_client_config(url: str, client: str | None = None) -> dict[str, Any]:
    _ = normalize_client_name(client)
    return {
        "mcpServers": {
            "unityMCP": {
                "url": url,
            }
        }
    }


def build_doctor_report(*, url: str, client: str | None = None, health: dict[str, Any] | None = None) -> dict[str, Any]:
    client_key = normalize_client_name(client)
    health = health or {}
    return {
        "python_version": platform.python_version(),
        "python_supported": sys.version_info >= (3, 10),
        "platform": platform.system(),
        "client": {
            "id": client_key,
            "label": SUPPORTED_CLIENTS[client_key],
        },
        "server": {
            "url": url,
            "health_checked": bool(health),
            "transport": health.get("transport"),
            "unity_connected": health.get("unity_connected"),
        },
        "next_steps": [
            "Open Unity and start the bridge from Window > Unity MCP.",
            "Paste the generated MCP config into your client settings.",
            "If the client does not discover tools, restart the MCP client once.",
        ],
    }