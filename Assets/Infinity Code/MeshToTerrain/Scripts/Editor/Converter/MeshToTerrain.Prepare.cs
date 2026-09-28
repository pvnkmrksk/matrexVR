/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrain
    {
        private static List<MeshToTerrainObject> terrainObjects;
        private List<Texture2D> checkedTextures;

        private bool CheckBounds()
        {
            if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains)
            {
                if (prefs.boundsType == MeshToTerrainBounds.fromGameobject)
                {
                    if (!CheckBoundsFromGameObject()) return false;
                }
                else if (prefs.boundsType == MeshToTerrainBounds.selectBounds)
                {
                    minBounds = prefs.bounds.min;
                    maxBounds = prefs.bounds.max;
                }
                else FindBounds();
            }
            else
            {
                FindBounds();
            }

            return true;
        }

        private bool CheckBoundsFromGameObject()
        {
            if (!prefs.boundsGameObject)
            {
                DisplayDialog("Boundaries GameObject are not set.\nSelect a GameObject in the scene, which will be the boundaries to generated Terrains.");
                return false;
            }

            Renderer r = prefs.boundsGameObject.GetComponent<Renderer>();
            if (!r)
            {
                DisplayDialog("Boundaries GameObject does not contain the Renderer component.\nSelect another GameObject.");
                return false;
            }

            minBounds = r.bounds.min;
            maxBounds = r.bounds.max;
            return true;
        }

        private bool CheckOnlySceneObjects()
        {
            if (prefs.meshes.Any(m => m.scene.name == null))
            {
                DisplayDialog("Selected meshes not in the scene.\nPlease add this meshes into the scene.\nIf the meshes in the scene, then make sure you choose from scene tab.");
                return false;
            }

            return true;
        }

        private void CheckReadWriteEnabled(Texture2D texture)
        {
            if (checkedTextures.Contains(texture)) return;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter && !textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            checkedTextures.Add(texture);
        }

        private bool CheckValues()
        {
            if (prefs.meshFindType == MeshToTerrainFindType.gameObjects)
            {
                if (prefs.meshes.Count == 0)
                {
                    DisplayDialog("No meshes added.");
                    return false;
                }

                if (!CheckOnlySceneObjects()) return false;

                prefs.meshLayer = FindFreeLayer();
                if (prefs.meshLayer == -1)
                {
                    prefs.meshLayer = 31;
                    DisplayDialog("Can not find the free layer.");
                    return false;
                }
            }
            else if (prefs.meshFindType == MeshToTerrainFindType.layers)
            {
                if (prefs.meshLayer == 0)
                {
                    DisplayDialog("Cannot use default layer.\nPlace the models for conversion to another layer.");
                    return false;
                }

                prefs.meshes = FindGameObjectsWithLayer(prefs.meshLayer).ToList();
            }

            if (!CheckBounds()) return false;

            if (prefs.yRange != MeshToTerrainYRange.minimalRange)
            {
                float yRange = maxBounds.y - minBounds.y;
                float halfRange = yRange / 2;
                float center = halfRange + minBounds.y;
                float scale = yRange / (prefs.yRange == MeshToTerrainYRange.fixedValue
                    ? prefs.yRangeValue
                    : Mathf.Max(maxBounds.x - minBounds.x, maxBounds.z - minBounds.z));

                if (scale < 1)
                {
                    maxBounds.y = center + halfRange / scale;
                    minBounds.y = center - halfRange / scale;
                }
            }

            boundsRange = maxBounds - minBounds;
            originalBoundsRange = boundsRange;

            if (prefs.textureResultType == MeshToTerrainTextureResultType.hugeTexture)
            {
#if !HUGETEXTURE
                if (EditorUtility.DisplayDialog("Error", "Huge Texture is not in the project.", "Download Huge Texture", "Cancel"))
                {
                    Application.OpenURL(MeshToTerrainLinks.hugeTexture);
                }
                return false;
#else
                prefs.textureWidth = prefs.hugeTexturePageSize * prefs.hugeTextureCols;
                prefs.textureHeight = prefs.hugeTexturePageSize * prefs.hugeTextureRows;

                if (3L * prefs.textureWidth * prefs.textureHeight > 2147483648L)
                {
                    DisplayDialog("Width * Height * 3 must be less than or equal to 2 GB (2 147 483 648 bytes).");
                    return false;
                }
#endif
            }
            else
            {
                if (prefs.textureWidth > 8192 || prefs.textureHeight > 8192)
                {
                    DisplayDialog("To use textures with side sizes larger than 8192px switch to Result Type - Huge Texture.");
                    return false;
                }
            }

            if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) CreateTerrainContainer();
            else if (prefs.terrains.Count == 0)
            {
                DisplayDialog("No terrains added.");
                return false;
            }

#if !UNITY_2019_3_OR_NEWER
            if (prefs.holes == MeshToTerrainHoles.remove)
            {
                DisplayDialog("Remove the hole requires Unity 2019.3 or later.");
                return false;
            }
#endif

            return true;
        }

        private void FindBounds()
        {
            minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            Renderer[] renderers = prefs.meshes.SelectMany(m => m.GetComponentsInChildren<Renderer>()).ToArray();

            if (renderers.Length == 0) return;

            foreach (Renderer r in renderers)
            {
                Bounds b = r.bounds;

                Vector3 min = b.min;
                Vector3 max = b.max;

                if (minBounds.x > min.x) minBounds.x = min.x;
                if (minBounds.y > min.y) minBounds.y = min.y;
                if (minBounds.z > min.z) minBounds.z = min.z;

                if (maxBounds.x < max.x) maxBounds.x = max.x;
                if (maxBounds.y < max.y) maxBounds.y = max.y;
                if (maxBounds.z < max.z) maxBounds.z = max.z;
            }
        }

        private int FindFreeLayer()
        {
            bool[] ls = new bool[32];

            for (int i = 0; i < 32; i++) ls[i] = true;
            foreach (GameObject go in MeshToTerrainUtils.FindObjectsOfType<GameObject>()) ls[go.layer] = false;

            for (int i = 31; i > 0; i--)
            {
                if (ls[i]) return i;
            }
                
            return -1;
        }

        private IEnumerable<GameObject> FindGameObjectsWithLayer(int layer)
        {
            return MeshToTerrainUtils.FindObjectsOfType<MeshFilter>().Select(m => m.gameObject).Where(go => go.layer == layer);
        }

        private void GetResultFolder()
        {
            const string baseResultFolder = "Assets/MTT_Results";
            string baseResultFullPath = Path.Combine(Application.dataPath, "MTT_Results");
            if (!Directory.Exists(baseResultFullPath)) Directory.CreateDirectory(baseResultFullPath);
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd HH-mm");

            int index = 0;
            bool appendIndex = false;
            while (true)
            {
                resultFolder = baseResultFolder + "/" + dateStr;
                string resultFullPath = Path.Combine(baseResultFullPath, dateStr);

                if (appendIndex)
                {
                    resultFolder += " " + index;
                    resultFullPath += " " + index;
                }

                if (!Directory.Exists(resultFullPath))
                {
                    Directory.CreateDirectory(resultFullPath);
                    break;
                }

                appendIndex = true;
                index++;
            }
        }

        private void Prepare()
        {
            activeIndex = 0;
            checkedTextures = new List<Texture2D>();
            colors = null;
            heightmap = null;
            progress = 0;
            terrainObjects = new List<MeshToTerrainObject>();

            GetResultFolder();

            if (!CheckValues())
            {
                Dispose();
                phase = MeshToTerrainPhase.idle;
                return;
            }

            if (prefs.generateTextures && prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera)
            {
                meshContainer = new GameObject("__Mesh Container__");
                meshContainer.transform.position = minBounds;
            }

            PrepareMeshes(terrainObjects);

            if (prefs.generateTextures && prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera && prefs.setAmbientLight)
            {
                PrevLightingSettings.Save();

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientSkyColor = Color.white;
            }

            if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) phase = MeshToTerrainPhase.createTerrains;
            else phase = MeshToTerrainPhase.generateHeightmaps;
        }

        private void PrepareGameObject(List<MeshToTerrainObject> objs, GameObject go)
        {
            MeshToTerrainObject m = new MeshToTerrainObject(go);
            objs.Add(m);

            if (prefs.generateTextures && prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(go))
                {
                    GameObject source = go;
                    go = Instantiate(go);
                    m.gameobject = go;
                    m.temporary = true;
                    go.transform.rotation = source.transform.rotation;
                }

                m.changedParent = true;
                m.originalParent = go.transform.parent;
                go.transform.parent = meshContainer.transform;
            }

            if (!go.GetComponent<Collider>())
            {
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.convex = false;
                m.tempCollider = collider;
            }

            go.layer = prefs.meshLayer;
        }

        private void PrepareMeshes(List<MeshToTerrainObject> objs)
        {
            if (prefs.meshFindType == MeshToTerrainFindType.gameObjects)
            {
                IEnumerable<GameObject> gos = prefs.meshes.SelectMany(m => m.GetComponentsInChildren<MeshFilter>()).Select(mf => mf.gameObject);
                foreach (GameObject go in gos)
                {
                    PrepareGameObject(objs, go);
                }
            }
            else if (prefs.meshFindType == MeshToTerrainFindType.layers)
            {
                foreach (GameObject go in FindGameObjectsWithLayer(prefs.meshLayer))
                {
                    PrepareGameObject(objs, go);
                }
            }
        }
    }
}