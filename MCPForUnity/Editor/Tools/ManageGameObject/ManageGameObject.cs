using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Full GameObject CRUD tool supporting create, delete, rename, duplicate,
    /// get, set_transform, set_parent, set_tag, set_layer, find, and list.
    /// </summary>
    [McpForUnityTool("manage_gameobject", group = "core",
        description = "Manage GameObjects: create, delete, rename, duplicate, get info, set transform/parent/tag/layer, find, and list root objects.")]
    public static class ManageGameObject
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "list");

                switch (action.ToLowerInvariant())
                {
                    case "create":
                        return CreateGameObject(p);
                    case "delete":
                        return DeleteGameObject(p);
                    case "rename":
                        return RenameGameObject(p);
                    case "duplicate":
                        return DuplicateGameObject(p);
                    case "get":
                        return GetGameObject(p);
                    case "set_transform":
                    case "settransform":
                        return SetTransform(p);
                    case "set_parent":
                    case "setparent":
                        return SetParent(p);
                    case "set_tag":
                    case "settag":
                        return SetTag(p);
                    case "set_layer":
                    case "setlayer":
                        return SetLayer(p);
                    case "find":
                        return FindGameObjects(p);
                    case "list":
                        return ListRootGameObjects(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, delete, rename, duplicate, get, set_transform, set_parent, set_tag, set_layer, find, list.");
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

        private static object CreateGameObject(ToolParams p)
        {
            var name = p.GetString("name", "GameObject");
            var primitiveType = p.GetString("type");
            var positionArray = p.GetArray("position");
            var rotationArray = p.GetArray("rotation");
            var scaleArray = p.GetArray("scale");
            var sceneName = p.GetString("scene");

            GameObject go;

            if (!string.IsNullOrEmpty(primitiveType) && GameObjectHelpers.TryParsePrimitiveType(primitiveType, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            // Apply transform
            if (positionArray != null && positionArray.Count >= 3)
            {
                go.transform.position = new Vector3(
                    positionArray[0].Value<float>(),
                    positionArray[1].Value<float>(),
                    positionArray[2].Value<float>());
            }
            if (rotationArray != null && rotationArray.Count >= 3)
            {
                go.transform.eulerAngles = new Vector3(
                    rotationArray[0].Value<float>(),
                    rotationArray[1].Value<float>(),
                    rotationArray[2].Value<float>());
            }
            if (scaleArray != null && scaleArray.Count >= 3)
            {
                go.transform.localScale = new Vector3(
                    scaleArray[0].Value<float>(),
                    scaleArray[1].Value<float>(),
                    scaleArray[2].Value<float>());
            }

            // Move to specific scene if requested
            if (!string.IsNullOrEmpty(sceneName))
            {
                var targetScene = SceneManager.GetSceneByName(sceneName);
                if (targetScene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(go, targetScene);
                }
            }

            return new SuccessResponse($"GameObject '{go.name}' created.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                rotation = new[] { go.transform.eulerAngles.x, go.transform.eulerAngles.y, go.transform.eulerAngles.z },
                scale = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z },
                scene = go.scene.name,
                tag = go.tag,
                layer = go.layer,
                layer_name = LayerMask.LayerToName(go.layer),
            });
        }

        private static object DeleteGameObject(ToolParams p)
        {
            var identifier = p.RequireString("identifier");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            var name = go.name;
            var scene = go.scene.name;

            UnityEngine.Object.DestroyImmediate(go);

            return new SuccessResponse($"GameObject '{name}' deleted.", new
            {
                name = name,
                scene = scene,
            });
        }

        private static object RenameGameObject(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var newName = p.RequireString("name");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            var oldName = go.name;
            go.name = newName;

            return new SuccessResponse($"GameObject renamed from '{oldName}' to '{newName}'.", new
            {
                old_name = oldName,
                new_name = newName,
                instance_id = go.GetInstanceID(),
            });
        }

        private static object DuplicateGameObject(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var newName = p.GetString("name");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            var duplicated = UnityEngine.Object.Instantiate(go);
            duplicated.name = string.IsNullOrEmpty(newName) ? go.name + "(1)" : newName;

            // Move to same scene as original
            SceneManager.MoveGameObjectToScene(duplicated, go.scene);

            return new SuccessResponse($"GameObject '{go.name}' duplicated as '{duplicated.name}'.", new
            {
                original_name = go.name,
                duplicate_name = duplicated.name,
                original_instance_id = go.GetInstanceID(),
                duplicate_instance_id = duplicated.GetInstanceID(),
                position = new[] { duplicated.transform.position.x, duplicated.transform.position.y, duplicated.transform.position.z },
            });
        }

        private static object GetGameObject(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var includeChildren = p.GetBool("include_children", false);

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            return new SuccessResponse($"GameObject '{go.name}' info retrieved.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                active_self = go.activeSelf,
                active_in_hierarchy = go.activeInHierarchy,
                is_static = go.isStatic,
                tag = go.tag,
                layer = go.layer,
                layer_name = LayerMask.LayerToName(go.layer),
                scene = go.scene.name,
                scene_path = GameObjectHelpers.GetScenePath(go),
                transform = new
                {
                    position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                    local_position = new[] { go.transform.localPosition.x, go.transform.localPosition.y, go.transform.localPosition.z },
                    rotation = new[] { go.transform.eulerAngles.x, go.transform.eulerAngles.y, go.transform.eulerAngles.z },
                    local_rotation = new[] { go.transform.localEulerAngles.x, go.transform.localEulerAngles.y, go.transform.localEulerAngles.z },
                    scale = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z },
                    local_scale = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z },
                    parent_name = go.transform.parent != null ? go.transform.parent.name : null,
                    parent_instance_id = go.transform.parent != null ? go.transform.parent.GetInstanceID() : 0,
                    child_count = go.transform.childCount,
                },
                components = GameObjectHelpers.GetComponentNames(go),
                children = includeChildren ? GetChildInfo(go) : null,
            });
        }

        private static object SetTransform(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var positionArray = p.GetArray("position");
            var localPositionArray = p.GetArray("local_position");
            var rotationArray = p.GetArray("rotation");
            var localRotationArray = p.GetArray("local_rotation");
            var scaleArray = p.GetArray("scale");
            var localScaleArray = p.GetArray("local_scale");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            if (positionArray != null && positionArray.Count >= 3)
            {
                go.transform.position = new Vector3(
                    positionArray[0].Value<float>(),
                    positionArray[1].Value<float>(),
                    positionArray[2].Value<float>());
            }
            if (localPositionArray != null && localPositionArray.Count >= 3)
            {
                go.transform.localPosition = new Vector3(
                    localPositionArray[0].Value<float>(),
                    localPositionArray[1].Value<float>(),
                    localPositionArray[2].Value<float>());
            }
            if (rotationArray != null && rotationArray.Count >= 3)
            {
                go.transform.eulerAngles = new Vector3(
                    rotationArray[0].Value<float>(),
                    rotationArray[1].Value<float>(),
                    rotationArray[2].Value<float>());
            }
            if (localRotationArray != null && localRotationArray.Count >= 3)
            {
                go.transform.localEulerAngles = new Vector3(
                    localRotationArray[0].Value<float>(),
                    localRotationArray[1].Value<float>(),
                    localRotationArray[2].Value<float>());
            }
            if (scaleArray != null && scaleArray.Count >= 3)
            {
                go.transform.localScale = new Vector3(
                    scaleArray[0].Value<float>(),
                    scaleArray[1].Value<float>(),
                    scaleArray[2].Value<float>());
            }
            if (localScaleArray != null && localScaleArray.Count >= 3)
            {
                go.transform.localScale = new Vector3(
                    localScaleArray[0].Value<float>(),
                    localScaleArray[1].Value<float>(),
                    localScaleArray[2].Value<float>());
            }

            return new SuccessResponse($"Transform updated for '{go.name}'.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                rotation = new[] { go.transform.eulerAngles.x, go.transform.eulerAngles.y, go.transform.eulerAngles.z },
                scale = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z },
            });
        }

        private static object SetParent(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var parentIdentifier = p.GetString("parent");
            var worldPositionStays = p.GetBool("world_position_stays", true);

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            Transform newParent = null;
            if (!string.IsNullOrEmpty(parentIdentifier))
            {
                var parentGo = GameObjectHelpers.ResolveGameObject(parentIdentifier);
                if (parentGo == null)
                {
                    return new ErrorResponse("ParentNotFound", $"Parent GameObject '{parentIdentifier}' not found.");
                }
                newParent = parentGo.transform;
            }

            go.transform.SetParent(newParent, worldPositionStays);

            return new SuccessResponse($"Parent of '{go.name}' set to '{(newParent != null ? newParent.name : "(null)")}'.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                parent_name = newParent != null ? newParent.name : null,
                parent_instance_id = newParent != null ? newParent.GetInstanceID() : 0,
                world_position_stays = worldPositionStays,
            });
        }

        private static object SetTag(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var tag = p.RequireString("tag");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            try
            {
                go.tag = tag;
                return new SuccessResponse($"Tag of '{go.name}' set to '{tag}'.", new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    tag = go.tag,
                });
            }
            catch (UnityException ex)
            {
                return new ErrorResponse("InvalidTag", ex.Message);
            }
        }

        private static object SetLayer(ToolParams p)
        {
            var identifier = p.RequireString("identifier");
            var layerValue = p.RequireString("layer");

            var go = GameObjectHelpers.ResolveGameObject(identifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{identifier}' not found.");
            }

            int layer;
            if (int.TryParse(layerValue, out layer))
            {
                go.layer = layer;
            }
            else
            {
                layer = LayerMask.NameToLayer(layerValue);
                if (layer == -1)
                {
                    return new ErrorResponse("InvalidLayer", $"Layer '{layerValue}' not found.");
                }
                go.layer = layer;
            }

            return new SuccessResponse($"Layer of '{go.name}' set to '{layerValue}' (layer {go.layer}).", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                layer = go.layer,
                layer_name = LayerMask.LayerToName(go.layer),
            });
        }

        private static object FindGameObjects(ToolParams p)
        {
            var name = p.GetString("name");
            var exact = p.GetBool("exact", false);
            var tag = p.GetString("tag");
            var layer = p.GetString("layer");
            var path = p.GetString("path");
            var component = p.GetString("component");

            List<GameObject> results;

            if (!string.IsNullOrEmpty(tag))
            {
                results = GameObjectHelpers.FindByTag(tag);
            }
            else if (!string.IsNullOrEmpty(layer))
            {
                if (int.TryParse(layer, out int layerNum))
                    results = GameObjectHelpers.FindByLayer(layerNum);
                else
                    results = GameObjectHelpers.FindByLayerName(layer);
            }
            else if (!string.IsNullOrEmpty(component))
            {
                results = GameObjectHelpers.FindByComponent(component);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                results = GameObjectHelpers.FindByName(name, exact);
            }
            else
            {
                return new ErrorResponse("InvalidParameters", "At least one search criterion (name, tag, layer, component) is required.");
            }

            var pageSize = p.GetInt("page_size", 100);
            var cursor = p.GetInt("cursor", 0);
            var total = results.Count;
            var page = results.GetRange(Math.Min(cursor, total), Math.Min(pageSize, total - Math.Min(cursor, total)));

            var pageData = new List<object>();
            foreach (var go in page)
            {
                pageData.Add(new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    scene = go.scene.name,
                    scene_path = GameObjectHelpers.GetScenePath(go),
                    tag = go.tag,
                    layer = go.layer,
                    layer_name = LayerMask.LayerToName(go.layer),
                    active_self = go.activeSelf,
                });
            }

            return new SuccessResponse($"Found {total} GameObject(s), returning {pageData.Count}.", new
            {
                total = total,
                page_size = pageSize,
                cursor = cursor,
                count = pageData.Count,
                game_objects = pageData,
            });
        }

        private static object ListRootGameObjects(ToolParams p)
        {
            var sceneName = p.GetString("scene");

            List<GameObject> roots;
            if (!string.IsNullOrEmpty(sceneName))
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid())
                {
                    return new ErrorResponse("SceneNotFound", $"Scene '{sceneName}' not found.");
                }
                roots = new List<GameObject>(scene.GetRootGameObjects());
            }
            else
            {
                var activeScene = SceneManager.GetActiveScene();
                roots = new List<GameObject>(activeScene.GetRootGameObjects());
            }

            var data = new List<object>();
            foreach (var go in roots)
            {
                data.Add(new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    scene = go.scene.name,
                    active_self = go.activeSelf,
                    child_count = go.transform.childCount,
                    tag = go.tag,
                    layer = go.layer,
                });
            }

            return new SuccessResponse($"Listed {data.Count} root GameObject(s).", new
            {
                count = data.Count,
                game_objects = data,
            });
        }

        private static List<object> GetChildInfo(GameObject go)
        {
            var result = new List<object>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                result.Add(new
                {
                    name = child.name,
                    instance_id = child.GetInstanceID(),
                    child_count = child.childCount,
                    position = new[] { child.position.x, child.position.y, child.position.z },
                });
            }
            return result;
        }
    }
}
