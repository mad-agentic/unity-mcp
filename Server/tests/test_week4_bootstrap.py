"""Week 4 tests for bootstrap and skill sync flows."""

from __future__ import annotations

import json
from pathlib import Path

from cli.utils.bootstrap import build_bootstrap_report, write_client_config, sync_skills_for_client


def test_build_bootstrap_report_contains_environment_and_config():
    report = build_bootstrap_report(
        endpoint_url="http://localhost:8080/mcp",
        client="cursor",
        health={"transport": "streamable-http", "unity_connected": True},
    )

    assert report["phase"] == "bootstrap"
    assert report["client"]["id"] == "cursor"
    assert report["environment"]["python_supported"] is True
    assert report["config"]["mcpServers"]["unityMCP"]["url"] == "http://localhost:8080/mcp"


def test_write_client_config_creates_file(tmp_path: Path):
    output = tmp_path / "client-config.json"
    config = {"mcpServers": {"unityMCP": {"url": "http://localhost:8080/mcp"}}}

    path = write_client_config(config, str(output))
    assert Path(path).exists()

    loaded = json.loads(Path(path).read_text(encoding="utf-8"))
    assert loaded == config


def test_sync_skills_reports_mismatch_and_writes_output(tmp_path: Path):
    manifest = tmp_path / "manifest.json"
    manifest.write_text(
        json.dumps(
            {
                "version": "9.9.9",
                "tools": [
                    {"name": "ping"},
                    {"name": "echo"},
                    {"name": "manage_prefabs"},
                ],
            }
        ),
        encoding="utf-8",
    )
    output = tmp_path / "skills.json"

    result = sync_skills_for_client(
        client="vscode-copilot",
        endpoint_url="http://localhost:8080/mcp",
        manifest_path=str(manifest),
        output_path=str(output),
        expected_version="0.1.0",
    )

    assert result["status"] in {"success", "warning"}
    assert output.exists()
    assert "echo" in result["missing_in_runtime"]
    assert "manage_prefabs" in result["missing_in_runtime"]
    assert any("version_mismatch" in warning for warning in result["warnings"])


def test_sync_skills_manifest_missing_returns_failed(tmp_path: Path):
    result = sync_skills_for_client(
        client="vscode-copilot",
        endpoint_url="http://localhost:8080/mcp",
        manifest_path=str(tmp_path / "not-found.json"),
        output_path=str(tmp_path / "skills.json"),
    )

    assert result["status"] == "failed"
    assert result["reason"] == "manifest_missing"


def test_bootstrap_report_normalizes_unknown_client():
    report = build_bootstrap_report(
        endpoint_url="http://localhost:8080/mcp",
        client="my-custom-client",
    )
    assert report["client"]["id"] == "other"
