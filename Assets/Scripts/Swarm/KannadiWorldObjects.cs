using System;
using UnityEngine;

/// <summary>Shared world objects use the same transform fields in Kannadi and Choice.</summary>
public sealed class KannadiWorldObjects : MonoBehaviour
{
    private Transform objectsRoot;
    public void Apply(SceneObject[] objects, ClosedLoop[] rigs)
    {
        Clear();
        if (objects == null || objects.Length == 0) return;
        objectsRoot = new GameObject("Kannadi world objects").transform;
        objectsRoot.gameObject.AddComponent<RecordedWorldStateTarget>().Key = "kannadi-world";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(objectsRoot.gameObject, gameObject.scene);
        var catalog = Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        foreach (var item in objects)
        {
            if (item == null || !item.enabled) continue;
            GameObject prefab = !string.IsNullOrWhiteSpace(item.modelPath) ? ExternalModelLibrary.GetPrefab(item.modelPath) : catalog?.Find(item.type);
            if (prefab == null) throw new ArgumentException("Unknown Kannadi world object: " + item.type);
            if (item.useHeadingReference)
            {
                foreach (var rig in rigs)
                {
                    var instance = Spawn(prefab, item);
                    instance.transform.position += rig.transform.position;
                    var reference = rig.GetComponent<StimulusHeadingReference>()?.Result;
                    if (reference != null) StimulusHeadingReference.RotateStimulus(instance.transform, rig.transform.position, reference);
                    Layer(instance.transform, RuntimeRigRegistry.StimulusLayer(Kannadi.ParseVRIndex(rig.name)));
                }
            }
            else Spawn(prefab, item);
        }
    }
    private GameObject Spawn(GameObject prefab, SceneObject item)
    {
        GameObject instance = ExternalModelLibrary.InstantiatePrefab(prefab, objectsRoot);
        ChoiceController.ApplyResolvedTransform(instance.transform, item);
        Layer(instance.transform, 0);
        if (item.groundOnTerrain) instance.transform.position = DesktopQuickStart.GroundPosition(instance.transform.position);
        if (!string.IsNullOrWhiteSpace(item.modelPath) || !string.IsNullOrWhiteSpace(item.animationClip) || item.animationSpeed != 1)
            ExternalModelLibrary.ConfigureAnimation(instance, item.animationClip, item.animationSpeed);
        return instance;
    }
    private static void Layer(Transform item, int layer)
    {
        item.gameObject.layer = layer;
        foreach (Transform child in item) Layer(child, layer);
    }
    private void Clear()
    {
        if (objectsRoot == null) return;
        objectsRoot.gameObject.SetActive(false); objectsRoot.gameObject.AddComponent<RecordedPendingDestroy>(); Destroy(objectsRoot.gameObject); objectsRoot = null;
    }
    private void OnDestroy() => Clear();
}
