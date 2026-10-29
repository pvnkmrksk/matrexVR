#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The editable six-face prefab remains the source; generated copies need no divergent variants.</summary>
public static class RigAuthoring
{
    [MenuItem("Tools/MATREX VR/Materialize VR1 through VR16 in current scene")]
    public static void Materialize()
    {
        Scene scene = SceneManager.GetActiveScene();
        var authored = Object.FindObjectsByType<ClosedLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.gameObject.scene == scene && RuntimeRigRegistry.ParseIndex(r.name) > 0).ToArray();
        if (authored.Length == 0)
        {
            Debug.LogError("Open an experiment scene containing an editable VR prefab first."); return;
        }
        Undo.RegisterFullObjectHierarchyUndo(authored[0].transform.root.gameObject, "Materialize 16 VR rigs");
        var active = new HashSet<int>(authored.Where(r => r.gameObject.activeSelf).Select(r => RuntimeRigRegistry.ParseIndex(r.name)));
        var before = new HashSet<int>(authored.Select(r => r.GetInstanceID()));
        var config = Enumerable.Range(1, RuntimeRigRegistry.MaximumRigCount).ToDictionary(i => "VR" + i, i => new SystemConfig { vrId = "VR" + i });
        RuntimeRigRegistry.Configure(scene, config);
        foreach (var rig in Object.FindObjectsByType<ClosedLoop>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.scene == scene))
        {
            int index = RuntimeRigRegistry.ParseIndex(rig.name);
            if (!before.Contains(rig.GetInstanceID())) Undo.RegisterCreatedObjectUndo(rig.gameObject, "Materialize VR" + index);
            rig.gameObject.SetActive(active.Contains(index));
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("VR1–VR16 are editable in this scene. Runtime system configuration enables exactly the requested rigs. Save the scene to retain the authored instances.");
    }
}
#endif
