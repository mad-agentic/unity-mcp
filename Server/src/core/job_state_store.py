"""Persistent storage for long-running MCP job states.

Phase 1 foundation goal: keep job status resilient across server restarts/reloads.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

from core.job_contract import JobState, serialize_job_state


class JobStateStore:
    def __init__(self, file_path: str | None = None) -> None:
        default_path = Path(".unity_mcp") / "job_states.json"
        self.file_path = Path(file_path or os.getenv("UNITY_MCP_JOB_STATE_FILE", str(default_path)))
        self.file_path.parent.mkdir(parents=True, exist_ok=True)

    def load_all(self) -> dict[str, dict[str, Any]]:
        if not self.file_path.exists():
            return {}
        try:
            payload = json.loads(self.file_path.read_text(encoding="utf-8"))
            if isinstance(payload, dict):
                return payload
            return {}
        except Exception:
            return {}

    def save_all(self, states: dict[str, dict[str, Any]]) -> None:
        self.file_path.parent.mkdir(parents=True, exist_ok=True)
        self.file_path.write_text(
            json.dumps(states, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

    def upsert(self, state: JobState) -> dict[str, Any]:
        states = self.load_all()
        payload = serialize_job_state(state)
        states[state.job_id] = payload
        self.save_all(states)
        return payload

    def get(self, job_id: str) -> dict[str, Any] | None:
        states = self.load_all()
        value = states.get(job_id)
        return value if isinstance(value, dict) else None

    def delete(self, job_id: str) -> bool:
        states = self.load_all()
        if job_id not in states:
            return False
        del states[job_id]
        self.save_all(states)
        return True

    def list_by_tool(self, tool_name: str) -> list[dict[str, Any]]:
        states = self.load_all()
        result: list[dict[str, Any]] = []
        for value in states.values():
            if isinstance(value, dict) and value.get("tool") == tool_name:
                result.append(value)
        return result
