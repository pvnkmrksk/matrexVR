using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private readonly Dictionary<TextField,string> textDrafts=new Dictionary<TextField,string>();
        private readonly Dictionary<IntegerField,string> integerDrafts=new Dictionary<IntegerField,string>();
        private sealed class DraftBinding<T>
        {
            public T Committed;
            public Action<T> Commit;
        }
        private readonly Dictionary<TextField,DraftBinding<string>> textDraftCommits=new Dictionary<TextField,DraftBinding<string>>();
        private readonly Dictionary<IntegerField,DraftBinding<int>> integerDraftCommits=new Dictionary<IntegerField,DraftBinding<int>>();
        private readonly HashSet<VisualElement> numberDrafts=new HashSet<VisualElement>();
        private bool HasPendingEditorDrafts => textDrafts.Any(pair=>pair.Key.panel!=null&&pair.Value!=(textDraftCommits.TryGetValue(pair.Key,out var binding)?binding.Committed:pair.Key.value))||
            integerDrafts.Any(pair=>pair.Key.panel!=null&&pair.Value!=(integerDraftCommits.TryGetValue(pair.Key,out var binding)?binding.Committed:pair.Key.value).ToString(CultureInfo.InvariantCulture))||numberDrafts.Any(field=>field.panel!=null);
        private void InstallDraftTracking()
        {
            root.RegisterCallback<InputEvent>(evt=>{
                var target=evt.target as VisualElement;
                var text=target as TextField??target?.GetFirstAncestorOfType<TextField>();
                var integer=target as IntegerField??target?.GetFirstAncestorOfType<IntegerField>();
                if(text?.isDelayed==true)textDrafts[text]=evt.newData;
                if(integer?.isDelayed==true)integerDrafts[integer]=evt.newData;
                RefreshSaveAvailability();
            },TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<string>>(evt=>{if(evt.target is TextField text)textDrafts.Remove(text);RefreshSaveAvailability();});
            root.RegisterCallback<ChangeEvent<int>>(evt=>{if(evt.target is IntegerField integer)integerDrafts.Remove(integer);RefreshSaveAvailability();});
        }
        private void RefreshSaveAvailability()
        {
            foreach(var field in textDrafts.Keys.Where(field=>field.panel==null).ToArray())textDrafts.Remove(field);
            foreach(var field in integerDrafts.Keys.Where(field=>field.panel==null).ToArray())integerDrafts.Remove(field);
            numberDrafts.RemoveWhere(field=>field.panel==null);
            save?.SetEnabled(!preparing&&!rawPending&&HasUnsavedChanges);
        }
        private void BindDelayedText(TextField field,Action<string> commit)
        {
            var binding=new DraftBinding<string>{Committed=field.value,Commit=commit};textDraftCommits[field]=binding;
            field.RegisterValueChangedCallback(evt=>{
                if(evt.target!=field||binding.Committed==evt.newValue||evt.newValue!=field.value)return;
                Guard(()=>{try{binding.Commit(evt.newValue);binding.Committed=evt.newValue;}catch{field.SetValueWithoutNotify(binding.Committed);throw;}});
            });
            field.RegisterCallback<DetachFromPanelEvent>(_=>{textDraftCommits.Remove(field);textDrafts.Remove(field);});
        }
        private void BindDelayedInteger(IntegerField field,Action<int> commit)
        {
            var binding=new DraftBinding<int>{Committed=field.value,Commit=commit};integerDraftCommits[field]=binding;
            field.RegisterValueChangedCallback(evt=>{
                if(evt.target!=field||binding.Committed==evt.newValue||evt.newValue!=field.value)return;
                Guard(()=>{try{binding.Commit(evt.newValue);binding.Committed=evt.newValue;}catch{field.SetValueWithoutNotify(binding.Committed);throw;}});
            });
            field.RegisterCallback<DetachFromPanelEvent>(_=>{integerDraftCommits.Remove(field);integerDrafts.Remove(field);});
        }
        private void MarkEditorCommitted(TextField field,string value)
        {if(textDraftCommits.TryGetValue(field,out var binding))binding.Committed=value;}
        private void FlushEditorDrafts()
        {
            foreach(var pair in textDrafts.ToArray())
            {
                textDrafts.Remove(pair.Key);
                if(pair.Key.panel==null)continue;
                // Nested UI Toolkit ChangeEvents wait until the current click/
                // key event returns. Save and navigation need the JSON now.
                if(!textDraftCommits.TryGetValue(pair.Key,out var binding)){pair.Key.value=pair.Value;continue;}
                pair.Key.SetValueWithoutNotify(pair.Value);
                if(binding.Committed==pair.Value)continue;
                try{binding.Commit(pair.Value);binding.Committed=pair.Value;}catch{pair.Key.SetValueWithoutNotify(binding.Committed);throw;}
            }
            foreach(var pair in integerDrafts.ToArray())
            {
                integerDrafts.Remove(pair.Key);
                if(pair.Key.panel!=null)
                {
                    if(!int.TryParse(pair.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int number))throw new ArgumentException("Enter a valid whole number before saving or leaving this field.");
                    if(!integerDraftCommits.TryGetValue(pair.Key,out var binding)){pair.Key.value=number;continue;}
                    pair.Key.SetValueWithoutNotify(number);if(binding.Committed==number)continue;
                    try{binding.Commit(number);binding.Committed=number;}catch{pair.Key.SetValueWithoutNotify(binding.Committed);throw;}
                }
            }
        }
    }
}
