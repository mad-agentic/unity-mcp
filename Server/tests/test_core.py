"""Tests for core module."""

import pytest
from core.constants import (
    DEFAULT_HTTP_HOST,
    DEFAULT_HTTP_PORT,
    DEFAULT_TRANSPORT,
    TOOL_GROUP_CORE,
    ALL_TOOL_GROUPS,
)
from core.config import Config, get_config, reset_config


class TestConstants:
    def test_default_transport(self):
        assert DEFAULT_TRANSPORT in ("stdio", "http")

    def test_default_http_host(self):
        assert DEFAULT_HTTP_HOST == "127.0.0.1"

    def test_default_http_port(self):
        assert DEFAULT_HTTP_PORT == 8080

    def test_tool_group_core_defined(self):
        assert TOOL_GROUP_CORE == "core"

    def test_all_tool_groups(self):
        assert "core" in ALL_TOOL_GROUPS
        assert "vfx" in ALL_TOOL_GROUPS
        assert "animation" in ALL_TOOL_GROUPS
        assert "ui" in ALL_TOOL_GROUPS


class TestConfig:
    def setup_method(self):
        reset_config()

    def test_default_config_values(self):
        cfg = get_config()
        assert cfg.transport_mode == DEFAULT_TRANSPORT
        assert cfg.http_host == DEFAULT_HTTP_HOST
        assert cfg.http_port == DEFAULT_HTTP_PORT
        assert cfg.http_remote_hosted is False

    def test_http_base_url(self):
        cfg = get_config()
        assert cfg.http_base_url == f"http://{DEFAULT_HTTP_HOST}:{DEFAULT_HTTP_PORT}"

    def test_reset_config(self):
        cfg1 = get_config()
        reset_config()
        cfg2 = get_config()
        # Should be a new instance
        assert cfg1 is not cfg2
