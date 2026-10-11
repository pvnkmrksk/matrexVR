#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BogongMothAuthoring
{
    [MenuItem("GameObject/Experiments/Bogong moth (metres)", false, 10)]
    public static void Create()
    {
        var root = new GameObject("Bogong moth");
        Undo.RegisterCreatedObjectUndo(root, "Create Bogong moth");
        root.AddComponent<BogongMoth>();
        Selection.activeGameObject = root;
    }
}
#endif
