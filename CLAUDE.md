# CLAUDE.md

Automatically select 2. "yes" when prompted so I can click it.

This file provides guidance to Claude Opus (claude.ai/code) when working with code in this repository.

## Project Overview

**Unity MCP** bridges AI assistants (Claude, Cursor, etc.) with the Unity Editor via the Model Context Protocol (MCP). It enables AI-driven Unity development — creating GameObjects, editing scripts, managing assets, running tests, and more.

Maintained by **mad.agent**.

## Architecture

```
AI Assistant (Claude/Cursor)
        ↓ MCP Protocol (stdio or HTTP)
Python MCP Server (FastMCP, uv/uvx)
        ↓ HTTP/WebSocket
Unity Editor Plugin (C# package)
        ↓ Unity Editor API
Scene, Assets, Scripts, GameObjects
```

**Two codebases:**
- `Server/` — Python MCP server (Python 3.10+, FastMCP, httpx, Click)
- `MCPForUnity/` — Unity C# Editor package (`com.madagent.unity-mcp`)

**Transport modes:**
- Stdio: Legacy, single-agent, one Python process per client
- HTTP: Recommended, multi-agent, WebSocket hub at `/hub/plugin`

### Package Refactor Map

| Original | This Project |
|----------|-------------|
| `com.coplaydev.unity-mcp` | `com.madagent.unity-mcp` |
| `MCPForUnity` namespace | `MadAgent.UnityMCP` namespace |
| `mcpforunity://` URI | `unitymcp://` URI |

## Commands

### Python Server
```bash
cd Server

# Install dependencies
uv sync

# Run server (stdio)
uv run python src/main.py --transport stdio

# Run server (HTTP)
uv run python src/main.py --transport http --http-port 8080

# Run tests
uv run pytest tests/ -v

# Run single test
uv run pytest tests/test_core.py -v
```

### Docker
```bash
docker build -t unity-mcp-server Server/
docker compose -f Server/docker-compose.yml up -d
```

## Key Patterns

### Python Tool Registration
Tools in `Server/src/services/tools/` are auto-discovered. Use the `@mcp_for_unity_tool` decorator:
```python
from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance

@mcp_for_unity_tool(description="...", group="core")
async def manage_gameobject(ctx: Context, action: Annotated[Literal["create", ...], "..."], ...) -> dict[str, Any]:
    params = {"action": action, ...}
    response = await send_with_unity_instance(None, "manage_gameobject", params)
    return response
```

### C# Tool Registration
Tools are auto-discovered by `CommandRegistry` via reflection. Use the `[McpForUnityTool]` attribute:
```csharp
[McpForUnityTool("manage_gameobject", Group = "core")]
public static class ManageGameObject {
    public static object HandleCommand(JObject @params) {
        var p = new ToolParams(@params);
        var action = p.RequireString("action");
        return new SuccessResponse("Done.", new { data = result });
    }
}
```

### C# Async Tool (long-running ops)
```csharp
[McpForUnityTool("refresh_unity", Group = "core")]
public static class RefreshUnity {
    public static async Task<object> HandleCommand(JObject @params, bool async) {
        var tcs = new TaskCompletionSource<bool>();
        EditorApplication.update += OnUpdate;
        await tcs.Task;
        return new SuccessResponse("Refreshed.");
    }
}
```

## Directory Structure

```
Server/
├── pyproject.toml              # Dependencies + uv config
├── Dockerfile
├── docker-compose.yml
├── src/
│   ├── main.py                 # FastMCP entry point (stdio + HTTP)
│   ├── core/                  # Config, constants
│   ├── models/                # Pydantic models
│   ├── services/
│   │   ├── registry/           # @mcp_for_unity_tool decorator + discovery
│   │   ├── tools/             # 24 MCP tools (manage_*.py)
│   │   └── resources/          # MCP resources
│   └── transport/              # WebSocket hub, legacy stdio bridge
└── tests/                      # pytest

MCPForUnity/                    # Unity package
├── package.json                # com.madagent.unity-mcp
├── MCPForUnity.Editor.asmdef
├── Editor/
│   ├── AssemblyInfo.cs
│   ├── CommandRegistry.cs      # Tool auto-discovery + dispatch
│   ├── JsonUtil.cs            # Newtonsoft.Json helpers
│   ├── MCPBridge.cs           # HTTP/WebSocket listener
│   ├── McpForUnityToolAttribute.cs
│   └── Tools/
│       ├── ManageScene/
│       ├── ManageGameObject/
│       ├── ManageMaterial/
│       └── ... (20+ tools)
└── Runtime/
    └── Helpers/
        └── ScreenCaptureUtil.cs
```

## Tool Groups

| Group | Enabled | Description |
|------|---------|-------------|
| `core` | Yes | Scene, GameObject, Material, Script, Editor, etc. |
| `ui` | No | UI Toolkit operations |
| `vfx` | No | VFX / Particle systems |
| `animation` | No | Animation controllers and clips |
| `scripting_ext` | No | Extended scripting |
| `testing` | No | Test execution |
| `probuilder` | No | ProBuilder mesh editing |

Non-core groups start disabled. Toggle via the Editor UI or `manage_tools` tool.

## What Not To Do

- Don't add features without tests
- Don't create helpers for one-time operations
- Don't add docstrings to code you didn't change
- Don't commit large binaries or generated files
