# CLI Bootstrap Scope (Week 4 Day 22 SoT)

## Goal
Define a minimal, reliable bootstrap interface that prepares users for first successful connection with explicit diagnostics.

## Primary command
- `unity-mcp --bootstrap`

## Required responsibilities
1. Environment checks
   - Python version support
   - `uv` availability
2. Configuration generation
   - Build MCP config payload for target client
   - Optional file output for client config
3. Basic connectivity verification
   - Optional health check against HTTP endpoint
4. Skill/config synchronization report
   - Optional sync snapshot per client
   - Warning output on version/tool mismatch

## Parameters
- `--bootstrap`
- `--client`
- `--check-connection`
- `--write-config`
- `--config-output`
- `--sync-skills`
- `--skills-manifest`
- `--skills-output`
- `--expected-skill-version`

## Output contract
- Single JSON report with:
  - `phase=bootstrap`
  - `environment`
  - `server`
  - `config`
  - optional `artifacts.client_config_path`
  - optional `artifacts.skill_sync`

## Non-goals
- Does not install system dependencies automatically
- Does not start Unity editor or bridge process
- Does not mutate server runtime settings beyond generated artifacts
