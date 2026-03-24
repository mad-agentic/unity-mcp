"""Global configuration for the Unity MCP server.

Loads from environment variables and CLI arguments.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from typing import Optional

from core.constants import (
    DEFAULT_HTTP_HOST,
    DEFAULT_HTTP_PORT,
    DEFAULT_HTTP_URL,
    DEFAULT_TRANSPORT,
    DEFAULT_ENABLED_GROUPS,
    MCP_URI_SCHEME,
)


@dataclass
class Config:
    """Global configuration state."""

    # Transport mode: "stdio" or "http"
    transport_mode: str = DEFAULT_TRANSPORT

    # HTTP server settings
    http_url: str = DEFAULT_HTTP_URL
    http_host: str = DEFAULT_HTTP_HOST
    http_port: int = DEFAULT_HTTP_PORT
    http_remote_hosted: bool = False

    # API key auth (remote-hosted mode)
    api_key_validation_url: Optional[str] = None
    api_key_login_url: Optional[str] = None
    api_key_cache_ttl: int = 300
    api_key_service_token_header: Optional[str] = None
    api_key_service_token: Optional[str] = None

    # Default Unity instance
    default_instance: Optional[str] = None

    # Tool groups
    enabled_tool_groups: set[str] = field(default_factory=lambda: DEFAULT_ENABLED_GROUPS.copy())

    # MCP settings
    mcp_uri_scheme: str = MCP_URI_SCHEME

    # Project-scoped tools
    project_scoped_tools: bool = False

    # Server source override (local development)
    server_source_override: Optional[str] = None

    # PID file
    pidfile: Optional[str] = None

    # Skip startup Unity connection
    skip_startup_connect: bool = False

    # Telemetry
    disable_telemetry: bool = False
    telemetry_endpoint: Optional[str] = None
    telemetry_timeout: float = 5.0

    # Dev mode
    dev_mode: bool = False

    @property
    def http_base_url(self) -> str:
        return f"http://{self.http_host}:{self.http_port}"


# Global config instance
_config: Optional[Config] = None


def get_config() -> Config:
    """Get the global config instance, creating it if needed."""
    global _config
    if _config is None:
        _config = _load_config()
    return _config


def _load_config() -> Config:
    """Load configuration from environment variables."""
    cfg = Config()

    # Transport
    cfg.transport_mode = os.getenv("UNITY_MCP_TRANSPORT", DEFAULT_TRANSPORT).lower()
    cfg.http_url = os.getenv("UNITY_MCP_HTTP_URL", DEFAULT_HTTP_URL)
    cfg.http_host = os.getenv("UNITY_MCP_HTTP_HOST", DEFAULT_HTTP_HOST)
    port_env = os.getenv("UNITY_MCP_HTTP_PORT")
    if port_env:
        cfg.http_port = int(port_env)

    # Remote hosted
    remoteHosted = os.getenv("UNITY_MCP_HTTP_REMOTE_HOSTED", "").lower()
    cfg.http_remote_hosted = remoteHosted in ("true", "1", "yes", "on")

    # API key
    cfg.api_key_validation_url = os.getenv("UNITY_MCP_API_KEY_VALIDATION_URL")
    cfg.api_key_login_url = os.getenv("UNITY_MCP_API_KEY_LOGIN_URL")
    ttl_env = os.getenv("UNITY_MCP_API_KEY_CACHE_TTL")
    if ttl_env:
        cfg.api_key_cache_ttl = int(ttl_env)
    cfg.api_key_service_token_header = os.getenv("UNITY_MCP_API_KEY_SERVICE_TOKEN_HEADER")
    cfg.api_key_service_token = os.getenv("UNITY_MCP_API_KEY_SERVICE_TOKEN")

    # Default instance
    cfg.default_instance = os.getenv("UNITY_MCP_DEFAULT_INSTANCE")

    # Project scoped
    projectScoped = os.getenv("UNITY_MCP_PROJECT_SCOPED_TOOLS", "").lower()
    cfg.project_scoped_tools = projectScoped in ("true", "1", "yes", "on")

    # Server source override
    cfg.server_source_override = os.getenv("UNITY_MCP_SERVER_SOURCE_OVERRIDE")

    # PID file
    cfg.pidfile = os.getenv("UNITY_MCP_PIDFILE")

    # Skip startup
    cfg.skip_startup_connect = os.getenv("UNITY_MCP_SKIP_STARTUP_CONNECT", "0") in ("1", "true", "yes")

    # Telemetry
    telemetry_disabled = (
        os.getenv("DISABLE_TELEMETRY", "0") in ("1", "true", "yes") or
        os.getenv("UNITY_MCP_DISABLE_TELEMETRY", "0") in ("1", "true", "yes") or
        os.getenv("MCP_DISABLE_TELEMETRY", "0") in ("1", "true", "yes")
    )
    cfg.disable_telemetry = telemetry_disabled
    cfg.telemetry_endpoint = os.getenv("UNITY_MCP_TELEMETRY_ENDPOINT")
    timeout_env = os.getenv("UNITY_MCP_TELEMETRY_TIMEOUT")
    if timeout_env:
        cfg.telemetry_timeout = float(timeout_env)

    # Dev mode
    cfg.dev_mode = os.getenv("UNITY_MCP_DEV_MODE", "0") in ("1", "true", "yes")

    return cfg


def reset_config() -> None:
    """Reset the global config. Used for testing."""
    global _config
    _config = None
