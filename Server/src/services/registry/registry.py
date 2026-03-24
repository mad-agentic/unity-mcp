"""Tool and resource registry with auto-discovery decorators."""

from __future__ import annotations

import importlib
import logging
import pkgutil
import warnings
from pathlib import Path
from typing import Any, Callable, Optional

# Suppress websockets deprecation warnings
warnings.filterwarnings("ignore", category=DeprecationWarning, module="websockets")

from core.constants import (
    TOOL_GROUP_CORE,
    ALL_TOOL_GROUPS,
    DEFAULT_TOOL_MATURITY,
    ALL_TOOL_MATURITY_LEVELS,
)

logger = logging.getLogger(__name__)


# Storage for registered tools and resources
_registered_tools: dict[str, dict[str, Any]] = {}
_registered_resources: dict[str, dict[str, Any]] = {}
_enabled_groups: set[str] = {TOOL_GROUP_CORE}


def mcp_for_unity_tool(
    *,
    description: str,
    group: str = TOOL_GROUP_CORE,
    maturity: str = DEFAULT_TOOL_MATURITY,
    annotations: Optional[dict[str, Any]] = None,
) -> Callable:
    """Decorator to register an MCP tool.

    Args:
        description: Human-readable description of the tool
        group: Tool group (core, vfx, animation, ui, etc.)
        maturity: Tool maturity level (core, advanced, experimental)
        annotations: MCP tool annotations (title, destructiveHint, etc.)
    """
    def decorator(func: Callable) -> Callable:
        tool_name = func.__name__
        normalized_maturity = maturity if maturity in ALL_TOOL_MATURITY_LEVELS else DEFAULT_TOOL_MATURITY
        _registered_tools[tool_name] = {
            "name": tool_name,
            "description": description,
            "group": group,
            "maturity": normalized_maturity,
            "annotations": annotations or {},
            "func": func,
        }
        logger.debug(
            f"Registered tool: {tool_name} (group={group}, maturity={normalized_maturity})"
        )
        return func
    return decorator


def mcp_for_unity_resource(
    *,
    uri: str,
    description: str = "",
    mime_type: str = "application/json",
) -> Callable:
    """Decorator to register an MCP resource.

    Args:
        uri: Resource URI (e.g., "unitymcp://editor/state")
        description: Human-readable description
        mime_type: MIME type of the resource content
    """
    def decorator(func: Callable) -> Callable:
        resource_name = func.__name__
        _registered_resources[resource_name] = {
            "uri": uri,
            "name": resource_name,
            "description": description,
            "mimeType": mime_type,
            "func": func,
        }
        logger.debug(f"Registered resource: {resource_name} (uri={uri})")
        return func
    return decorator


def get_tool(name: str) -> Optional[dict[str, Any]]:
    """Get a registered tool by name."""
    return _registered_tools.get(name)


def get_all_tools() -> dict[str, dict[str, Any]]:
    """Get all registered tools."""
    return _registered_tools.copy()


def get_tools_for_group(group: str) -> dict[str, dict[str, Any]]:
    """Get all registered tools in a specific group."""
    return {
        name: tool for name, tool in _registered_tools.items()
        if tool["group"] == group
    }


def get_enabled_tools() -> dict[str, dict[str, Any]]:
    """Get all registered tools in enabled groups."""
    return {
        name: tool for name, tool in _registered_tools.items()
        if tool["group"] in _enabled_groups
    }


def get_tool_taxonomy() -> dict[str, Any]:
    """Get current tool taxonomy snapshot grouped by group and maturity."""
    by_group: dict[str, list[str]] = {}
    by_maturity: dict[str, list[str]] = {}

    for name, tool in _registered_tools.items():
        group = tool.get("group", TOOL_GROUP_CORE)
        maturity = tool.get("maturity", DEFAULT_TOOL_MATURITY)

        by_group.setdefault(group, []).append(name)
        by_maturity.setdefault(maturity, []).append(name)

    return {
        "total_tools": len(_registered_tools),
        "enabled_groups": sorted(_enabled_groups),
        "by_group": {k: sorted(v) for k, v in sorted(by_group.items())},
        "by_maturity": {k: sorted(v) for k, v in sorted(by_maturity.items())},
    }


def get_resource(name: str) -> Optional[dict[str, Any]]:
    """Get a registered resource by name."""
    return _registered_resources.get(name)


def get_all_resources() -> dict[str, dict[str, Any]]:
    """Get all registered resources."""
    return _registered_resources.copy()


def get_all_groups() -> set[str]:
    """Get all known tool groups."""
    groups = {TOOL_GROUP_CORE}
    for tool in _registered_tools.values():
        groups.add(tool["group"])
    return groups


def get_enabled_groups() -> set[str]:
    """Get currently enabled tool groups."""
    return _enabled_groups.copy()


def enable_group(group: str) -> bool:
    """Enable a tool group. Returns True if changed."""
    if group not in ALL_TOOL_GROUPS:
        return False
    if group not in _enabled_groups:
        _enabled_groups.add(group)
        logger.info(f"Enabled tool group: {group}")
        return True
    return False


def disable_group(group: str) -> bool:
    """Disable a tool group. Returns True if changed."""
    if group == TOOL_GROUP_CORE:
        return False  # Can't disable core
    if group in _enabled_groups:
        _enabled_groups.discard(group)
        logger.info(f"Disabled tool group: {group}")
        return True
    return False


def set_enabled_groups(groups: set[str]) -> None:
    """Set the enabled tool groups directly."""
    global _enabled_groups
    valid_groups = groups & ALL_TOOL_GROUPS
    valid_groups.add(TOOL_GROUP_CORE)  # Always include core
    _enabled_groups = valid_groups


def reset_groups() -> None:
    """Reset to default enabled groups (core only)."""
    global _enabled_groups
    _enabled_groups = {TOOL_GROUP_CORE}


def auto_discover_tools(package_path: str) -> int:
    """Auto-discover tools from a package path.

    Imports all modules in the package to trigger decorator registration.
    Returns the number of tools discovered.
    """
    initial_count = len(_registered_tools)
    package = importlib.import_module(package_path)
    package_path_obj = Path(package.__file__).parent if package.__file__ else Path(".")

    for _importer, modname, _ispkg in pkgutil.iter_modules([str(package_path_obj)]):
        if modname.startswith("_"):
            continue
        full_module = f"{package_path}.{modname}"
        try:
            importlib.import_module(full_module)
            logger.debug(f"Imported module: {full_module}")
        except Exception as e:
            logger.warning(f"Failed to import {full_module}: {e}")

    discovered = len(_registered_tools) - initial_count
    logger.info(f"Auto-discovered {discovered} tools from {package_path}")
    return discovered


def auto_discover_resources(package_path: str) -> int:
    """Auto-discover resources from a package path."""
    initial_count = len(_registered_resources)
    package = importlib.import_module(package_path)
    package_path_obj = Path(package.__file__).parent if package.__file__ else Path(".")

    for _importer, modname, _ispkg in pkgutil.iter_modules([str(package_path_obj)]):
        if modname.startswith("_"):
            continue
        full_module = f"{package_path}.{modname}"
        try:
            importlib.import_module(full_module)
            logger.debug(f"Imported module: {full_module}")
        except Exception as e:
            logger.warning(f"Failed to import {full_module}: {e}")

    discovered = len(_registered_resources) - initial_count
    logger.info(f"Auto-discovered {discovered} resources from {package_path}")
    return discovered
