/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        private readonly int[] availableHeights = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
        private readonly int[] availableResolutions = { 32, 64, 128, 256, 512, 1024, 2048 };

        private string[] availableResolutionsStr;
        private string[] availableHeightsStr;
        
        private bool hasBounds;
        private bool showTerrains = true;

        private void CreateBoundsHelper()
        {
            GameObject helperGO = new GameObject("Mesh To Terrain Helper");
            helperGO.transform.position = Vector3.zero;
            boundsHelper = helperGO.AddComponent<MeshToTerrainBoundsHelper>();

            boundsHelper.bounds = hasBounds ? bounds : new Bounds();
            boundsHelper.OnBoundChanged += delegate
            {
                bounds = boundsHelper.bounds;
                MeshToTerrain.RepaintWindow();
            };

            boundsHelper.OnDestroyed += MeshToTerrain.RepaintWindow;

            if (!hasBounds)
            {
                bool findedBounds = false;

                foreach (GameObject meshGO in meshes)
                {
                    MeshRenderer[] meshRenderers = meshGO.GetComponentsInChildren<MeshRenderer>();

                    foreach (MeshRenderer meshRenderer in meshRenderers)
                    {
                        if (!findedBounds)
                        {
                            findedBounds = true;
                            boundsHelper.bounds = meshRenderer.bounds;
                        }
                        else boundsHelper.bounds.Encapsulate(meshRenderer.bounds);
                    }
                }

                if (!findedBounds) boundsHelper.bounds = new Bounds(Vector3.zero, Vector3.one);
                bounds = boundsHelper.bounds;
                hasBounds = true;
            }

            Selection.activeGameObject = helperGO;
        }

        private void DetectBounds()
        {
            bool findedBounds = false;

            Bounds newBounds = new Bounds();

            foreach (GameObject meshGO in meshes)
            {
                MeshRenderer[] meshRenderers = meshGO.GetComponentsInChildren<MeshRenderer>();

                foreach (MeshRenderer meshRenderer in meshRenderers)
                {
                    if (!findedBounds)
                    {
                        findedBounds = true;
                        newBounds = meshRenderer.bounds;
                    }
                    else newBounds.Encapsulate(meshRenderer.bounds);
                }
            }

            if (findedBounds)
            {
                bounds = newBounds;
            }

            if (boundsHelper != null)
            {
                boundsHelper.bounds = bounds;
                if (findedBounds) SceneView.RepaintAll();
            }
        }

        private void ExistTerrainUI()
        {
            for (int i = 0; i < terrains.Count; i++) terrains[i] = (Terrain)EditorGUILayout.ObjectField(terrains[i], typeof(Terrain), true);
            terrains.RemoveAll(t => !t);

            Terrain newTerrain = (Terrain)EditorGUILayout.ObjectField(null, typeof(Terrain), true);
            if (newTerrain != null)
            {
                if (!terrains.Contains(newTerrain)) terrains.Add(newTerrain);
                else EditorUtility.DisplayDialog("Warning", "Terrain already added", "OK");
            }
        }

        private void NewTerrainUI()
        {
            if (availableResolutionsStr == null || availableHeightsStr == null)
            {
                availableHeightsStr = availableHeights.Select(h => h.ToString()).ToArray();
                availableResolutionsStr = availableResolutions.Select(r => r.ToString()).ToArray();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Count Terrains. X: ", GUILayout.ExpandWidth(false));
            newTerrainCountX = Mathf.Max(EditorGUILayout.IntField(newTerrainCountX, GUILayout.ExpandWidth(false)), 1);
            GUILayout.Label("Y: ", GUILayout.ExpandWidth(false));
            newTerrainCountY = Mathf.Max(EditorGUILayout.IntField(newTerrainCountY, GUILayout.ExpandWidth(false)), 1);
            GUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            boundsType = (MeshToTerrainBounds)EditorGUILayout.EnumPopup("Bounds", boundsType);

            if (EditorGUI.EndChangeCheck())
            {
                if (boundsType == MeshToTerrainBounds.selectBounds) { }
                else if (boundsHelper != null) Object.DestroyImmediate(boundsHelper.gameObject);
            }

            if (boundsType == MeshToTerrainBounds.fromGameobject) boundsGameObject = (GameObject)EditorGUILayout.ObjectField("Bounds GameObject: ", boundsGameObject, typeof(GameObject), true);
            else if (boundsType == MeshToTerrainBounds.selectBounds)
            {
                if (!boundsHelper) showBoundSelector = false;

                showBoundSelector = GUILayout.Toggle(showBoundSelector, "Show Selector", GUI.skin.button);
                if (showBoundSelector && !boundsHelper) CreateBoundsHelper();
                else if (!showBoundSelector && boundsHelper)
                {
                    Object.DestroyImmediate(boundsHelper.gameObject);
                    boundsHelper = null;
                }

                EditorGUI.BeginChangeCheck();
                bounds = EditorGUILayout.BoundsField("Bounds", bounds);

                if (EditorGUI.EndChangeCheck() && boundsHelper != null) boundsHelper.bounds = bounds;

                if (GUILayout.Button("Detect Bounds")) DetectBounds();
            }

            const string detailTooltip = "The resolution of the map that controls grass and detail meshes. For performance reasons (to save on draw calls) the lower you set this number the better.";
            const string resolutionPerPatchTooltip = "Specifies the size in pixels of each individually rendered detail patch. A larger number reduces draw calls, but might increase triangle count since detail patches are culled on a per batch basis. A recommended value is 16. If you use a very large detail object distance and your grass is very sparse, it makes sense to increase the value.";
            const string basemapTooltip = "Resolution of the composite texture used on the terrain when viewed from a distance greater than the Basemap Distance.";
            const string heightmapTooltip = "Pixel resolution of the terrains heightmap.";
            const string alphamapTooltip = "Resolution of the splatmap that controls the blending of the different terrain textures.";
            const string pixelErrorTooltip = "The accuracy of the mapping between Terrain maps (such as heightmaps and Textures) and generated Terrain. Higher values indicate lower accuracy, but with lower rendering overhead.";
            const string basemapDistanceTooltip = "Heightmap patches beyond basemap distance will use a precomputed low res basemap. This improves performance for far away patches.Close up Unity renders the heightmap using splat maps by blending between any amount of provided terrain textures.";
            const string helpHref = "http://docs.unity3d.com/Manual/terrain-OtherSettings.html";
            

            DrawField("Heightmap Resolution", ref heightmapResolution, heightmapTooltip, helpHref, availableHeightsStr, availableHeights);
            DrawField("Detail Resolution", ref detailResolution, detailTooltip, helpHref);
            DrawField("Control Texture Resolution", ref alphamapResolution, alphamapTooltip, helpHref, availableResolutionsStr, availableResolutions);
            DrawField("Base Texture Resolution", ref baseMapResolution, basemapTooltip, helpHref, availableResolutionsStr, availableResolutions);
            DrawField("Resolution Per Patch", ref resolutionPerPatch, resolutionPerPatchTooltip, helpHref);

            pixelError = IntSlider("Pixel Error", pixelError, 1, 200, pixelErrorTooltip);
            basemapDistance = IntSlider("Basemap Distance", basemapDistance, 0, 20000, basemapDistanceTooltip);

            if (resolutionPerPatch < 1) resolutionPerPatch = 1;
            detailResolution = detailResolution / resolutionPerPatch * resolutionPerPatch;
        }

        private void TerrainsUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showTerrains = Foldout(showTerrains, "Terrains: ");
            if (showTerrains)
            {
                terrainType = (MeshToTerrainSelectTerrainType)EditorGUILayout.EnumPopup("Type", terrainType);
                if (terrainType == MeshToTerrainSelectTerrainType.existTerrains) ExistTerrainUI();
                else
                {
                    NewTerrainUI();
                }

                useHeightmapSmoothing = GUILayout.Toggle(useHeightmapSmoothing, "Use smoothing of height maps.");

                if (useHeightmapSmoothing)
                {
                    smoothingFactor = EditorGUILayout.IntField("Smoothing factor", smoothingFactor);
                    if (smoothingFactor < 1) smoothingFactor = 1;
                    else if (smoothingFactor > 128) smoothingFactor = 128;
                }

                holes = (MeshToTerrainHoles)EditorGUILayout.EnumPopup("Holes", holes);
                if (holes == MeshToTerrainHoles.remove)
                {
                    removeHoleExtraPixels = EditorGUILayout.IntSlider("Remove Extra Pixels", removeHoleExtraPixels, 0, 5);
                }
            }
            EditorGUILayout.EndVertical();
        }
    }
}