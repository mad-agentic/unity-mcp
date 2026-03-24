# Response and Error Contract (Week 1 SoT)

## Canonical response envelope
All server-side tool wrappers should normalize output to this structure:

```json
{
  "status": "success|error|pending",
  "tool": "manage_scene",
  "message": "human readable summary",
  "data": {},
  "error": null,
  "next_actions": [],
  "meta": {}
}
```

## `status` semantics
- `success`: operation completed
- `error`: operation failed with actionable error payload
- `pending`: long-running operation started and requires polling

## Error payload
```json
{
  "code": "E_*|W_*",
  "retryable": true,
  "details": "optional technical detail"
}
```

## Core error code matrix (P0/P1 focus)
- `E_INVALID_INPUT`: invalid/missing request fields
- `E_UNITY_UNAVAILABLE`: Unity instance disconnected/unreachable
- `E_TRANSPORT_FAILURE`: bridge/protocol communication failure
- `E_TIMEOUT`: operation timed out
- `E_INTERNAL`: unexpected server-side error
- `W_PARTIAL_SUCCESS`: partial completion

## Python ↔ Unity format harmonization
- Unity payload may come in legacy/raw format
- Python wrapper converts raw result into canonical envelope
- If Unity returns pending-like payload (`_mcp_status=pending`), wrapper maps it to canonical `pending`

## Migration policy
- Backward compatibility is preserved at transport boundary
- Canonical envelope is guaranteed at server tool wrapper output
