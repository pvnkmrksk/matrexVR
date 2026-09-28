/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if HUGETEXTURE
using InfinityCode.HugeTexture.Editors;
#endif

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrain
    {
        private static readonly int MainTextProperty = Shader.PropertyToID("_MainTex");
        private static readonly int ColsProperty = Shader.PropertyToID("_Cols");
        private static readonly int RowsProperty = Shader.PropertyToID("_Rows");
        
        private Color32[] colors;
        private Material material;
        private Texture2D mainTexture;

#if RTP
        private List<Texture2D> rtpTextures;
#endif

        private void FixHugeTextureImporter(string textureFilename)
        {
#if HUGETEXTURE
            HugeRawImporter importer = AssetImporter.GetAtPath(textureFilename) as HugeRawImporter;
            if (!importer) return;
            
            SerializedObject so = new SerializedObject(importer);
            so.FindProperty("pageSize").intValue = prefs.hugeTexturePageSize;
            so.FindProperty("cols").intValue = prefs.hugeTextureCols;
            so.FindProperty("rows").intValue = prefs.hugeTextureRows;
            so.FindProperty("originalWidth").intValue = prefs.textureWidth;
            so.FindProperty("originalHeight").intValue = prefs.textureHeight;
            so.FindProperty("compressed").boolValue = true;
            so.ApplyModifiedProperties();
            importer.SaveAndReimport();
#endif
        }

        private Color GetColor(Vector3 curPoint, float raycastDistance, Vector3 raycastDirection, int mLayer, ref Renderer lastRenderer)
        {
            RaycastHit hit;
            if (!Physics.Raycast(curPoint, raycastDirection, out hit, raycastDistance, mLayer)) return prefs.textureEmptyColor;

            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (!renderer || !renderer.sharedMaterial) return prefs.textureEmptyColor;

            if (lastRenderer != renderer)
            {
                lastRenderer = renderer;
                material = renderer.sharedMaterial;
                mainTexture = material.mainTexture as Texture2D;
                if (mainTexture) CheckReadWriteEnabled(mainTexture);
            }

            if (mainTexture)
            {
                Vector2 tc = hit.textureCoord;
                return mainTexture.GetPixelBilinear(tc.x, tc.y);
            }

            return material.color;
        }

        private Texture2DArray GetHugeTexture(Terrain t)
        {
            string textureFilename = Path.Combine(resultFolder, t.name + ".hugeraw");
            
            FileStream stream = File.Open(textureFilename, FileMode.Create);

            for (int y = prefs.textureHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < prefs.textureWidth; x++)
                {
                    Color32 clr = colors[y * prefs.textureWidth + x];
                    stream.WriteByte(clr.r);
                    stream.WriteByte(clr.g);
                    stream.WriteByte(clr.b);
                }
            }
            stream.Close();
            
            AssetDatabase.Refresh();
            FixHugeTextureImporter(textureFilename);

            return AssetDatabase.LoadAssetAtPath<Texture2DArray>(textureFilename);
        }

        private Texture2D GetTexture(Terrain t)
        {
            Texture2D texture = new Texture2D(prefs.textureWidth, prefs.textureHeight);
            texture.SetPixels32(colors);
            texture.Apply();

            string textureFilename = Path.Combine(resultFolder, t.name + ".png");
            File.WriteAllBytes(textureFilename, texture.EncodeToPNG());
            AssetDatabase.Refresh();
            TextureImporter importer = AssetImporter.GetAtPath(textureFilename) as TextureImporter;
            if (importer)
            {
                importer.maxTextureSize = Mathf.Max(prefs.textureWidth, prefs.textureHeight);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            texture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureFilename, typeof(Texture2D));
            return texture;
        }

#if RTP
        private void LoadRTPTextures()
        {
            if (rtpTextures != null && rtpTextures.Count == 12) return;

            rtpTextures = new List<Texture2D>
            {
                (Texture2D) FindAndLoad("Dirt.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Dirt Height.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Dirt Normal.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Grass.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Grass Height.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Grass Normal.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("GrassRock.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("GrassRock Height.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("GrassRock Normal.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Cliff.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Cliff Height.png", typeof(Texture2D)),
                (Texture2D) FindAndLoad("Cliff Normal.png", typeof(Texture2D))
            };
        }
#endif

        private void PrepareTexture()
        {
            if (prefs.textureCaptureMode != MeshToTerrainTextureCaptureMode.camera)
            {
                phase = MeshToTerrainPhase.generateTextures;
                return;
            }

            float sx = prefs.textureWidth * prefs.newTerrainCountX / boundsRange.x;
            float sz = prefs.textureHeight * prefs.newTerrainCountY / boundsRange.z;

            meshScale = new Vector3(sx, (sx + sz) / 2, sz);
            meshContainer.transform.localScale = meshScale;
            boundsRange.Scale(meshScale);
            maxBounds = minBounds + boundsRange;

            phase = MeshToTerrainPhase.generateTextures;
        }

        private Texture2D RenderCamera(Vector3 curPoint, int mLayer, float raycastDistance)
        {
            GameObject cameraGo = new GameObject("__Mesh to Terrain Camera__");
            Camera camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.position = curPoint;
            cameraGo.transform.rotation = Quaternion.Euler(prefs.direction == MeshToTerrainDirection.normal ? 90 : -90, 0, 0);
            camera.orthographic = true;
            camera.orthographicSize = boundsRange.x / 2 / prefs.newTerrainCountX;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = prefs.textureEmptyColor;
            camera.cullingMask = mLayer;
            camera.targetTexture = new RenderTexture(prefs.textureWidth, prefs.textureHeight, 16);
            camera.nearClipPlane = 0.00001f;
            camera.farClipPlane = raycastDistance * 10 * Mathf.Max(prefs.newTerrainCountX, prefs.newTerrainCountY);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            camera.Render();
            GameObject cameraGO = cameraGo;

            Texture2D texture = new Texture2D(prefs.textureWidth, prefs.textureHeight);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.ReadPixels(new Rect(0, 0, prefs.textureWidth, prefs.textureHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = currentRT;

            DestroyImmediate(cameraGO);
            return texture;
        }

        private string SaveTexture(Texture2D texture, string name)
        {
            string path = Path.Combine(resultFolder, name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.Refresh();
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer)
            {
                importer.maxTextureSize = Mathf.Max(prefs.textureWidth, prefs.textureHeight);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return path;
        }

        private void SetHugeTextureToTerrain(Terrain t, Texture2DArray textureArray)
        {
            string matPath = Path.Combine(resultFolder, t.name); ;
            if (File.Exists(matPath + ".mat"))
            {
                int index = 1;
                while (File.Exists(matPath + "_" + index + ".mat"))
                {
                    index++;
                }

                matPath += "_" + index;
            }

            matPath += ".mat";

            Shader shader;

#if UNITY_6000_0_OR_NEWER
            if (!GraphicsSettings.defaultRenderPipeline)
#else
            if (!GraphicsSettings.renderPipelineAsset)
#endif
            {
                shader = Shader.Find("Huge Texture/Diffuse Array");
            }
            else shader = Shader.Find("Shader Graphs/HugeTexturePBR");

            Material mat = new Material(shader);
            mat.SetTexture(MainTextProperty, textureArray);
            mat.SetInt(ColsProperty, prefs.hugeTextureCols);
            mat.SetInt(RowsProperty, prefs.hugeTextureRows);

            AssetDatabase.CreateAsset(mat, matPath);

            AssetDatabase.LoadAssetAtPath<Material>(matPath);

            t.materialTemplate = mat;
        }

        private void SetTextureToTerrain(Terrain t, Texture2D texture)
        {
#if !RTP
            float tsx = prefs.textureWidth - 4;
            float tsy = prefs.textureHeight - 4;

            Vector3 size = t.terrainData.size;
            Vector2 tileSize = new Vector2(size.x + size.x / tsx * 4, size.z + size.z / tsy * 4);
            Vector2 tileOffset = new Vector2(size.x / prefs.textureWidth / 2, size.z / prefs.textureHeight / 2);

            TerrainLayer tl = new TerrainLayer
            {
                tileSize = tileSize,
                tileOffset = tileOffset,
                diffuseTexture = texture
            };

            string filename = Path.Combine(resultFolder, t.name + ".terrainlayer");

            AssetDatabase.CreateAsset(tl, filename);
            AssetDatabase.Refresh();

            t.terrainData.terrainLayers = new[] { AssetDatabase.LoadAssetAtPath<TerrainLayer>(filename) };

            SetAlphaMaps(t);
#else
            LoadRTPTextures();
            TerrainLayer[] tls = new TerrainLayer[4];

            for (int i = 0; i < 4; i++)
            {
                tls[i] = new TerrainLayer { diffuseTexture = rtpTextures[i * 3] };
            }

            t.terrainData.terrainLayers = tls;
            SetAlphaMaps(t);

            ReliefTerrain reliefTerrain = t.gameObject.GetComponent<ReliefTerrain>() ?? t.gameObject.AddComponent<ReliefTerrain>();
            reliefTerrain.InitArrays();
            reliefTerrain.ColorGlobal = texture;
#endif
        }

        private void UpdateTexture(Terrain t)
        {
            if (prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.raycast) UpdateTextureRaycast(t);
            else UpdateTextureCamera(t);
        }

        private void UpdateTextureCamera(Terrain t)
        {
            int mLayer = 1 << prefs.meshLayer;
            float raycastDistance = boundsRange.y + 10 * Mathf.Max(prefs.newTerrainCountX, prefs.newTerrainCountY);

            Vector3 vScale = t.terrainData.size;
            float tsx = prefs.textureWidth + 1;
            float tsy = prefs.textureHeight + 1;

            vScale.x /= tsx;
            vScale.z /= tsy;

            Vector3 beginPoint = t.transform.position;
            if (prefs.direction == MeshToTerrainDirection.normal) beginPoint.y += raycastDistance;
            else beginPoint.y = maxBounds.y - raycastDistance;

            Vector3 curPoint = beginPoint + new Vector3(prefs.textureWidth / 2 * vScale.x, 0, prefs.textureHeight / 2 * vScale.z);

            curPoint.x = (curPoint.x - minBounds.x) * boundsRange.x / originalBoundsRange.x + minBounds.x;
            curPoint.y = (curPoint.y - minBounds.y) * boundsRange.y / originalBoundsRange.y + minBounds.y;
            curPoint.z = (curPoint.z - minBounds.z) * boundsRange.z / originalBoundsRange.z + minBounds.z;

            Texture2D texture = RenderCamera(curPoint, mLayer, raycastDistance);

            string path = SaveTexture(texture, t.name);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            SetTextureToTerrain(t, texture);

            activeIndex++;
            progress = activeIndex / (float)prefs.terrains.Count;
            if (activeIndex >= prefs.terrains.Count)
            {
                activeIndex = 0;
                phase = MeshToTerrainPhase.finish;
            }
        }

        private void UpdateTextureRaycast(Terrain t)
        {
            int mLayer = 1 << prefs.meshLayer;
            float raycastDistance = maxBounds.y - minBounds.y + 10;

            Vector3 vScale = t.terrainData.size;
            Vector3 beginPoint = t.transform.position;
            Vector3 raycastDirection = -Vector3.up;
            if (prefs.direction == MeshToTerrainDirection.normal) beginPoint.y += raycastDistance - 5;
            else
            {
                beginPoint.y = maxBounds.y - raycastDistance + 5;
                raycastDirection = Vector3.up;
            }

            float tsx = prefs.textureWidth + 1;
            float tsy = prefs.textureHeight + 1;

            vScale.x = vScale.x / tsx;
            vScale.z = vScale.z / tsy;

            beginPoint += new Vector3(vScale.x / 2, 0, vScale.z / 2);

            Renderer lastRenderer = null;

            if (colors == null)
            {
                colors = new Color32[prefs.textureWidth * prefs.textureHeight];
                lastX = 0;
            }

            double startTime = EditorApplication.timeSinceStartup;

            for (int tx = lastX; tx < prefs.textureWidth; tx++)
            {
                for (int ty = 0; ty < prefs.textureHeight; ty++)
                {
                    int cPos = ty * prefs.textureWidth + tx;

                    Vector3 curPoint = beginPoint + new Vector3(tx * vScale.x, 0, ty * vScale.z);
                    colors[cPos] = GetColor(curPoint, raycastDistance, raycastDirection, mLayer, ref lastRenderer);
                }

                if (EditorApplication.timeSinceStartup - startTime >= 1)
                {
                    lastX = tx + 1;
                    progress = (activeIndex + lastX / (float)prefs.textureWidth) / prefs.terrains.Count;
                    return;
                }
            }

            lastX = 0;

            if (prefs.textureResultType == MeshToTerrainTextureResultType.regularTexture) SetTextureToTerrain(t, GetTexture(t));
            else SetHugeTextureToTerrain(t, GetHugeTexture(t));

            colors = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();

            activeIndex++;
            progress = activeIndex / (float)prefs.terrains.Count;
            if (activeIndex >= prefs.terrains.Count)
            {
                activeIndex = 0;
                phase = MeshToTerrainPhase.finish;
            }
        }

        private static class PrevLightingSettings
        {
            private static AmbientMode ambientMode;
            private static Color ambientSkyColor;
            private static List<Light> disabledLights;

            public static void Restore()
            {
                if (disabledLights == null) return;

                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSkyColor;

                for (int i = 0; i < disabledLights.Count; i++) disabledLights[i].enabled = true;
                disabledLights = null;
            }

            public static void Save()
            {
                ambientMode = RenderSettings.ambientMode;
                ambientSkyColor = RenderSettings.ambientSkyColor;

                disabledLights = new List<Light>();

                Light[] lights = MeshToTerrainUtils.FindObjectsOfType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (!light.enabled) continue;

                    light.enabled = false;
                    disabledLights.Add(light);
                }
            }
        }
    }
}