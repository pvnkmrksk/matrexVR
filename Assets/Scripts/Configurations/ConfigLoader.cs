// ConfigurationLoader.cs

using UnityEngine;
using System.IO;

public static class ConfigLoader
{

    // flexible method to load config with error handlign for flexible data class defined in the call
    public static T LoadConfig<T>(string resourcePath) where T : class
    {
        TextAsset configFile = Resources.Load<TextAsset>(resourcePath);
        if (configFile == null)
        {
            Debug.LogError($"Configuration file not found at path: {resourcePath}");
            return null;
        }

        return JsonUtility.FromJson<T>(configFile.text);
    }

    // loadParadigmConfig method using the flexible method
    public static ParadigmConfig LoadParadigmConfig(string resourcePath)
    {
        return LoadConfig<ParadigmConfig>(resourcePath);
    }

    // loadExperimentConfig method using the flexible method
    public static ExperimentConfig LoadExperimentConfig(string resourcePath)
    {
        return LoadConfig<ExperimentConfig>(resourcePath);
    }

}
