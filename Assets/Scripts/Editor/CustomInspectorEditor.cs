using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BandSpawner))]
public class CustomInspectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BandSpawner spawner = (BandSpawner)target;

        DrawDefaultInspector();

        // Show hexRadius only when gridType is Hexagonal
        if (spawner.gridType == SpawnGridType.Hexagonal)
        {
            spawner.hexRadius = EditorGUILayout.FloatField("Hex Radius", spawner.hexRadius);
        }

        // Show numberOfInstances only when gridType is Manhattan
        if (spawner.gridType == SpawnGridType.Manhattan)
        {
            spawner.sectionLengthX = EditorGUILayout.FloatField("Section Length X", spawner.sectionLengthX);
            spawner.sectionLengthZ = EditorGUILayout.FloatField("Section Length Z", spawner.sectionLengthZ);
        }

        if (spawner.gridType == SpawnGridType.Random || spawner.prioritizeNumbers)
        {
            spawner.numberOfInstances = EditorGUILayout.IntField("Number of Instances", spawner.numberOfInstances);
        }


        if (GUI.changed)
        {
            EditorUtility.SetDirty(spawner);
        }
    }
}