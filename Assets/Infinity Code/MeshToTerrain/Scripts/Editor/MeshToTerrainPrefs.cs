/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.Collections.Generic;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        private const string appKey = "MTT_";

        public int alphamapResolution = 512;
        public int basemapDistance = 20000;
        public int baseMapResolution = 1024;
        public Bounds bounds;
        public GameObject boundsGameObject;
        public MeshToTerrainBoundsHelper boundsHelper;
        public MeshToTerrainBounds boundsType = MeshToTerrainBounds.autoDetect;
        public MeshToTerrainDirection direction;
        public int detailResolution = 1024;
        public bool generateTextures;
        public int heightmapResolution = 129;
        public MeshToTerrainHoles holes = MeshToTerrainHoles.minimumValue;
        public int hugeTexturePageSize = 2048;
        public int hugeTextureRows = 13;
        public int hugeTextureCols = 13;
        public List<GameObject> meshes = new List<GameObject>();
        public MeshToTerrainFindType meshFindType = MeshToTerrainFindType.gameObjects;
        public int meshLayer = 31;
        public int newTerrainCountX = 1;
        public int newTerrainCountY = 1;
        public int pixelError = 1;
        public int removeHoleExtraPixels = 0;
        public int resolutionPerPatch = 16;
        public bool setAmbientLight = true;
        public bool showBoundSelector;
        public int smoothingFactor = 1;
        public List<Terrain> terrains = new List<Terrain>();
        public MeshToTerrainSelectTerrainType terrainType = MeshToTerrainSelectTerrainType.newTerrains;
        public MeshToTerrainTextureCaptureMode textureCaptureMode = MeshToTerrainTextureCaptureMode.camera;
        public Color textureEmptyColor = Color.white;
        public MeshToTerrainTextureResultType textureResultType = MeshToTerrainTextureResultType.regularTexture;
        public int textureHeight = 1024;
        public int textureWidth = 1024;
        public bool useHeightmapSmoothing = true;
        public MeshToTerrainYRange yRange = MeshToTerrainYRange.minimalRange;
        public int yRangeValue = 1000;
    }
}