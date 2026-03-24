# Scene Auto-Repair (Week 3 SoT)

## Goal
Provide an operational loop to detect common scene issues and safely auto-fix a constrained subset with explicit dry-run.

## Tool
- `scene_auto_repair`

## Modes
- `audit`: detect and prioritize issues, no mutations
- `repair`: plan fixes and optionally apply safe fixes

## Inputs
- `mode`: `audit | repair`
- `dry_run`: defaults to `true`; when true, no changes are applied
- `console_scan_count`: number of console error entries to analyze (default `200`)

## Detection sources
- Console errors (`read_console get_errors`) for missing script/reference patterns
- Scene scan (`find_gameobjects`) for baseline critical objects

## Current issue classes
- `missing_script_reference` (console pattern)
- `missing_object_reference` (console pattern)
- `missing_main_camera` (no Camera found)
- `missing_directional_light` (no Light found)

## Current safe auto-fixes
- Create `Main Camera` with `Camera` component and `MainCamera` tag
- Create `Directional Light` with `Light` component

Console-derived missing references are reported but not auto-fixed in current version.

## Output contract
- Canonical envelope with:
  - `summary` (total/fixable/unresolved/planned/applied)
  - `issues` (priority + fixability)
  - `planned_actions`
  - `applied_actions`
  - `unresolved_issues`

## Safety guarantees
- `dry_run=true` never mutates scene
- Only predefined safe fixes are auto-applied
- Non-fixable issues always return actionable guidance
