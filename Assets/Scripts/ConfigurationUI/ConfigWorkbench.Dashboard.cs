using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void UpdateFrameRate()
        {
            var timing=fps.Latest;
            if(frameRate==null) return;
            frameRate.text=timing==null?"— FPS":$"{timing.framesPerSecond:0} FPS  ·  {timing.frameMeanMs:0.0} ms";
            frameRate.tooltip=timing==null?"Measured frame rate and average frame time. Waiting for the first timing sample.":
                $"Measured frame rate: {timing.framesPerSecond:0.0} FPS\nFrame mean / peak: {timing.frameMeanMs:0.0} / {timing.framePeakMs:0.0} ms\n"+
                $"Current target: {timing.targetFrameRate} · VSync: {timing.vSyncCount}\nThese are live setup measurements. Frame timing and telemetry use the same collector.";
        }
        // VisualElement.tooltip is Editor-only. This setup-owned help layer works in
        // both Play mode and standalone builds, and never receives pointer input.
        private void InstallHoverHelp()
        {
            hoverHelp=new VisualElement {name="runtime-hover-help",pickingMode=PickingMode.Ignore}; hoverHelp.AddToClassList("hover-help");
            hoverHelpText=new Label {pickingMode=PickingMode.Ignore}; hoverHelp.Add(hoverHelpText); root.Add(hoverHelp); HideHoverHelp();
            root.RegisterCallback<PointerMoveEvent>(evt=>{
                var target=evt.target as VisualElement;
                while(target!=null && string.IsNullOrEmpty(target.tooltip)) target=target.parent;
                if(target==helpTarget) return;
                HideHoverHelp(); helpTarget=target;
                if(target==null) return;
                var point=root.WorldToLocal(evt.position);
                helpDelay=root.schedule.Execute(()=>ShowHoverHelp(target,point)).StartingIn(500);
            });
            root.RegisterCallback<PointerDownEvent>(_=>HideHoverHelp(),TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(_=>HideHoverHelp(),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(evt=>{ if(evt.target==root) HideHoverHelp(); });
        }
        private void ShowHoverHelp(VisualElement target,Vector2 point)
        {
            if(target.panel==null) return;
            string text=target.tooltip;
            for(var parent=target.parent;parent!=null;parent=parent.parent)
                if(parent.ClassListContains("studio-setting")) { if(parent.tooltip!=text) text=parent.tooltip+"\n\n"+text; break; }
            hoverHelpText.text=text; hoverHelp.style.display=DisplayStyle.Flex; hoverHelp.BringToFront();
            float width=Mathf.Min(360,root.resolvedStyle.width-32);
            hoverHelp.style.width=width;
            hoverHelp.style.left=Mathf.Clamp(point.x+14,16,root.resolvedStyle.width-width-16);
            hoverHelp.style.top=Mathf.Clamp(point.y+22,76,root.resolvedStyle.height-260);
        }
        private void HideHoverHelp()
        { helpDelay?.Pause(); helpTarget=null; if(hoverHelp!=null) hoverHelp.style.display=DisplayStyle.None; }
        // Materialize only the path the operator edits. Viewing inherited defaults
        // never writes those values into the original lossless document.
        private static void AssignAt(JToken root,string[] parts,JToken value)
        {
            JToken parent=root;
            for(int i=0;i<parts.Length-1;i++)
            {
                if(parent is JArray array) parent=array[int.Parse(parts[i])];
                else
                {
                    var obj=(JObject)parent;
                    if(!(obj[parts[i]] is JContainer)) obj[parts[i]]=new JObject();
                    parent=obj[parts[i]];
                }
            }
            if(parent is JObject owner) owner[parts[parts.Length-1]]=value;
            else ((JArray)parent)[int.Parse(parts[parts.Length-1])]=value;
        }
    }
}
