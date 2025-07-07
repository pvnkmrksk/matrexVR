/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrain
    {
        private GameObject container;
        private float[,] heightmap;
        private bool[,] holes;
        private int countHoles;

        private void AverageFillHoles()
        {
            if (countHoles == 0) return;

            int w = heightmap.GetLength(1);
            int h = heightmap.GetLength(0);

            List<AverageHoleItem> holeItems = new List<AverageHoleItem>();

            EditorUtility.DisplayCancelableProgressBar("Fill Holes", "0%", 0);
            int filled = 0;

            while (true)
            {
                holeItems.Clear();

                for (int tx = 0; tx < w; tx++)
                {
                    for (int ty = 0; ty < h; ty++)
                    {
                        if (Math.Abs(heightmap[ty, tx] - float.MinValue) < float.Epsilon)
                        {
                            int countNeighbors;
                            float newValue;

                            GetAverageHoleValue(tx, ty, w, h, out countNeighbors, out newValue);

                            if (countNeighbors >= 3) holeItems.Add(new AverageHoleItem(tx, ty, newValue));
                        }
                    }
                }

                if (holeItems.Count == 0) break;
                foreach (AverageHoleItem item in holeItems) heightmap[item.y, item.x] = item.newValue;
                filled += holeItems.Count;
                float progress = (float) filled / countHoles;
                if (EditorUtility.DisplayCancelableProgressBar("Fill Holes", Mathf.RoundToInt(progress * 100) + "%", progress))
                {
                    break;
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private void CreateTerrain(int terrainIndex)
        {
            int x = terrainIndex % prefs.newTerrainCountX;
            int y = terrainIndex / prefs.newTerrainCountX;

            float w = boundsRange.x;
            float h = boundsRange.z;

            float sW = w / prefs.newTerrainCountX;
            float sH = h / prefs.newTerrainCountY;
            float sY = boundsRange.y * 1.5f;

            float offX = (w - sW * prefs.newTerrainCountX) / 2;
            float offY = (h - sH * prefs.newTerrainCountY) / 2;

            string terrainName = $"Terrain {x}x{y}";
            GameObject terrainGO = CreateTerrainGameObject(sW, sY, sH, terrainName);

            terrainGO.name = terrainName;
            terrainGO.transform.parent = container.transform;
            terrainGO.transform.localPosition = new Vector3(x * sW + offX, 0, y * sH + offY);
            Terrain terrain = terrainGO.GetComponent<Terrain>();
            terrain.basemapDistance = prefs.basemapDistance;
            terrain.heightmapPixelError = prefs.pixelError;
            prefs.terrains.Add(terrain);

            activeIndex++;
            progress = activeIndex / (float)(prefs.newTerrainCountX * prefs.newTerrainCountY);
            if (activeIndex >= prefs.newTerrainCountX * prefs.newTerrainCountY)
            {
                if (prefs.terrains.Count > 1)
                {
                    for (int i = 0; i < prefs.terrains.Count; i++)
                    {
                        SetTerrainNeighbors(i);
                    }
                }

                activeIndex = 0;
                progress = 0;
                phase = MeshToTerrainPhase.generateHeightmaps;
            }
        }

        private void CreateTerrainContainer()
        {
            const string containerName = "Generated terrains";

            prefs.terrains = new List<Terrain>();

            string cName = containerName;
            int index = 1;
            while (GameObject.Find(cName)) cName = containerName + " " + index++;

            container = new GameObject(cName);
            container.transform.position = new Vector3(minBounds.x, minBounds.y, minBounds.z);
        }

        private GameObject CreateTerrainGameObject(float sW, float sY, float sH, string terrainName)
        {
            TerrainData tdata = new TerrainData();
            tdata.SetDetailResolution(prefs.detailResolution, prefs.resolutionPerPatch);
            tdata.alphamapResolution = prefs.alphamapResolution;
            tdata.baseMapResolution = prefs.baseMapResolution;
            tdata.heightmapResolution = prefs.heightmapResolution;
            tdata.size = new Vector3(sW, sY, sH);

            string filename = Path.Combine(resultFolder, terrainName + ".asset");

            AssetDatabase.CreateAsset(tdata, filename);
            GameObject terrainGO = Terrain.CreateTerrainGameObject(tdata);
            return terrainGO;
        }

        private void GetAverageHoleValue(int tx, int ty, int w, int h, out int countNeighbors, out float newValue)
        {
            countNeighbors = 0;
            newValue = float.MinValue;

            int[] dirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dirY = { -1, -1, 0, 1, 1, 1, 0, -1 };

            List<float> neighborValues = new List<float>();

            for (int i = 0; i < 8; i++)
            {
                int nx = tx + dirX[i];
                int ny = ty + dirY[i];

                float p;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) p = float.MinValue;
                else p = heightmap[ny, nx];
                if (Math.Abs(p - float.MinValue) > float.Epsilon)
                {
                    countNeighbors++;
                    neighborValues.Add(p);
                }
            }

            if (countNeighbors >= 3) newValue = neighborValues.Average();
        }

        private float GetValue(int X, int Y)
        {
            X = Mathf.Clamp(X, 0, prefs.heightmapResolution - 1);
            Y = Mathf.Clamp(Y, 0, prefs.heightmapResolution - 1);
            return heightmap[X, Y];
        }

        private double GetValuesAround(int x, int y, int offset, float scale)
        {
            double val = GetValue(x - offset, y - offset) * scale;
            val += GetValue(x, y - offset) * scale;
            val += GetValue(x + offset, y - offset) * scale;
            val += GetValue(x + offset, y) * scale;
            val += GetValue(x + offset, y + offset) * scale;
            val += GetValue(x, y + offset) * scale;
            val += GetValue(x - offset, y + offset) * scale;
            val += GetValue(x - offset, y) * scale;
            return val;
        }

        private void InitHeightmap(TerrainData tdata)
        {
            int heightmapResolution = tdata.heightmapResolution;
            heightmap = new float[heightmapResolution, heightmapResolution];
            lastX = 0;

            int holesResolution = tdata.holesResolution;
            holes = new bool[holesResolution, holesResolution];
            countHoles = 0;
            for (int tx = 0; tx < holesResolution; tx++)
            {
                for (int ty = 0; ty < holesResolution; ty++)
                {
                    holes[ty, tx] = true;
                }
            }
        }

        private void RemoveHoleExtraPixel()
        {
            int s = holes.GetLength(0);
            bool[,] temp = new bool[s, s];
            
            for (int i = 0; i < s; i++)
            {
                for (int j = 0; j < s; j++)
                {
                    bool v = holes[i, j];
                    if (i > 0) v &= holes[i - 1, j];
                    if (j > 0) v &= holes[i, j - 1];
                    if (i < s - 1) v &= holes[i + 1, j];
                    if (j < s - 1) v &= holes[i, j + 1];
                    temp[i, j] = v;
                }
            }
            
            holes = temp;
        }

        private void SetAlphaMaps(Terrain t)
        {
            float[,,] alphamaps = new float[prefs.alphamapResolution, prefs.alphamapResolution, t.terrainData.alphamapLayers];
            for (int x = 0; x < prefs.alphamapResolution; x++)
            {
                for (int y = 0; y < prefs.alphamapResolution; y++)
                {
                    alphamaps[x, y, 0] = 1;
                }
            }

            t.terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        private void SetTerrainNeighbors(int i)
        {
            int leftIndex = i % prefs.newTerrainCountX != 0 ? i - 1 : -1;
            int rightIndex = i % prefs.newTerrainCountX != prefs.newTerrainCountX - 1 ? i + 1 : -1;
            int topIndex = i - prefs.newTerrainCountX;
            int bottomIndex = i + prefs.newTerrainCountX;
            Terrain left = prefs.newTerrainCountX > 1 && leftIndex != -1 ? prefs.terrains[leftIndex] : null;
            Terrain right = prefs.newTerrainCountX > 1 && rightIndex != -1 ? prefs.terrains[rightIndex] : null;
            Terrain top = prefs.newTerrainCountY > 1 && topIndex >= 0 ? prefs.terrains[topIndex] : null;
            Terrain bottom = prefs.newTerrainCountY > 1 && bottomIndex < prefs.terrains.Count ? prefs.terrains[bottomIndex] : null;
            prefs.terrains[i].SetNeighbors(left, bottom, right, top);
        }

        private void SmoothHeightmap()
        {
            int h = heightmap.GetLength(0);
            float[,] smoothedHeightmap = new float[h, h];
            int sf = prefs.smoothingFactor;
            int sfStep = 1;
            if (sf > 8)
            {
                sfStep = sf / 8;
                sf = 8;
            }

            EditorUtility.DisplayCancelableProgressBar("Smooth Heightmap", "0%", 0);

            float progresMul = 1;
            float progressOffset = 0;

            if (sfStep > 1)
            {
                progresMul = 0.5f;
            }

            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    float curV = 0.5f;
                    double totalV = curV;
                    float origVal = GetValue(i, j);
                    double val = origVal * curV;

                    if (i == 0 || i == h - 1 || j == 0 || j == h - 1)
                    {
                        smoothedHeightmap[i, j] = origVal;
                        continue;
                    }

                    curV = 0.3f;

                    for (int v = 1; v <= sf; v++)
                    {
                        int v1 = v * sfStep;
                        val += GetValuesAround(i, j, v1, curV);
                        totalV += curV * 8;
                    }

                    smoothedHeightmap[i, j] = (float)(val / totalV);
                }

                float progress = progresMul * i / h + progressOffset;
                if (EditorUtility.DisplayCancelableProgressBar("Smooth Heightmap", Mathf.RoundToInt(progress * 100) + "%", progress))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
            }

            if (sfStep > 1)
            {
                heightmap = smoothedHeightmap;
                smoothedHeightmap = new float[h, h];
                progressOffset = 0.5f;

                for (int i = 0; i < h; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        float curV = 0.7f;
                        double totalV = curV;
                        double val = GetValue(i, j) * curV;

                        curV = 0.3f;
                        val += GetValuesAround(i, j, 1, curV);
                        totalV += curV * 8;

                        smoothedHeightmap[i, j] = (float)(val / totalV);
                    }

                    float progress = progresMul * i / h + progressOffset;
                    if (EditorUtility.DisplayCancelableProgressBar("Smooth Heightmap", Mathf.RoundToInt(progress * 100) + "%", progress))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }
            }

            EditorUtility.ClearProgressBar();

            heightmap = smoothedHeightmap;
        }

        private void UpdateTerrain(Terrain t)
        {
            int mLayer = 1 << prefs.meshLayer;
            float raycastDistance = boundsRange.y * 1.5f;

            TerrainData tdata = t.terrainData;
            Vector3 vScale = tdata.heightmapScale;
            Vector3 beginPoint = t.transform.position;
            float bx = beginPoint.x;
            float by = beginPoint.y;
            float bz = beginPoint.z;

            Vector3 raycastDirection = Vector3.down;
            if (prefs.direction == MeshToTerrainDirection.normal) by += raycastDistance;
            else
            {
                by = maxBounds.y - raycastDistance;
                raycastDirection = Vector3.up;
            }

            int heightmapResolution = tdata.heightmapResolution;
            if (heightmap == null) InitHeightmap(tdata);

            long startTime = DateTime.Now.Ticks;

            float nodataValue = prefs.holes == MeshToTerrainHoles.minimumValue ? 0 : float.MinValue;

            Vector3 curPoint = new Vector3(bx, by, bz);

            for (int tx = lastX; tx < heightmapResolution; tx++)
            {
                curPoint.x = bx + tx * vScale.x;
                curPoint.z = bz;

                for (int ty = 0; ty < heightmapResolution; ty++)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(curPoint, raycastDirection, out hit, raycastDistance, mLayer))
                    {
                        if (prefs.direction == MeshToTerrainDirection.normal)
                        {
                            float v = (raycastDistance - hit.distance) / vScale.y;
                            heightmap[ty, tx] = v;
                        }
                        else heightmap[ty, tx] = hit.distance / vScale.y;
                    }
                    else
                    {
                        countHoles++;
                        heightmap[ty, tx] = nodataValue;

                        if (ty > 0 && tx > 0) holes[ty - 1, tx - 1] = false;
                        if (ty < heightmapResolution - 1 && tx < heightmapResolution - 1)
                        {
                            if (ty > 0) holes[ty - 1, tx] = false;
                            if (tx > 0) holes[ty, tx - 1] = false;
                            holes[ty, tx] = false;
                        }
                    }

                    curPoint.z += vScale.z;
                }

                if (DateTime.Now.Ticks - startTime >= 10000000)
                {
                    lastX = tx + 1;
                    progress = (activeIndex + lastX / (float)heightmapResolution) / prefs.terrains.Count;
                    return;
                }
            }

            lastX = 0;

            if (prefs.holes == MeshToTerrainHoles.neighborAverage) AverageFillHoles();
            if (prefs.useHeightmapSmoothing) SmoothHeightmap();

            tdata.SetHeights(0, 0, heightmap);

            if (prefs.holes == MeshToTerrainHoles.remove)
            {
                for (int i = 0; i < prefs.removeHoleExtraPixels; i++) RemoveHoleExtraPixel();
                tdata.SetHoles(0, 0, holes);
            }

            t.Flush();

            heightmap = null;

            activeIndex++;
            progress = activeIndex / (float)prefs.terrains.Count;
            if (activeIndex < prefs.terrains.Count) return;
            
            activeIndex = 0;
            progress = 0;
            phase = prefs.generateTextures ? MeshToTerrainPhase.prepareTextures : MeshToTerrainPhase.finish;
        }

        internal struct AverageHoleItem
        {
            public int x;
            public int y;
            public float newValue;

            public AverageHoleItem(int x, int y, float newValue)
            {
                this.x = x;
                this.y = y;
                this.newValue = newValue;
            }
        }
    }
}