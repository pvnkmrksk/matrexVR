using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void BuildStimulusResolutionControls(VisualElement setting, ConfigDocument doc, string[] path, JObject schema)
        {
            var modes = StimulusResolutionConfig.Modes.ToList();
            var labels = modes.Select(mode => mode == StimulusResolutionConfig.NativeMode ? "Native monitor" :
                mode == StimulusResolutionConfig.CustomMode ? "Custom" : mode.Replace('x', '×')).ToList();
            var controls = new VisualElement();
            setting.Add(controls);
            var modePicker = new DropdownField(labels, modes.IndexOf(StimulusResolutionConfig.DefaultMode)) {
                name = "stimulus-resolution", tooltip = StimulusResolutionConfig.Help
            };
            controls.Add(modePicker);
            var custom = Row("vector-controls");
            controls.Add(custom);

            JObject Current() => ConfigDocument.At(doc.Root, path) as JObject;
            void Save(string field, JToken value)
            {
                StudioEdit(doc, root => {
                    if (!(ConfigDocument.At(root, path) is JObject)) AssignAt(root, path, new JObject());
                    ConfigDocument.At(root, path)[field] = value;
                }, false);
            }

            void AddDimension(string key, string label, int defaultValue)
            {
                var axis = Row("vector-axis");
                axis.Add(new Label(label));
                var fieldSchema = ConfigSchema.Child(schema, key);
                var initial = ConfigSchema.EffectiveValue(Current()?[key], fieldSchema) ?? new JValue(defaultValue);
                var entry = NumberControl(initial, fieldSchema, "stimulus-" + key, value => Save(key, value), false);
                var input = entry.Q<TextField>();
                input.tooltip = $"Custom fullscreen {key} in pixels, from 1 to {StimulusResolutionConfig.MaximumDimension}.";
                axis.Add(entry);
                custom.Add(axis);
                BindViewState(input, () => RefreshBoundText(input,
                    (ConfigSchema.EffectiveValue(Current()?[key], fieldSchema) ?? new JValue(defaultValue)).ToString()));
            }

            var defaults = new StimulusResolutionConfig();
            AddDimension("width", "Width (px)", defaults.width);
            AddDimension("height", "Height (px)", defaults.height);
            modePicker.RegisterValueChangedCallback(change => Guard(() => {
                int index = labels.IndexOf(change.newValue);
                if (index >= 0) Save("mode", new JValue(modes[index]));
            }));
            BindViewState(controls, () => {
                string mode = Current()?["mode"]?.ToString() ?? StimulusResolutionConfig.DefaultMode;
                int index = modes.IndexOf(mode);
                modePicker.SetValueWithoutNotify(labels[index >= 0 ? index : modes.IndexOf(StimulusResolutionConfig.DefaultMode)]);
                custom.style.display = mode == StimulusResolutionConfig.CustomMode ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }
    }
}
