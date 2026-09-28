/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.Text;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        public static bool meshesChecked;

        private string meshErrorMessage;
        private bool showMeshes = true;

        private static void CheckIndexFormat(GameObject prefab, ModelImporter importer, StringBuilder builder)
        {
            if (importer.indexFormat == ModelImporterIndexFormat.UInt32)
            {
                builder.Append(prefab.name).Append(" has an Index Format of 32 bits. This can cause any unpredictable problems, including freezing or editor crash.\nTo solve the problem, it is recommended to use Index Format - 16 bits.\n\n");
            }
            else if (importer.indexFormat == ModelImporterIndexFormat.Auto)
            {
                MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter filter in meshFilters)
                {
                    if (filter && filter.sharedMesh.vertexCount > 65000)
                    {
                        builder.Append(prefab.name).Append(" has an Index Format - Auto and Vertex Count > 65k. This can cause any unpredictable problems, including freezing or editor crash.\nTo solve the problem, it is recommended to use Index Format - 16 bits.\n\n");
                        break;
                    }
                }
            }
        }

        private void CheckMeshes()
        {
            StringBuilder builder = new StringBuilder();

            foreach (GameObject go in meshes)
            {
                if (go.scene.name == null) builder.Append(go.name).Append(" is not in the scene.\n\n");
                if (!PrefabUtility.IsPartOfAnyPrefab(go)) continue;
                
                CheckPrefab(go, builder);
            }

            if (builder.Length > 0) builder.Length -= 2;
            meshErrorMessage = builder.ToString();
            meshesChecked = true;
        }

        private static void CheckPrefab(GameObject instance, StringBuilder builder)
        {
            GameObject prefab = PrefabUtility.GetNearestPrefabInstanceRoot(instance);
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) return;
            
            if (!prefab) prefab = instance;        
            CheckIndexFormat(prefab, importer, builder);
        }

        private void MeshesUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showMeshes = Foldout(showMeshes, "Meshes");
            if (showMeshes)
            {
                meshFindType = (MeshToTerrainFindType)EditorGUILayout.EnumPopup("Mesh Select Type", meshFindType);

                if (meshFindType == MeshToTerrainFindType.gameObjects) MeshesUIGameObjects();
                else if (meshFindType == MeshToTerrainFindType.layers) meshLayer = EditorGUILayout.LayerField("Layer", meshLayer);

                direction = (MeshToTerrainDirection)EditorGUILayout.EnumPopup("Direction", direction);
                if (direction == MeshToTerrainDirection.reversed) GUILayout.Label("Use the reverse direction, if that model has inverted the normal.");

                yRange = (MeshToTerrainYRange)EditorGUILayout.EnumPopup("Y Range", yRange);
                if (yRange == MeshToTerrainYRange.fixedValue) yRangeValue = EditorGUILayout.IntField("Y Range Value", yRangeValue);
            }
            EditorGUILayout.EndVertical();
        }

        private void MeshesUIGameObjects()
        {
            bool hasEmpty = false;
            for (int i = 0; i < meshes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                GameObject go = (GameObject)EditorGUILayout.ObjectField("Mesh " + (i + 1), meshes[i], typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    meshes[i] = go;
                    meshesChecked = false;
                }

                if (GUILayout.Button("X", GUILayout.ExpandWidth(false))) meshes[i] = null;
                EditorGUILayout.EndHorizontal();

                if (!meshes[i]) hasEmpty = true;
            }

            if (hasEmpty)
            {
                meshes.RemoveAll(m => !m);
                meshesChecked = false;
            }

            GameObject newMesh = (GameObject)EditorGUILayout.ObjectField("Mesh GameObject", null, typeof(GameObject), true);
            if (newMesh != null)
            {
                meshesChecked = false;
                if (newMesh.scene.name == null)
                {
                    EditorUtility.DisplayDialog("Error", "GameObject must be in the scene, not in the project!", "OK");
                }
                else if (!meshes.Contains(newMesh)) meshes.Add(newMesh);
                else EditorUtility.DisplayDialog("Error", "The selected GameObject is already in the list!", "OK");
            }

            if (!meshesChecked) CheckMeshes();
            if (!string.IsNullOrEmpty(meshErrorMessage)) EditorGUILayout.HelpBox(meshErrorMessage, MessageType.Error);
        }
    }
}