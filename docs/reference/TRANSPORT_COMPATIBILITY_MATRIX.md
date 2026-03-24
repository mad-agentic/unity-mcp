# Transport Compatibility Matrix

## Goal
Clarify which transport to use, how Unity connects, and what the client should expect during setup and troubleshooting.

## Matrix

| Mode | Client entrypoint | Unity bridge path | Best for | Notes |
|------|-------------------|-------------------|----------|-------|
| `stdio` | `uv run python src/main.py --transport stdio` | Legacy connection pool | Local single-client debugging | No HTTP health endpoint. Lowest ceremony for local experiments. |
| `http` | `uv run python src/main.py --transport http --http-port 8080` | WebSocket hub at `/hub/plugin` | VS Code Copilot, Cursor, Claude Desktop, Claude Code | Recommended mode. MCP client uses `/mcp`, Unity plugin connects to `/hub/plugin`, health is exposed at `/health`. |

## Endpoint contract
- MCP client endpoint: `http://localhost:8080/mcp`
- Unity bridge endpoint: `ws://127.0.0.1:8080/hub/plugin`
- Health endpoint: `http://localhost:8080/health`

## Client guidance
- VS Code Copilot: use HTTP mode and paste the JSON config produced by `unity-mcp --doctor`.
- Cursor: use HTTP mode and reconnect the MCP client after changing server-side tool groups.
- Claude Desktop / Claude Code: use HTTP mode unless you explicitly want local stdio experimentation.

## Tool-group toggle behavior
- `manage_tool_groups` updates the server registry immediately.
- Connected MCP clients may cache the previous tool list.
- After enabling or disabling groups, reconnect the MCP client once.
- If the client still shows stale tools, restart the Unity MCP server.

## Failure triage
- `health` reachable but `unity_connected=false`: Unity window is open but the bridge is not started yet.
- MCP client cannot discover tools: verify `/mcp` is reachable, then reconnect the client.
- Unity fails to receive commands: verify the Unity window shows connected status and the WebSocket hub path is `/hub/plugin`.