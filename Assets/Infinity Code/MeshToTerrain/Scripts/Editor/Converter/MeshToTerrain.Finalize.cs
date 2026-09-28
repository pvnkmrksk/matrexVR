/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrain
    {
        private void Dispose()
        {
            if (meshContainer) meshContainer.transform.localScale = Vector3.one;

            FinalizeMeshes();

            if (meshContainer)
            {
                DestroyImmediate(meshContainer);
                meshContainer = null;
            }

            checkedTextures = null;
            colors = null;
            heightmap = null;
            lastX = 0;
            material = null;
            mainTexture = null;

            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
        }

        private static void FinalizeMeshes()
        {
            foreach (MeshToTerrainObject m in terrainObjects)
            {
                m.gameobject.layer = m.layer;
                if (m.tempCollider)
                {
                    DestroyImmediate(m.tempCollider);
                    m.tempCollider = null;
                }

                if (m.changedParent && !m.temporary)
                {
                    m.gameobject.transform.parent = m.originalParent;
                    m.originalParent = null;
                }

                m.gameobject = null;
            }

            terrainObjects = null;
        }

        private void Finish()
        {
#if RTP
            if (prefs.generateTextures)
            {
                ReliefTerrain reliefTerrain = prefs.terrains[0].GetComponent<ReliefTerrain>();
                ReliefTerrainGlobalSettingsHolder settingsHolder = reliefTerrain.globalSettingsHolder;

                settingsHolder.numLayers = 4;
                settingsHolder.splats = new Texture2D[4];
                settingsHolder.Bumps = new Texture2D[4];
                settingsHolder.Heights = new Texture2D[4];

                for (int i = 0; i < 4; i++)
                {
                    settingsHolder.splats[i] = rtpTextures[i * 3];
                    settingsHolder.Heights[i] = rtpTextures[i * 3 + 1];
                    settingsHolder.Bumps[i] = rtpTextures[i * 3 + 2];
                }

                settingsHolder.GlobalColorMapBlendValues = new Vector3(1, 1, 1);
                settingsHolder._GlobalColorMapNearMIP = 1;
                settingsHolder.GlobalColorMapSaturation = 1;
                settingsHolder.GlobalColorMapSaturationFar = 1;
                settingsHolder.GlobalColorMapBrightness = 1;
                settingsHolder.GlobalColorMapBrightnessFar = 1;

                foreach (Terrain item in prefs.terrains) item.GetComponent<ReliefTerrain>().RefreshTextures();

                settingsHolder.Refresh();
            }
#endif

            if (prefs.generateTextures && prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera)
            {
                if (prefs.setAmbientLight) PrevLightingSettings.Restore();
            }

            if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) EditorGUIUtility.PingObject(container);
            else
            {
                foreach (Terrain t in prefs.terrains) EditorGUIUtility.PingObject(t.gameObject);
            }

            Dispose();

            phase = MeshToTerrainPhase.idle;
        }
    }
}