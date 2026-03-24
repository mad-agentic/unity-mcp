from __future__ import annotations

import pytest

from services.tools.batch_execute import batch_execute
from services.tools.manage_packages import manage_packages
from services.tools.scene_auto_repair import scene_auto_repair


@pytest.mark.anyio
async def test_verification_loop_blocks_sensitive_action_with_missing_fields(monkeypatch):
    response = await manage_packages(None, action="install")
    assert response["status"] == "error"
    assert response["error"]["code"] == "E_INVALID_INPUT"


@pytest.mark.anyio
async def test_verification_loop_calls_transport_probe(monkeypatch):
    calls = []

    async def fake_send(_send_fn, tool_name, params, **kwargs):
        calls.append((tool_name, params))
        if tool_name == "get_project_info":
            return {"project": "ok"}
        return {"ok": True}

    monkeypatch.setattr("services.tools.utils.send_with_unity_instance", fake_send)

    response = await manage_packages(None, action="install", package_name="com.unity.textmeshpro")
    assert response["status"] in {"success", "pending"}
    assert calls[0][0] == "get_project_info"
    assert calls[1][0] == "manage_packages"


@pytest.mark.anyio
async def test_batch_execute_enforces_limit():
    commands = [{"tool": "ping", "params": {}} for _ in range(3)]
    response = await batch_execute(None, commands=commands, max_commands_per_batch=2)
    assert response["status"] == "error"
    assert response["error"]["code"] == "E_INVALID_INPUT"


@pytest.mark.anyio
async def test_batch_execute_mixed_result_with_fail_fast(monkeypatch):
    from services.registry.registry import _registered_tools

    async def ok_tool(ctx, **kwargs):
        return {"status": "success", "tool": "ok_tool", "message": "ok", "data": {}, "error": None, "next_actions": [], "meta": {}}

    async def fail_tool(ctx, **kwargs):
        return {"status": "error", "tool": "fail_tool", "message": "fail", "data": None, "error": {"code": "E_INTERNAL", "retryable": False, "details": "x"}, "next_actions": [], "meta": {}}

    _registered_tools["ok_tool"] = {"name": "ok_tool", "group": "core", "description": "", "maturity": "core", "annotations": {}, "func": ok_tool}
    _registered_tools["fail_tool"] = {"name": "fail_tool", "group": "core", "description": "", "maturity": "core", "annotations": {}, "func": fail_tool}

    response = await batch_execute(
        None,
        commands=[
            {"tool": "ok_tool", "params": {}},
            {"tool": "fail_tool", "params": {}},
            {"tool": "ok_tool", "params": {}},
        ],
        fail_fast=True,
    )

    assert response["status"] == "error"
    assert response["error"]["code"] == "W_PARTIAL_SUCCESS"
    assert response["meta"]["summary"]["executed"] == 2


@pytest.mark.anyio
async def test_batch_execute_soft_rollback(monkeypatch):
    from services.registry.registry import _registered_tools

    async def create_marker(ctx, **kwargs):
        return {"status": "success", "tool": "create_marker", "message": "ok", "data": {"id": 1}, "error": None, "next_actions": [], "meta": {}}

    async def delete_marker(ctx, **kwargs):
        return {"status": "success", "tool": "delete_marker", "message": "rolled back", "data": {}, "error": None, "next_actions": [], "meta": {}}

    async def always_fail(ctx, **kwargs):
        return {"status": "error", "tool": "always_fail", "message": "nope", "data": None, "error": {"code": "E_INTERNAL", "retryable": False, "details": "x"}, "next_actions": [], "meta": {}}

    _registered_tools["create_marker"] = {"name": "create_marker", "group": "core", "description": "", "maturity": "core", "annotations": {}, "func": create_marker}
    _registered_tools["delete_marker"] = {"name": "delete_marker", "group": "core", "description": "", "maturity": "core", "annotations": {}, "func": delete_marker}
    _registered_tools["always_fail"] = {"name": "always_fail", "group": "core", "description": "", "maturity": "core", "annotations": {}, "func": always_fail}

    response = await batch_execute(
        None,
        commands=[
            {"tool": "create_marker", "params": {}, "rollback": {"tool": "delete_marker", "params": {}}},
            {"tool": "always_fail", "params": {}},
        ],
        rollback_soft=True,
        fail_fast=True,
    )

    assert response["status"] == "error"
    assert response["meta"]["summary"]["failed"] == 1
    assert len(response["meta"]["rollback_results"]) == 1
    assert response["meta"]["rollback_results"][0]["status"] == "success"


@pytest.mark.anyio
async def test_scene_auto_repair_audit_reports_missing_camera_light(monkeypatch):
    async def fake_read_console(ctx, action, count=None, filter=None):
        return {"status": "success", "data": ["The referenced script on this Behaviour is missing!"]}

    async def fake_find_gameobjects(ctx, **kwargs):
        return {"status": "success", "data": []}

    monkeypatch.setattr("services.tools.scene_auto_repair.read_console", fake_read_console)
    monkeypatch.setattr("services.tools.scene_auto_repair.find_gameobjects", fake_find_gameobjects)

    response = await scene_auto_repair(None, mode="audit")
    assert response["status"] == "success"
    ids = {issue["id"] for issue in response["data"]["issues"]}
    assert "missing_script_reference" in ids
    assert "missing_main_camera" in ids
    assert "missing_directional_light" in ids


@pytest.mark.anyio
async def test_scene_auto_repair_repair_dry_run_plans_actions(monkeypatch):
    async def fake_read_console(ctx, action, count=None, filter=None):
        return {"status": "success", "data": []}

    async def fake_find_gameobjects(ctx, **kwargs):
        return {"status": "success", "data": []}

    async def fake_manage_gameobject(ctx, **kwargs):
        return {"status": "success", "tool": "manage_gameobject", "data": kwargs}

    monkeypatch.setattr("services.tools.scene_auto_repair.read_console", fake_read_console)
    monkeypatch.setattr("services.tools.scene_auto_repair.find_gameobjects", fake_find_gameobjects)
    monkeypatch.setattr("services.tools.scene_auto_repair.manage_gameobject", fake_manage_gameobject)

    response = await scene_auto_repair(None, mode="repair", dry_run=True)
    assert response["status"] == "success"
    assert response["data"]["summary"]["planned_actions"] == 2
    assert response["data"]["summary"]["applied_actions"] == 0
