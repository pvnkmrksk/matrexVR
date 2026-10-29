using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Give worlds without a Choice/Kannadi object controller the same model transform contract.</summary>
public static class ExperimentWorldObjects
{
    public static void Apply(Scene scene, JObject config)
    {
        var roots = scene.GetRootGameObjects();
        if (roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Any(component => component is ChoiceController || component is Kannadi)) return;
        var host = roots.FirstOrDefault(root => root.name == "Configured world objects");
        if (host == null && !(config?["objects"] is JArray)) return;
        if (host == null) { host = new GameObject("Configured world objects"); SceneManager.MoveGameObjectToScene(host, scene); }
        var objects = host.GetComponent<KannadiWorldObjects>() ?? host.AddComponent<KannadiWorldObjects>();
        objects.Apply(config?["objects"]?.ToObject<SceneObject[]>(), RuntimeRigRegistry.Rigs(scene));
    }
}
