"""Legacy stdio transport for Unity connection.

Used when transport_mode is "stdio". Communicates with Unity via
stdin/stdout JSON-RPC messages through a TCP-like bridge.
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import sys
from typing import Any, Dict, Optional

from core.constants import DEFAULT_MAX_RETRIES, DEFAULT_RETRY_DELAY

logger = logging.getLogger(__name__)

# Global connection pool (for stdio mode)
_connection_pool: Optional["UnityConnectionPool"] = None


class UnityConnectionPool:
    """Manages connections to Unity editor instances (stdio mode)."""

    def __init__(self):
        self._instances: Dict[str, "UnityInstance"] = {}
        self._lock = asyncio.Lock()

    async def get_instance(self, instance_name: Optional[str]) -> "UnityInstance":
        """Get or create a connection to a Unity instance."""
        async with self._lock:
            if not instance_name:
                instance_name = "__default__"
            if instance_name not in self._instances:
                self._instances[instance_name] = UnityInstance(instance_name)
            return self._instances[instance_name]

    async def remove_instance(self, instance_name: str) -> None:
        """Remove an instance from the pool."""
        async with self._lock:
            if instance_name in self._instances:
                await self._instances[instance_name].close()
                del self._instances[instance_name]

    def list_instances(self) -> list[str]:
        """List all connected instances."""
        return list(self._instances.keys())


class UnityInstance:
    """A connection to a Unity editor instance via stdio."""

    def __init__(self, name: str):
        self.name = name
        self._connected = False
        self._reader: Optional[asyncio.StreamReader] = None
        self._writer: Optional[asyncio.StreamWriter] = None

    async def connect(self) -> None:
        """Establish connection to Unity via stdio."""
        if self._connected:
            return
        # In stdio mode, Unity communicates via stdin/stdout
        # This is handled by the process management layer
        self._connected = True
        logger.info(f"Unity instance '{self.name}' connected (stdio)")

    async def send_command(
        self,
        command: str,
        params: Dict[str, Any],
        timeout: float = 30.0,
    ) -> Any:
        """Send a command to Unity via stdio."""
        if not self._connected:
            await self.connect()

        request = {
            "jsonrpc": "2.0",
            "id": os.urandom(8).hex(),
            "method": command,
            "params": params,
        }

        # Write to stdout
        line = json.dumps(request) + "\n"
        sys.stdout.write(line)
        sys.stdout.flush()

        # Read from stdin (blocking read simulation)
        loop = asyncio.get_event_loop()
        try:
            line = await asyncio.wait_for(
                loop.run_in_executor(None, sys.stdin.readline),
                timeout=timeout,
            )
        except asyncio.TimeoutError:
            raise TimeoutError(f"Command '{command}' timed out")

        if not line:
            raise EOFError("Unity process closed stdin")

        response = json.loads(line.strip())
        if "error" in response:
            raise RuntimeError(f"Unity error: {response['error']}")
        return response.get("result")

    async def close(self) -> None:
        """Close the connection."""
        self._connected = False
        if self._writer:
            self._writer.close()
            await self._writer.wait_closed()


def get_unity_connection_pool() -> UnityConnectionPool:
    """Get the global connection pool."""
    global _connection_pool
    if _connection_pool is None:
        _connection_pool = UnityConnectionPool()
    return _connection_pool


async def async_send_command_with_retry(
    instance_name: Optional[str],
    command: str,
    params: Optional[Dict[str, Any]] = None,
    max_retries: int = DEFAULT_MAX_RETRIES,
    retry_delay: float = DEFAULT_RETRY_DELAY,
) -> Any:
    """Send a command to Unity with automatic retry on failure."""
    pool = get_unity_connection_pool()
    instance = await pool.get_instance(instance_name)

    last_error: Optional[Exception] = None
    for attempt in range(max_retries):
        try:
            return await instance.send_command(command, params or {})
        except Exception as e:
            last_error = e
            logger.warning(
                f"Command '{command}' failed (attempt {attempt + 1}/{max_retries}): {e}"
            )
            if attempt < max_retries - 1:
                await asyncio.sleep(retry_delay * (attempt + 1))

    raise RuntimeError(
        f"Command '{command}' failed after {max_retries} attempts"
    ) from last_error
