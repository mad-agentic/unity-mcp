"""Tests for Week 2 setup doctor helpers."""

from cli.utils.setup_doctor import build_client_config, build_doctor_report, normalize_client_name


def test_normalize_client_name_defaults_to_other_for_unknown_client():
    assert normalize_client_name("something-custom") == "other"


def test_build_client_config_uses_expected_shape():
    payload = build_client_config("http://localhost:8080/mcp", "vscode-copilot")
    assert payload == {
        "mcpServers": {
            "unityMCP": {
                "url": "http://localhost:8080/mcp",
            }
        }
    }


def test_build_doctor_report_carries_health_status():
    report = build_doctor_report(
        url="http://localhost:8080/mcp",
        client="cursor",
        health={"transport": "streamable-http", "unity_connected": True},
    )
    assert report["client"]["id"] == "cursor"
    assert report["server"]["health_checked"] is True
    assert report["server"]["transport"] == "streamable-http"
    assert report["server"]["unity_connected"] is True