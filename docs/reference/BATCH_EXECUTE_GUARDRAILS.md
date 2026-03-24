# Batch Execute Guardrails (Week 3 SoT)

## Goal
Run multiple MCP commands in one request while preserving safety, debuggability, and predictable failure behavior.

## Tool
- `batch_execute`

## Input contract
- `commands`: ordered list of command objects
  - `tool`: target tool name
  - `params`: tool params object (optional, defaults to `{}`)
  - `rollback`: optional compensation command `{tool, params}` for soft rollback
- `max_commands_per_batch`: optional per-request cap (default `25`, hard max `100`)
- `fail_fast`: stop at first failure (`true` by default)
- `rollback_soft`: attempt best-effort rollback for completed commands after a failure (`false` by default)

## Guardrails
- Reject empty command list
- Reject batch size over configured limit
- Reject nested `batch_execute`
- Reject unknown tools and invalid params shape
- Preserve per-command result rows for transparency

## Failure modes
- Validation failures: canonical `error` with `E_INVALID_INPUT`
- Mixed success/failure batch: canonical `error` with `W_PARTIAL_SUCCESS`
- Unhandled exception in a command: command row marked `error` with `E_INTERNAL`

## Rollback semantics
- Rollback is **soft** and best-effort only
- No transactional guarantees across commands
- Rollback runs in reverse order of succeeded commands
- Missing rollback command is reported as `skipped`

## Result transparency
Response includes:
- `summary`: total/executed/succeeded/failed, flags, limit
- `results`: per-command status + response/error
- `rollback_results`: per-compensation outcome
