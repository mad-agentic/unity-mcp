"""MCP tool for validating Unity C# scripts: syntax checks, compilation, and error reporting."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from transport.unity_transport import send_with_unity_instance


@mcp_for_unity_tool(
    description="Validate Unity C# scripts: syntax checks, full compilation via Roslyn, "
    "and detailed error/warning reporting.",
    group="core",
)
async def validate_script(
    ctx: Context,
    action: Annotated[
        Literal["syntax_check", "full_compilation", "get_errors"],
        "The validation operation to perform",
    ],
    script_path: Annotated[
        Optional[str],
        "Full path to the script file (relative to Assets). Used with all actions.",
    ] = None,
    full_check: Annotated[
        Optional[bool],
        "If true, perform a full Roslyn compilation check including type resolution. "
        "If false, perform a lightweight syntax-only check. Used with syntax_check.",
    ] = False,
) -> dict[str, Any]:
    """Validate Unity C# scripts.

    Actions:
    - syntax_check: Perform a syntax-level validation of the script (lightweight or full)
    - full_compilation: Trigger a full Unity compilation and report all results
    - get_errors: Get existing compilation errors and warnings for the script
    """
    params: dict[str, Any] = {"action": action}

    if script_path is not None:
        params["script_path"] = script_path
    if full_check is not None:
        params["full_check"] = full_check

    result = await send_with_unity_instance(None, "validate_script", params)
    return result
