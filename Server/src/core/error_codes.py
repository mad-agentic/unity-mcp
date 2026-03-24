"""Standardized error codes for Phase 1 foundation contracts."""

from __future__ import annotations

from typing import Final

ERROR_INVALID_INPUT: Final[str] = "E_INVALID_INPUT"
ERROR_UNITY_UNAVAILABLE: Final[str] = "E_UNITY_UNAVAILABLE"
ERROR_TRANSPORT_FAILURE: Final[str] = "E_TRANSPORT_FAILURE"
ERROR_TIMEOUT: Final[str] = "E_TIMEOUT"
ERROR_INTERNAL: Final[str] = "E_INTERNAL"

WARNING_PARTIAL_SUCCESS: Final[str] = "W_PARTIAL_SUCCESS"

ERROR_CATALOG: Final[dict[str, str]] = {
    ERROR_INVALID_INPUT: "Request data is invalid or incomplete.",
    ERROR_UNITY_UNAVAILABLE: "Unity instance is not available or not connected.",
    ERROR_TRANSPORT_FAILURE: "Failed to communicate with Unity transport.",
    ERROR_TIMEOUT: "Operation timed out before completion.",
    ERROR_INTERNAL: "Unexpected internal server error.",
    WARNING_PARTIAL_SUCCESS: "Operation finished with partial success.",
}
