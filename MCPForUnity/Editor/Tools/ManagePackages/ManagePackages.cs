using System;
using System.Collections.Generic;
using System.Linq;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

#if UNITY_2020_3_OR_NEWER
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
#endif

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Package manager tool supporting list, install, remove, embed, and list_registry operations.
    /// </summary>
    [McpForUnityTool("manage_packages", Group = "core",
        Description = "Manage Unity packages: list installed, install by name/version/Git URL, remove, embed local packages, and search registry.")]
    public static class ManagePackages
    {
#if UNITY_2020_3_OR_NEWER
        private static ListRequest _listRequest;
        private static SearchRequest _searchRequest;
#endif

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "list");

                switch (action.ToLowerInvariant())
                {
                    case "list":
                        return ListPackages(p);
                    case "install":
                        return InstallPackage(p);
                    case "remove":
                        return RemovePackage(p);
                    case "embed":
                        return EmbedPackage(p);
                    case "list_registry":
                    case "listregistry":
                    case "search":
                        return SearchRegistry(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: list, install, remove, embed, list_registry.");
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

        private static object ListPackages(ToolParams p)
        {
            var includeDependencies = p.GetBool("include_dependencies", false);

#if UNITY_2020_3_OR_NEWER
            try
            {
                var request = UnityEditor.PackageManager.Client.List(includeDependencies);
                var packages = new List<object>();

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    foreach (var info in request.Result)
                    {
                        packages.Add(new
                        {
                            name = info.name,
                            display_name = info.displayName,
                            version = info.version,
                            resolved_version = info.resolvedPath != null ? info.version : null,
                            source = info.source.ToString(),
                            is_direct_dependency = info.isDirectDependency,
                            is_asset_store_package = info.source == UnityEditor.PackageManager.PackageSource.AssetStore,
                            is_embedded = info.source == UnityEditor.PackageManager.PackageSource.Local,
                            is_git = info.source == UnityEditor.PackageManager.PackageSource.Git,
                            git_url = info.git != null ? info.git.url : null,
                            path = info.resolvedPath,
                            description = info.description,
                            author = info.author?.name,
                            homepage = info.author?.url,
                            unity_version = info.unityVersion,
                            status = info.status.ToString(),
                        });
                    }
                }
                else
                {
                    return new ErrorResponse("ListPackagesFailed",
                        $"Failed to list packages: {request.Error?.message}");
                }

                return new SuccessResponse($"Listed {packages.Count} package(s).", new
                {
                    count = packages.Count,
                    packages = packages
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ListPackagesFailed",
                    $"Failed to list packages: {ex.Message}");
            }
#else
            // Fallback for older Unity versions - scan UPM manifest
            return ListPackagesFromManifest(includeDependencies);
#endif
        }

        private static object ListPackagesFromManifest(bool includeDependencies)
        {
            try
            {
                var manifestPath = System.IO.Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!System.IO.File.Exists(manifestPath))
                {
                    return new SuccessResponse("No packages found (manifest not found).", new
                    {
                        count = 0,
                        packages = new List<object>()
                    });
                }

                var json = System.IO.File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(json);

                var packages = new List<object>();

                // Read dependencies
                var deps = manifest["dependencies"] as JObject;
                if (deps != null)
                {
                    foreach (var dep in deps.Properties())
                    {
                        packages.Add(new
                        {
                            name = dep.Name,
                            version = dep.Value?.ToString(),
                            source = "builtin",
                            is_direct_dependency = true,
                        });
                    }
                }

                // Read scopedRegistries
                var scopedRegs = manifest["scopedRegistries"] as JArray;
                if (scopedRegs != null)
                {
                    foreach (var reg in scopedRegs)
                    {
                        var regName = reg["name"]?.ToString();
                        var regUrl = reg["url"]?.ToString();
                    }
                }

                return new SuccessResponse($"Listed {packages.Count} package(s) from manifest.", new
                {
                    count = packages.Count,
                    packages = packages,
                    note = "Older Unity version - using manifest parsing instead of Package Manager API."
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ListPackagesFailed",
                    $"Failed to read package manifest: {ex.Message}");
            }
        }

        private static object InstallPackage(ToolParams p)
        {
            var packageName = p.RequireString("package_name");
            var version = p.GetString("version");
            var gitUrl = p.GetString("git_url");
            var registryUrl = p.GetString("registry_url");

#if UNITY_2020_3_OR_NEWER
            try
            {
                string packageIdentifier;

                if (!string.IsNullOrEmpty(gitUrl))
                {
                    packageIdentifier = gitUrl;
                    if (!string.IsNullOrEmpty(version))
                    {
                        packageIdentifier += $"#{version}";
                    }
                }
                else if (!string.IsNullOrEmpty(version))
                {
                    packageIdentifier = $"{packageName}@{version}";
                }
                else
                {
                    packageIdentifier = packageName;
                }

                var request = UnityEditor.PackageManager.Client.Add(packageIdentifier);

                // Wait for completion (with timeout)
                var timeout = 60000; // 60 seconds
                var start = DateTime.Now;
                while (!request.IsCompleted && (DateTime.Now - start).TotalMilliseconds < timeout)
                {
                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    return new SuccessResponse(
                        $"Package '{packageIdentifier}' installed successfully.", new
                        {
                            package_name = packageName,
                            version = version,
                            git_url = gitUrl,
                            status = "success"
                        });
                }
                else if (request.Status == UnityEditor.PackageManager.StatusCode.InProgress)
                {
                    return new SuccessResponse(
                        $"Package installation of '{packageIdentifier}' is in progress.", new
                        {
                            package_name = packageName,
                            version = version,
                            status = "in_progress",
                            note = "The package is being installed asynchronously. Check the Package Manager window for progress."
                        });
                }
                else
                {
                    return new ErrorResponse("InstallFailed",
                        $"Failed to install package '{packageIdentifier}': {request.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse("InstallFailed",
                    $"Failed to install package: {ex.Message}");
            }
#else
            return AddPackageToManifest(packageName, version, gitUrl, registryUrl);
#endif
        }

        private static object AddPackageToManifest(string packageName, string version, string gitUrl, string registryUrl)
        {
            try
            {
                var manifestPath = System.IO.Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                var json = System.IO.File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(json);

                var deps = manifest["dependencies"] as JObject;
                if (deps == null)
                {
                    deps = new JObject();
                    manifest["dependencies"] = deps;
                }

                string versionString;
                if (!string.IsNullOrEmpty(gitUrl))
                {
                    versionString = !string.IsNullOrEmpty(version)
                        ? $"git+{gitUrl}#{version}"
                        : $"git+{gitUrl}";
                }
                else if (!string.IsNullOrEmpty(version))
                {
                    versionString = version;
                }
                else
                {
                    versionString = "0.0.0";
                }

                deps[packageName] = versionString;

                System.IO.File.WriteAllText(manifestPath, manifest.ToString());
                AssetDatabase.Refresh();

                return new SuccessResponse(
                    $"Package '{packageName}' added to manifest.", new
                    {
                        package_name = packageName,
                        version = versionString,
                        manifest_path = manifestPath,
                        note = "Package added to manifest. Unity will resolve and download on next refresh."
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("InstallFailed",
                    $"Failed to add package to manifest: {ex.Message}");
            }
        }

        private static object RemovePackage(ToolParams p)
        {
            var packageName = p.RequireString("package_name");

#if UNITY_2020_3_OR_NEWER
            try
            {
                var request = UnityEditor.PackageManager.Client.Remove(packageName);

                var timeout = 60000;
                var start = DateTime.Now;
                while (!request.IsCompleted && (DateTime.Now - start).TotalMilliseconds < timeout)
                {
                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    return new SuccessResponse($"Package '{packageName}' removed successfully.", new
                    {
                        package_name = packageName,
                        status = "success"
                    });
                }
                else if (request.Status == UnityEditor.PackageManager.StatusCode.InProgress)
                {
                    return new SuccessResponse(
                        $"Removal of '{packageName}' is in progress.", new
                        {
                            package_name = packageName,
                            status = "in_progress"
                        });
                }
                else
                {
                    return new ErrorResponse("RemoveFailed",
                        $"Failed to remove package '{packageName}': {request.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RemoveFailed",
                    $"Failed to remove package: {ex.Message}");
            }
#else
            return RemovePackageFromManifest(packageName);
#endif
        }

        private static object RemovePackageFromManifest(string packageName)
        {
            try
            {
                var manifestPath = System.IO.Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                var json = System.IO.File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(json);

                var deps = manifest["dependencies"] as JObject;
                if (deps == null || !deps.ContainsKey(packageName))
                {
                    return new ErrorResponse("PackageNotInManifest",
                        $"Package '{packageName}' is not in the dependencies section of manifest.json.");
                }

                deps.Remove(packageName);
                System.IO.File.WriteAllText(manifestPath, manifest.ToString());
                AssetDatabase.Refresh();

                return new SuccessResponse($"Package '{packageName}' removed from manifest.", new
                {
                    package_name = packageName,
                    manifest_path = manifestPath,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RemoveFailed",
                    $"Failed to remove package from manifest: {ex.Message}");
            }
        }

        private static object EmbedPackage(ToolParams p)
        {
            var packageName = p.RequireString("package_name");

#if UNITY_2020_3_OR_NEWER
            try
            {
                var request = UnityEditor.PackageManager.Client.Embed(packageName);

                var timeout = 60000;
                var start = DateTime.Now;
                while (!request.IsCompleted && (DateTime.Now - start).TotalMilliseconds < timeout)
                {
                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    return new SuccessResponse($"Package '{packageName}' embedded successfully.", new
                    {
                        package_name = packageName,
                        status = "embedded",
                    });
                }
                else if (request.Status == UnityEditor.PackageManager.StatusCode.InProgress)
                {
                    return new SuccessResponse(
                        $"Embedding of '{packageName}' is in progress.", new
                        {
                            package_name = packageName,
                            status = "in_progress"
                        });
                }
                else
                {
                    return new ErrorResponse("EmbedFailed",
                        $"Failed to embed package '{packageName}': {request.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse("EmbedFailed",
                    $"Failed to embed package: {ex.Message}");
            }
#else
            return new ErrorResponse("NotSupported",
                "Package embedding is only supported in Unity 2020.3 and newer.");
#endif
        }

        private static object SearchRegistry(ToolParams p)
        {
            var packageName = p.GetString("package_name");
            var searchQuery = p.GetString("search_query");
            var count = p.GetInt("count", 50);

            if (string.IsNullOrEmpty(packageName) && string.IsNullOrEmpty(searchQuery))
            {
                return new ErrorResponse("InvalidParameters",
                    "Either 'package_name' or 'search_query' is required for list_registry.");
            }

            var query = !string.IsNullOrEmpty(packageName) ? packageName : searchQuery;

#if UNITY_2020_3_OR_NEWER
            try
            {
                var request = UnityEditor.PackageManager.Client.Search(query);

                var timeout = 60000;
                var start = DateTime.Now;
                while (!request.IsCompleted && (DateTime.Now - start).TotalMilliseconds < timeout)
                {
                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    var results = new List<object>();
                    foreach (var info in request.Result.Take(count))
                    {
                        results.Add(new
                        {
                            name = info.name,
                            display_name = info.displayName,
                            version = info.version,
                            description = TruncateString(info.description, 200),
                            author = info.author?.name,
                            unity_version = info.unityVersion,
                        });
                    }

                    return new SuccessResponse(
                        $"Found {results.Count} package(s) matching '{query}'.", new
                        {
                            query = query,
                            count = results.Count,
                            packages = results
                        });
                }
                else
                {
                    return new ErrorResponse("SearchFailed",
                        $"Failed to search packages: {request.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SearchFailed",
                    $"Failed to search packages: {ex.Message}");
            }
#else
            return new ErrorResponse("NotSupported",
                "Package registry search is only supported in Unity 2020.3 and newer. "
                + "Use 'list' to see installed packages.");
#endif
        }

        private static string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
        }
    }
}
