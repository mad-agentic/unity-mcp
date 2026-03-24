# Long-running Jobs Contract (Week 1 SoT)

## Goal
Define safe lifecycle for long operations and keep status resilient after server reload.

## Lifecycle
`start` -> `pending` -> `status` -> `result`

Terminal states:
- `succeeded`
- `failed`
- `cancelled`

Non-terminal states:
- `queued`
- `running`
- `pending`

## Canonical pending response
```json
{
  "status": "pending",
  "tool": "manage_packages",
  "message": "Job pending",
  "meta": {
    "job_id": "job-123",
    "poll_after_seconds": 1.0
  }
}
```

## Retry/timeout semantics
- Timeout should map to `E_TIMEOUT` with `retryable=true`
- Transport disconnection should map to `E_TRANSPORT_FAILURE` with retry hint

## Persistence after reload
Week 1 implementation adds file-backed store:
- Module: `Server/src/core/job_state_store.py`
- Data file default: `.unity_mcp/job_states.json`
- Capabilities:
  - upsert by `job_id`
  - get by `job_id`
  - delete by `job_id`
  - list by `tool`

This enables restoring in-progress/completed state snapshots after server restart.
