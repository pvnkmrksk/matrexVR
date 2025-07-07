/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        private void Load()
        {
            basemapDistance = LoadPref("BasemapDistance", 20000);
            baseMapResolution = LoadPref("BaseMapRes", 1024);
            boundsGameObject = LoadPrefGameObject("BoundsGameObject", null);
            boundsType = (MeshToTerrainBounds)LoadPref("BoundsType", (int)MeshToTerrainBounds.autoDetect);
            if (boundsType == MeshToTerrainBounds.selectBounds)
            {
                bounds = LoadPref("Bounds", new Bounds(Vector3.zero, Vector3.one));
            }
            detailResolution = LoadPref("DetailMapRes", 1024);
            direction = (MeshToTerrainDirection)LoadPref("Direction", (int)MeshToTerrainDirection.normal);
            generateTextures = LoadPref("GenerateTextures", false);
            heightmapResolution = LoadPref("HeightMapRes", 129);
            meshes = LoadPref("Meshes", new List<GameObject>());
            holes = (MeshToTerrainHoles)LoadPref("Holes", (int)MeshToTerrainHoles.minimumValue);
            hugeTexturePageSize = LoadPref("HugeTexturePageSize", 2048);
            hugeTextureRows = LoadPref("HugeTextureRows", 13);
            hugeTextureCols = LoadPref("HugeTextureCols", 13);
            meshFindType = (MeshToTerrainFindType)LoadPref("MeshFindType", (int)MeshToTerrainFindType.gameObjects);
            meshLayer = LoadPref("MeshLayer", 31);
            newTerrainCountX = LoadPref("CountX", 1);
            newTerrainCountY = LoadPref("CountY", 1);
            pixelError = LoadPref("PixelError", 1);
            removeHoleExtraPixels = LoadPref("RemoveHoleExtraPixels", 0);
            resolutionPerPatch = LoadPref("ResPerPath", 16);
            showMeshes = LoadPref("ShowMeshes", true);
            setAmbientLight = LoadPref("SetAmbientLight", true);
            showTerrains = LoadPref("ShowTerrains", true);
            showTextures = LoadPref("ShowTextures", true);
            smoothingFactor = LoadPref("SmoothingFactor", 1);
            terrains = LoadPref("Terrains", new List<Terrain>());
            terrainType = (MeshToTerrainSelectTerrainType)LoadPref("TerrainType", (int)MeshToTerrainSelectTerrainType.newTerrains);
            textureCaptureMode = (MeshToTerrainTextureCaptureMode)LoadPref("TextureCaptureMode", (int)MeshToTerrainTextureCaptureMode.camera);
            textureEmptyColor = LoadPref("TextureEmptyColor", Color.white);
            textureHeight = LoadPref("TextureHeight", 1024);
            textureResultType = (MeshToTerrainTextureResultType)LoadPref("TextureResultType", (int) MeshToTerrainTextureResultType.regularTexture);
            textureWidth = LoadPref("TextureWidth", 1024);
            useHeightmapSmoothing = LoadPref("UseHeightmapSmoothing", true);
            yRange = (MeshToTerrainYRange)LoadPref("YRange", 0);
            yRangeValue = LoadPref("YRangeValue", yRangeValue);
        }

        private bool LoadPref(string id, bool defVal)
        {
            string key = appKey + id;
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetBool(key) : defVal;
        }

        private Color LoadPref(string id, Color defVal)
        {
            return new Color(LoadPref(id + "_R", defVal.r), LoadPref(id + "_G", defVal.g), LoadPref(id + "_B", defVal.b), LoadPref(id + "_A", defVal.a));
        }

        private Bounds LoadPref(string id, Bounds defVal)
        {
            return new Bounds(LoadPref(id + "_Center", defVal.center), LoadPref(id + "_Size", defVal.size));
        }

        private Vector3 LoadPref(string id, Vector3 defVal)
        {
            return new Vector3(LoadPref(id + "_X", defVal.x), LoadPref(id + "_Y", defVal.y), LoadPref(id + "_Z", defVal.z));
        }

        private float LoadPref(string id, float defVal)
        {
            string key = appKey + id;
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetFloat(key) : defVal;
        }

        private int LoadPref(string id, int defVal)
        {
            string key = appKey + id;
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetInt(key) : defVal;
        }

        private List<GameObject> LoadPref(string id, List<GameObject> defVals)
        {
            string key = appKey + id + "_Count";
            if (EditorPrefs.HasKey(key))
            {
                int count = EditorPrefs.GetInt(appKey + id + "_Count");
                List<GameObject> retVal = new List<GameObject>();
                for (int i = 0; i < count; i++) retVal.Add(EditorUtility.InstanceIDToObject(EditorPrefs.GetInt(appKey + id + "_" + i)) as GameObject);
                return retVal;
            }
            return defVals;
        }

        private List<Terrain> LoadPref(string id, List<Terrain> defVals)
        {
            string key = appKey + id + "_Count";
            if (EditorPrefs.HasKey(key))
            {
                int count = EditorPrefs.GetInt(appKey + id + "_Count");
                List<Terrain> retVal = new List<Terrain>();
                for (int i = 0; i < count; i++) retVal.Add(EditorUtility.InstanceIDToObject(EditorPrefs.GetInt(appKey + id + "_" + i)) as Terrain);
                return retVal;
            }
            return defVals;
        }

        private GameObject LoadPrefGameObject(string id, GameObject defVal)
        {
            int goID = LoadPref(id, -1);
            if (goID == -1) return defVal;
            GameObject go = EditorUtility.InstanceIDToObject(goID) as GameObject;
            return go ?? defVal;
        }

        public void Save()
        {
            SetPref("BasemapDistance", basemapDistance);
            SetPref("BaseMapRes", baseMapResolution);
            SetPref("BoundsGameObject", boundsGameObject);
            SetPref("BoundsType", (int)boundsType);
            if (boundsType == MeshToTerrainBounds.selectBounds) SetPref("Bounds", bounds);
            SetPref("CountX", newTerrainCountX);
            SetPref("CountY", newTerrainCountY);
            SetPref("DetailMapRes", detailResolution);
            SetPref("Direction", (int)direction);
            SetPref("GenerateTextures", generateTextures);
            SetPref("HeightMapRes", heightmapResolution);
            SetPref("Holes", (int)holes); ;
            SetPref("HugeTexturePageSize", hugeTexturePageSize);
            SetPref("HugeTextureRows", hugeTextureRows);
            SetPref("HugeTextureCols", hugeTextureCols);
            SetPref("Meshes", meshes);
            SetPref("MeshFindType", (int)meshFindType);
            SetPref("MeshLayer", meshLayer);
            SetPref("PixelError", pixelError);
            SetPref("RemoveHoleExtraPixels", removeHoleExtraPixels);
            SetPref("ResPerPath", resolutionPerPatch);
            SetPref("SetAmbientLight", setAmbientLight);
            SetPref("ShowMeshes", showMeshes);
            SetPref("ShowTerrains", showTerrains);
            SetPref("ShowTextures", showTextures);
            SetPref("SmoothingFactor", smoothingFactor);
            SetPref("Terrains", terrains);
            SetPref("TerrainType", (int)terrainType);
            SetPref("TextureCaptureMode", (int)textureCaptureMode);
            SetPref("TextureEmptyColor", textureEmptyColor);
            SetPref("TextureHeight", textureHeight);
            SetPref("TextureResultType", (int)textureResultType);
            SetPref("TextureWidth", textureWidth);
            SetPref("UseHeightmapSmoothing", useHeightmapSmoothing);
            SetPref("YRange", (int)yRange);
            SetPref("YRangeValue", yRangeValue);
        }

        private void SetPref(string id, bool val)
        {
            EditorPrefs.SetBool(appKey + id, val);
        }

        private void SetPref(string id, Color val)
        {
            SetPref(id + "_R", val.r);
            SetPref(id + "_G", val.g);
            SetPref(id + "_B", val.b);
            SetPref(id + "_A", val.a);
        }

        private void SetPref(string id, Vector3 val)
        {
            SetPref(id + "_X", val.x);
            SetPref(id + "_Y", val.y);
            SetPref(id + "_Z", val.z);
        }

        private void SetPref(string id, Bounds val)
        {
            SetPref(id + "_Center", val.center);
            SetPref(id + "_Size", val.size);
        }

        private void SetPref(string id, float val)
        {
            EditorPrefs.SetFloat(appKey + id, val);
        }

        private void SetPref(string id, int val)
        {
            EditorPrefs.SetInt(appKey + id, val);
        }

        private void SetPref(string id, List<GameObject> vals)
        {
            if (vals != null)
            {
                EditorPrefs.SetInt(appKey + id + "_Count", vals.Count);

                for (int i = 0; i < vals.Count; i++)
                {
                    Object val = vals[i];
                    if (val != null) EditorPrefs.SetInt(appKey + id + "_" + i, val.GetInstanceID());
                }
            }
            else EditorPrefs.SetInt(appKey + id + "_Count", 0);
        }

        private void SetPref(string id, List<Terrain> vals)
        {
            if (vals != null)
            {
                EditorPrefs.SetInt(appKey + id + "_Count", vals.Count);

                for (int i = 0; i < vals.Count; i++)
                {
                    Object val = vals[i];
                    if (val != null) EditorPrefs.SetInt(appKey + id + "_" + i, val.GetInstanceID());
                }
            }
            else EditorPrefs.SetInt(appKey + id + "_Count", 0);
        }

        private void SetPref(string id, Object val)
        {
            if (val != null) EditorPrefs.SetInt(appKey + id, val.GetInstanceID());
        }
    }
}