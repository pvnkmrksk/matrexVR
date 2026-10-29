using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private bool TryVisibilityControl(ConfigDocument document, string[] path, out VisualElement control)
        {
            control = null;
            if (path.Last() != ExperimentVisibility.Field) return false;
            var panel = new VisualElement { name = "visibility-matrix-editor" };
            control = panel;
            var defaults = ExperimentVisibility.Default();
            JObject Value() => ConfigDocument.At(document.Root, path) as JObject ?? defaults;
            bool Enabled() => Value()["enabled"]?.Type == JTokenType.Boolean && (bool)Value()["enabled"];
            JArray Rows() => Value()["rows"] as JArray;
            void Edit(Action<JObject> change) => StudioEdit(document, root => {
                if (!(ConfigDocument.At(root, path) is JObject)) AssignAt(root, path, defaults.DeepClone());
                change((JObject)ConfigDocument.At(root, path));
            }, false);
            var enabled = StateSwitch(Enabled(), value => Edit(matrix => matrix["enabled"] = value), "visibility-matrix-enabled");
            enabled.text = "Use visibility matrix";
            panel.Add(enabled);
            BindViewState(enabled, () => enabled.SetValueWithoutNotify(Enabled()));
            var description = new Label("Rows: observer. Columns: visible VR population. Ground, sky and private Choice objects keep their normal visibility.");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.maxWidth = 460;
            panel.Add(description);
            var active = (Workspace.System.Root["configs"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(rig => RuntimeRigRegistry.ParseIndex((string)rig["vrId"])).Where(index => index > 0).ToHashSet();
            int required = active.DefaultIfEmpty(1).Max();
            var actions = Row(); panel.Add(actions);
            actions.Add(StudioButton("All peers", "reset", () => Edit(matrix => {
                int dimension = Math.Min(RuntimeRigRegistry.MaximumRigCount, Math.Max(required, (matrix["rows"] as JArray)?.Count ?? RuntimeRigRegistry.MaximumRigCount));
                matrix["rows"] = ExperimentVisibility.Default(dimension)["rows"];
            }), "quiet"));
            actions.Add(StudioButton("None", "stop", () => Edit(matrix => {
                int dimension = Math.Min(RuntimeRigRegistry.MaximumRigCount, Math.Max(required, (matrix["rows"] as JArray)?.Count ?? RuntimeRigRegistry.MaximumRigCount));
                matrix["rows"] = new JArray(Enumerable.Range(0, dimension)
                    .Select(_ => new JArray(Enumerable.Repeat(false, dimension))));
            }), "quiet"));
            actions.Add(StudioButton("Resize to configured VRs", "system", () => Edit(matrix => {
                var previous = matrix["rows"] as JArray;
                var next = (JArray)ExperimentVisibility.Default(required)["rows"];
                for (int row = 0; row < Math.Min(required, previous?.Count ?? 0); row++)
                    if (previous[row] is JArray columns)
                        for (int column = 0; column < Math.Min(required, columns.Count); column++)
                            next[row][column] = columns[column].DeepClone();
                matrix["rows"] = next;
            }), "quiet"));
            BindViewState(actions, () => actions.SetEnabled(Enabled()));
            var table = new VisualElement { name = "visibility-matrix-grid" }; panel.Add(table);
            // Always reserve the same 16×16 geometry. Unconfigured entries stay
            // gray, so enabling the feature or a cell cannot move the form.
            var headings = Row(); table.Add(headings);
            Label CellLabel(string text, float width)
            {
                var label = new Label(text);
                label.style.width = width; label.style.minWidth = width; label.style.maxWidth = width;
                label.style.marginLeft = 0; label.style.marginRight = 0;
                label.style.fontSize = 10; label.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                return label;
            }
            headings.Add(CellLabel("Sees →", 46));
            for (int column = 1; column <= RuntimeRigRegistry.MaximumRigCount; column++) headings.Add(CellLabel(column.ToString(), 24));
            for (int row = 0; row < RuntimeRigRegistry.MaximumRigCount; row++)
            {
                var line = Row(); table.Add(line); line.Add(CellLabel("VR" + (row + 1), 46));
                for (int column = 0; column < RuntimeRigRegistry.MaximumRigCount; column++)
                {
                    int observer = row, target = column;
                    var cell = StateSwitch(false, value => Edit(matrix => ((JArray)matrix["rows"])[observer][target] = value),
                        "visibility-vr" + (row + 1) + "-vr" + (column + 1));
                    cell.tooltip = "Observer VR" + (row + 1) + " sees the VR" + (column + 1) + " population layer.";
                    cell.style.width = 24; cell.style.minWidth = 24; cell.style.maxWidth = 24;
                    cell.style.height = 24; cell.style.marginLeft = 0; cell.style.marginRight = 0;
                    line.Add(cell);
                    BindViewState(cell, () => {
                        var rows = Rows(); var values = observer < (rows?.Count ?? 0) ? rows[observer] as JArray : null;
                        var token = target < (values?.Count ?? 0) ? values[target] : null;
                        cell.SetValueWithoutNotify(token?.Type == JTokenType.Boolean && (bool)token);
                        cell.SetEnabled(Enabled() && token != null && active.Contains(observer + 1) && active.Contains(target + 1));
                    });
                }
            }
            BindViewState(table, () => table.SetEnabled(Enabled()));
            var note = new Label(); note.style.whiteSpace = WhiteSpace.Normal; note.style.maxWidth = 460; panel.Add(note);
            BindViewState(note, () => note.text = (Rows()?.Count ?? 0) < required
                ? "Resize the matrix to include VR" + required + " before starting."
                : "Gray cells belong to inactive VRs. Disabled preserves the normal scene visibility.");
            return true;
        }
    }
}
