using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void ShowAbout()
        {
            var dialog = OpenModal("About MATREX VR", "MATRIX · Matrix Architecture for Terraforming Realistic Environments in X");
            dialog.AddToClassList("small-dialog");
            dialog.Add(new Label("X spans land, water and air.") { name = "about-expansion" });
            var credits = new Label("Designed and conceptualized by Pavan Kumar Kaushik.\nWith August Paula and Chi-Yu Lee.") { name = "about-credits" };
            credits.style.whiteSpace = WhiteSpace.Normal;
            credits.style.marginTop = 16; credits.style.marginBottom = 16;
            dialog.Add(credits);
            dialog.Add(new Label("Version " + Application.version) { name = "about-version" });
            var actions = Row(); actions.style.marginTop = 12; dialog.Add(actions);
            actions.Add(StudioButton("Source repository", "file", () => Application.OpenURL("https://github.com/pvnkmrksk/matrexVR"), "quiet"));
            actions.Add(StudioButton("Third-party notices", "file", ShowThirdPartyNotices, "quiet"));
        }

        private void ShowThirdPartyNotices()
        {
            var dialog = OpenModal("Third-party notices", "Libraries, fonts and icons retain their own licenses and attribution.");
            var folder = Path.Combine(Application.streamingAssetsPath, "ThirdPartyNotices");
            var scroll = new ScrollView { name = "third-party-notices" }; scroll.style.maxHeight = 420; dialog.Add(scroll);
            if (!Directory.Exists(folder)) { scroll.Add(new Label("Bundled notices are unavailable in this installation.")); return; }
            foreach (var file in Directory.EnumerateFiles(folder).Where(path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path, StringComparer.Ordinal))
            {
                var entry = new Foldout { text = Path.GetFileNameWithoutExtension(file), value = false }; scroll.Add(entry);
                var text = new TextField { value = File.ReadAllText(file), multiline = true, isReadOnly = true };
                text.style.maxHeight = 240; entry.Add(text);
            }
        }
    }
}
