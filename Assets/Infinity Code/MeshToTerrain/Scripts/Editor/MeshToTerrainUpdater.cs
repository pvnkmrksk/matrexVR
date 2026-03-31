/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace InfinityCode.MeshToTerrain
{
    public class MeshToTerrainUpdater : EditorWindow
    {
        private MeshToTerrainUpdateChannel channel = MeshToTerrainUpdateChannel.stable;
        private string invoiceNumber;
        private Vector2 scrollPosition;
        private List<MeshToTerrainUpdateItem> updates;

        private void CheckNewVersions()
        {
            if (string.IsNullOrEmpty(invoiceNumber))
            {
                EditorUtility.DisplayDialog("Error", "Please enter the Invoice Number.", "OK");
                return;
            }

            SavePrefs();

            string updateKey = GetUpdateKey();
            GetUpdateList(updateKey);
        }

        private string GetUpdateKey()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            string updateKey = client.UploadString("http://infinity-code.com/products_update/getupdatekey.php",
                "key=" + invoiceNumber + "&package=" + EscapeURL("Mesh to Terrain"));

            return updateKey;
        }

        private static string EscapeURL(string url)
        {
            return UnityWebRequest.EscapeURL(url);
        }

        private void GetUpdateList(string updateKey)
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";

            string response;

            try
            {
                response = client.UploadString("http://infinity-code.com/products_update/checkupdates.php",
                    "k=" + EscapeURL(updateKey) + "&v=" + MeshToTerrain.version + "&c=" + (int) channel);
            }
            catch
            {
                return;
            }

            XmlDocument document = new XmlDocument();
            document.LoadXml(response);

            XmlNode firstChild = document.FirstChild;
            updates = new List<MeshToTerrainUpdateItem>();

            foreach (XmlNode node in firstChild.ChildNodes) updates.Add(new MeshToTerrainUpdateItem(node));
        }

        private void OnEnable()
        {
            if (EditorPrefs.HasKey("MeshToTerrainInvoiceNumber"))
                invoiceNumber = EditorPrefs.GetString("MeshToTerrainInvoiceNumber");
            else invoiceNumber = "";

            if (EditorPrefs.HasKey("MeshToTerrainUpdateChannel"))
                channel = (MeshToTerrainUpdateChannel) EditorPrefs.GetInt("MeshToTerrainUpdateChannel");
            else channel = MeshToTerrainUpdateChannel.stable;
        }

        private void OnDestroy()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            invoiceNumber = EditorGUILayout.TextField("Invoice Number:", invoiceNumber).Trim(' ');
            channel = (MeshToTerrainUpdateChannel) EditorGUILayout.EnumPopup("Channel:", channel);

            if (GUILayout.Button("Check new versions")) CheckNewVersions();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (updates != null)
            {
                foreach (MeshToTerrainUpdateItem update in updates) update.Draw();
                if (updates.Count == 0) GUILayout.Label("No updates");
            }

            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Window/Infinity Code/Mesh to Terrain/Check Updates", false, 2)]
        public static void OpenWindow()
        {
            GetWindow<MeshToTerrainUpdater>(false, "Mesh to Terrain Updater", true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString("MeshToTerrainInvoiceNumber", invoiceNumber);
            EditorPrefs.SetInt("MeshToTerrainUpdateChannel", (int) channel);
        }
    }

    public class MeshToTerrainUpdateItem
    {
        private string version;
        private int type;
        private string changelog;
        private string download;
        private string date;

        private static GUIStyle _changelogStyle;
        private static GUIStyle _titleStyle;

        private static GUIStyle changelogStyle
        {
            get
            {
                if (_changelogStyle != null) return _changelogStyle;
                
                _changelogStyle = new GUIStyle(EditorStyles.label) {wordWrap = true};
                return _changelogStyle;
            }
        }

        private static GUIStyle titleStyle
        {
            get
            {
                if (_titleStyle != null) return _titleStyle;
                
                _titleStyle = new GUIStyle(EditorStyles.boldLabel) {alignment = TextAnchor.MiddleCenter};
                return _titleStyle;
            }
        }

        public string typeStr
        {
            get { return Enum.GetName(typeof(MeshToTerrainUpdateChannel), type); }
        }

        public MeshToTerrainUpdateItem(XmlNode node)
        {
            version = node.SelectSingleNode("Version").InnerText;
            type = int.Parse(node.SelectSingleNode("Type").InnerText);
            changelog = Unescape(node.SelectSingleNode("ChangeLog").InnerText);
            download = node.SelectSingleNode("Download").InnerText;
            date = node.SelectSingleNode("Date").InnerText;

            string[] vars = version.Split('.');
            string[] vars2 = new string[4];
            vars2[0] = vars[0];
            vars2[1] = int.Parse(vars[1].Substring(0, 2)).ToString();
            vars2[2] = int.Parse(vars[1].Substring(2, 2)).ToString();
            vars2[3] = int.Parse(vars[1].Substring(4, 4)).ToString();
            version = string.Join(".", vars2);
        }

        public void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Version: " + version + " (" + typeStr + "). " + date, titleStyle);

            GUILayout.Label(changelog, changelogStyle);

            if (GUILayout.Button("Download"))
            {
                Application.OpenURL("https://infinity-code.com/products_update/download.php?k=" + download);
            }

            GUILayout.EndVertical();
        }

        private static string Unescape(string str)
        {
            return Regex.Replace(str, @"&(\w+);", delegate (Match match)
            {
                string v = match.Groups[1].Value;
                if (v == "amp") return "&";
                if (v == "lt") return "<";
                if (v == "gt") return ">";
                if (v == "quot") return "\"";
                if (v == "apos") return "'";
                return match.Value;
            });
        }
    }

    public enum MeshToTerrainUpdateChannel
    {
        stable = 10,
        releaseCandidate = 20,
        beta = 30,
        alpha = 40,
        working = 50
    }
}