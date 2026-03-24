using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Camera management tool supporting list, get, create, set_transform, set_background,
    /// add_component, and set_orthographic operations.
    /// </summary>
    [McpForUnityTool("manage_camera", Group = "core",
        Description = "Manage Unity cameras: list, get, create, set transform, set background color, add components, and configure orthographic mode.")]
    public static class ManageCamera
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "list");

                switch (action.ToLowerInvariant())
                {
                    case "list":
                        return ListCameras();
                    case "get":
                        return GetCamera(p);
                    case "create":
                        return CreateCamera(p);
                    case "set_transform":
                    case "settransform":
                        return SetTransform(p);
                    case "set_background":
                    case "setbackground":
                        return SetBackground(p);
                    case "add_component":
                    case "addcomponent":
                        return AddComponent(p);
                    case "set_orthographic":
                    case "setorthographic":
                        return SetOrthographic(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: list, get, create, set_transform, set_background, add_component, set_orthographic.");
                }
            }
            catch (ArgumentException ex)
            {
                return new ErrorResponse("InvalidParameters", ex.Message);
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.GetType().Name, ex.Message);
            }
        }

        private static object ListCameras()
        {
            var cameras = Camera.allCameras;
            var results = new List<object>();

            foreach (var cam in cameras)
            {
                results.Add(new
                {
                    name = cam.name,
                    instance_id = cam.gameObject.GetInstanceID(),
                    gameobject_name = cam.gameObject.name,
                    depth = cam.depth,
                    field_of_view = cam.fieldOfView,
                    orthographic_size = cam.orthographicSize,
                    is_orthographic = cam.orthographic,
                    clear_flags = cam.clearFlags.ToString(),
                    background_color = new[] { cam.backgroundColor.r, cam.backgroundColor.g, cam.backgroundColor.b, cam.backgroundColor.a },
                    culling_mask = cam.cullingMask,
                    near_clip = cam.nearClipPlane,
                    far_clip = cam.farClipPlane,
                    rect = new[] { cam.rect.x, cam.rect.y, cam.rect.width, cam.rect.height },
                    pixel_rect = new[] { cam.pixelRect.x, cam.pixelRect.y, cam.pixelRect.width, cam.pixelRect.height },
                    aspect = cam.aspect,
                    rendering_path = cam.renderingPath.ToString(),
                    target_texture = cam.targetTexture != null ? cam.targetTexture.name : null,
                    active = cam.enabled,
                    scene = cam.gameObject.scene.name,
                    tag = cam.tag,
                    target_display = cam.targetDisplay,
                });
            }

            return new SuccessResponse(
                $"Found {results.Count} active camera(s).",
                new
                {
                    count = results.Count,
                    cameras = results
                });
        }

        private static object GetCamera(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var cam = go.GetComponent<Camera>();
            if (cam == null)
            {
                return new ErrorResponse("NoCamera",
                    $"GameObject '{go.name}' does not have a Camera component.");
            }

            return new SuccessResponse($"Camera info for '{go.name}'.", GetCameraInfo(cam));
        }

        private static object CreateCamera(ToolParams p)
        {
            var cameraName = p.GetString("camera_name", "Main Camera");
            var positionArray = p.GetArray("position");
            var rotationArray = p.GetArray("rotation");
            var isOrthographic = p.GetBool("is_orthographic");
            var fov = p.GetFloat("fov", 60f);
            var orthographicSize = p.GetFloat("orthographic_size", 5f);
            var backgroundColorArray = p.GetArray("background_color");
            var depth = p.GetFloat("depth");
            var cullingMask = p.GetInt("culling_mask");

            try
            {
                // Check if a Main Camera already exists
                Camera existingMain = null;
                if (cameraName == "Main Camera")
                {
                    var allCameras = Object.FindObjectsOfType<Camera>();
                    foreach (var c in allCameras)
                    {
                        if (c.tag == "MainCamera")
                        {
                            existingMain = c;
                            break;
                        }
                    }
                }

                Camera newCam;
                if (existingMain != null)
                {
                    newCam = existingMain;
                }
                else
                {
                    var camObj = new GameObject(cameraName);
                    newCam = camObj.AddComponent<Camera>();

                    if (cameraName == "Main Camera")
                    {
                        camObj.tag = "MainCamera";
                    }
                }

                // Apply transform
                if (positionArray != null && positionArray.Count >= 3)
                {
                    newCam.transform.position = new Vector3(
                        positionArray[0].Value<float>(),
                        positionArray[1].Value<float>(),
                        positionArray[2].Value<float>());
                }
                if (rotationArray != null && rotationArray.Count >= 3)
                {
                    newCam.transform.eulerAngles = new Vector3(
                        rotationArray[0].Value<float>(),
                        rotationArray[1].Value<float>(),
                        rotationArray[2].Value<float>());
                }

                // Apply camera settings
                if (isOrthographic.HasValue)
                {
                    newCam.orthographic = isOrthographic.Value;
                }
                if (fov.HasValue && !newCam.orthographic)
                {
                    newCam.fieldOfView = fov.Value;
                }
                if (orthographicSize.HasValue && newCam.orthographic)
                {
                    newCam.orthographicSize = orthographicSize.Value;
                }
                if (backgroundColorArray != null && backgroundColorArray.Count >= 3)
                {
                    float r = backgroundColorArray[0].Value<float>();
                    float g = backgroundColorArray[1].Value<float>();
                    float b = backgroundColorArray[2].Value<float>();
                    float a = backgroundColorArray.Count >= 4 ? backgroundColorArray[3].Value<float>() : 1.0f;
                    newCam.backgroundColor = new Color(r, g, b, a);
                }
                if (depth.HasValue)
                {
                    newCam.depth = depth.Value;
                }
                if (cullingMask.HasValue)
                {
                    newCam.cullingMask = cullingMask.Value;
                }

                return new SuccessResponse($"Camera '{newCam.name}' created/configured.", GetCameraInfo(newCam));
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateCameraFailed",
                    $"Failed to create camera: {ex.Message}");
            }
        }

        private static object SetTransform(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var positionArray = p.GetArray("position");
            var rotationArray = p.GetArray("rotation");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var cam = go.GetComponent<Camera>();
            if (cam == null)
            {
                return new ErrorResponse("NoCamera",
                    $"GameObject '{go.name}' does not have a Camera component.");
            }

            try
            {
                if (positionArray != null && positionArray.Count >= 3)
                {
                    cam.transform.position = new Vector3(
                        positionArray[0].Value<float>(),
                        positionArray[1].Value<float>(),
                        positionArray[2].Value<float>());
                }
                if (rotationArray != null && rotationArray.Count >= 3)
                {
                    cam.transform.eulerAngles = new Vector3(
                        rotationArray[0].Value<float>(),
                        rotationArray[1].Value<float>(),
                        rotationArray[2].Value<float>());
                }

                return new SuccessResponse($"Camera transform updated for '{go.name}'.", new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    position = new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z },
                    rotation = new[] { cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z },
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetTransformFailed",
                    $"Failed to set camera transform: {ex.Message}");
            }
        }

        private static object SetBackground(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var colorArray = p.RequireArray("color");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var cam = go.GetComponent<Camera>();
            if (cam == null)
            {
                return new ErrorResponse("NoCamera",
                    $"GameObject '{go.name}' does not have a Camera component.");
            }

            try
            {
                float r = colorArray[0].Value<float>();
                float g = colorArray[1].Value<float>();
                float b = colorArray[2].Value<float>();
                float a = colorArray.Count >= 4 ? colorArray[3].Value<float>() : 1.0f;
                cam.backgroundColor = new Color(r, g, b, a);

                return new SuccessResponse($"Background color set for '{go.name}'.", new
                {
                    name = go.name,
                    background_color = new[] { r, g, b, a }
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetBackgroundFailed",
                    $"Failed to set background color: {ex.Message}");
            }
        }

        private static object AddComponent(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var componentType = p.RequireString("component_type");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            try
            {
                // Try to add the component by type name
                Type compType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic) continue;
                    compType = assembly.GetType($"UnityEngine.{componentType}");
                    if (compType != null) break;
                    compType = assembly.GetType(componentType);
                    if (compType != null) break;
                }

                if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                {
                    return new ErrorResponse("ComponentTypeNotFound",
                        $"Component type '{componentType}' not found.");
                }

                var existing = go.GetComponent(compType);
                if (existing != null)
                {
                    return new SuccessResponse(
                        $"Component '{componentType}' already exists on '{go.name}'.", new
                        {
                            name = go.name,
                            component_type = componentType,
                            already_exists = true,
                        });
                }

                var newComp = go.AddComponent(compType);

                return new SuccessResponse($"Component '{componentType}' added to '{go.name}'.", new
                {
                    name = go.name,
                    component_type = componentType,
                    instance_id = newComp.GetInstanceID(),
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("AddComponentFailed",
                    $"Failed to add component: {ex.Message}");
            }
        }

        private static object SetOrthographic(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var isOrthographic = p.RequireBool("is_orthographic");
            var fov = p.GetFloat("fov");
            var orthographicSize = p.GetFloat("orthographic_size");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var cam = go.GetComponent<Camera>();
            if (cam == null)
            {
                return new ErrorResponse("NoCamera",
                    $"GameObject '{go.name}' does not have a Camera component.");
            }

            try
            {
                cam.orthographic = isOrthographic;
                if (isOrthographic && orthographicSize.HasValue)
                {
                    cam.orthographicSize = orthographicSize.Value;
                }
                else if (!isOrthographic && fov.HasValue)
                {
                    cam.fieldOfView = fov.Value;
                }

                return new SuccessResponse(
                    $"Camera '{go.name}' set to {(isOrthographic ? "orthographic" : "perspective")} mode.",
                    new
                    {
                        name = go.name,
                        is_orthographic = cam.orthographic,
                        field_of_view = cam.fieldOfView,
                        orthographic_size = cam.orthographicSize,
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetOrthographicFailed",
                    $"Failed to set orthographic mode: {ex.Message}");
            }
        }

        private static GameObject ResolveGameObject(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            if (int.TryParse(identifier, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                return obj as GameObject;
            }

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name == identifier && !EditorUtility.IsPersistent(go))
                    return go;
            }
            return null;
        }

        private static object GetCameraInfo(Camera cam)
        {
            return new
            {
                name = cam.name,
                instance_id = cam.gameObject.GetInstanceID(),
                gameobject_name = cam.gameObject.name,
                depth = cam.depth,
                field_of_view = cam.fieldOfView,
                orthographic_size = cam.orthographicSize,
                is_orthographic = cam.orthographic,
                clear_flags = cam.clearFlags.ToString(),
                background_color = new[] { cam.backgroundColor.r, cam.backgroundColor.g, cam.backgroundColor.b, cam.backgroundColor.a },
                culling_mask = cam.cullingMask,
                near_clip = cam.nearClipPlane,
                far_clip = cam.farClipPlane,
                rect = new[] { cam.rect.x, cam.rect.y, cam.rect.width, cam.rect.height },
                aspect = cam.aspect,
                rendering_path = cam.renderingPath.ToString(),
                target_texture = cam.targetTexture != null ? cam.targetTexture.name : null,
                active = cam.enabled,
                scene = cam.gameObject.scene.name,
                tag = cam.tag,
                position = new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z },
                rotation = new[] { cam.transform.eulerAngles.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z },
            };
        }
    }
}
