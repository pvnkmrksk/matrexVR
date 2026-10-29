#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Unknown runtime GLB materials need shader variants absent from authored scenes.</summary>
public sealed class ExternalModelShaderRetention : IPreprocessBuildWithReport
{
    public int callbackOrder => -10000;
    public void OnPreprocessBuild(BuildReport report) => Ensure();

    [MenuItem("Tools/MATREX VR/Retain runtime model shaders")]
    public static void Ensure()
    {
        var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
        if (graphics == null) throw new BuildFailedException("Cannot retain runtime model shaders: GraphicsSettings asset is unavailable.");
        var settings = new SerializedObject(graphics);
        var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
        if (shaders == null || !shaders.isArray) throw new BuildFailedException("Cannot find GraphicsSettings always-included shader list.");
        bool changed = false;
        foreach (var spec in new[] {
            new[] { "UnityGLTF/PBRGraph", "478ce3626be7a5f4ea58d6b13f05a2e4" },
            new[] { "UnityGLTF/UnlitGraph", "59541e6caf586ca4f96ccf48a4813a51" }
        })
        {
            Shader shader = Shader.Find(spec[0]) ?? AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(spec[1]));
            if (shader == null) throw new BuildFailedException("Runtime model shader is unavailable: " + spec[0] + ". Restore the pinned UnityGLTF/ShaderGraph packages before building.");
            bool found = false;
            for (int i = 0; i < shaders.arraySize; i++) found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            if (found) continue;
            int index = shaders.arraySize; shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader; changed = true;
        }
        if (changed)
        {
            settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            Debug.Log("Retained UnityGLTF PBR and unlit shader variants for models imported after building.");
        }
    }
}
#endif
