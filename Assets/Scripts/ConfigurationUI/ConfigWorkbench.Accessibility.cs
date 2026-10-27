using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void InstallKeyboardNavigation()
        {
            root.RegisterCallback<KeyDownEvent>(evt=>{
                if(preparing)return;
                bool command=evt.ctrlKey||evt.commandKey;
                Action action=null;
                if(evt.keyCode==KeyCode.F1)action=ShowKeyboardHelp;
                else if(command && evt.keyCode==KeyCode.S)action=()=>{RequireApplied();Workspace.SaveAll();SaveMetadata();UpdateStatus("Setup and session details saved.");};
                else if(command && evt.shiftKey && evt.keyCode==KeyCode.J)action=()=>{RequireApplied();LeaveRecordingLibrary();homeMode=experimentIndexMode=metadataMode=advancedMode=false;jsonMode=true;path.Clear();BuildContent();};
                else if(evt.altKey && evt.keyCode==KeyCode.Alpha1)action=ShowSessionHome;
                else if(evt.altKey && evt.keyCode==KeyCode.Alpha2)action=()=>Select(Workspace.Sequence);
                else if(evt.altKey && evt.keyCode==KeyCode.Alpha3)action=ShowExperimentConfigs;
                else if(evt.altKey && evt.keyCode==KeyCode.Alpha4)action=ShowRecordingLibrary;
                else if(evt.altKey && evt.keyCode==KeyCode.Alpha5)action=()=>Select(Workspace.System);
                if(action==null)return;
                // A modal owns keyboard input; navigation must not discard a draft.
                if(modal!=null && evt.keyCode!=KeyCode.F1)return;
                evt.PreventDefault();evt.StopPropagation();Guard(action);
            },TrickleDown.TrickleDown);
        }
        // MainController owns Escape at the input-system boundary so the same key
        // cannot close a dialog and then also quit the application in that frame.
        public bool TryHandleSetupEscape()
        {
            if(modal!=null){CloseModal();return true;}
            if(rawPending){UpdateStatus("JSON draft kept. Apply or discard it before leaving this editor.");return true;}
            if(!homeMode){Guard(ShowSessionHome);return true;}
            return false;
        }
        private void ShowKeyboardHelp()
        {
            var dialog=OpenModal("Keyboard shortcuts","Tab / Shift+Tab moves between controls. Enter or Space activates the focused button; arrow keys change choices.");
            dialog.Add(new Label("⌘ / Ctrl+S   Save changes\n⌘ / Ctrl+Shift+J   Edit the current JSON\nAlt+1   Start\nAlt+2   Sequence\nAlt+3   Experiment settings\nAlt+4   Recordings\nAlt+5   Apparatus\nF1   Keyboard help\nEscape   Close a dialog or return to Start"));
            dialog.Add(new Label("Number fields: ↑ / ↓ steps the value; Shift uses larger steps, Alt smaller steps. Start experiment always requires its explicit button."));
        }
        private void ShowEffectiveConfiguration()
        {
            RequireApplied();
            var effective=ConfigSchema.EffectiveValue(selected.Root,Workspace.Schemas.Get(selected.SchemaId),true)??new JObject();
            string json=effective.ToString(Formatting.Indented);
            var dialog=OpenModal("Effective JSON & defaults","Explicit settings plus declared runtime defaults. This preview leaves the source file unchanged. Invalid explicit values still block starting.");
            if(selected.SchemaId=="system")dialog.Add(new Label("Inherited rig displays stay omitted so system and command-line display routing retain their runtime meaning."));
            dialog.AddToClassList("effective-json-dialog");
            var preview=new TextField {name="effective-json-preview",multiline=true,isReadOnly=true,value=json,verticalScrollerVisibility=ScrollerVisibility.Auto};
            preview.AddToClassList("raw-json");preview.style.minHeight=260;preview.style.maxHeight=480;dialog.Add(preview);
            var actions=Row();dialog.Add(actions);
            actions.Add(StudioButton("Copy effective JSON","copy",()=>{GUIUtility.systemCopyBuffer=json;UpdateStatus("Effective JSON copied. The source file is unchanged.");},"quiet"));
            actions.Add(StudioButton("Use these explicit values","file",()=>{
                selected.ApplyJson(json);CloseModal();RebuildPreservingView(true);UpdateStatus("Effective values are now explicit in the draft. Review and save when ready.");
            },"quiet"));
        }
    }
}
