"""MCP tool for managing Unity packages: list installed, install, remove, embed, and query registry."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Manage Unity packages: list installed packages, install new packages by name/version "
    "or Git URL, remove packages, embed local packages, and query the package registry.",
    group="core",
)
async def manage_packages(
    ctx: Context,
    action: Annotated[
        Literal["list", "install", "remove", "embed", "list_registry"],
        "The package management operation to perform",
    ],
    package_name: Annotated[
        Optional[str],
        "Package name to install, remove, or query "
        "(e.g. 'com.unity.postprocessing', 'com.unity.cinemachine')",
    ] = None,
    version: Annotated[
        Optional[str],
        "Specific version to install (e.g. '1.0.0', '1.2.3-preview.1'). "
        "Defaults to latest compatible version.",
    ] = None,
    registry_url: Annotated[
        Optional[str],
        "Custom npm registry URL for scoped packages",
    ] = None,
    git_url: Annotated[
        Optional[str],
        "Git repository URL for installing packages directly from GitHub/GitLab "
        "(e.g. 'https://github.com/Unity-Technologies/Samples.git')",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity packages via the Package Manager.

    Actions:
    - list: List all currently installed packages
    - install: Install a package by name, version, or Git URL
    - remove: Remove an installed package
    - embed: Embed a local package (make it editable)
    - list_registry: List packages from the Unity package registry (search)
    """
    params: dict[str, Any] = {"action": action}

    if package_name is not None:
        params["package_name"] = package_name
    if version is not None:
        params["version"] = version
    if registry_url is not None:
        params["registry_url"] = registry_url
    if git_url is not None:
        params["git_url"] = git_url

    return await execute_tool_with_contract("manage_packages", params)
