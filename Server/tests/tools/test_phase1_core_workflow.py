"""Phase 1 golden core workflow tests using standardized response contract."""

from __future__ import annotations

import pytest

from services.tools.manage_components import manage_components
from services.tools.manage_gameobject import manage_gameobject
from services.tools.manage_scene import manage_scene


@pytest.mark.anyio
async def test_golden_workflow_scene_gameobject_component_success(monkeypatch):
    async def fake_send(_send_fn, tool_name, params, **kwargs):
        action = params.get("action")
        if tool_name == "manage_scene" and action == "create":
            return {"scene": params.get("scene_name", "NewScene")}
        if tool_name == "manage_gameobject" and action == "create":
            return {"gameobject": params.get("name", "Cube")}
        if tool_name == "manage_components" and action == "add":
            return {"component": params.get("component_type", "Rigidbody")}
        return {"ok": True}

    monkeypatch.setattr("services.tools.utils.send_with_unity_instance", fake_send)

    create_scene = await manage_scene(None, action="create", scene_name="Phase1Scene")
    assert create_scene["status"] == "success"
    assert create_scene["tool"] == "manage_scene"

    create_go = await manage_gameobject(None, action="create", name="Phase1Cube", primitive_type="Cube")
    assert create_go["status"] == "success"
    assert create_go["tool"] == "manage_gameobject"

    add_component = await manage_components(
        None,
        action="add",
        gameobject="Phase1Cube",
        component_type="Rigidbody",
    )
    assert add_component["status"] == "success"
    assert add_component["tool"] == "manage_components"


@pytest.mark.anyio
async def test_tool_contract_returns_error_on_invalid_input(monkeypatch):
    async def fake_send(_send_fn, tool_name, params, **kwargs):
        raise ValueError("invalid payload")

    monkeypatch.setattr("services.tools.utils.send_with_unity_instance", fake_send)

    response = await manage_gameobject(None, action="create", name="BadObject")
    assert response["status"] == "error"
    assert response["tool"] == "manage_gameobject"
    assert response["error"]["code"] == "E_INVALID_INPUT"


@pytest.mark.anyio
async def test_tool_contract_returns_pending_when_unity_payload_pending(monkeypatch):
    async def fake_send(_send_fn, tool_name, params, **kwargs):
        return {"_mcp_status": "pending", "_mcp_job_id": "job-42", "message": "Running"}

    monkeypatch.setattr("services.tools.utils.send_with_unity_instance", fake_send)

    response = await manage_scene(None, action="save")
    assert response["status"] == "pending"
    assert response["meta"]["job_id"] == "job-42"
