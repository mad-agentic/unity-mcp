"""Constants used throughout the Unity MCP server."""

# Transport
DEFAULT_TRANSPORT = "stdio"
DEFAULT_HTTP_HOST = "127.0.0.1"
DEFAULT_HTTP_PORT = 8080
DEFAULT_HTTP_URL = f"http://{DEFAULT_HTTP_HOST}:{DEFAULT_HTTP_PORT}"

# HTTP headers
API_KEY_HEADER = "X-API-Key"
UNITY_INSTANCE_HEADER = "X-Unity-Instance"
CLIENT_ID_HEADER = "X-Client-ID"
CONTENT_TYPE_JSON = "application/json"

# WebSocket paths
WS_HUB_PATH = "/hub/plugin"
WS_UNITY_PATH = "/unity"

# MCP URI scheme
MCP_URI_SCHEME = "unitymcp://"

# Tool groups
TOOL_GROUP_CORE = "core"
TOOL_GROUP_VFX = "vfx"
TOOL_GROUP_ANIMATION = "animation"
TOOL_GROUP_UI = "ui"
TOOL_GROUP_SCRIPTING_EXT = "scripting_ext"
TOOL_GROUP_TESTING = "testing"
TOOL_GROUP_PROBUILDER = "probuilder"

# Default tool groups
DEFAULT_ENABLED_GROUPS = {TOOL_GROUP_CORE}

# All tool groups
ALL_TOOL_GROUPS = {
    TOOL_GROUP_CORE,
    TOOL_GROUP_VFX,
    TOOL_GROUP_ANIMATION,
    TOOL_GROUP_UI,
    TOOL_GROUP_SCRIPTING_EXT,
    TOOL_GROUP_TESTING,
    TOOL_GROUP_PROBUILDER,
}

# HTTP route limits
MAX_REQUEST_SIZE = 10 * 1024 * 1024  # 10MB

# Retries
DEFAULT_MAX_RETRIES = 3
DEFAULT_RETRY_DELAY = 0.5

# Timeouts
DEFAULT_HTTP_TIMEOUT = 30.0
LONG_RUNNING_TIMEOUT = 300.0  # 5 minutes for build, tests, etc.

# Unity instance
DEFAULT_INSTANCE_MARKER = "__default__"
