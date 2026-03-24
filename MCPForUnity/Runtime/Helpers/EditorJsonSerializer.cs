using System;

namespace MadAgent.UnityMCP.Runtime
{
    /// <summary>
    /// Runtime-safe JSON serialization helpers.
    /// Uses Unity's JsonUtility for basic types.
    /// </summary>
    public static class EditorJsonSerializer
    {
        /// <summary>
        /// Serialize an object to JSON string (runtime-safe).
        /// </summary>
        public static string Serialize<T>(T obj) where T : class
        {
            try
            {
                return UnityEngine.JsonUtility.ToJson(obj);
            }
            catch (Exception)
            {
                return "{}";
            }
        }

        /// <summary>
        /// Deserialize a JSON string to an object (runtime-safe).
        /// </summary>
        public static T Deserialize<T>(string json) where T : class
        {
            try
            {
                return UnityEngine.JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
