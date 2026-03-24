# services/registry/__init__.py
from services.registry.registry import (
    mcp_for_unity_tool,
    mcp_for_unity_resource,
    get_tool,
    get_all_tools,
    get_tools_for_group,
    get_tool_taxonomy,
    get_enabled_tools,
    get_resource,
    get_all_resources,
    get_all_groups,
    get_enabled_groups,
    enable_group,
    disable_group,
    set_enabled_groups,
    reset_groups,
    auto_discover_tools,
    auto_discover_resources,
    # Private for testing
    _registered_tools,
    _registered_resources,
    _enabled_groups,
)
