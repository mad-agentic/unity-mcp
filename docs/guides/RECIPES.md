# Common Recipes

## Scene recipe
Prompt:
`Create a new scene named DemoScene and save it under Assets/Scenes.`

Expected outcome:
- `manage_scene` returns a canonical `success` envelope.
- Scene name and saved path are present in `data`.

## Object recipe
Prompt:
`Create a red cube at position 0,1,0 and add a Rigidbody.`

Expected outcome:
- `manage_gameobject` creates the object.
- `manage_components` adds `Rigidbody`.
- Both tool calls return canonical envelopes with `status`, `message`, and `data`.

## Script recipe
Prompt:
`Create a PlayerMover MonoBehaviour with Start and Update methods.`

Expected outcome:
- `create_script` or `manage_script` creates the file.
- `validate_script` returns success or actionable compile guidance.

## Package recipe
Prompt:
`List installed Unity packages and install TextMeshPro if missing.`

Expected outcome:
- `manage_packages` returns package list in a canonical envelope.
- Long-running actions can return `pending` with `meta.job_id`.

## Debug recipe
Prompt:
`Read the latest Unity errors and refresh the editor.`

Expected outcome:
- `read_console` returns filtered logs.
- `refresh_unity` returns success or pending with follow-up guidance.