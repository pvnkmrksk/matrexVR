using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared panorama orientation for acquisition, previews and archived replay.</summary>
public static class ExperimentSky
{
    private const string SharedHost = "Configured world sky";
    public const string RotationField = "panoramaRotationDegrees";
    public static float Rotation(JObject configuration)
    {
        var value = configuration?[RotationField];
        if (value == null || value.Type == JTokenType.Null) return 0;
        if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
            throw new ArgumentException("Panorama rotation must be a number from 0 to 360 degrees.");
        double degrees = (double)value;
        if (double.IsNaN(degrees) || double.IsInfinity(degrees) || degrees < 0 || degrees > 360)
            throw new ArgumentException("Panorama rotation must be a finite number from 0 to 360 degrees.");
        return degrees == 360 ? 0 : (float)degrees;
    }

    public static void Apply(JObject configuration)
    {
        float rotation = Rotation(configuration);
        if (string.IsNullOrWhiteSpace((string)configuration?["skyboxPath"])) return;
        var material = RenderSettings.skybox;
        if (material == null || !material.HasProperty("_Rotation"))
            throw new InvalidOperationException("The selected panorama shader does not support horizontal rotation.");
        material.SetFloat("_Rotation", rotation);
    }

    /// <summary>Supply the common sky options to worlds without their own sky controller.</summary>
    public static void ApplyScene(Scene scene, JObject configuration)
    {
        if (ReplayExecutionContext.Active) return; // Replay restores the actual archived image.
        var roots = scene.GetRootGameObjects();
        // These controllers already configure and archive their exact sky; in
        // particular, Choice retains its established custom-panorama path.
        if (roots.Any(root => root.GetComponentInChildren<ChoiceController>(true) != null ||
                              root.GetComponentInChildren<Kannadi>(true) != null ||
                              root.GetComponentInChildren<SwarmController>(true) != null)) return;
        var sky = configuration?["nightSky"]?.ToObject<NightSkyConfig>();
        var uniform = configuration?["uniformSkyColor"]?.Type == JTokenType.Object
            ? configuration["uniformSkyColor"].ToObject<ColorConfig>() : null;
        string path = (string)configuration?["skyboxPath"];
        var host = roots.FirstOrDefault(root => root.name == SharedHost);
        bool requested = uniform != null || !string.IsNullOrWhiteSpace(path) || sky?.enabled == true;
        if (host == null && !requested) return;
        if (host == null)
        {
            host = new GameObject(SharedHost);
            SceneManager.MoveGameObjectToScene(host, scene);
        }
        NightSkyController.Apply(host, sky, path, uniform);
    }
}
