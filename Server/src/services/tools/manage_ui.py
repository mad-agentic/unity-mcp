"""MCP tool for managing Unity UI elements using UXML (UI Toolkit) and USS stylesheets."""

from __future__ import annotations

from typing import Annotated, Any, Literal, Optional

from fastmcp import Context

from services.registry import mcp_for_unity_tool
from services.tools.utils import execute_tool_with_contract


@mcp_for_unity_tool(
    description="Create and manage Unity UI Toolkit (UI Elements) documents: UXML templates, "
    "USS stylesheets, UI elements, and style properties. Group: ui.",
    group="ui",
)
async def manage_ui(
    ctx: Context,
    action: Annotated[
        Literal["create_uxml", "create_uss", "attach_to_document", "get_elements", "set_style"],
        "The UI operation to perform",
    ],
    document_path: Annotated[
        Optional[str],
        "Path to a UXML or USS file (e.g. 'Assets/UI/myui.uxml')",
    ] = None,
    element_type: Annotated[
        Optional[str],
        "UI element type to create (e.g. 'VisualElement', 'Button', 'Label', 'Toggle', "
        "'Slider', 'TextField', 'ScrollView', 'ListView')",
    ] = None,
    parent_path: Annotated[
        Optional[str],
        "Path or name of parent element within the document (for attach operations)",
    ] = None,
    uss_properties: Annotated[
        Optional[dict[str, Any]],
        "USS property overrides as key-value pairs "
        "(e.g. {'width': '200px', 'height': '50px', 'color': '#ff0000'})",
    ] = None,
    style_path: Annotated[
        Optional[str],
        "Path to USS stylesheet file or selector (e.g. 'Assets/UI/styles.uss' or '.my-class')",
    ] = None,
    name: Annotated[
        Optional[str],
        "Name/identifier for created UI elements",
    ] = None,
) -> dict[str, Any]:
    """Manage Unity UI Toolkit (UITK) elements.

    Actions:
    - create_uxml: Create a new UXML document template
    - create_uss: Create a new USS stylesheet
    - attach_to_document: Attach a UI element to an existing document
    - get_elements: Get UI elements from a document by name or type
    - set_style: Apply USS property overrides to UI elements
    """
    params: dict[str, Any] = {"action": action}

    if document_path is not None:
        params["document_path"] = document_path
    if element_type is not None:
        params["element_type"] = element_type
    if parent_path is not None:
        params["parent_path"] = parent_path
    if uss_properties is not None:
        params["uss_properties"] = uss_properties
    if style_path is not None:
        params["style_path"] = style_path
    if name is not None:
        params["name"] = name

    return await execute_tool_with_contract("manage_ui", params)
