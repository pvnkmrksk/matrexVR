/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public class MeshToTerrainAboutWindow : EditorWindow
    {
        [MenuItem("Window/Infinity Code/Mesh to Terrain/About")]
        public static void OpenWindow()
        {
            MeshToTerrainAboutWindow window = GetWindow<MeshToTerrainAboutWindow>(true, "About", true);
            window.minSize = new Vector2(200, 100);
            window.maxSize = new Vector2(200, 100);
        }

        public void OnGUI()
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.alignment = TextAnchor.MiddleCenter;

            GUILayout.Label("Mesh to Terrain", titleStyle);
            GUILayout.Label("version " + MeshToTerrain.version, textStyle);
            GUILayout.Label("created Infinity Code", textStyle);
            GUILayout.Label("2013-" + DateTime.Now.Year, textStyle);
        }
    }
}