"""Week 4 bootstrap and skill sync helpers."""

from __future__ import annotations

import json
import platform
import shutil
import sys
import time
from pathlib import Path
from typing import Any

from cli.utils.setup_doctor import build_client_config, normalize_client_name

try:
    import tomllib  # Python 3.11+
except ModuleNotFoundError:  # pragma: no cover - fallback for 3.10
    import tomli as tomllib


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[4]


def _default_manifest_path() -> Path:
    return _repo_root() / "manifest.json"


def _default_skill_output_path(client_id: str) -> Path:
    return _repo_root() / ".unity_mcp" / "skills" / f"{client_id}.json"


def _read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    return value if isinstance(value, dict) else {}


def _read_pyproject_version(pyproject_path: Path) -> str | None:
    if not pyproject_path.exists():
        return None
    payload = tomllib.loads(pyproject_path.read_text(encoding="utf-8"))
    return str(payload.get("project", {}).get("version")) if payload.get("project", {}).get("version") else None


def _load_runtime_tools() -> list[str]:
    from services.registry import auto_discover_tools, get_all_tools

    tools = get_all_tools()
    if not tools:
        auto_discover_tools("services.tools")
        tools = get_all_tools()
    return sorted(tools.keys())


def _manifest_tools(manifest: dict[str, Any]) -> list[str]:
    raw = manifest.get("tools", [])
    if not isinstance(raw, list):
        return []
    names: list[str] = []
    for item in raw:
        if isinstance(item, dict) and isinstance(item.get("name"), str):
            names.append(item["name"])
    return sorted(set(names))


def build_bootstrap_report(
    *,
    endpoint_url: str,
    client: str,
    health: dict[str, Any] | None = None,
) -> dict[str, Any]:
    started = time.perf_counter()
    client_id = normalize_client_name(client)
    uv_path = shutil.which("uv")
    report = {
        "phase": "bootstrap",
        "client": {
            "id": client_id,
        },
        "environment": {
            "platform": platform.system(),
            "python_version": platform.python_version(),
            "python_supported": sys.version_info >= (3, 10),
            "uv_installed": bool(uv_path),
            "uv_path": uv_path,
        },
        "server": {
            "mcp_endpoint": endpoint_url,
            "health_checked": bool(health),
            "health": health or {},
        },
        "config": build_client_config(endpoint_url, client_id),
        "next_actions": [
            "Run uv sync inside Server if dependencies are missing.",
            "Start Unity bridge from Window > Unity MCP.",
            "Paste generated MCP config into your client and reconnect once.",
        ],
    }
    report["meta"] = {
        "duration_ms": round((time.perf_counter() - started) * 1000, 2),
    }
    return report


def write_client_config(config: dict[str, Any], output_path: str | None = None) -> str:
    path = Path(output_path) if output_path else (_repo_root() / ".unity_mcp" / "client-config.json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(config, indent=2), encoding="utf-8")
    return str(path)


def sync_skills_for_client(
    *,
    client: str,
    endpoint_url: str,
    manifest_path: str | None = None,
    output_path: str | None = None,
    expected_version: str | None = None,
) -> dict[str, Any]:
    client_id = normalize_client_name(client)
    manifest_file = Path(manifest_path) if manifest_path else _default_manifest_path()

    if not manifest_file.exists():
        return {
            "status": "failed",
            "reason": "manifest_missing",
            "manifest_path": str(manifest_file),
        }

    manifest = _read_json(manifest_file)
    manifest_version = str(manifest.get("version")) if manifest.get("version") else None
    runtime_version = _read_pyproject_version(_repo_root() / "Server" / "pyproject.toml")

    manifest_tools = _manifest_tools(manifest)
    runtime_tools = _load_runtime_tools()

    missing_in_runtime = sorted(set(manifest_tools) - set(runtime_tools))
    missing_in_manifest = sorted(set(runtime_tools) - set(manifest_tools))
    warnings: list[str] = []

    if runtime_version and manifest_version and runtime_version != manifest_version:
        warnings.append(
            f"version_mismatch: manifest={manifest_version}, runtime={runtime_version}"
        )

    if expected_version and runtime_version and expected_version != runtime_version:
        warnings.append(
            f"expected_version_mismatch: expected={expected_version}, runtime={runtime_version}"
        )

    if missing_in_runtime:
        warnings.append(
            f"manifest_tools_not_in_runtime: {', '.join(missing_in_runtime)}"
        )
    if missing_in_manifest:
        warnings.append(
            f"runtime_tools_not_in_manifest: {', '.join(missing_in_manifest)}"
        )

    synced_payload = {
        "client": client_id,
        "version": runtime_version or manifest_version,
        "source": {
            "manifest_path": str(manifest_file),
            "manifest_version": manifest_version,
            "runtime_version": runtime_version,
        },
        "mcp_config": build_client_config(endpoint_url, client_id),
        "tools": runtime_tools,
        "warnings": warnings,
        "generated_at": int(time.time()),
    }

    destination = Path(output_path) if output_path else _default_skill_output_path(client_id)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(synced_payload, indent=2), encoding="utf-8")

    return {
        "status": "success" if not warnings else "warning",
        "output_path": str(destination),
        "manifest_path": str(manifest_file),
        "manifest_tools": len(manifest_tools),
        "runtime_tools": len(runtime_tools),
        "missing_in_runtime": missing_in_runtime,
        "missing_in_manifest": missing_in_manifest,
        "warnings": warnings,
    }
