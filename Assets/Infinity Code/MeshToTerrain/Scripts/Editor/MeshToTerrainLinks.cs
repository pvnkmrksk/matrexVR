/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public static class MeshToTerrainLinks
    {
        public const string hugeTexture = "https://assetstore.unity.com/packages/tools/input-management/huge-texture-163576?aid=1100liByC&pubref=mtt_ht_asset";

        private const string aid = "?aid=1100liByC&pubref=mtt_asset";
        private const string assetStore = "https://assetstore.unity.com/packages/tools/terrain/mesh-to-terrain-7271";
        private const string changelog = "https://infinity-code.com/products_update/get-changelog.php?asset=Mesh%20to%20Terrain&from=1.0";
        private const string documentation = "https://infinity-code.com/documentation/mesh-to-terrain.html";
        private const string forum = "https://forum.infinity-code.com";
        private const string homepage = "https://infinity-code.com/assets/mesh-to-terrain";
        private const string reviews = assetStore + "/reviews";
        private const string support = "mailto:support@infinity-code.com?subject=Mesh%20to%20Terrain";
        private const string youtube = "https://www.youtube.com/playlist?list=PL2QU1uhBMew_mR83EYhex5q3uZaMTwg1S";

        public static void Open(string url)
        {
            Application.OpenURL(url);
        }

        public static void OpenAssetStore()
        {
            Open(assetStore + aid);
        }

        public static void OpenChangelog()
        {
            Open(changelog);
        }

        [MenuItem(MeshToTerrainPrefs.MenuPath + "Documentation", false, 120)]
        public static void OpenDocumentation()
        {
            OpenDocumentation(null);
        }

        public static void OpenDocumentation(string anchor)
        {
            string url = documentation;
            if (!string.IsNullOrEmpty(anchor)) url += "#" + anchor;
            Open(url);
        }

        public static void OpenForum()
        {
            Open(forum);
        }

        public static void OpenHomepage()
        {
            Open(homepage);
        }

        public static void OpenReviews()
        {
            Open(reviews + aid);
        }

        public static void OpenSupport()
        {
            Open(support);
        }

        public static void OpenYouTube()
        {
            Open(youtube);
        }
    }
}