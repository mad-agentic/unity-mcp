using System;

namespace MadAgent.UnityMCP.Editor
{
    /// <summary>
    /// Attribute to mark a class as an MCP tool handler.
    /// CommandRegistry auto-discovers all classes with this attribute via reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class McpForUnityToolAttribute : Attribute
    {
        /// <summary>
        /// The tool name exposed to the MCP protocol.
        /// If not set, defaults to the class name (without "Manage" prefix).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The tool group. Only "core" is enabled by default.
        /// Others (vfx, animation, ui, scripting_ext, testing, probuilder) start disabled.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Whether to auto-register this tool. Set to false for manual registration.
        /// </summary>
        public bool AutoRegister { get; set; }

        /// <summary>
        /// Description shown to the AI client.
        /// </summary>
        public string Description { get; set; }

        public string group
        {
            get => Group;
            set => Group = value;
        }

        public bool autoRegister
        {
            get => AutoRegister;
            set => AutoRegister = value;
        }

        public string description
        {
            get => Description;
            set => Description = value;
        }

        public McpForUnityToolAttribute(
            string name = null,
            string group = "core",
            bool autoRegister = true,
            string description = null)
        {
            Name = name;
            Group = group;
            AutoRegister = autoRegister;
            Description = description;
        }
    }

    /// <summary>
    /// Attribute to mark a class as an MCP resource provider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class McpForUnityResourceAttribute : Attribute
    {
        public string Uri { get; }
        public string Description { get; }
        public string MimeType { get; }

        public McpForUnityResourceAttribute(
            string uri,
            string description = null,
            string mimeType = "application/json")
        {
            Uri = uri;
            Description = description;
            MimeType = mimeType;
        }
    }
}
