/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.IO;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    [CustomEditor(typeof(MeshToTerrainDocumentation))]
    [InitializeOnLoad]
    public class MeshToTerrainDocumentationEditor: Editor
    {
        private static GUIStyle style;

        static MeshToTerrainDocumentationEditor()
        {
            EditorApplication.delayCall += TryRemoveOldDoc; 
        }

        private static void DrawDocumentation()
        {
            if (GUILayout.Button("Local Documentation"))
            {
                Application.OpenURL(MeshToTerrainPrefs.assetFolder + "Documentation/Content/Documentation-Content.html");
            }

            if (GUILayout.Button("Online Documentation"))
            {
                MeshToTerrainLinks.OpenDocumentation();
            }

            GUILayout.Space(10);
        }

        private static void DrawExtra()
        {
            if (GUILayout.Button("Homepage"))
            {
                MeshToTerrainLinks.OpenHomepage();
            }

            if (GUILayout.Button("Asset Store"))
            {
                MeshToTerrainLinks.OpenAssetStore();
            }

            if (GUILayout.Button("Changelog"))
            {
                MeshToTerrainLinks.OpenChangelog();
            }

            GUILayout.Space(10);
        }

        private new static void DrawHeader()
        {
            if (style == null)
            {
                style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
            }

            GUILayout.Label("Mesh to Terrain", style);
            GUILayout.Label("version: " + MeshToTerrain.version, style);
            GUILayout.Space(10);
        }

        private void DrawRateAndReview()
        {
            EditorGUILayout.HelpBox("Please don't forget to leave a review on the store page if you liked Mesh to Terrain, this helps us a lot!", MessageType.Warning);

            if (GUILayout.Button("Rate & Review"))
            {
                MeshToTerrainLinks.OpenReviews();
            }
        }

        private void DrawSupport()
        {
            if (GUILayout.Button("Support"))
            {
                MeshToTerrainLinks.OpenSupport();
            }

            if (GUILayout.Button("Forum"))
            {
                MeshToTerrainLinks.OpenForum();
            }

            GUILayout.Space(10);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            DrawDocumentation();
            DrawExtra();
            DrawSupport();
            DrawRateAndReview();
        }

        private static void TryRemoveOldDoc()
        {
            string filename = MeshToTerrainPrefs.assetFolder + "Documentation/Readme.pdf";
            if (!File.Exists(filename)) return;

            try
            {
                File.Delete(filename);
            }
            catch
            {
                return;
            }

            filename += ".meta";
            if (!File.Exists(filename)) return;

            try
            {
                File.Delete(filename);
            }
            catch
            {

            }
        }
    }
}
