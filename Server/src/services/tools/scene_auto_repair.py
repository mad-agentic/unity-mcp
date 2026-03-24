"""Scene auto-repair tool with audit and safe repair modes."""

from __future__ import annotations

import re
from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from core.error_codes import WARNING_PARTIAL_SUCCESS
from core.response_contract import error_response, success_response
from services.registry import mcp_for_unity_tool
from services.tools.find_gameobjects import find_gameobjects
from services.tools.manage_gameobject import manage_gameobject
from services.tools.read_console import read_console


_MISSING_SCRIPT_PATTERNS = [
    re.compile(r"referenced script on this Behaviour is missing", re.IGNORECASE),
    re.compile(r"Missing \(Mono Script\)", re.IGNORECASE),
]

_MISSING_REFERENCE_PATTERNS = [
    re.compile(r"MissingReferenceException", re.IGNORECASE),
    re.compile(r"SerializedObjectNotCreatableException", re.IGNORECASE),
]


def _as_list_of_entries(console_data: Any) -> list[str]:
    if isinstance(console_data, list):
        return [str(item) for item in console_data]
    if isinstance(console_data, dict):
        for key in ("entries", "logs", "errors", "data"):
            value = console_data.get(key)
            if isinstance(value, list):
                return [str(item) for item in value]
        return [str(console_data)]
    if console_data is None:
        return []
    return [str(console_data)]


def _build_console_issues(entries: list[str]) -> list[dict[str, Any]]:
    issues: list[dict[str, Any]] = []
    for msg in entries:
        if any(p.search(msg) for p in _MISSING_SCRIPT_PATTERNS):
            issues.append(
                {
                    "id": "missing_script_reference",
                    "title": "Missing script reference detected",
                    "priority": "high",
                    "fixable": False,
                    "source": "console",
                    "evidence": msg,
                    "recommended_action": "Open affected GameObject and reattach or remove missing script component.",
                }
            )
            continue
        if any(p.search(msg) for p in _MISSING_REFERENCE_PATTERNS):
            issues.append(
                {
                    "id": "missing_object_reference",
                    "title": "Missing object reference detected",
                    "priority": "medium",
                    "fixable": False,
                    "source": "console",
                    "evidence": msg,
                    "recommended_action": "Rebind serialized field references in Inspector.",
                }
            )
    return issues


@mcp_for_unity_tool(
    description=(
        "Audit and safely repair common scene issues. "
        "Audit mode reports issues. Repair mode can auto-create missing Camera/Directional Light with dry-run support."
    ),
    group="core",
    maturity="advanced",
)
async def scene_auto_repair(
    ctx: Context,
    mode: Annotated[Literal["audit", "repair"], "Run audit only or perform safe repairs"],
    dry_run: Annotated[
        Optional[bool],
        "When mode=repair, only plan actions without applying changes",
    ] = True,
    console_scan_count: Annotated[
        Optional[int],
        "Number of recent console errors to analyze",
    ] = 200,
) -> dict[str, Any]:
    dry_run_enabled = True if dry_run is None else bool(dry_run)
    count = 200 if console_scan_count is None else int(console_scan_count)

    console_response = await read_console(ctx, action="get_errors", count=count)
    console_entries = _as_list_of_entries(console_response.get("data") if isinstance(console_response, dict) else None)
    issues = _build_console_issues(console_entries)

    camera_query = await find_gameobjects(ctx, has_component="Camera", page_size=5)
    light_query = await find_gameobjects(ctx, has_component="Light", page_size=10)

    camera_data = camera_query.get("data") if isinstance(camera_query, dict) else None
    light_data = light_query.get("data") if isinstance(light_query, dict) else None

    has_camera = bool(camera_data)
    has_light = bool(light_data)

    if not has_camera:
        issues.append(
            {
                "id": "missing_main_camera",
                "title": "No Camera found in scene",
                "priority": "high",
                "fixable": True,
                "source": "scene_scan",
                "recommended_action": "Create a Main Camera GameObject with Camera component.",
            }
        )

    if not has_light:
        issues.append(
            {
                "id": "missing_directional_light",
                "title": "No Light found in scene",
                "priority": "high",
                "fixable": True,
                "source": "scene_scan",
                "recommended_action": "Create a Directional Light GameObject.",
            }
        )

    issues.sort(key=lambda i: {"high": 0, "medium": 1, "low": 2}.get(str(i.get("priority")), 99))

    planned_actions: list[dict[str, Any]] = []
    applied_actions: list[dict[str, Any]] = []
    unresolved_issues: list[dict[str, Any]] = []

    for issue in issues:
        if not issue.get("fixable"):
            unresolved_issues.append(issue)
            continue

        if issue["id"] == "missing_main_camera":
            action = {
                "tool": "manage_gameobject",
                "params": {
                    "action": "create",
                    "name": "Main Camera",
                    "tag": "MainCamera",
                    "components_to_add": ["Camera"],
                    "position": [0, 1, -10],
                },
            }
            planned_actions.append(action)
        elif issue["id"] == "missing_directional_light":
            action = {
                "tool": "manage_gameobject",
                "params": {
                    "action": "create",
                    "name": "Directional Light",
                    "components_to_add": ["Light"],
                    "rotation": [50, -30, 0],
                },
            }
            planned_actions.append(action)

    if mode == "repair" and planned_actions and not dry_run_enabled:
        for action in planned_actions:
            response = await manage_gameobject(ctx, **action["params"])
            applied_actions.append(
                {
                    "action": action,
                    "status": response.get("status") if isinstance(response, dict) else "error",
                    "response": response,
                }
            )

    data = {
        "mode": mode,
        "dry_run": dry_run_enabled,
        "summary": {
            "total_issues": len(issues),
            "fixable_issues": len([i for i in issues if i.get("fixable")]),
            "unresolved_issues": len(unresolved_issues),
            "planned_actions": len(planned_actions),
            "applied_actions": len(applied_actions),
        },
        "issues": issues,
        "planned_actions": planned_actions,
        "applied_actions": applied_actions,
        "unresolved_issues": unresolved_issues,
    }

    if mode == "audit":
        return success_response(
            tool="scene_auto_repair",
            message="Scene audit completed",
            data=data,
        )

    if unresolved_issues and not planned_actions:
        return error_response(
            tool="scene_auto_repair",
            code=WARNING_PARTIAL_SUCCESS,
            message="Repair completed with unresolved issues",
            retryable=False,
            details={"unresolved_count": len(unresolved_issues)},
            next_actions=[
                "Review unresolved issues and fix manually in Unity Inspector.",
                "Run audit again to confirm status.",
            ],
            meta={"summary": data["summary"]},
        )

    return success_response(
        tool="scene_auto_repair",
        message="Scene repair flow completed",
        data=data,
        next_actions=["Run scene_auto_repair in audit mode to verify remaining issues."],
    )
