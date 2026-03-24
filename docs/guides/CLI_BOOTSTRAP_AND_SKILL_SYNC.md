# CLI Bootstrap + Skill Sync

## Quick bootstrap

```powershell
cd Server
uv run unity-mcp --bootstrap --client vscode-copilot --check-connection
```

## Write client config artifact

```powershell
cd Server
uv run unity-mcp --bootstrap --client cursor --write-config
```

Optional custom path:

```powershell
cd Server
uv run unity-mcp --bootstrap --client cursor --write-config --config-output ../.unity_mcp/cursor-config.json
```

## Sync skills snapshot per client

```powershell
cd Server
uv run unity-mcp --bootstrap --client vscode-copilot --sync-skills
```

Optional strict version expectation:

```powershell
cd Server
uv run unity-mcp --bootstrap --client vscode-copilot --sync-skills --expected-skill-version 0.1.0
```

## Output interpretation
- `status=warning` in `artifacts.skill_sync` means sync succeeded but mismatch warnings exist.
- `missing_in_runtime` means manifest declares tools unavailable in runtime registry.
- `missing_in_manifest` means runtime has tools not yet reflected in manifest metadata.
