"""Utility functions for tool implementations."""

from __future__ import annotations

import json
from typing import Any


def parse_json_payload(value: Any) -> Any:
    """Parse a JSON string payload, returning the parsed object."""
    if isinstance(value, str):
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value
    return value


def coerce_bool(value: Any, default: bool = False) -> bool:
    """Coerce a value to a boolean."""
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.lower() in ("true", "1", "yes", "on")
    return bool(value)


def normalize_string_list(value: Any) -> list[str]:
    """Normalize a value to a list of strings."""
    if value is None:
        return []
    if isinstance(value, str):
        # Try parsing as JSON array
        try:
            parsed = json.loads(value)
            if isinstance(parsed, list):
                return [str(v) for v in parsed]
        except json.JSONDecodeError:
            # Split by comma
            return [s.strip() for s in value.split(",") if s.strip()]
    if isinstance(value, list):
        return [str(v) for v in value]
    return [str(value)]
