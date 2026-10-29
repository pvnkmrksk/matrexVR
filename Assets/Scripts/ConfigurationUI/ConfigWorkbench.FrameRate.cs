using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        public static void RequestFrameRateSetting()
        {
            if(Instance!=null)Instance.Guard(Instance.OpenFrameRateSetting);
            else RuntimeFrameRateEditor.Open();
        }
        public void OpenFrameRateSetting()
        {
            RequireApplied();CloseModal();Select(Workspace.System);
            var options=root.Q<Foldout>("apparatus-operator-options");if(options!=null)options.value=true;
            var setting=root.Q<VisualElement>("setting-targetFrameRate");
            setting?.schedule.Execute(()=>{
                setting.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(setting);
                setting.Q<TextField>()?.Focus();
            });
        }
    }
}
