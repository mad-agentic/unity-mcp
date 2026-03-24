using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MadAgent.UnityMCP.Editor
{
    /// <summary>
    /// JSON serialization utilities using Newtonsoft.Json.
    /// </summary>
    public static class JsonUtil
    {
        private static readonly JsonSerializerSettings s_Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new JsonConverter[]
            {
                new UnityVector3Converter(),
                new UnityQuaternionConverter(),
                new UnityColorConverter(),
            }
        };

        /// <summary>
        /// Serialize an object to a JSON string.
        /// </summary>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, s_Settings);
        }

        /// <summary>
        /// Deserialize a JSON string to an object of type T.
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, s_Settings);
        }

        /// <summary>
        /// Parse a JSON string into a JObject.
        /// </summary>
        public static JObject Parse(string json)
        {
            return JObject.Parse(json);
        }

        /// <summary>
        /// Try to parse a JSON string, returning null on failure.
        /// </summary>
        public static JObject TryParse(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Safely get a string value from a JObject.
        /// </summary>
        public static string GetString(JObject obj, string key, string defaultValue = null)
        {
            var token = obj?[key];
            return token?.Type == JTokenType.String ? token.Value<string>() : defaultValue;
        }

        /// <summary>
        /// Safely get an int value from a JObject.
        /// </summary>
        public static int? GetInt(JObject obj, string key, string altKey = null)
        {
            var token = obj?[key] ?? (altKey != null ? obj?[altKey] : null);
            if (token == null) return null;
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out var v)) return v;
            return null;
        }

        /// <summary>
        /// Safely get a float value from a JObject.
        /// </summary>
        public static float? GetFloat(JObject obj, string key)
        {
            var token = obj?[key];
            if (token == null) return null;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return token.Value<float>();
            if (token.Type == JTokenType.String && float.TryParse(token.Value<string>(), out var v)) return v;
            return null;
        }

        /// <summary>
        /// Safely get a bool value from a JObject.
        /// </summary>
        public static bool? GetBool(JObject obj, string key)
        {
            var token = obj?[key];
            if (token == null) return null;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (token.Type == JTokenType.String)
            {
                var s = token.Value<string>()?.ToLowerInvariant();
                if (s == "true" || s == "1" || s == "yes") return true;
                if (s == "false" || s == "0" || s == "no") return false;
            }
            return null;
        }

        /// <summary>
        /// Safely get a JArray from a JObject.
        /// </summary>
        public static JArray GetArray(JObject obj, string key)
        {
            return obj?[key] as JArray;
        }

        /// <summary>
        /// Safely get a JObject from a JObject.
        /// </summary>
        public static JObject GetObject(JObject obj, string key)
        {
            return obj?[key] as JObject;
        }
    }

    // ─── Unity Type Converters ────────────────────────────────────────────────

    public class UnityVector3Converter : JsonConverter<UnityEngine.Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                float x = obj["x"]?.Value<float>() ?? 0f;
                float y = obj["y"]?.Value<float>() ?? 0f;
                float z = obj["z"]?.Value<float>() ?? 0f;
                return new Vector3(x, y, z);
            }
            if (reader.TokenType == JsonToken.StartArray)
            {
                var arr = JArray.Load(reader);
                return new Vector3(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f
                );
            }
            return Vector3.zero;
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WriteEndObject();
        }
    }

    public class UnityQuaternionConverter : JsonConverter<UnityEngine.Quaternion>
    {
        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                float x = obj["x"]?.Value<float>() ?? 0f;
                float y = obj["y"]?.Value<float>() ?? 0f;
                float z = obj["z"]?.Value<float>() ?? 0f;
                float w = obj["w"]?.Value<float>() ?? 1f;
                return new Quaternion(x, y, z, w);
            }
            if (reader.TokenType == JsonToken.StartArray)
            {
                var arr = JArray.Load(reader);
                return new Quaternion(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f,
                    arr.Count > 3 ? arr[3].Value<float>() : 1f
                );
            }
            return Quaternion.identity;
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WritePropertyName("w"); writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    public class UnityColorConverter : JsonConverter<UnityEngine.Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                return new Color(
                    obj["r"]?.Value<float>() ?? 0f,
                    obj["g"]?.Value<float>() ?? 0f,
                    obj["b"]?.Value<float>() ?? 0f,
                    obj["a"]?.Value<float>() ?? 1f
                );
            }
            if (reader.TokenType == JsonToken.StartArray)
            {
                var arr = JArray.Load(reader);
                return new Color(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f,
                    arr.Count > 3 ? arr[3].Value<float>() : 1f
                );
            }
            return Color.white;
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r"); writer.WriteValue(value.r);
            writer.WritePropertyName("g"); writer.WriteValue(value.g);
            writer.WritePropertyName("b"); writer.WriteValue(value.b);
            writer.WritePropertyName("a"); writer.WriteValue(value.a);
            writer.WriteEndObject();
        }
    }
}
