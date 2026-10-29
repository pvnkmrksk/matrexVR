using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class SunConfig
{
    public bool enabled = true;
    public float intensity = 1f;
    public ColorConfig color = new ColorConfig { r = 1, g = 1, b = 1, a = 1 };
    public Vector3Config rotation = new Vector3Config { x = 50, y = 330, z = 0 };
    public void Validate()
    {
        if (!Finite(intensity) || intensity < 0) throw new ArgumentException("Sun intensity must be finite and nonnegative.");
        if (color == null || !Finite(color.r) || !Finite(color.g) || !Finite(color.b) ||
            color.r < 0 || color.r > 1 || color.g < 0 || color.g > 1 || color.b < 0 || color.b > 1)
            throw new ArgumentException("Sun color RGB components must be in [0,1].");
        if (rotation == null || !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z))
            throw new ArgumentException("Sun orientation must be finite angles.");
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public static class ExperimentSun
{
    public static void Apply(Scene scene, JObject config)
    {
        JToken value = config?["sun"];
        if (value == null || value.Type == JTokenType.Null) return;
        SunConfig settings = value.ToObject<SunConfig>(); settings.Validate();
        var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .Where(light => light.type == LightType.Directional).OrderBy(light => light.name, StringComparer.Ordinal).ToArray();
        Light sun = RenderSettings.sun != null && RenderSettings.sun.gameObject.scene == scene ? RenderSettings.sun : lights.FirstOrDefault();
        if (sun == null)
        {
            var host = new GameObject("Experiment Sun"); SceneManager.MoveGameObjectToScene(host, scene);
            sun = host.AddComponent<Light>(); sun.type = LightType.Directional;
        }
        foreach (Light light in lights) if (light != sun) light.enabled = false;
        sun.gameObject.SetActive(true); sun.enabled = settings.enabled;
        sun.intensity = settings.intensity; sun.color = new Color(settings.color.r, settings.color.g, settings.color.b, 1);
        sun.transform.SetPositionAndRotation(new Vector3(0, 10000, 0), Quaternion.Euler(settings.rotation.ToVector3()));
        RenderSettings.sun = sun;
    }
}
