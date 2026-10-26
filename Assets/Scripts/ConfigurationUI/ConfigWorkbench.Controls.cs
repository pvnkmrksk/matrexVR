using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private void ApplyResponsiveLayout(float width)
        {
            root.EnableInClassList("compact-layout",width<1220);root.EnableInClassList("narrow-layout",width<1020);
            if(inspector!=null) inspector.style.display=homeMode || experimentIndexMode || replayLibraryMode || width<1220?DisplayStyle.None:DisplayStyle.Flex;
        }
        private void AddTimingWarnings(VisualElement target,ConfigDocument doc)
        {
            var slot=new VisualElement {name="timing-warnings"};target.Add(slot);PopulateTimingWarnings(slot,doc);
        }
        private void PopulateTimingWarnings(VisualElement target,ConfigDocument doc)
        {
            target.Clear();
            foreach(var step in BuilderSteps.OfType<JObject>())
            {
                var parameters=step["parameters"] as JObject; string reference=(string)(parameters?["configFile"]??parameters?["design"]);
                if(reference==null || System.IO.Path.GetFullPath(ExperimentConfigFiles.Resolve(reference))!=doc.Path) continue;
                foreach(string message in ExperimentBlocks.TimingWarnings((JObject)doc.Root,ExperimentBlocks.Number(step["duration"])))
                    target.Add(WarningStrip(message));
            }
        }
        private VisualElement WarningStrip(string message)
        {
            var row=Row("timing-warning");row.name="heading-duration-warning";
            row.Add(new WorkbenchIcon("settings"));row.Add(new Label(message));return row;
        }
        private void ShowParameterDialog(ConfigDocument doc,string[] parts)
        {
            var dialog=OpenModal("Add a parameter","New keys and extensions stay in the same JSON file. Nothing is assigned until you choose Add.");
            dialog.AddToClassList("small-dialog");
            var key=new TextField("Parameter name"){name="new-parameter-key"};dialog.Add(key);
            var value=new TextField("JSON value"){name="new-parameter-value",value="null",multiline=true};dialog.Add(value);
            dialog.Add(Button("Add parameter",()=>{
                string name=key.value.Trim(); if(name.Length==0) throw new ArgumentException("Enter a parameter name.");
                var owner=ConfigDocument.At(doc.Root,parts) as JObject;
                if(owner==null || owner.Property(name)!=null) throw new ArgumentException("Choose a new key in an object.");
                JToken parsed=LosslessJson.Parse(value.value);
                StudioEdit(doc,r=>((JObject)ConfigDocument.At(r,parts))[name]=parsed);CloseModal();
            },"primary"));
        }
        private VisualElement ColorControl(ConfigDocument doc,string[] parts,JObject source)
        {
            var current=new Color((float)ExperimentBlocks.Number(source["r"]),(float)ExperimentBlocks.Number(source["g"]),
                (float)ExperimentBlocks.Number(source["b"]),(float)ExperimentBlocks.Number(source["a"],1));
            var row=Row("color-control");
            Action<Color> commit=color=>StudioEdit(doc,r=>{
                if(ConfigDocument.At(r,parts)==null) AssignAt(r,parts,source.DeepClone());
                var obj=(JObject)ConfigDocument.At(r,parts);
                obj["r"]=new JRaw(color.r.ToString("R",CultureInfo.InvariantCulture));
                obj["g"]=new JRaw(color.g.ToString("R",CultureInfo.InvariantCulture));
                obj["b"]=new JRaw(color.b.ToString("R",CultureInfo.InvariantCulture));
                if(obj["a"]!=null) obj["a"]=new JRaw(color.a.ToString("R",CultureInfo.InvariantCulture));
            },false);
#if UNITY_EDITOR
            var picker=new UnityEditor.UIElements.ColorField {name="color-"+string.Join("-",parts),showAlpha=source["a"]!=null,hdr=false,showEyeDropper=true};
            picker.SetValueWithoutNotify(current);picker.RegisterValueChangedCallback(e=>Guard(()=>commit(e.newValue)));row.Add(picker);
#else
            var picker=new Button(()=>ShowRuntimeColorDialog(current,source["a"]!=null,commit)){text="Choose color",name="color-"+string.Join("-",parts)};
            picker.style.backgroundColor=current;row.Add(picker);
#endif
            var hex=new Label("#"+ColorUtility.ToHtmlStringRGB(current));hex.AddToClassList("color-hex");row.Add(hex);
#if UNITY_EDITOR
            picker.RegisterValueChangedCallback(e=>hex.text="#"+ColorUtility.ToHtmlStringRGB(e.newValue));
#endif
            return row;
        }
        private void ShowRuntimeColorDialog(Color initial,bool alpha,Action<Color> commit)
        {
            var dialog=OpenModal("Color","Normalized RGB · enter hex or adjust channels.");dialog.AddToClassList("small-dialog");
            Color draft=initial;
            var preview=new VisualElement();preview.AddToClassList("color-preview");preview.style.backgroundColor=draft;dialog.Add(preview);
            var hex=new TextField("Hex"){value="#"+ColorUtility.ToHtmlStringRGBA(draft)};dialog.Add(hex);
            var fields=new Slider[4];string[] channels={"Red","Green","Blue","Alpha"};
            Action refresh=()=>{preview.style.backgroundColor=draft;hex.SetValueWithoutNotify("#"+ColorUtility.ToHtmlStringRGBA(draft));for(int i=0;i<4;i++) fields[i]?.SetValueWithoutNotify(draft[i]);};
            for(int i=0;i<(alpha?4:3);i++) {int channel=i;fields[i]=new Slider(channels[i],0,1){value=draft[i],showInputField=true};fields[i].RegisterValueChangedCallback(e=>{draft[channel]=e.newValue;refresh();});dialog.Add(fields[i]);}
            hex.RegisterValueChangedCallback(e=>Guard(()=>{if(!ColorUtility.TryParseHtmlString(e.newValue,out Color chosen)) throw new ArgumentException("Enter #RRGGBB or #RRGGBBAA.");draft=chosen;refresh();}));
            dialog.Add(Button("Apply color",()=>{commit(draft);CloseModal();BuildContent();},"primary"));
        }
    }
}
