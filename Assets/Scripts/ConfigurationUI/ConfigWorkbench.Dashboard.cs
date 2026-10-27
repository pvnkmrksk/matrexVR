using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private Label hoverHelpTitle,hoverHelpFacts;
        private Vector2 hoverHelpPoint;
        private void UpdateFrameRate()
        {
            var timing=fps.Latest;
            if(frameRate==null) return;
            string pacing=timing==null?"":timing.vSyncCount>0?"VSync /"+timing.vSyncCount:timing.targetFrameRate>0?"cap "+timing.targetFrameRate:"uncapped";
            frameRate.text=timing==null?"— FPS":$"{timing.framesPerSecond:0} FPS  ·  {pacing}";
            frameRate.tooltip=timing==null?"Measured frame rate and average frame time. Waiting for the first timing sample.":
                $"Measured frame rate: {timing.framesPerSecond:0.0} FPS\nFrame mean / peak: {timing.frameMeanMs:0.0} / {timing.framePeakMs:0.0} ms\n"+
                $"Current target: {timing.targetFrameRate} · VSync: {timing.vSyncCount}\n"+
                (timing.processCpuMsPerFrame.HasValue?$"Process CPU work: {timing.processCpuMsPerFrame:0.00} ms/frame across all Unity threads ({timing.processCpuPercentOfOneCore:0}% of one CPU core).\n":"")+
                "Present wait is one presentation timing field, not all idle time. These are live setup measurements; capped FPS does not measure uncapped throughput.";
        }
        // VisualElement.tooltip is Editor-only. This setup-owned help layer works in
        // both Play mode and standalone builds, and never receives pointer input.
        private void InstallHoverHelp()
        {
            hoverHelp=new VisualElement {name="runtime-hover-help",pickingMode=PickingMode.Ignore}; hoverHelp.AddToClassList("hover-help");
            hoverHelpTitle=new Label {name="runtime-hover-help-title",pickingMode=PickingMode.Ignore};hoverHelpTitle.AddToClassList("hover-help-title");hoverHelp.Add(hoverHelpTitle);
            hoverHelpText=new Label {name="runtime-hover-help-body",pickingMode=PickingMode.Ignore};hoverHelpText.AddToClassList("hover-help-body");hoverHelp.Add(hoverHelpText);
            hoverHelpFacts=new Label {name="runtime-hover-help-facts",pickingMode=PickingMode.Ignore};hoverHelpFacts.AddToClassList("hover-help-facts");hoverHelp.Add(hoverHelpFacts);
            hoverHelp.RegisterCallback<GeometryChangedEvent>(_=>PositionHoverHelp());
            root.Add(hoverHelp); HideHoverHelp();
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
            root.RegisterCallback<KeyDownEvent>(_=>HideHoverHelp(),TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(evt=>{
                var target=evt.target as VisualElement;
                while(target!=null&&string.IsNullOrEmpty(target.tooltip))target=target.parent;
                if(target==null)return;
                HideHoverHelp();helpTarget=target;
                var point=root.WorldToLocal(new Vector2(target.worldBound.xMin,target.worldBound.yMax));
                helpDelay=root.schedule.Execute(()=>ShowHoverHelp(target,point)).StartingIn(650);
            });
            root.RegisterCallback<FocusOutEvent>(_=>HideHoverHelp());
            root.RegisterCallback<PointerLeaveEvent>(evt=>{ if(evt.target==root) HideHoverHelp(); });
        }
        private void ShowHoverHelp(VisualElement target,Vector2 point)
        {
            if(target.panel==null||string.IsNullOrWhiteSpace(target.tooltip)) return;
            VisualElement owner=target;
            while(owner!=null&&!owner.ClassListContains("studio-setting"))owner=owner.parent;
            string text=owner?.tooltip??target.tooltip;
            string title=owner?.Q<Label>(className:"setting-title")?.text??target.Q<Label>()?.text??"About this control";
            int facts=text.IndexOf("\n\nJSON:",StringComparison.Ordinal);
            hoverHelpTitle.text=title;
            hoverHelpText.text=facts<0?text:text.Substring(0,facts).Trim();
            hoverHelpFacts.text=facts<0?"":text.Substring(facts+2).Trim();
            hoverHelpFacts.style.display=facts<0?DisplayStyle.None:DisplayStyle.Flex;
            hoverHelpPoint=point;
            hoverHelp.style.display=DisplayStyle.Flex; hoverHelp.BringToFront();
            float width=Mathf.Min(420,root.resolvedStyle.width-32);
            hoverHelp.style.width=width;
            PositionHoverHelp();
        }
        private void PositionHoverHelp()
        {
            if(hoverHelp==null||hoverHelp.resolvedStyle.display==DisplayStyle.None)return;
            float width=hoverHelp.resolvedStyle.width,height=hoverHelp.resolvedStyle.height;
            if(float.IsNaN(width)||width<=0)width=Mathf.Min(420,root.resolvedStyle.width-32);
            if(float.IsNaN(height)||height<=0)height=220;
            float x=hoverHelpPoint.x+16,y=hoverHelpPoint.y+20;
            if(x+width>root.resolvedStyle.width-16)x=hoverHelpPoint.x-width-16;
            if(y+height>root.resolvedStyle.height-16)y=hoverHelpPoint.y-height-16;
            hoverHelp.style.left=Mathf.Clamp(x,16,Mathf.Max(16,root.resolvedStyle.width-width-16));
            hoverHelp.style.top=Mathf.Clamp(y,16,Mathf.Max(16,root.resolvedStyle.height-height-16));
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
