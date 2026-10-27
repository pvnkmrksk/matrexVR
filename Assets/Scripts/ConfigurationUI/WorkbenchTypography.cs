using System;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>One native signed-distance font family, shared across setup visits.</summary>
    internal static class WorkbenchTypography
    {
        private static FontAsset regular, semibold;
        internal static void Apply(VisualElement root)
        {
            if (regular == null)
            {
                regular = Create("Inter-Regular");
                semibold = Create("Inter-SemiBold");
                if (regular != null && semibold != null)
                    regular.fontWeightTable[7] = new FontWeightPair { regularTypeface = semibold, italicTypeface = semibold };
            }
            if (regular != null) root.style.unityFontDefinition = new FontDefinition { fontAsset = regular };
        }
        private static FontAsset Create(string file)
        {
            var source = Resources.Load<Font>("ConfigurationUI/Fonts/" + file);
            if (source == null) return null;
            var asset = FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "MATREX " + file + " SDF";
            // Retain the face's OpenType positioning when dynamic glyphs are
            // added later (file names and participant labels included).
            asset.getFontFeatures = true;
            // TextCore defaults to 7% extra synthetic-bold spacing. We provide
            // the real semibold face, so keep its designed advances instead.
            asset.regularStyleSpacing = 0;
            asset.boldStyleSpacing = 0;
            // Warm common UI text once; input-specific Unicode is added dynamically.
            string characters = "";
            for (int c = 32; c < 127; c++) characters += (char)c;
            asset.TryAddCharacters(characters + "éβ°×−–—→↗↓…·ಕನ್ನಡಿ", out _, true);
            return asset;
        }
    }
}
