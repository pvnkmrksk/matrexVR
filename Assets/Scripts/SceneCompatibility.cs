using System.Collections.Generic;

/// <summary>Historical scene names remain readable while new experiments share Kannadi.</summary>
public static class SceneCompatibility
{
    public static string Resolve(string sceneName) => sceneName == "Swarm" ? "Kannadi" : sceneName;
    public static Dictionary<string, object> Parameters(string originalSceneName, Dictionary<string, object> source)
    {
        if (originalSceneName != "Swarm") return source;
        var result = source == null ? new Dictionary<string, object>() : new Dictionary<string, object>(source);
        if (!result.ContainsKey("populationMode")) result["populationMode"] = "Swarm";
        if (!result.ContainsKey("worldUnitsPerMetre")) result["worldUnitsPerMetre"] = 100f;
        return result;
    }
}
