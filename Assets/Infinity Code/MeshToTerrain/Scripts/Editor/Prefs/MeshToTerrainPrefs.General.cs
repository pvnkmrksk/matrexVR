/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    public partial class MeshToTerrainPrefs
    {
        public const string MenuPath = "Window/Infinity Code/Mesh to Terrain/";

        private static string _assetFolder;
        private static Texture2D _helpIcon;
        private static GUIStyle _helpStyle;
        private static string _iconsFolder;
        private static bool hasThirdParty;
        private static bool hasRTP;
        private static bool needFindIcon = true;

        private Vector2 scrollPos = Vector2.zero;

        public static string assetFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_assetFolder))
                {
                    string[] assets = AssetDatabase.FindAssets("MeshToTerrainObject t:script");
                    string filename = AssetDatabase.GUIDToAssetPath(assets[0]);
                    FileInfo info = new FileInfo(filename);
                    _assetFolder = info.Directory.Parent.Parent.FullName.Substring(Application.dataPath.Length - 6) + "/";
                }

                return _assetFolder;
            }
        }

        public static string iconsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_iconsFolder)) _iconsFolder = assetFolder + "Icons/";

                return _iconsFolder;
            }
        }

        public static Texture2D helpIcon
        {
            get
            {
                if (!_helpIcon && needFindIcon)
                {
                    string[] guids = AssetDatabase.FindAssets("HelpIcon t:texture");
                    if (guids != null && guids.Length > 0)
                    {
                        string iconPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _helpIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(iconPath, typeof(Texture2D));
                    }
                    needFindIcon = false;
                }
                return _helpIcon;
            }
        }

        public static GUIStyle helpStyle
        {
            get
            {
                if (_helpStyle == null)
                {
                    _helpStyle = new GUIStyle();
                    _helpStyle.margin = new RectOffset(0, 0, 2, 0);
                }
                return _helpStyle;
            }
        }

        private static void AddCompilerDirective(object key)
        {
            BuildTargetGroup g = EditorUserBuildSettings.selectedBuildTargetGroup;
            
#if UNITY_2023_1_OR_NEWER
            UnityEditor.Build.NamedBuildTarget buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g);
            string currentDefinitions = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
#else
            string currentDefinitions = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
#endif

            string[] defs = currentDefinitions.Split(';').Select(d => d.Trim(' ')).ToArray();

            if (defs.Any(d => d == key.ToString())) return;
            currentDefinitions += ";" + key;

#if UNITY_2023_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, currentDefinitions);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(g, currentDefinitions);
#endif
        }

        private static void DeleteCompilerDirective(object key)
        {
            BuildTargetGroup g = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2023_1_OR_NEWER
            UnityEditor.Build.NamedBuildTarget buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(g);
            string currentDefinitions = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
#else
            string currentDefinitions = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
#endif

            string[] defs = currentDefinitions.Split(';').Select(d => d.Trim(' ')).ToArray();

            if (defs.All(d => d != key.ToString())) return;
            ArrayUtility.Remove(ref defs, key.ToString());
            currentDefinitions = string.Join(";", defs);
                
#if UNITY_2023_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, currentDefinitions);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(g, currentDefinitions);
#endif
        }

        public static void DrawField(string label, ref int value, string tooltip, string href, string[] displayedOptions = null, int[] optionValues = null)
        {
            EditorGUILayout.BeginHorizontal();
            DrawHelpButton(tooltip, href);
            EditorGUILayout.LabelField(label, EditorStyles.label);
            if (displayedOptions != null) value = EditorGUILayout.IntPopup(string.Empty, value, displayedOptions, optionValues);
            else value = EditorGUILayout.IntField("", value);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHelpButton(string tooltip, string href = null)
        {
            if (GUILayout.Button(new GUIContent(helpIcon, tooltip), helpStyle, GUILayout.ExpandWidth(false)))
            {
                if (!string.IsNullOrEmpty(href)) Application.OpenURL(href);
            }
        }

        public static bool Foldout(bool value, string text)
        {
            return GUILayout.Toggle(value, text, EditorStyles.foldout);
        }

        public void Init()
        {
            Load();

            hasRTP = typeof(MeshToTerrain).Assembly.GetType("RTP_LODmanagerEditor") != null;
            hasThirdParty = hasRTP;
        }

        public static int IntSlider(string label, int value, int leftValue, int rightValue, string tooltip, string href = null)
        {
            EditorGUILayout.BeginHorizontal();
            DrawHelpButton(tooltip, href);
            value = EditorGUILayout.IntSlider(label, value, leftValue, rightValue);
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private static int LimitPowTwo(int val, int min = 32, int max = 4096)
        {
            return Mathf.Clamp(Mathf.ClosestPowerOfTwo(val), min, max);
        }

        public void OnGUI()
        {
            ToolbarUI();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            MeshesUI();
            TerrainsUI();
            TextureHeaderUI();

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Start")) MeshToTerrain.phase = MeshToTerrainPhase.prepare;
        }

        private static void ToolbarUI()
        {
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.toolbarButton);

            GUILayout.BeginHorizontal();
            GUILayout.Label("", buttonStyle);

            ToolbarThirdPartyUI(buttonStyle);
            ToolbarHelpUI(buttonStyle);

            GUILayout.EndHorizontal();
        }

        private static void ToolbarHelpUI(GUIStyle buttonStyle)
        {
            if (!GUILayout.Button("Help", buttonStyle, GUILayout.ExpandWidth(false))) return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Product Page"), false, MeshToTerrainLinks.OpenHomepage);
            menu.AddItem(new GUIContent("Documentation"), false, MeshToTerrainLinks.OpenDocumentation);
            menu.AddItem(new GUIContent("Forum"), false, MeshToTerrainLinks.OpenForum);
            menu.AddItem(new GUIContent("Support"), false, MeshToTerrainLinks.OpenSupport);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Check Updates"), false, MeshToTerrainUpdater.OpenWindow);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("About"), false, MeshToTerrainAboutWindow.OpenWindow);
            menu.ShowAsContext();
        }

        private static void ToolbarThirdPartyUI(GUIStyle buttonStyle)
        {
            if (!hasThirdParty || !GUILayout.Button("Third-party", buttonStyle, GUILayout.ExpandWidth(false))) return;

            GenericMenu menu = new GenericMenu();

            if (hasRTP)
            {
#if RTP
                menu.AddItem(new GUIContent("Disable Relief Terrain Pack"), false, DeleteCompilerDirective, "RTP");
#else
                menu.AddItem(new GUIContent("Enable Relief Terrain Pack"), false, AddCompilerDirective, "RTP");
#endif
            }
            menu.ShowAsContext();
        }
    }
}