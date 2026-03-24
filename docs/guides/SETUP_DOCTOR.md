# Setup Doctor and One-Command Checks

## Goal
Use one command to print the MCP client config, validate the local Python runtime, and optionally probe the HTTP health endpoint.

## Windows PowerShell
```powershell
cd Server
uv sync
uv run unity-mcp --doctor --client vscode-copilot
```

## macOS / Linux
```bash
cd Server
uv sync
uv run unity-mcp --doctor --client cursor
```

## Check live server connectivity
If the HTTP server is already running, add `--check-connection`:

```bash
cd Server
uv run unity-mcp --doctor --client claude-code --check-connection
```

## Expected output
- JSON report with Python version, target client, and optional health status
- JSON block to paste directly into your MCP client config

## Recommended flow
1. Run `uv run unity-mcp --doctor --client <client-id>`.
2. Paste the emitted config into your MCP client.
3. In Unity open `Window > Unity MCP` and click `Start Server`.
4. If tools do not appear immediately, reconnect the MCP client once.

## Supported client identifiers
- `claude-desktop`
- `claude-code`
- `cursor`
- `vscode-copilot`
- `other`

## Troubleshooting by error code
- `E_INVALID_INPUT`: verify `--client` is one of the supported identifiers and rerun with the exact value.
- `E_UNITY_UNAVAILABLE`: open Unity, go to `Window > Unity MCP`, then click `Start Server` and rerun with `--check-connection`.
- `E_TRANSPORT_FAILURE`: confirm `http://localhost:8080/health` is reachable, then restart Unity MCP server and reconnect MCP client.
- `E_TIMEOUT`: retry once; if it persists, reduce concurrent operations and verify Unity is responsive in Editor.
- `E_INTERNAL`: rerun with same command, then inspect server logs and Unity console to capture the failing step.