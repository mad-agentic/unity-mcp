"""Tests for runtime tool group management."""

from __future__ import annotations

import pytest

from services.registry import reset_groups
from services.tools.manage_tool_groups import manage_tool_groups


@pytest.fixture(autouse=True)
def reset_group_state():
    reset_groups()
    yield
    reset_groups()


@pytest.mark.anyio
async def test_manage_tool_groups_list_returns_core_group():
    response = await manage_tool_groups(None, action="list")
    assert response["status"] == "success"
    assert "core" in response["data"]["all_groups"]
    assert response["data"]["enabled_groups"] == ["core"]


@pytest.mark.anyio
async def test_manage_tool_groups_enable_and_disable_flow():
    enabled = await manage_tool_groups(None, action="enable", group="ui")
    assert enabled["status"] == "success"
    assert "ui" in enabled["data"]["enabled_groups"]
    assert enabled["data"]["sync_required"] is True

    disabled = await manage_tool_groups(None, action="disable", group="ui")
    assert disabled["status"] == "success"
    assert "ui" not in disabled["data"]["enabled_groups"]


@pytest.mark.anyio
async def test_manage_tool_groups_rejects_disabling_core():
    response = await manage_tool_groups(None, action="disable", group="core")
    assert response["status"] == "error"
    assert response["error"]["code"] == "E_INVALID_INPUT"


@pytest.mark.anyio
async def test_manage_tool_groups_set_rejects_invalid_group_names():
    response = await manage_tool_groups(None, action="set", groups=["core", "unknown-group"])
    assert response["status"] == "error"
    assert response["meta"]["invalid_groups"] == ["unknown-group"]