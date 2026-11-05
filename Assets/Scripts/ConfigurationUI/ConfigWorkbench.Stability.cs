using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        // Value edits keep their controls alive. Structural changes (add/remove,
        // navigation, Undo) may rebuild; ordinary flags and modes only refresh state.
        private void BindViewState(VisualElement element, Action refresh)
        {
            enableUpdates.Add(refresh);
            element.RegisterCallback<DetachFromPanelEvent>(_ => enableUpdates.Remove(refresh));
            refresh();
        }

        private void RefreshBoundText(TextField field, string value)
        {
            if (textDrafts.ContainsKey(field)) return;
            field.SetValueWithoutNotify(value ?? "");
            MarkEditorCommitted(field, value ?? "");
        }

        private void RefreshNavigationLabels()
        {
            if(side==null)return;
            foreach(var button in side.Query<Button>().ToList())
                if(button.userData is ConfigDocument document && button.Q<Label>() is Label label)
                    label.text=ExperimentBlocks.Title(document.Root as JObject,document.Path)+(document.Dirty?" •":"");
            if(pageLocation!=null&&!homeMode&&!experimentIndexMode&&!replayLibraryMode&&selected!=null&&selected!=Workspace.System&&selected!=Workspace.Sequence)
                pageLocation.text=ExperimentBlocks.Title(selected.Root as JObject,selected.Path);
        }

        private static bool BooleanControl(JToken value, JObject schema) =>
            ConfigSchema.Types(schema).Contains("boolean") || value?.Type == JTokenType.Boolean;

        private static JToken BooleanValue(bool value, JToken previous, JObject schema)
        {
            // Some extension schemas deliberately use integer flags. Their
            // checkbox writes the original representation, not a different type.
            return previous?.Type == JTokenType.Integer && ConfigSchema.Types(schema).Contains("integer")
                ? new JValue(value ? 1 : 0) : new JValue(value);
        }

        private static bool ManualRecorder(JToken value) => value?.Type==JTokenType.Integer
            ? (int)value==(int)TrackerRecordingControlMode.Manual
            : string.Equals(value?.ToString(),"Manual",StringComparison.OrdinalIgnoreCase);

        private DropdownField EnumControl(JArray options, JToken shown, Action<JToken> changed)
        {
            var names = options.Where(value => value.Type == JTokenType.String).ToList();
            var numbers = options.Where(value => value.Type == JTokenType.Integer).ToList();
            bool numeric = shown?.Type == JTokenType.Integer;
            // Generated enum schemas contain names followed by their underlying
            // numbers. Show each semantic choice once; retain the saved type.
            var values = names.Count > 0 && names.Count == numbers.Count
                ? (numeric ? numbers : names) : options.Where(value => value.Type != JTokenType.Null).ToList();
            var labels = names.Count > 0 && names.Count == numbers.Count
                ? names.Select(value => Pretty(value.ToString())).ToList()
                : values.Select(value => Pretty(value.ToString())).ToList();
            int selectedIndex = values.FindIndex(value => LosslessJson.Equivalent(value, shown));
            if (selectedIndex < 0)
            {
                values.Add(shown?.DeepClone() ?? JValue.CreateNull());
                labels.Add("Current: " + (shown?.ToString() ?? "unset"));
                selectedIndex = values.Count - 1;
            }
            var field = new DropdownField(labels, selectedIndex);
            field.RegisterValueChangedCallback(evt => Guard(() => { RequireApplied(); changed(values[labels.IndexOf(evt.newValue)].DeepClone()); }));
            return field;
        }
    }
}
