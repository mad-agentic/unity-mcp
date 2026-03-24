"""Transport helpers for routing commands to Unity."""

from __future__ import annotations

import logging
from typing import Any, Awaitable, Callable, Optional, TypeVar

from core.config import get_config

logger = logging.getLogger(__name__)
T = TypeVar("T")


def _is_http_transport() -> bool:
    return get_config().transport_mode.lower() == "http"


async def send_with_unity_instance(
    send_fn: Callable[..., Awaitable[T]],
    unity_instance: Optional[str],
    *args,
    user_id: Optional[str] = None,
    **kwargs,
) -> Any:
    """Route a command to a Unity instance.

    In HTTP mode, this routes via the WebSocket hub.
    In stdio mode, this routes via the legacy TCP bridge.
    """
    if _is_http_transport():
        if not args:
            raise ValueError("HTTP transport requires command arguments")
        command_type = args[0]
        params = args[1] if len(args) > 1 else kwargs.get("params")
        if params is None:
            params = {}
        if not isinstance(params, dict):
            raise TypeError("Command parameters must be a dict for HTTP transport")

        # Import here to avoid circular imports
        from transport.plugin_hub import get_plugin_hub
        hub = get_plugin_hub()
        response = await hub.send_command(
            command_type,
            params,
            unity_instance,
            client_id=user_id,
        )
        return response
    else:
        # Legacy stdio transport
        from transport.legacy.unity_connection import async_send_command_with_retry
        response = await async_send_command_with_retry(
            unity_instance, *args, **kwargs
        )
        return response


def get_unity_instance_from_context_sync() -> Optional[str]:
    """Get the default Unity instance name synchronously.

    Used in stdio mode where context isn't available.
    """
    cfg = get_config()
    if cfg.default_instance:
        return cfg.default_instance
    return None
