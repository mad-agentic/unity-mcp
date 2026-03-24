"""MCP tool for managing Unity cameras: list, get, create, configure, and set camera parameters."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Manage Unity cameras: list all cameras, get camera properties, create cameras, "
    "set transform, background color, orthographic/perspective mode, FOV, depth, and culling mask.",
    group="core",
)
async def manage_camera(
    ctx: Context,
    action: Annotated[
        Literal["list", "get", "create", "set_transform", "set_background", "add_component", "set_orthographic"],
        "The camera operation to perform",
    ],
    camera_name: Annotated[
        Optional[str],
        "Name for a new camera or identifier for existing camera operations",
    ] = None,
    is_orthographic: Annotated[
        Optional[bool],
        "Whether the camera should be orthographic (true) or perspective (false)",
    ] = None,
    fov: Annotated[
        Optional[float],
        "Field of view in degrees for perspective cameras",
    ] = None,
    orthographic_size: Annotated[
        Optional[float],
        "Orthographic size (half-height) for orthographic cameras",
    ] = None,
    background_color: Annotated[
        Optional[list[float]],
        "Background color as [r, g, b, a] with values 0-1",
    ] = None,
    depth: Annotated[
        Optional[float],
        "Camera depth/priority in the rendering order",
    ] = None,
    culling_mask: Annotated[
        Optional[int],
        "Culling mask as a layer bitmask (e.g. -1 for everything, 1<<0 for Default, 1<<8 for UI)",
    ] = None,
    target: Annotated[
        Optional[str],
        "GameObject identifier for set operations on existing cameras",
    ] = None,
    position: Annotated[
        Optional[list[float]],
        "Camera position [x, y, z] for create or set_transform actions",
    ] = None,
    rotation: Annotated[
        Optional[list[float]],
        "Camera rotation [x, y, z] Euler angles for set_transform action",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity Camera components.

    Actions:
    - list: List all cameras in the current scene
    - get: Get properties of a specific camera
    - create: Create a new camera GameObject
    - set_transform: Set camera position and rotation
    - set_background: Set camera background color
    - add_component: Add a Camera component (or additional camera-specific component)
    - set_orthographic: Switch between orthographic and perspective mode
    """
    params: dict[str, Any] = {"action": action}

    if camera_name is not None:
        params["camera_name"] = camera_name
    if is_orthographic is not None:
        params["is_orthographic"] = is_orthographic
    if fov is not None:
        params["fov"] = fov
    if orthographic_size is not None:
        params["orthographic_size"] = orthographic_size
    if background_color is not None:
        params["background_color"] = background_color
    if depth is not None:
        params["depth"] = depth
    if culling_mask is not None:
        params["culling_mask"] = culling_mask
    if target is not None:
        params["target"] = target
    if position is not None:
        params["position"] = position
    if rotation is not None:
        params["rotation"] = rotation

    result = await send_with_unity_instance(None, "manage_camera", params)
    return result
