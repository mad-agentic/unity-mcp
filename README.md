# Unity MCP

| [English](README.md) |
|----------------------|

[![License: MIT](https://img.shields.io/badge/License-MIT-red.svg)](https://opensource.org/licenses/MIT)
[![Python 3.10+](https://img.shields.io/badge/Python-3.10+-3776AB.svg?style=flat&logo=python&logoColor=white)](https://www.python.org)
[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3+-000000?style=flat&logo=unity&logoColor=blue)](https://unity.com/releases/editor/archive)

**Create your Unity apps with LLMs — clean, reliable, and uniquely practical.** Unity MCP bridges AI assistants (Claude, Claude Opus, Cursor, VS Code, etc.) with your Unity Editor via the [Model Context Protocol](https://modelcontextprotocol.io/introduction). Give your LLM the tools to manage assets, control scenes, edit scripts, and automate tasks.

## Quick Start

Fast path:

1. `cd Server && uv sync`
2. `uv run unity-mcp --doctor --client vscode-copilot`
3. In Unity open `Window > Unity MCP`, click **Start Server**, then paste the printed config into your MCP client.

For the 3-minute onboarding path, see [docs/guides/QUICK_START_3_MINUTES.md](docs/guides/QUICK_START_3_MINUTES.md).

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

### Available Tools (27 total)

**Core tools** (enabled by default):
`manage_scene` • `manage_gameobject` • `find_gameobjects` • `get_project_info` • `manage_material` • `manage_shader` • `manage_texture` • `manage_asset` • `manage_script` • `manage_components` • `create_script` • `script_apply_edits` • `validate_script` • `manage_editor` • `refresh_unity` • `execute_menu_item` • `read_console` • `manage_prefab` • `manage_camera` • `manage_packages` • `manage_tool_groups` • `batch_execute` • `scene_auto_repair` • `ping`

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

### Foundation contracts (single source of truth)
- [Tool naming and taxonomy](docs/reference/TOOL_NAMING_AND_TAXONOMY.md)
- [Response and error contract](docs/reference/RESPONSE_ERROR_CONTRACT.md)
- [Long-running jobs contract](docs/reference/LONG_RUNNING_JOBS.md)
- [Transport compatibility matrix](docs/reference/TRANSPORT_COMPATIBILITY_MATRIX.md)

### Week 3 signature contracts
- [Verification Loop policy](docs/reference/VERIFICATION_LOOP_POLICY.md)
- [Batch execute guardrails](docs/reference/BATCH_EXECUTE_GUARDRAILS.md)
- [Scene auto-repair contract](docs/reference/SCENE_AUTO_REPAIR.md)

### Week 4 ecosystem contracts
- [CLI bootstrap scope](docs/reference/CLI_BOOTSTRAP_SCOPE.md)
- [CLI bootstrap + skill sync guide](docs/guides/CLI_BOOTSTRAP_AND_SKILL_SYNC.md)

### Week 2 DX docs
- [Quick Start in 3 minutes](docs/guides/QUICK_START_3_MINUTES.md)
- [Setup doctor and one-command checks](docs/guides/SETUP_DOCTOR.md)
- [Common recipes](docs/guides/RECIPES.md)

### Weekly execution notes
- [Week 1 closeout](docs/development/WEEK1_CLOSEOUT.md)
- [Week 2 backlog](docs/development/WEEK2_BACKLOG.md)
- [Week 3 demo and tuning](docs/development/WEEK3_DEMO_AND_TUNING.md)
- [Week 4 release notes](docs/development/WEEK4_RELEASE_NOTES.md)
- [Week 4 release summary](docs/development/WEEK4_RELEASE_SUMMARY.md)

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
