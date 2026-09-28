/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        private readonly int[] textureSizes = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 12288, 16384, 20480, 26624 };
        private readonly string[] textureSizesS = { "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192", "12288", "16384", "20480", "26624" };

        private static readonly string[] hugeTexturePageSizes = { "256", "512", "1024", "2048", "4096", "8192" };
        private static readonly int[] hugeTexturePageSizesS = { 256, 512, 1024, 2048, 4096, 8192 };

        private bool showTextures = true;

        private void HugeTextureUI()
        {
#if HUGETEXTURE
            EditorGUILayout.HelpBox("Important: when using Huge Texture, you cannot use Terrain Layers.\nHuge Texture can only use Capture Mode - Raycast.", MessageType.Info);

#if RTP
            EditorGUILayout.HelpBox("Relief Terrain Pack Detected: when using Huge Texture, you will not be able to use Relief Terrain Pack.", MessageType.Info);
#endif

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("Capture Mode", MeshToTerrainTextureCaptureMode.raycast);
            textureCaptureMode = MeshToTerrainTextureCaptureMode.raycast;
            EditorGUI.EndDisabledGroup();

            TextureRaycastMessage();

            hugeTexturePageSize = EditorGUILayout.IntPopup("Page Size", hugeTexturePageSize, hugeTexturePageSizes, hugeTexturePageSizesS);
            hugeTextureCols = EditorGUILayout.IntField("Cols", hugeTextureCols);
            hugeTextureRows = EditorGUILayout.IntField("Rows", hugeTextureRows);

            if (hugeTextureCols < 1) hugeTextureCols = 1;
            if (hugeTextureRows < 1) hugeTextureRows = 1;

            long width = hugeTexturePageSize * hugeTextureCols;
            long height = hugeTexturePageSize * hugeTextureRows;

            EditorGUILayout.LabelField("Width", width.ToString());
            EditorGUILayout.LabelField("Height", height.ToString());
            EditorGUILayout.LabelField("Total Pages", (hugeTextureCols * hugeTextureCols).ToString());

            if (width * height * 3 > 2147483648L)
            {
                EditorGUILayout.HelpBox("Width * Height * 3 must be less than or equal to 2 GB (2 147 483 648 bytes).", MessageType.Error);
            }
#else
            EditorGUILayout.HelpBox("Huge Texture is not in the project.", MessageType.Warning);
            if (GUILayout.Button("Download Huge Texture")) Application.OpenURL(MeshToTerrainLinks.hugeTexture);
#endif
        }

        private void RegularTextureUI()
        {
            if (terrainType == MeshToTerrainSelectTerrainType.newTerrains)
            {
                textureCaptureMode = (MeshToTerrainTextureCaptureMode)EditorGUILayout.EnumPopup("Capture Mode", textureCaptureMode);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.EnumPopup("Capture Mode", MeshToTerrainTextureCaptureMode.raycast);
                EditorGUI.EndDisabledGroup();

                textureCaptureMode = MeshToTerrainTextureCaptureMode.raycast;
            }

            if (textureCaptureMode == MeshToTerrainTextureCaptureMode.camera)
            {
                setAmbientLight = EditorGUILayout.Toggle("Auto Setup Lighting", setAmbientLight);

                if (!setAmbientLight) EditorGUILayout.HelpBox("Texture Capture Mode - Camera is affected by the lighting of the scene. Prepare it first.", MessageType.Info);

                textureWidth = textureHeight = EditorGUILayout.IntPopup("Resolution", textureWidth, textureSizesS, textureSizes);
            }
            else
            {
                EditorGUILayout.HelpBox("Terrain Type - Exist Terrains can only use Capture Mode - Raycast.", MessageType.Info);
                TextureRaycastMessage();

                textureWidth = EditorGUILayout.IntPopup("Width", textureWidth, textureSizesS, textureSizes);
                textureHeight = EditorGUILayout.IntPopup("Height", textureHeight, textureSizesS, textureSizes);
            }

            if (textureWidth > 8192 || textureHeight > 8192)
            {
#if !HUGETEXTURE
                EditorGUILayout.HelpBox("To use textures with side sizes larger than 8192px, you must have a Huge Texture asset in the project.", MessageType.Error);
                if (GUILayout.Button("Huge Texture")) Application.OpenURL(MeshToTerrainLinks.hugeTexture);
#else
                EditorGUILayout.HelpBox("To use textures with side sizes larger than 8192px switch to Result Type - Huge Texture.", MessageType.Error);
                if (GUILayout.Button("Set Result Type - Huge Texture"))
                {
                    textureResultType = MeshToTerrainTextureResultType.hugeTexture;
                    hugeTexturePageSize = 1024;
                    hugeTextureCols = Mathf.CeilToInt(textureWidth / (float) hugeTexturePageSize);
                    hugeTextureRows = Mathf.CeilToInt(textureHeight / (float) hugeTexturePageSize);
                }
#endif
            }
        }

        private void TextureRaycastMessage()
        {
            EditorGUILayout.HelpBox("Capture Mode - Raycast does not support complex texturing methods and only uses the main texture.", MessageType.Warning);
        }

        private void TextureContentUI()
        {
            textureResultType = (MeshToTerrainTextureResultType)EditorGUILayout.EnumPopup("Result Type", textureResultType);

            if (textureResultType == MeshToTerrainTextureResultType.regularTexture) RegularTextureUI();
            else if (textureResultType == MeshToTerrainTextureResultType.hugeTexture) HugeTextureUI();

            if (holes != MeshToTerrainHoles.remove) textureEmptyColor = EditorGUILayout.ColorField("Empty Color", textureEmptyColor);
        }

        private void TextureHeaderUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            showTextures = GUILayout.Toggle(showTextures, GUIContent.none, EditorStyles.foldout, GUILayout.ExpandWidth(false));
            generateTextures = GUILayout.Toggle(generateTextures, "Textures");
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!generateTextures);
            if (showTextures) TextureContentUI();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}