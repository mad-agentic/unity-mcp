# Unity MCP

| [English](README.md) |
|----------------------|

[![License: MIT](https://img.shields.io/badge/License-MIT-red.svg)](https://opensource.org/licenses/MIT)
[![Python 3.10+](https://img.shields.io/badge/Python-3.10+-3776AB.svg?style=flat&logo=python&logoColor=white)](https://www.python.org)
[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3+-000000?style=flat&logo=unity&logoColor=blue)](https://unity.com/releases/editor/archive)

**Create your Unity apps with LLMs!** Unity MCP bridges AI assistants (Claude, Claude Opus, Cursor, VS Code, etc.) with your Unity Editor via the [Model Context Protocol](https://modelcontextprotocol.io/introduction). Give your LLM the tools to manage assets, control scenes, edit scripts, and automate tasks.

## Quick Start

### Prerequisites

- **Unity 2021.3 LTS+** — [Download Unity](https://unity.com/download)
- **Python 3.10+** and **uv** — [Install uv](https://docs.astral.sh/uv/getting-started/installation/)
- **An MCP Client** — [Claude Desktop](https://claude.ai/download) | [Claude Opus](https://docs.anthropic.com/en/docs/claude-code) | [Cursor](https://www.cursor.com/en/downloads) | [VS Code Copilot](https://code.visualstudio.com/docs/copilot/overview)

### 1. Install the Unity Package

In Unity: `Window > Package Manager > + > Add package from git URL...`

```
https://github.com/mad-agent/unity-mcp.git?path=/MCPForUnity#main
```

### 2. Start the Server & Connect

1. In Unity: `Window > Unity MCP`
2. Click **Start Server** (launches HTTP server on `localhost:8080`)
3. Select your MCP Client from the dropdown and click **Configure**
4. Look for 🟢 "Connected ✓"
5. **Connect your client:** Configure your MCP client with the HTTP URL

**That's it!** Try a prompt like: *"Create a red cube"* or *"Build a simple player controller"*

## Features & Tools

### Available Tools (24 total)

**Core tools** (enabled by default):
`manage_scene` • `manage_gameobject` • `find_gameobjects` • `get_project_info` • `manage_material` • `manage_shader` • `manage_texture` • `manage_asset` • `manage_script` • `manage_components` • `create_script` • `script_apply_edits` • `validate_script` • `manage_editor` • `refresh_unity` • `execute_menu_item` • `read_console` • `manage_prefabs` • `manage_camera` • `manage_packages` • `ping` • `echo`

**UI tools** (group `ui`):
`manage_ui`

**VFX tools** (group `vfx`):
`manage_vfx`

**Animation tools** (group `animation`):
`manage_animation`

## Manual Configuration

```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

## Docker

```bash
docker compose -f Server/docker-compose.yml up -d
```

## Development

See [plan.md](plan.md) for the implementation plan and [CLAUDE.md](CLAUDE.md) for development guidance.

```bash
# Install dependencies
cd Server && uv sync

# Run server (stdio)
uv run python src/main.py --transport stdio

# Run server (HTTP)
uv run python src/main.py --transport http --http-port 8080

# Run tests
cd Server && uv run pytest tests/ -v
```

## License

MIT — See [LICENSE](LICENSE)
