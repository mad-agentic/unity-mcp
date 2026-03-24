"""MCP tool for applying text edits to Unity C# scripts: replace text, methods, insert, add using."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Apply text edits to Unity C# scripts: replace text, replace method bodies, "
    "insert code after methods, and add using statements.",
    group="core",
)
async def script_apply_edits(
    ctx: Context,
    action: Annotated[
        Literal["replace_text", "replace_method", "insert_after", "add_using"],
        "The edit operation to perform",
    ],
    script_path: Annotated[
        Optional[str],
        "Full path to the script file (relative to Assets). Used with all actions.",
    ] = None,
    old_text: Annotated[
        Optional[str],
        "The exact text to find and replace. Used with replace_text.",
    ] = None,
    new_text: Annotated[
        Optional[str],
        "The replacement text. Used with replace_text, replace_method, insert_after.",
    ] = None,
    method_name: Annotated[
        Optional[str],
        "Method name to target for replacement or insertion. Used with replace_method, insert_after.",
    ] = None,
    new_method_body: Annotated[
        Optional[str],
        "The new method body content. Used with replace_method.",
    ] = None,
    after_method: Annotated[
        Optional[str],
        "Insert code after this method name. Used with insert_after.",
    ] = None,
    using_statement: Annotated[
        Optional[str],
        "The using statement to add (e.g., 'using System.Linq;'). Used with add_using.",
    ] = None,
) -> dict[str, Any]:
    """Apply text edits to Unity C# scripts.

    Actions:
    - replace_text: Replace exact text in the script
    - replace_method: Replace an entire method body by method name
    - insert_after: Insert text after a specific method
    - add_using: Add a using statement to the top of the script
    """
    params: dict[str, Any] = {"action": action}

    if script_path is not None:
        params["script_path"] = script_path
    if old_text is not None:
        params["old_text"] = old_text
    if new_text is not None:
        params["new_text"] = new_text
    if method_name is not None:
        params["method_name"] = method_name
    if new_method_body is not None:
        params["new_method_body"] = new_method_body
    if after_method is not None:
        params["after_method"] = after_method
    if using_statement is not None:
        params["using_statement"] = using_statement

    return await execute_tool_with_contract("script_apply_edits", params)
