"""Regression tests for Week 2 wrapper normalization rollout."""

from __future__ import annotations

from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1] / "src" / "services" / "tools"

WRAPPER_FILES = {
    "create_script.py",
    "execute_menu_item.py",
    "find_gameobjects.py",
    "manage_animation.py",
    "manage_asset.py",
    "manage_camera.py",
    "manage_components.py",
    "manage_editor.py",
    "manage_gameobject.py",
    "manage_material.py",
    "manage_packages.py",
    "manage_prefabs.py",
    "manage_scene.py",
    "manage_script.py",
    "manage_shader.py",
    "manage_texture.py",
    "manage_ui.py",
    "manage_vfx.py",
    "project_info.py",
    "read_console.py",
    "refresh_unity.py",
    "script_apply_edits.py",
    "validate_script.py",
}


def test_all_unity_wrappers_use_contract_adapter():
    for file_name in WRAPPER_FILES:
        content = (TOOLS_DIR / file_name).read_text(encoding="utf-8")
        assert "execute_tool_with_contract" in content, f"{file_name} is missing contract normalization"
        assert "from transport.unity_transport import send_with_unity_instance" not in content, (
            f"{file_name} still imports raw transport helper"
        )