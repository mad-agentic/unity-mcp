---
name: unity-mcp-orchestrator
description: Orchestrate Unity Editor via MCP (Model Context Protocol) tools and resources. Use when working with Unity projects through Unity MCP - creating/modifying GameObjects, editing scripts, managing scenes, running tests, or any Unity Editor automation. Provides best practices, tool schemas, and workflow patterns for effective Unity-MCP integration.
---

# Unity MCP Operator Guide

This skill helps you effectively use the Unity Editor with MCP tools and resources.

## Quick Start: Resource-First Workflow

**Always read relevant resources before using tools.**

```
1. Check editor state     → mcp://editor/state
2. Understand the scene  → manage_scene(action="list") or find_gameobjects
3. Find what you need    → find_gameobjects, get_project_info
4. Take action           → tools (manage_gameobject, create_script, script_apply_edits, etc.)
5. Verify results        → read_console, manage_editor
```

## Critical Best Practices

### 1. After Writing/Editing Scripts: Wait for Compilation

```python
# create_script and script_apply_edits trigger import + compilation automatically.
# Just wait, then check for errors:

read_console(action="get", types=["error"], count=10)
```

### 2. Use `batch_execute` for Multiple Operations

```python
# Faster than sequential calls
batch_execute(
    commands=[
        {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube1", "primitive_type": "Cube"}},
        {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube2", "primitive_type": "Cube"}},
        {"tool": "manage_gameobject", "params": {"action": "create", "name": "Cube3", "primitive_type": "Cube"}}
    ]
)
```

### 3. Check Console After Major Changes

```python
read_console(action="get", types=["error", "warning"], count=10)
```

### 4. Check `editor_state` Before Complex Operations

```python
# Read mcp://editor/state to check:
# - is_compiling: Wait if true
# - is_playing: Check before play mode operations
# - ready: Only proceed if true
```

## Parameter Conventions

### Vectors
```python
position=[1.0, 2.0, 3.0]      # List [x, y, z]
position={"x": 1.0, "y": 2.0, "z": 3.0}  # Dict
```

### Colors
```python
color=[1.0, 0.0, 0.0, 1.0]  # RGBA 0-1 range
```

### Paths
```python
path="Assets/Scripts/MyScript.cs"  # Assets-relative
```

## Tool Categories

| Category | Tools | Use For |
|----------|-------|---------|
| **Scene** | `manage_scene` | Scene CRUD, hierarchy, load/save |
| **Objects** | `manage_gameobject`, `find_gameobjects` | GameObject CRUD, search |
| **Components** | `manage_components` | Add/remove/set component properties |
| **Scripts** | `manage_script`, `create_script`, `script_apply_edits`, `validate_script` | C# code (auto-refreshes) |
| **Materials** | `manage_material`, `manage_shader`, `manage_texture` | Material and shader ops |
| **Assets** | `manage_asset`, `manage_prefabs` | Asset CRUD, prefab ops |
| **Editor** | `manage_editor`, `refresh_unity`, `execute_menu_item`, `read_console` | Editor control |
| **Camera** | `manage_camera` | Camera management |
| **Packages** | `manage_packages` | Unity package manager |
| **UI** | `manage_ui` | UI Toolkit (UXML/USS) |
| **Animation** | `manage_animation` | Animator controllers, clips |
| **VFX** | `manage_vfx` | Particle systems |

## Common Workflows

### Creating a Script
```python
# 1. Create (auto-triggers compilation)
create_script(
    path="Assets/Scripts/Player.cs",
    class_name="Player",
    template="MonoBehaviour"
)

# 2. Wait, check errors
read_console(action="get", types=["error"], count=10)

# 3. Attach to GameObject
manage_components(action="add", gameobject="Player", component_type="Player")
```

### Creating GameObjects
```python
manage_gameobject(
    action="create",
    name="MyCube",
    primitive_type="Cube",
    position=[0, 1, 0]
)
```

### Finding Objects
```python
find_gameobjects(name="Player", search_method="by_name")
manage_gameobject(action="get", target="Player")
```

## Error Recovery

| Symptom | Cause | Solution |
|---------|-------|----------|
| "compiling" | Script compilation | Wait, check console |
| "not found" | Wrong identifier | Use find_gameobjects to get correct ID |
| Connection lost | Domain reload | Wait 5s, retry |

## Tool Schemas

### manage_gameobject
```
action: create|delete|rename|duplicate|get|set_transform|set_parent|set_tag|set_layer|find|list
name: str (for create)
target: str (for modify/delete — by name, path, or ID)
primitive_type: Cube|Sphere|Plane|Capsule|Cylinder|Quad
position/rotation/scale: list[float]
tag/layer: str or int
parent: str (parent GameObject reference)
components_to_add: list[str]
```

### manage_scene
```
action: list|get_current|save|save_all|load|create|add_to_build|remove_from_build
scene_name: str
scene_path: str
force: bool
```

### manage_components
```
action: add|remove|get|set_property|get_property|list_types|has
gameobject: str (target)
component_type: str (e.g. Rigidbody, BoxCollider)
property_name/value: str
```

### manage_editor
```
action: play|pause|stop|step|is_playing|set_playmode_toggle|get_selection|set_selection|focus_window
gameobjects: list[str] (for selection)
window_type: str
```

### manage_script
```
action: create|get|rename|delete|get_methods|get_properties
name/class_name/template/namespace: str
script_path: str
target: str
```
