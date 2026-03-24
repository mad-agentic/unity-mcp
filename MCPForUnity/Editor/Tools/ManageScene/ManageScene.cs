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
    /// Full scene management tool supporting list, get_current, save, save_all,
    /// load, create, create_with_objects, add_to_build, and remove_from_build.
    /// </summary>
    [McpForUnityTool("manage_scene", Group = "core",
        Description = "Manage Unity scenes: list, get current, save, save all, load, create, create with GameObjects, add to build, remove from build.")]
    public static class ManageScene
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
                        return ListScenes();
                    case "get_current":
                    case "getcurrent":
                    case "get_active":
                    case "getactive":
                        return GetCurrentScene();
                    case "save":
                        return SaveScene(p);
                    case "save_all":
                    case "saveall":
                        return SaveAllScenes();
                    case "load":
                        return LoadScene(p);
                    case "create":
                        return CreateScene(p);
                    case "create_with_objects":
                    case "createwithobjects":
                        return CreateSceneWithObjects(p);
                    case "add_to_build":
                    case "addtobuild":
                        return AddToBuild(p);
                    case "remove_from_build":
                    case "removefrombuild":
                        return RemoveFromBuild(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: list, get_current, save, save_all, load, create, create_with_objects, add_to_build, remove_from_build.");
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

        private static object ListScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var result = new List<object>();

            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                result.Add(new
                {
                    index = i,
                    path = scene.path,
                    enabled = scene.enabled,
                    guid = scene.guid.ToString(),
                });
            }

            return new SuccessResponse(
                $"Found {result.Count} scenes in build settings.",
                new
                {
                    count = result.Count,
                    scenes = result
                });
        }

        private static object GetCurrentScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            var sceneCount = SceneManager.sceneCount;

            var openScenes = new List<object>();
            for (int i = 0; i < sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                openScenes.Add(new
                {
                    name = s.name,
                    path = s.path,
                    buildIndex = s.buildIndex,
                    isDirty = s.isDirty,
                    isLoaded = s.isLoaded,
                    rootCount = s.rootCount,
                });
            }

            return new SuccessResponse(
                $"Active scene: {activeScene.name}.",
                new
                {
                    active_scene = new
                    {
                        name = activeScene.name,
                        path = activeScene.path,
                        build_index = activeScene.buildIndex,
                        is_dirty = activeScene.isDirty,
                        is_loaded = activeScene.isLoaded,
                        root_count = activeScene.rootCount,
                    },
                    open_scenes = openScenes
                });
        }

        private static object SaveScene(ToolParams p)
        {
            if (!Application.isPlaying)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.isDirty)
                {
                    return new SuccessResponse($"Scene '{activeScene.name}' is already saved.", null);
                }

                var success = EditorSceneManager.SaveScene(activeScene);
                if (success)
                {
                    return new SuccessResponse($"Scene '{activeScene.name}' saved.", null);
                }
                return new ErrorResponse("SaveFailed", $"Failed to save scene '{activeScene.name}'.");
            }
            else
            {
                return new ErrorResponse("InvalidContext", "Cannot save scenes in Play Mode.");
            }
        }

        private static object SaveAllScenes()
        {
            if (!Application.isPlaying)
            {
                var success = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (success)
                {
                    var count = SceneManager.sceneCount;
                    return new SuccessResponse($"All {count} open scene(s) saved.", null);
                }
                return new ErrorResponse("SaveFailed", "User cancelled the save operation or save failed.");
            }
            else
            {
                return new ErrorResponse("InvalidContext", "Cannot save scenes in Play Mode.");
            }
        }

        private static object LoadScene(ToolParams p)
        {
            var sceneNameOrPath = p.RequireString("name");
            var additive = p.GetBool("additive", false);
            var loadSceneMode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;

            if (!Application.isPlaying)
            {
                return new ErrorResponse("InvalidContext", "Scene loading without Play Mode is only supported in Play Mode. Use EditorSceneManager for programmatic loading.");
            }

            try
            {
                var asyncOp = SceneManager.LoadSceneAsync(sceneNameOrPath, loadSceneMode);
                return new SuccessResponse($"Loading scene '{sceneNameOrPath}' (additive={additive}).", new
                {
                    scene_name = sceneNameOrPath,
                    additive = additive,
                    load_scene_mode = loadSceneMode.ToString(),
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("LoadSceneFailed", $"Failed to load scene '{sceneNameOrPath}': {ex.Message}");
            }
        }

        private static object CreateScene(ToolParams p)
        {
            if (Application.isPlaying)
            {
                return new ErrorResponse("InvalidContext", "Cannot create new scenes in Play Mode.");
            }

            var sceneName = p.GetString("name", "New Scene");
            var scenePath = p.GetString("path");

            try
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                if (!string.IsNullOrEmpty(scenePath))
                {
                    var fullPath = scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        ? scenePath
                        : scenePath + ".unity";

                    var success = EditorSceneManager.SaveScene(newScene, fullPath);
                    if (!success)
                    {
                        return new ErrorResponse("SaveFailed", $"Scene created but failed to save to '{fullPath}'.");
                    }
                }

                return new SuccessResponse($"Scene created: '{newScene.name}'.", new
                {
                    name = newScene.name,
                    path = newScene.path,
                    build_index = newScene.buildIndex,
                    root_count = newScene.rootCount,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateSceneFailed", $"Failed to create scene: {ex.Message}");
            }
        }

        private static object CreateSceneWithObjects(ToolParams p)
        {
            if (Application.isPlaying)
            {
                return new ErrorResponse("InvalidContext", "Cannot create new scenes in Play Mode.");
            }

            var sceneName = p.GetString("name", "New Scene");
            var scenePath = p.GetString("path");
            var gameObjectsJson = p.GetArray("game_objects");

            try
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                var createdObjects = new List<object>();

                if (gameObjectsJson != null)
                {
                    foreach (var item in gameObjectsJson)
                    {
                        if (item is JObject obj)
                        {
                            var tp = new ToolParams(obj);
                            var goName = tp.GetString("name", "GameObject");
                            var primitiveType = tp.GetString("type");
                            var positionArray = tp.GetArray("position");
                            var rotationArray = tp.GetArray("rotation");
                            var scaleArray = tp.GetArray("scale");

                            GameObject go;
                            if (!string.IsNullOrEmpty(primitiveType) && Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
                            {
                                go = GameObject.CreatePrimitive(pt);
                                go.name = goName;
                            }
                            else
                            {
                                go = new GameObject(goName);
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

                            createdObjects.Add(new
                            {
                                name = go.name,
                                type = go.GetType().Name,
                                position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                                rotation = new[] { go.transform.eulerAngles.x, go.transform.eulerAngles.y, go.transform.eulerAngles.z },
                                scale = new[] { go.transform.localScale.x, go.transform.localScale.y, go.transform.localScale.z },
                                instance_id = go.GetInstanceID(),
                            });
                        }
                    }
                }

                if (!string.IsNullOrEmpty(scenePath))
                {
                    var fullPath = scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        ? scenePath
                        : scenePath + ".unity";

                    var success = EditorSceneManager.SaveScene(newScene, fullPath);
                    if (!success)
                    {
                        return new ErrorResponse("SaveFailed", $"Scene created with objects but failed to save to '{fullPath}'.");
                    }
                }

                return new SuccessResponse(
                    $"Scene '{sceneName}' created with {createdObjects.Count} GameObject(s).",
                    new
                    {
                        scene_name = newScene.name,
                        scene_path = newScene.path,
                        game_objects_created = createdObjects.Count,
                        game_objects = createdObjects,
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateSceneWithObjectsFailed", $"Failed to create scene with objects: {ex.Message}");
            }
        }

        private static object AddToBuild(ToolParams p)
        {
            var scenePath = p.RequireString("path");

            try
            {
                var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                if (string.IsNullOrEmpty(sceneGuid))
                {
                    return new ErrorResponse("AssetNotFound", $"Scene at path '{scenePath}' not found in asset database.");
                }

                var scenes = EditorBuildSettings.scenes;

                // Check if already in build settings
                foreach (var s in scenes)
                {
                    if (s.path == scenePath)
                    {
                        return new SuccessResponse($"Scene '{scenePath}' is already in build settings.", new
                        {
                            path = scenePath,
                            guid = sceneGuid,
                            already_added = true,
                        });
                    }
                }

                // Add to build settings
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                Array.Copy(scenes, newScenes, scenes.Length);
                newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newScenes;

                return new SuccessResponse($"Scene '{scenePath}' added to build settings.", new
                {
                    path = scenePath,
                    guid = sceneGuid,
                    already_added = false,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("AddToBuildFailed", $"Failed to add scene to build: {ex.Message}");
            }
        }

        private static object RemoveFromBuild(ToolParams p)
        {
            var scenePath = p.RequireString("path");

            try
            {
                var scenes = EditorBuildSettings.scenes;
                var newScenes = new List<EditorBuildSettingsScene>();

                bool found = false;
                foreach (var s in scenes)
                {
                    if (s.path == scenePath)
                    {
                        found = true;
                    }
                    else
                    {
                        newScenes.Add(s);
                    }
                }

                if (!found)
                {
                    return new ErrorResponse("SceneNotInBuild", $"Scene '{scenePath}' is not in build settings.");
                }

                EditorBuildSettings.scenes = newScenes.ToArray();

                return new SuccessResponse($"Scene '{scenePath}' removed from build settings.", new
                {
                    path = scenePath,
                    removed = true,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RemoveFromBuildFailed", $"Failed to remove scene from build: {ex.Message}");
            }
        }
    }
}
