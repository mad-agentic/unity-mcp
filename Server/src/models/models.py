"""Pydantic models for MCP server request/response types."""

from __future__ import annotations

from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field


class MCPError(BaseModel):
    """Standard MCP error response."""
    code: int
    message: str
    data: Optional[Any] = None


class MCPSuccessResponse(BaseModel):
    """Standard MCP success response."""
    success: bool = True
    data: Optional[Any] = None
    message: Optional[str] = None


class UnityCommandRequest(BaseModel):
    """Request sent from Python server to Unity editor."""
    command: str
    params: Dict[str, Any] = Field(default_factory=dict)
    unity_instance: Optional[str] = None
    client_id: Optional[str] = None


class UnityCommandResponse(BaseModel):
    """Response from Unity editor to Python server."""
    success: bool
    data: Optional[Any] = None
    error: Optional[str] = None
    message: Optional[str] = None


class ToolDefinition(BaseModel):
    """MCP tool definition."""
    name: str
    description: str
    inputSchema: Dict[str, Any] = Field(default_factory=dict)
    annotations: Optional[Dict[str, Any]] = None
    group: str = "core"


class ResourceDefinition(BaseModel):
    """MCP resource definition."""
    uri: str
    name: str
    description: str
    mimeType: str = "application/json"


class UnityInstanceInfo(BaseModel):
    """Information about a connected Unity editor instance."""
    name: str
    project_path: str
    project_name: str
    hash_id: str
    unity_version: str
    platform: str
    connected_at: float
    api_key: Optional[str] = None


class ToolGroupState(BaseModel):
    """State of a tool group."""
    name: str
    enabled: bool
    tool_count: int
    tools: List[str]
