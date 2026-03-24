# Week 3 Demo + Tuning

Date: 2026-03-24

## Scenario 1 — Safe batch orchestration with partial failure

Flow:
1. Run `batch_execute` with 3 commands (success, failure, success candidate)
2. Enable `fail_fast=true` and `rollback_soft=true`
3. Verify execution stops at first failure and rollback attempts execute

Observed behavior:
- Batch returns canonical `error` with `W_PARTIAL_SUCCESS`
- Per-command rows show which step failed
- Rollback rows are visible and explicit (`success|error|skipped`)

Value:
- Failure transparency is high
- Operators can retry only failed commands

## Scenario 2 — Scene audit and constrained repair

Flow:
1. Run `scene_auto_repair mode=audit`
2. Review prioritized issues and planned safe fixes
3. Run `scene_auto_repair mode=repair dry_run=true` then `dry_run=false`

Observed behavior:
- Audit reports console-derived missing references and scene baseline gaps
- Repair auto-creates missing camera/light only
- Non-fixable issues remain actionable with guidance

Value:
- Safe defaults avoid destructive or speculative mutations
- Dry-run provides operator confidence before apply

## Tuning outcomes

- Verification Loop preflight added for sensitive actions (`manage_script`, `script_apply_edits`, `manage_scene`, `manage_packages`)
- Preflight failures now return early with actionable `next_actions`
- Transport readiness checked before sensitive mutation to reduce blind failures

## Fallback behavior

- Transport unavailable: return `E_UNITY_UNAVAILABLE` / `E_TRANSPORT_FAILURE` with reconnect steps
- Invalid sensitive input: return `E_INVALID_INPUT` before Unity call
- Batch partial completion: return `W_PARTIAL_SUCCESS` with granular result map
