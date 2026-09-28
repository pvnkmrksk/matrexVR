using System.Collections.Generic;
using System.IO;
using System.Linq;
using InSceneSequence;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class SwarmController : MonoBehaviour, IInSceneSequencer
{
    public void InitializeScene(Dictionary<string, object> parameters)
    {
        JObject values = new JObject();
        if (parameters != null && parameters.TryGetValue("configFile", out object file))
            values = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, file.ToString())));
        if (parameters != null) values.Merge(JObject.FromObject(parameters), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        KannadiConfig movement = KannadiConfig.Load(parameters);
        foreach (LocustSpawner spawner in FindObjectsOfType<LocustSpawner>())
        {
            if (!spawner.isActiveAndEnabled || spawner.gameObject.scene != gameObject.scene) continue;
            spawner.loadConfigFromJsonFile = false;
            spawner.numberOfLocusts = values.Value<int?>("numberOfLocusts") ?? spawner.numberOfLocusts;
            spawner.spawnAreaSize = values.Value<float?>("spawnAreaSize") ?? spawner.spawnAreaSize;
            spawner.mu = values.Value<float?>("mu") ?? spawner.mu;
            spawner.kappa = values.Value<float?>("kappa") ?? spawner.kappa;
            spawner.locustSpeed = values.Value<float?>("locustSpeed") ?? spawner.locustSpeed;
            ClosedLoop closedLoop = spawner.GetComponent<ClosedLoop>();
            VRConfig vr = movement.vrConfigs?.FirstOrDefault(v => v.vrIndex == Kannadi.ParseVRIndex(spawner.name));
            if (closedLoop != null)
            {
                closedLoop.SetLocustGains(vr?.closedLoopPosition ?? movement.closedLoopPosition ?? 1,
                                          vr?.closedLoopOrientation ?? movement.closedLoopOrientation ?? 1);
                if (vr != null)
                    closedLoop.SetPositionAndRotation(vr.initialPosition?.ToVector3() ?? spawner.transform.position,
                        vr.initialRotation != null ? Quaternion.Euler(vr.initialRotation.ToVector3()) : spawner.transform.rotation);
            }
            spawner.Rebuild();
        }
        SimpleOverheadCamera.EnsureInScene();
    }
    public void AdvanceStep(Dictionary<string, object> parameters) => InitializeScene(parameters);
    private void Start() => SimpleOverheadCamera.EnsureInScene();
}
