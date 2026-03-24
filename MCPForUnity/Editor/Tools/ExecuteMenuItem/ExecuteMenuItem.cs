using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Menu item execution tool supporting execute, get_recent, and search actions.
    /// Uses UnityEditor.Menu to execute menu items and search menu paths.
    /// </summary>
    [McpForUnityTool("execute_menu_item", Group = "core",
        Description = "Execute Unity menu items by path, get recent items, and search menu paths.")]
    public static class ExecuteMenuItem
    {
        private static readonly List<string> _recentMenuItems = new List<string>();
        private const int MaxRecentItems = 20;

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "execute");

                switch (action.ToLowerInvariant())
                {
                    case "execute":
                        return ExecuteMenu(p);
                    case "get_recent":
                    case "getrecent":
                        return GetRecentItems();
                    case "search":
                        return SearchMenuItems(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: execute, get_recent, search.");
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

        private static object ExecuteMenu(ToolParams p)
        {
            var menuPath = p.RequireString("menu_path");

            try
            {
                // Validate menu item exists before executing
                if (!IsValidMenuItem(menuPath))
                {
                    return new ErrorResponse("MenuItemNotFound",
                        $"Menu item '{menuPath}' not found. "
                        + "Check the path format: use '/' separators (e.g. 'File/Save', 'GameObject/Create Empty').");
                }

                UnityEditor.Menu.ExecuteFunction(menuPath);

                // Track recent item
                AddRecentItem(menuPath);

                return new SuccessResponse($"Menu item '{menuPath}' executed successfully.", new
                {
                    menu_path = menuPath,
                    executed_at = DateTime.Now.ToString("HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("MenuExecutionFailed",
                    $"Failed to execute menu item '{menuPath}': {ex.Message}");
            }
        }

        private static object GetRecentItems()
        {
            return new SuccessResponse(
                $"Returning {_recentMenuItems.Count} recently executed menu item(s).",
                new
                {
                    count = _recentMenuItems.Count,
                    recent_items = _recentMenuItems
                });
        }

        private static object SearchMenuItems(ToolParams p)
        {
            var query = p.RequireString("query");

            var results = SearchAllMenuItems(query);

            return new SuccessResponse(
                $"Found {results.Count} menu item(s) matching '{query}'.",
                new
                {
                    query = query,
                    count = results.Count,
                    results = results
                });
        }

        private static bool IsValidMenuItem(string menuPath)
        {
            try
            {
                // Check if the menu path resolves to a valid item
                var menu = UnityEditor.Menu.GetMenuItem(menuPath, false);
                return menu != null && menu.IsValid();
            }
            catch
            {
                return false;
            }
        }

        private static void AddRecentItem(string menuPath)
        {
            _recentMenuItems.Remove(menuPath);
            _recentMenuItems.Insert(0, menuPath);

            if (_recentMenuItems.Count > MaxRecentItems)
            {
                _recentMenuItems.RemoveAt(_recentMenuItems.Count - 1);
            }
        }

        private static List<object> SearchAllMenuItems(string query)
        {
            var results = new List<object>();
            var seenPaths = new HashSet<string>();

            // Common menu categories to search
            var menuSources = new[]
            {
                "Assets",
                "GameObject",
                "Component",
                "Window",
                "Help",
                "Edit",
                "File",
                "Object"
            };

            // Search through EditorUtility menu items
            // Note: Unity's public API doesn't expose a full menu enumeration,
            // so we search known patterns and common paths.
            var searchTerms = query.ToLowerInvariant().Split(new[] { ' ', '/', '_' }, StringSplitOptions.RemoveEmptyEntries);

            // Build a set of known useful menu paths for common operations
            var knownMenus = GetKnownMenuPaths();

            foreach (var menuPath in knownMenus)
            {
                var menuLower = menuPath.ToLowerInvariant();
                bool matches = true;

                foreach (var term in searchTerms)
                {
                    if (!menuLower.Contains(term))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches && !seenPaths.Contains(menuPath))
                {
                    seenPaths.Add(menuPath);
                    results.Add(new
                    {
                        path = menuPath,
                        category = GetMenuCategory(menuPath),
                        exact_match = menuLower.Contains(query.ToLowerInvariant())
                    });
                }
            }

            // Sort by exact match first, then alphabetically
            results.Sort((a, b) =>
            {
                var aDict = a as IDictionary<string, object>;
                var bDict = b as IDictionary<string, object>;
                bool aExact = aDict != null && aDict.ContainsKey("exact_match") && (bool)aDict["exact_match"];
                bool bExact = bDict != null && bDict.ContainsKey("exact_match") && (bool)bDict["exact_match"];
                if (aExact != bExact) return bExact ? 1 : -1;
                string aPath = aDict != null && aDict.ContainsKey("path") ? aDict["path"].ToString() : "";
                string bPath = bDict != null && bDict.ContainsKey("path") ? bDict["path"].ToString() : "";
                return string.Compare(aPath, bPath, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        private static string GetMenuCategory(string menuPath)
        {
            if (menuPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return "Assets";
            if (menuPath.StartsWith("GameObject/", StringComparison.OrdinalIgnoreCase)) return "GameObject";
            if (menuPath.StartsWith("Component/", StringComparison.OrdinalIgnoreCase)) return "Component";
            if (menuPath.StartsWith("Window/", StringComparison.OrdinalIgnoreCase)) return "Window";
            if (menuPath.StartsWith("Help/", StringComparison.OrdinalIgnoreCase)) return "Help";
            if (menuPath.StartsWith("Edit/", StringComparison.OrdinalIgnoreCase)) return "Edit";
            if (menuPath.StartsWith("File/", StringComparison.OrdinalIgnoreCase)) return "File";
            if (menuPath.StartsWith("Object/", StringComparison.OrdinalIgnoreCase)) return "Object";
            return "Other";
        }

        private static List<string> GetKnownMenuPaths()
        {
            var menus = new List<string>
            {
                // File menu
                "File/New Scene",
                "File/Open Scene",
                "File/Save",
                "File/Save As...",
                "File/Save Scene",
                "File/Save Scene As...",
                "File/Save All",
                "File/New Project",
                "File/Open Project",
                "File/Build Settings...",
                "File/Build And Run",
                "File/Exit",
                "File/New Scene",

                // Edit menu
                "Edit/Undo",
                "Edit/Redo",
                "Edit/Cut",
                "Edit/Copy",
                "Edit/Paste",
                "Edit/Duplicate",
                "Edit/Delete",
                "Edit/Select All",
                "Edit/Frame Selected",
                "Edit/Lock View",
                "Edit/Preferences...",
                "Edit/Play",
                "Edit/Pause",
                "Edit/Step",
                "Edit/Reset",

                // GameObject menu
                "GameObject/Create Empty",
                "GameObject/Create Empty Child",
                "GameObject/3D Object/Cube",
                "GameObject/3D Object/Sphere",
                "GameObject/3D Object/Capsule",
                "GameObject/3D Object/Cylinder",
                "GameObject/3D Object/Plane",
                "GameObject/3D Object/Quad",
                "GameObject/3D Object/Ragdoll...",
                "GameObject/3D Object/Terrain",
                "GameObject/3D Object/Tree",
                "GameObject/3D Object/Wind Zone",
                "GameObject/2D Object/Sprite",
                "GameObject/2D Object/Sprite Shape",
                "GameObject/Light/Directional Light",
                "GameObject/Light/Point Light",
                "GameObject/Light/Spotlight",
                "GameObject/Light/Area Light",
                "GameObject/Light/Reflection Probe",
                "GameObject/Audio/Audio Source",
                "GameObject/Audio/Audio Reverb Zone",
                "GameObject/UI/Text",
                "GameObject/UI/Image",
                "GameObject/UI/Button",
                "GameObject/UI/Raw Image",
                "GameObject/UI/Slider",
                "GameObject/UI/Scrollbar",
                "GameObject/UI/Dropdown",
                "GameObject/UI/Input Field",
                "GameObject/UI/Toggle",
                "GameObject/UI/Canvas",
                "GameObject/UI/Event System",
                "GameObject/UI/Panel",
                "GameObject/Video/Video Player",
                "GameObject/Video/Video Surface",
                "GameObject/Create Parent",
                "GameObject/Clear Parent",
                "GameObject/Set as first sibling",
                "GameObject/Set as last sibling",
                "GameObject/Move To View",
                "GameObject/Align With View",
                "GameObject/Align View to Selected",

                // Assets menu
                "Assets/Create/Folder",
                "Assets/Create/C# Script",
                "Assets/Create/JavaScript",
                "Assets/Create/Shader/Standard Surface Shader",
                "Assets/Create/Shader/Unlit Shader",
                "Assets/Create/Shader/Image Effect Shader",
                "Assets/Create/Compute Shader",
                "Assets/Create/Material",
                "Assets/Create/Prefab",
                "Assets/Create/Animation",
                "Assets/Create/Animator Controller",
                "Assets/Create/Sprites/AutoSpriteMator",
                "Assets/Create/Sprites/Palette",
                "Assets/Create/Tile",
                "Assets/Create/Tilemap",
                "Assets/Create/Audio Source",
                "Assets/Create/Render Texture",
                "Assets/Create/Lightmap Parameters",
                "Assets/Import New Asset...",
                "Assets/Delete...",
                "Assets/Duplicate",
                "Assets/Rename",
                "Assets/Open",
                "Assets/Find References In Scene",
                "Assets/Select Dependencies",
                "Assets/Refresh",
                "Assets/Reimport",
                "Assets/Reimport All",
                "Assets/Export Package...",
                "Assets/Import New Asset...",
                "Assets/Playable Import...",
                "Assets/CrossReference Import...",
                "Assets/Debugger/Attach to Unity",
                "Assets/External Version Control/EnableSafeMode",

                // Component menu
                "Component/Add...",
                "Component/Mesh/Mesh Filter",
                "Component/Mesh/Mesh Renderer",
                "Component/Mesh/Skinned Mesh Renderer",
                "Component/Mesh/Text Mesh",
                "Component/Effects/Particle System",
                "Component/Effects/Trail Renderer",
                "Component/Effects/Line Renderer",
                "Component/Effects/Halo",
                "Component/Effects/Light Probe Group",
                "Component/Effects/Light Probes",
                "Component/Physics/Rigidbody",
                "Component/Physics/Rigidbody 2D",
                "Component/Physics/Box Collider",
                "Component/Physics/Sphere Collider",
                "Component/Physics/Capsule Collider",
                "Component/Physics/Mesh Collider",
                "Component/Physics/Wheel Collider",
                "Component/Physics/Terrain Collider",
                "Component/Physics/Character Controller",
                "Component/Physics 2D/Rigidbody 2D",
                "Component/Physics 2D/Box Collider 2D",
                "Component/Physics 2D/Circle Collider 2D",
                "Component/Physics 2D/Polygon Collider 2D",
                "Component/Physics 2D/Edge Collider 2D",
                "Component/Audio/Audio Source",
                "Component/Audio/Audio Reverb Zone",
                "Component/Audio/Audio Low Pass Filter",
                "Component/Audio/Audio High Pass Filter",
                "Component/Audio/Audio Echo Filter",
                "Component/Audio/Audio Distortion Filter",
                "Component/Audio/Audio Reverb Filter",
                "Component/Audio/Audio Listener",
                "Component/Rendering/Camera",
                "Component/Rendering/Skybox",
                "Component/Rendering/Flare Layer",
                "Component/Rendering/Light",
                "Component/Rendering/LOD Group",
                "Component/Rendering/Occlusion Area",
                "Component/Rendering/Occlusion Portal",
                "Component/Rendering/Skinned Mesh Renderer",
                "Component/Rendering/Sprite Mask",
                "Component/Rendering/Sprite Renderer",
                "Component/Rendering/Canvas Renderer",
                "Component/UI/Text",
                "Component/UI/Image",
                "Component/UI/Raw Image",
                "Component/UI/Mask",
                "Component/UI/RectMask2D",
                "Component/UI/Button",
                "Component/UI/InputField",
                "Component/UI/Toggle",
                "Component/UI/Slider",
                "Component/UI/Scrollbar",
                "Component/UI/Dropdown",
                "Component/Navigation/NavMesh Agent",
                "Component/Navigation/NavMesh Obstacle",
                "Component/Navigation/Off Mesh Link",
                "Component/Navigation/NavMesh Surface",
                "Component/Add Component",

                // Window menu
                "Window/General/Game",
                "Window/General/Scene",
                "Window/General/Inspector",
                "Window/General/Hierarchy",
                "Window/General/Project",
                "Window/General/Console",
                "Window/Animation/Animation",
                "Window/Animation/Animator",
                "Window/Asset Management/Package Manager",
                "Window/Asset Management/Asset Store",
                "Window/Analysis/Profiler",
                "Window/Analysis/Frame Debugger",
                "Window/Rendering/Lighting",
                "Window/Rendering/Light Explorer",
                "Window/Rendering/Occlusion Culling",
                "Window/Rendering/Navigation",
                "Window/Rendering/Rendering Debugger",

                // Help menu
                "Help/Unity Manual",
                "Help/Scripting Reference",
                "Help/Unity Services",
                "Help/About Unity",

                // Toolbar
                "Assets/Import Package/Custom Package...",
                "Assets/Import Package/Effects",
                "Assets/Import Package/Characters",
                "Assets/Import Package/Environment",
                "Assets/Import Package/ParticleSystems",

                // Additional common paths
                "GameObject/Apply Changes To Prefab",
                "GameObject/Break Prefab Instance",
                "Assets/Create/Prefab Variant",
                "Assets/Properties...",

                // ProBuilder (if installed)
                "Tools/ProBuilder/Information",
                "Tools/ProBuilder/Preference",
                "Tools/ProBuilder/Actions/Export",
                "Tools/ProBuilder/Actions/Export Obj",

                // Package-specific menus
                "Window/Package Manager",
                "Window/General/Package Manager",
            };

            return menus;
        }
    }
}
