"""WebSocket hub for managing Unity plugin connections."""

from __future__ import annotations

import asyncio
import json
import logging
from typing import Any, Dict, Optional

import websockets
from websockets import WebSocketClientProtocol

from core.constants import WS_HUB_PATH, DEFAULT_HTTP_TIMEOUT

logger = logging.getLogger(__name__)


class PluginHub:
    """Manages WebSocket connections to Unity editor plugins.

    Acts as a hub that routes MCP commands to the appropriate Unity instance.
    """

    def __init__(self, base_url: str):
        self.base_url = base_url.replace("http://", "ws://").replace("https://", "wss://")
        self.hub_uri = f"{self.base_url}{WS_HUB_PATH}"
        self._socket: Optional[WebSocketClientProtocol] = None
        self._lock = asyncio.Lock()
        self._connected: bool = False
        self._connect_task: Optional[asyncio.Task] = None
        self._pending: Dict[str, asyncio.Future] = {}
        self._message_id: int = 0

    async def connect(self) -> None:
        """Connect to the Unity plugin WebSocket hub."""
        if self._connected:
            return
        async with self._lock:
            if self._connected:
                return
            try:
                self._socket = await websockets.connect(
                    self.hub_uri,
                    open_timeout=10,
                    close_timeout=5,
                )
                self._connected = True
                logger.info(f"Connected to Unity plugin hub: {self.hub_uri}")
                asyncio.create_task(self._receive_loop())
            except Exception as e:
                logger.error(f"Failed to connect to Unity hub: {e}")
                self._connected = False
                raise

    async def disconnect(self) -> None:
        """Disconnect from the Unity plugin WebSocket hub."""
        async with self._lock:
            if self._socket:
                await self._socket.close()
                self._socket = None
            self._connected = False
            # Cancel all pending
            for future in self._pending.values():
                if not future.done():
                    future.cancel()
            self._pending.clear()

    async def _receive_loop(self) -> None:
        """Continuously receive messages from Unity."""
        if not self._socket:
            return
        try:
            async for raw_message in self._socket:
                try:
                    message = json.loads(raw_message)
                    await self._handle_message(message)
                except Exception as e:
                    logger.error(f"Error handling message: {e}")
        except websockets.exceptions.ConnectionClosed:
            logger.info("Unity hub connection closed")
        except Exception as e:
            logger.error(f"Hub receive loop error: {e}")
        finally:
            self._connected = False

    async def _handle_message(self, message: Dict[str, Any]) -> None:
        """Handle an incoming message from Unity."""
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
        if not self._connected:
            await self.connect()

        async with self._lock:
            if not self._socket:
                raise RuntimeError("Not connected to Unity hub")
            self._message_id += 1
            msg_id = str(self._message_id)
            future: asyncio.Future = asyncio.get_event_loop().create_future()
            self._pending[msg_id] = future

        request = {
            "jsonrpc": "2.0",
            "id": msg_id,
            "method": command,
            "params": params,
        }
        if unity_instance:
            request["unityInstance"] = unity_instance
        if client_id:
            request["clientId"] = client_id

        await self._socket.send(json.dumps(request))

        try:
            result = await asyncio.wait_for(future, timeout=timeout)
            return result.get("result", result)
        except asyncio.TimeoutError:
            self._pending.pop(msg_id, None)
            raise TimeoutError(f"Command '{command}' timed out after {timeout}s")

    async def send_notification(
        self,
        event: str,
        data: Optional[Dict[str, Any]] = None,
    ) -> None:
        """Send a notification (no response expected)."""
        if not self._connected:
            return
        async with self._lock:
            if not self._socket:
                return
        notification = {
            "jsonrpc": "2.0",
            "method": event,
            "params": data or {},
        }
        await self._socket.send(json.dumps(notification))

    @property
    def is_connected(self) -> bool:
        return self._connected


# Global hub instance
_hub: Optional[PluginHub] = None


def get_plugin_hub() -> PluginHub:
    """Get the global plugin hub instance."""
    global _hub
    if _hub is None:
        from core.config import get_config
        cfg = get_config()
        _hub = PluginHub(cfg.http_base_url)
    return _hub


def reset_plugin_hub() -> None:
    """Reset the global plugin hub. Used for testing."""
    global _hub
    _hub = None
