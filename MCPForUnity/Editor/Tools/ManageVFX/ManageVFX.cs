using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Visual Effects (VFX) and particle system management tool supporting
    /// create_particle, get_particle_settings, set_parameter, and emit.
    /// </summary>
    [McpForUnityTool("manage_vfx", Group = "vfx",
        Description = "Create and manage Unity Visual Effects and particle systems: create emitters, configure parameters, emit bursts.")]
    public static class ManageVFX
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get_particle_settings");

                switch (action.ToLowerInvariant())
                {
                    case "create_particle":
                    case "createparticle":
                        return CreateParticle(p);
                    case "get_particle_settings":
                    case "getparticlesettings":
                        return GetParticleSettings(p);
                    case "set_parameter":
                    case "setparameter":
                        return SetParameter(p);
                    case "emit":
                        return EmitParticles(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create_particle, get_particle_settings, set_parameter, emit.");
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

        private static object CreateParticle(ToolParams p)
        {
            var name = p.GetString("name", "VFX Particle");
            var parentIdentifier = p.GetString("parent");
            var emissionRate = p.GetFloat("emission_rate");
            var lifetime = p.GetFloat("lifetime");
            var speed = p.GetFloat("speed");
            var size = p.GetFloat("size");
            var colorArray = p.GetArray("color");
            var positionArray = p.GetArray("position");

            // Create or find parent
            Transform parent = null;
            if (!string.IsNullOrEmpty(parentIdentifier))
            {
                var parentGo = ResolveGameObject(parentIdentifier);
                if (parentGo == null)
                {
                    return new ErrorResponse("ParentNotFound",
                        $"Parent GameObject '{parentIdentifier}' not found.");
                }
                parent = parentGo.transform;
            }

            try
            {
                var goName = !string.IsNullOrEmpty(name) ? name : "Particle System";
                var particleObj = new GameObject(goName);

                if (parent != null)
                {
                    particleObj.transform.SetParent(parent, false);
                }

                if (positionArray != null && positionArray.Count >= 3)
                {
                    particleObj.transform.position = new Vector3(
                        positionArray[0].Value<float>(),
                        positionArray[1].Value<float>(),
                        positionArray[2].Value<float>());
                }

                var ps = particleObj.AddComponent<ParticleSystem>();

                // Configure emission
                if (emissionRate.HasValue)
                {
                    var emission = ps.emission;
                    emission.rateOverTime = emissionRate.Value;
                }

                // Configure lifetime
                if (lifetime.HasValue)
                {
                    var main = ps.main;
                    main.startLifetime = lifetime.Value;
                }

                // Configure speed
                if (speed.HasValue)
                {
                    var main = ps.main;
                    main.startSpeed = speed.Value;
                }

                // Configure size
                if (size.HasValue)
                {
                    var main = ps.main;
                    main.startSize = size.Value;
                }

                // Configure color
                if (colorArray != null && colorArray.Count >= 3)
                {
                    var main = ps.main;
                    float r = colorArray[0].Value<float>();
                    float g = colorArray[1].Value<float>();
                    float b = colorArray[2].Value<float>();
                    float a = colorArray.Count >= 4 ? colorArray[3].Value<float>() : 1.0f;
                    main.startColor = new Color(r, g, b, a);
                }

                // Ensure it plays
                ps.Play();

                return new SuccessResponse($"Particle system '{goName}' created.", new
                {
                    name = goName,
                    instance_id = particleObj.GetInstanceID(),
                    position = new[] {
                        particleObj.transform.position.x,
                        particleObj.transform.position.y,
                        particleObj.transform.position.z
                    },
                    emission_rate = ps.emission.rateOverTime.constant,
                    lifetime = ps.main.startLifetime.constant,
                    speed = ps.main.startSpeed.constant,
                    size = ps.main.startSize.constant,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateParticleFailed",
                    $"Failed to create particle system: {ex.Message}");
            }
        }

        private static object GetParticleSettings(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                return new ErrorResponse("NoParticleSystem",
                    $"GameObject '{go.name}' does not have a ParticleSystem component.");
            }

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var colorOverLifetime = ps.colorOverLifetime;
            var sizeOverLifetime = ps.sizeOverLifetime;

            return new SuccessResponse($"Particle settings for '{go.name}'.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                // Main module
                duration = ps.duration,
                loop = main.loop,
                prewarm = main.prewarm,
                start_lifetime = main.startLifetime.constant,
                start_speed = main.startSpeed.constant,
                start_size = main.startSize.constant,
                start_color = ColorToArray(main.startColor.colorMax),
                gravity_modifier = main.gravityModifier.constant,
                max_particles = main.maxParticles,
                // Emission module
                emission_rate = emission.rateOverTime.constant,
                burst_count = emission.burstCount,
                // Shape module
                shape_type = shape.shapeType.ToString(),
                shape_radius = shape.radius,
                shape_angle = shape.angle,
                // Color over lifetime
                color_over_lifetime_enabled = colorOverLifetime.enabled,
                color_over_lifetime = GetColorOverLifetimeInfo(colorOverLifetime),
                // Size over lifetime
                size_over_lifetime_enabled = sizeOverLifetime.enabled,
            });
        }

        private static object SetParameter(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var emissionRate = p.GetFloat("emission_rate");
            var lifetime = p.GetFloat("lifetime");
            var speed = p.GetFloat("speed");
            var size = p.GetFloat("size");
            var colorArray = p.GetArray("color");
            var gravityModifier = p.GetFloat("gravity_modifier");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                return new ErrorResponse("NoParticleSystem",
                    $"GameObject '{go.name}' does not have a ParticleSystem component.");
            }

            try
            {
                if (emissionRate.HasValue)
                {
                    ps.emission.rateOverTime = emissionRate.Value;
                }
                if (lifetime.HasValue)
                {
                    ps.main.startLifetime = lifetime.Value;
                }
                if (speed.HasValue)
                {
                    ps.main.startSpeed = speed.Value;
                }
                if (size.HasValue)
                {
                    ps.main.startSize = size.Value;
                }
                if (colorArray != null && colorArray.Count >= 3)
                {
                    float r = colorArray[0].Value<float>();
                    float g = colorArray[1].Value<float>();
                    float b = colorArray[2].Value<float>();
                    float a = colorArray.Count >= 4 ? colorArray[3].Value<float>() : 1.0f;
                    ps.main.startColor = new Color(r, g, b, a);
                }
                if (gravityModifier.HasValue)
                {
                    ps.main.gravityModifier = gravityModifier.Value;
                }

                return new SuccessResponse($"Parameters updated for '{go.name}'.", new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    emission_rate = ps.emission.rateOverTime.constant,
                    lifetime = ps.main.startLifetime.constant,
                    speed = ps.main.startSpeed.constant,
                    size = ps.main.startSize.constant,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetParameterFailed",
                    $"Failed to set parameter: {ex.Message}");
            }
        }

        private static object EmitParticles(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");
            var count = p.GetInt("count", 10);
            var positionArray = p.GetArray("position");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                return new ErrorResponse("NoParticleSystem",
                    $"GameObject '{go.name}' does not have a ParticleSystem component.");
            }

            try
            {
                var emitParams = new ParticleSystem.EmitParams();
                if (positionArray != null && positionArray.Count >= 3)
                {
                    emitParams.position = new Vector3(
                        positionArray[0].Value<float>(),
                        positionArray[1].Value<float>(),
                        positionArray[2].Value<float>());
                }

                ps.Emit(emitParams, count);

                return new SuccessResponse($"Emitted {count} particle(s) from '{go.name}'.", new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    count = count,
                    position = positionArray != null && positionArray.Count >= 3
                        ? new[] {
                            positionArray[0].Value<float>(),
                            positionArray[1].Value<float>(),
                            positionArray[2].Value<float>()
                        }
                        : null,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("EmitFailed",
                    $"Failed to emit particles: {ex.Message}");
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

        private static float[] ColorToArray(Color c)
        {
            return new[] { c.r, c.g, c.b, c.a };
        }

        private static object GetColorOverLifetimeInfo(ParticleSystem.ColorOverLifetimeModule col)
        {
            if (!col.enabled || col.color.minColor == null) return null;
            return new
            {
                min = ColorToArray(col.color.minColor),
                max = ColorToArray(col.color.maxColor),
            };
        }
    }
}
