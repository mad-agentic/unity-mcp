# Tool Naming and Taxonomy (Week 1 SoT)

## Scope
This document is the single source of truth for:
- Tool naming rules
- Action naming rules
- Group + maturity taxonomy

## Tool naming rules
- Pattern: `verb_or_domain_noun` in snake_case
- Recommended style for manage tools: `manage_<domain>`
- Exception tools allowed for utility-only operations: `ping`, `refresh_unity`, `read_console`, `execute_menu_item`
- Avoid aliases for same capability (one canonical name)

Examples:
- `manage_scene`
- `manage_gameobject`
- `manage_asset`

## Action naming rules
- Use snake_case action names
- Use verbs from this set whenever possible:
  - `create`, `get`, `list`, `find`, `update`, `delete`, `rename`, `move`, `copy`, `add`, `remove`, `save`, `load`, `validate`
- Avoid semantic duplicates (`fetch` vs `get`, `destroy` vs `delete`) unless backward compatibility requires both

## Taxonomy model

### Group taxonomy
- `core`
- `vfx`
- `animation`
- `ui`
- `scripting_ext`
- `testing`
- `probuilder`

### Maturity taxonomy
- `core`: default, stable, user-facing
- `advanced`: feature-complete but higher complexity
- `experimental`: evolving API or behavior

## Contract with registry metadata
Each registered tool should expose:
- `name`
- `description`
- `group`
- `maturity`
- `annotations`

## Canonical tool inventory (audited 2026-03-24)

### Core (24)
- `batch_execute`
- `create_script`
- `execute_menu_item`
- `find_gameobjects`
- `get_project_info`
- `manage_asset`
- `manage_camera`
- `manage_components`
- `manage_editor`
- `manage_gameobject`
- `manage_material`
- `manage_packages`
- `manage_prefab`
- `manage_scene`
- `manage_script`
- `manage_shader`
- `manage_texture`
- `manage_tool_groups`
- `ping`
- `read_console`
- `refresh_unity`
- `scene_auto_repair`
- `script_apply_edits`
- `validate_script`

### UI (1)
- `manage_ui`

### VFX (1)
- `manage_vfx`

### Animation (1)
- `manage_animation`

## Review checklist
- No tool orphaned outside known groups
- Every new tool defines group + maturity
- Action names follow canonical verb set
