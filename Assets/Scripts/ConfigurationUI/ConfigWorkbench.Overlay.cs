using System;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void AddOverlayAdvancedSettings(VisualElement target)
        {
            var section = new Foldout { name = "overlay-advanced-settings", text = "Overlay placement & configuration file", value = false };
            section.AddToClassList("apparatus-advanced"); target.Add(section);
            var path = SessionLabel(Workspace.System.Path, "studio-subtitle"); path.name = "overlay-configuration-path";
            path.style.whiteSpace = WhiteSpace.Normal;
            path.tooltip = "This is the system JSON file storing the settings below. Opening this section does not change the file."; section.Add(path);
            section.Add(SessionLabel("Positions use physical display pixels from the bottom-left. Automatic placement keeps widgets clear of LED output and other overlays. A preferred position also relocates when it would cover stimulus pixels; if no space fits, the widget hides. Fullscreen monitor views leave no unused display area, so their operator controls may overlay the view.", "apparatus-copy"));
            foreach (string key in new[] { "statusOverlay", "frameOverlay", "desktopControls" })
            {
                var schema = ConfigSchema.Child(Workspace.Schemas.Get("system"), key);
                var group = new Foldout { name = "overlay-settings-" + key, text = key == "statusOverlay" ? "Run status" : key == "frameOverlay" ? "Frame rate" : "Desktop run controls", value = false };
                section.Add(group);
                var location = SessionLabel("JSON location: $." + key, "studio-subtitle"); location.tooltip = Workspace.System.Path + "\n" + location.text; group.Add(location);
                var values = Workspace.System.Root[key] as JObject ?? new JObject();
                bool automatic = (bool?)values["automaticPlacement"] ?? true;
                group.Add(Setting(Workspace.System, new[] { key, "visible" }, ConfigSchema.Child(schema, "visible"), values, schema));
                var controls = new VisualElement(); controls.SetEnabled((bool?)values["visible"] ?? true); group.Add(controls);
                foreach (string field in new[] { "targetDisplay", "automaticPlacement", "x", "y" })
                {
                    var control = Setting(Workspace.System, new[] { key, field }, ConfigSchema.Child(schema, field), values, schema);
                    if (field == "x" || field == "y") { control.SetEnabled(!automatic); control.tooltip = (field == "x" ? "Horizontal" : "Vertical") + " preferred position in physical pixels from the display's bottom-left. LED rectangles remain protected."; }
                    if (field == "targetDisplay") control.tooltip = "Output display index, starting at 0. Desktop controls also accept -1 to follow the first desktop preview rig.";
                    if (field == "automaticPlacement") control.tooltip = "ON chooses an available location near the usual corner. OFF uses your preferred X/Y coordinates and relocates only when needed to protect stimulus pixels.";
                    controls.Add(control);
                }
            }
            section.Add(SessionLabel("The overhead view is stored in $.overheadCamera in this same file. Its X/Y/width/height are normalized display fractions; its placement uses the same LED protection.", "apparatus-copy"));
        }
    }
}
