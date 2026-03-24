using System;
using System.IO;
using UnityEngine;

namespace MadAgent.UnityMCP.Runtime
{
    /// <summary>
    /// Screenshot capture utility for editor state feedback.
    /// Lives in Runtime to be accessible from both Editor and Play modes.
    /// </summary>
    public static class ScreenCaptureUtil
    {
        private const string ScreenshotsFolderName = "Screenshots";

        public static bool IsScreenCaptureModuleAvailable
        {
            get
            {
                try
                {
                    return Application.HasProLicense();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Capture a screenshot and return the path.
        /// </summary>
        public static string CaptureScreenshot(string projectPath, int superSize = 1)
        {
            var screenshotsDir = Path.Combine(projectPath, "Assets", ScreenshotsFolderName);
            if (!Directory.Exists(screenshotsDir))
                Directory.CreateDirectory(screenshotsDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"mcp_screenshot_{timestamp}.png";
            var fullPath = Path.Combine(screenshotsDir, filename);

            try
            {
                ScreenCapture.CaptureScreenshot(fullPath, superSize);
                var assetsRelativePath = $"Assets/{ScreenshotsFolderName}/{filename}";
                return fullPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Screenshot capture failed: {ex.Message}");
                return null;
            }
        }
    }
}
