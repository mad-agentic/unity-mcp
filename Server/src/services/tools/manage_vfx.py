"""MCP tool for managing Unity Visual Effects (VFX) and particle systems."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Create and manage Unity Visual Effect Graph assets and particle systems: "
    "create particle emitters, configure emission, lifetime, speed, size, and color. Group: vfx.",
    group="vfx",
)
async def manage_vfx(
    ctx: Context,
    action: Annotated[
        Literal["create_particle", "get_particle_settings", "set_parameter", "emit"],
        "The VFX operation to perform",
    ],
    name: Annotated[
        Optional[str],
        "Name for the created particle system or VFX asset",
    ] = None,
    parent: Annotated[
        Optional[str],
        "Parent GameObject identifier (name or instance ID) for the VFX",
    ] = None,
    emission_rate: Annotated[
        Optional[float],
        "Number of particles emitted per second",
    ] = None,
    lifetime: Annotated[
        Optional[float],
        "Particle lifetime in seconds (how long each particle lives)",
    ] = None,
    speed: Annotated[
        Optional[float],
        "Initial particle speed (units per second)",
    ] = None,
    size: Annotated[
        Optional[float],
        "Initial particle size multiplier",
    ] = None,
    color: Annotated[
        Optional[list[float]],
        "Particle color as [r, g, b, a] with values 0-1",
    ] = None,
    target: Annotated[
        Optional[str],
        "GameObject identifier for set_parameter/emit operations on existing systems",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Visual Effects and particle systems.

    Actions:
    - create_particle: Create a new Particle System as a child of a GameObject
    - get_particle_settings: Get current particle system configuration
    - set_parameter: Update specific particle system parameters
    - emit: Manually emit a burst of particles from a particle system
    """
    params: dict[str, Any] = {"action": action}

    if name is not None:
        params["name"] = name
    if parent is not None:
        params["parent"] = parent
    if emission_rate is not None:
        params["emission_rate"] = emission_rate
    if lifetime is not None:
        params["lifetime"] = lifetime
    if speed is not None:
        params["speed"] = speed
    if size is not None:
        params["size"] = size
    if color is not None:
        params["color"] = color
    if target is not None:
        params["target"] = target

    return await execute_tool_with_contract("manage_vfx", params)
