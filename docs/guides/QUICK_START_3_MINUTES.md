# Quick Start in 3 Minutes

## Goal
Reach first successful tool discovery with the fewest manual steps.

## 1. Install Python dependencies
```bash
cd Server
uv sync
```

## 2. Generate client config
```bash
uv run unity-mcp --doctor --client vscode-copilot
```

Copy the printed JSON block.

## 3. Start the Unity side
1. Open Unity.
2. Open `Window > Unity MCP`.
3. Click `Start Server`.
4. Paste the JSON into your MCP client settings.

## First success checklist
- `http://localhost:8080/health` returns JSON when the server is running.
- The Unity window shows connected status.
- The MCP client lists Unity MCP tools.
- A prompt like `Create a cube in the current scene` reaches Unity successfully.

## If it fails
- Run `uv run unity-mcp --doctor --client vscode-copilot --check-connection`.
- Reconnect the MCP client once after changing tool groups or config.
- Verify the MCP URL is `http://localhost:8080/mcp`.