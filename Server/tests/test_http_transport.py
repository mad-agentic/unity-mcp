"""Regression tests for HTTP transport startup."""

import asyncio

import uvicorn

import main


def test_run_http_registers_unity_websocket_route(monkeypatch):
    captured_routes = []

    async def fake_serve(self):
        captured_routes.extend(
            (type(route).__name__, getattr(route, "path", None))
            for route in self.config.app.routes
        )
        return None

    monkeypatch.setattr(uvicorn.Server, "serve", fake_serve)

    asyncio.run(main._run_http(main._build_mcp_server()))

    assert ("WebSocketRoute", "/hub/plugin") in captured_routes
    assert ("APIRoute", "/health") in captured_routes
    assert ("Mount", "/mcp") in captured_routes