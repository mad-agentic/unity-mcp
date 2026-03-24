"""Tests for the service registry."""

import pytest
from core.constants import TOOL_GROUP_CORE
from services.registry import (
    mcp_for_unity_tool,
    mcp_for_unity_resource,
    get_tool,
    get_all_tools,
    get_enabled_groups,
    enable_group,
    disable_group,
    reset_groups,
    auto_discover_tools,
    _registered_tools,
    _registered_resources,
    _enabled_groups,
)


class TestToolRegistration:
    def setup_method(self):
        # Clear registered tools before each test
        _registered_tools.clear()
        reset_groups()

    def test_register_tool_decorator(self):
        @mcp_for_unity_tool(description="Test tool", group="core")
        async def test_tool(ctx):
            return {"result": "ok"}

        assert "test_tool" in _registered_tools
        tool = get_tool("test_tool")
        assert tool is not None
        assert tool["description"] == "Test tool"
        assert tool["group"] == "core"

    def test_register_tool_with_annotations(self):
        @mcp_for_unity_tool(
            description="Tool with annotations",
            group="vfx",
            annotations={"destructiveHint": True},
        )
        async def annotated_tool(ctx):
            return {}

        tool = get_tool("annotated_tool")
        assert tool is not None
        assert tool["group"] == "vfx"
        assert tool["annotations"]["destructiveHint"] is True

    def test_get_all_tools(self):
        @mcp_for_unity_tool(description="Tool 1", group="core")
        async def tool1(ctx): return {}
        @mcp_for_unity_tool(description="Tool 2", group="vfx")
        async def tool2(ctx): return {}

        tools = get_all_tools()
        assert "tool1" in tools
        assert "tool2" in tools

    def test_get_nonexistent_tool(self):
        assert get_tool("nonexistent") is None


class TestGroupManagement:
    def setup_method(self):
        # Reset directly to ensure clean state
        from services.registry.registry import _enabled_groups as reg_groups
        reg_groups.clear()
        reg_groups.add(TOOL_GROUP_CORE)

    def test_core_always_enabled(self):
        assert TOOL_GROUP_CORE in _enabled_groups
        result = disable_group(TOOL_GROUP_CORE)
        assert result is False
        assert TOOL_GROUP_CORE in _enabled_groups

    def test_enable_group(self):
        from core.constants import ALL_TOOL_GROUPS
        from services.registry.registry import _enabled_groups as reg_groups
        # Ensure clean state
        reg_groups.clear()
        reg_groups.add("core")
        result = enable_group("vfx")
        assert result is True
        assert "vfx" in get_enabled_groups()
        assert "vfx" in ALL_TOOL_GROUPS

    def test_enable_group_idempotent(self):
        enable_group("vfx")
        result = enable_group("vfx")
        assert result is False

    def test_disable_group(self):
        enable_group("vfx")
        result = disable_group("vfx")
        assert result is True
        assert "vfx" not in _enabled_groups

    def test_get_enabled_groups(self):
        enable_group("animation")
        groups = get_enabled_groups()
        assert "core" in groups
        assert "animation" in groups

    def test_reset_groups(self):
        enable_group("vfx")
        enable_group("animation")
        reset_groups()
        assert get_enabled_groups() == {TOOL_GROUP_CORE}
