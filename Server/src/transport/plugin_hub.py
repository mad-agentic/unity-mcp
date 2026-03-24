"""WebSocket hub for managing Unity plugin connections.

The Python server accepts incoming WebSocket connections from Unity.
Unity connects to ws://{host}:{port}/hub/plugin and registers itself.
"""

from __future__ import annotations

import asyncio
import json
import logging
from typing import Any, Dict, Optional

from core.constants import DEFAULT_HTTP_TIMEOUT

logger = logging.getLogger(__name__)


class PluginHub:
    """Manages WebSocket connections from Unity editor plugins.

    Unity connects to this hub's WebSocket endpoint, registers itself,
    and listens for JSON-RPC commands which it processes and returns responses.
    """

    def __init__(self) -> None:
        self._websocket: Any = None  # starlette WebSocket
        self._lock = asyncio.Lock()
        self._connected: bool = False
        self._pending: Dict[str, asyncio.Future] = {}
        self._message_id: int = 0
        self._unity_info: Dict[str, Any] = {}

    async def handle_connection(self, websocket: Any) -> None:
        """Accept and manage an incoming Unity WebSocket connection.

        This is called by the /hub/plugin WebSocket endpoint when Unity connects.
        Blocks until the connection is closed.
        """
        async with self._lock:
            if self._connected and self._websocket is not None:
                logger.warning("New Unity connection replacing existing one")
                try:
                    await self._websocket.close()
                except Exception:
                    pass
            self._websocket = websocket
            self._connected = True

        logger.info("Unity plugin connected to hub")

        try:
            while True:
                data = await websocket.receive_text()
                try:
                    message = json.loads(data)
                    await self._handle_message(message)
                except Exception as e:
                    logger.error(f"Error handling Unity message: {e}")
        except Exception:
            pass
        finally:
            async with self._lock:
                if self._websocket is websocket:
                    self._websocket = None
                    self._connected = False
            for future in list(self._pending.values()):
                if not future.done():
                    future.cancel()
            self._pending.clear()
            logger.info("Unity plugin disconnected from hub")

    async def _handle_message(self, message: Dict[str, Any]) -> None:
        """Handle an incoming message from Unity (response to a command or registration)."""
        msg_type = message.get("type")
        if msg_type == "register":
            self._unity_info = message.get("info", {})
            logger.info(f"Unity registered: {self._unity_info.get('name', 'unknown')}")
            return

        msg_id = message.get("id")
        if msg_id and msg_id in self._pending:
            future = self._pending.pop(msg_id)
            if not future.done():
                future.set_result(message)

    async def send_command(
        self,
        command: str,
        params: Dict[str, Any],
        unity_instance: Optional[str] = None,
        client_id: Optional[str] = None,
        timeout: float = DEFAULT_HTTP_TIMEOUT,
    ) -> Any:
        """Send a command to Unity and wait for response."""
        if not self._connected or self._websocket is None:
            raise RuntimeError(
                "Unity Editor is not connected. Open Unity and start the MCP bridge "
                "via Window > Unity MCP > Start Server."
            )

        async with self._lock:
            self._message_id += 1
            msg_id = str(self._message_id)
            future: asyncio.Future = asyncio.get_event_loop().create_future()
            self._pending[msg_id] = future

        request: Dict[str, Any] = {
            "jsonrpc": "2.0",
            "id": msg_id,
            "method": command,
            "params": params,
        }
        if unity_instance:
            request["unityInstance"] = unity_instance
        if client_id:
            request["clientId"] = client_id

        await self._websocket.send_text(json.dumps(request))

        try:
            result = await asyncio.wait_for(future, timeout=timeout)
            return result.get("result", result)
        except asyncio.TimeoutError:
            self._pending.pop(msg_id, None)
            raise TimeoutError(f"Command '{command}' timed out after {timeout}s")

    @property
    def is_connected(self) -> bool:
        return self._connected

    @property
    def unity_info(self) -> Dict[str, Any]:
        return self._unity_info


# Global hub instance
_hub: Optional[PluginHub] = None


def get_plugin_hub() -> PluginHub:
    """Get the global plugin hub instance."""
    global _hub
    if _hub is None:
        _hub = PluginHub()
    return _hub


def reset_plugin_hub() -> None:
    """Reset the global plugin hub. Used for testing."""
    global _hub
    _hub = None
