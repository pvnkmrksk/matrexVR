using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private bool topicSetupDirty;
        private void RefreshTopicViews()
        {
            if(PreExperimentOpen)
            {
                containedTopicEditors.Clear();
                foreach(var card in preExperimentAnimalCards.Values){OperatorOverlayLayout.Release(card.name);card.RemoveFromHierarchy();}
                preExperimentAnimalCards.Clear();
                if(preExperimentRecording!=null){OperatorOverlayLayout.Release(preExperimentRecording.name);preExperimentRecording.RemoveFromHierarchy();}
                BuildPreparationMetadata();
                PlacePreparationMetadata();
            }
            else BuildContent();
            UpdateStatus();
        }
        private void ShowTopicSetup()
        {
            EnsureMetadata();
            var definitions=SessionTopics.Definitions(metadata);
            var editor=OpenTopicEditor("Choose details to record","Checked fields appear in both setup and preparation. Hidden values are kept in the draft and omitted from new recordings.");
            var dialog=editor.Body;
            var closeSetup=editor.Close;
            editor.Close=()=>{closeSetup();if(topicSetupDirty){topicSetupDirty=false;RefreshTopicViews();}};
            dialog.name="topic-setup";
            VisualElement scroll=PreExperimentOpen?dialog:new ScrollView {horizontalScrollerVisibility=ScrollerVisibility.Hidden};if(!PreExperimentOpen){scroll.style.flexGrow=1;dialog.Add(scroll);}
            void Save(){SessionTopics.StoreDefinitions(metadata,definitions);topicSetupDirty=true;UpdateStatus();}
            var columns=Row();columns.style.alignItems=Align.FlexStart;if(PreExperimentOpen)columns.style.flexDirection=FlexDirection.Column;scroll.Add(columns);
            foreach(string scope in new[]{"Animal","Recording"})
            {
                var column=new VisualElement();if(!PreExperimentOpen){column.style.flexGrow=1;column.style.flexBasis=0;}column.style.flexShrink=0;columns.Add(column);
                column.Add(new Label(scope+" details"));
                foreach(var topic in definitions.Where(t=>t.Scope==scope))
                {
                    var toggle=new Toggle(topic.Label){value=topic.Enabled,name="select-topic-"+scope+"-"+topic.Key};
                    toggle.RegisterValueChangedCallback(e=>{topic.Enabled=e.newValue;Save();});column.Add(toggle);
                }
            }
            scroll.Add(new Label("Add a custom topic"));
            var name=new TextField("Field name"){name="custom-topic-name"};scroll.Add(name);
            var scopeField=TopicChoice("Applies to",new List<string>{"Animal","Recording"},"Animal");scroll.Add(scopeField);
            var kind=TopicChoice("Input",new List<string>{"Text","Number","Choice","Checkbox"},"Text");scroll.Add(kind);
            var unit=new TextField("Unit (optional)");scroll.Add(unit);
            var choices=new TextField("Choices (comma separated)");scroll.Add(choices);
            var error=new Label();scroll.Add(error);
            scroll.Add(StudioButton("Add topic","plus",()=>{
                string label=name.value?.Trim();
                string[] options=(choices.value??"").Split(',').Select(v=>v.Trim()).Where(v=>v.Length>0).Distinct().ToArray();
                if(string.IsNullOrWhiteSpace(label)){error.text="Enter a field name.";return;}
                if(definitions.Any(t=>t.Scope==scopeField.value&&string.Equals(t.Label,label,StringComparison.OrdinalIgnoreCase))){error.text="This field name is already in use.";return;}
                if(kind.value=="Choice"&&options.Length==0){error.text="Enter at least one choice.";return;}
                definitions.Add(new SessionTopics.Topic {Key="custom_"+Guid.NewGuid().ToString("N"),Label=label,Scope=scopeField.value,Kind=kind.value,Unit=unit.value?.Trim(),Choices=options,Enabled=true});
                Save();editor.Close();ShowTopicSetup();
            },"quiet"));
            dialog.Add(StudioButton("Done","check",()=>editor.Close(),"primary"));
        }
        private void BuildTopicFields(VisualElement parent,object target,string scope,bool compact=false)
        {
            var topics=SessionTopics.Definitions(metadata).Where(t=>t.Scope==scope&&t.Enabled).ToList();
            int count=compact&&topics.Count>5?4:topics.Count;
            foreach(var topic in topics.Take(count))BuildTopicField(parent,target,topic,compact);
            if(compact&&topics.Count>5)
            {
                var details=new Button(()=>ShowAllTopicDetails(target,scope,parent)){text="All "+topics.Count+" details…"};
                details.style.height=20;details.style.minHeight=20;details.style.marginTop=details.style.marginBottom=0;
                details.style.fontSize=11;parent.Add(details);
            }
            if(topics.Count==0)parent.Add(new Label("No details selected"));
        }
        private void ShowAllTopicDetails(object target,string scope,VisualElement owner=null)
        {
            var editor=OpenTopicEditor(target is AnimalMetadata a?a.VR+" · animal details":"Recording details","Selected fields are saved with the new recording.",owner);
            var dialog=editor.Body;
            if(PreExperimentOpen)
            {
                var closeDetails=editor.Close;
                var card=PreparationOwner(owner);
                editor.Close=()=>{closeDetails();if(card!=null && target is AnimalMetadata animal){card.Clear();AddPreparationHeading(card,animal.VR+" · Animal");BuildTopicFields(card,target,scope,true);}};
            }
            VisualElement scroll=PreExperimentOpen?dialog:new ScrollView {horizontalScrollerVisibility=ScrollerVisibility.Hidden};if(!PreExperimentOpen){scroll.style.flexGrow=1;scroll.style.minHeight=0;dialog.Add(scroll);}
            BuildTopicFields(scroll,target,scope);
            dialog.Add(StudioButton("Done","check",()=>{editor.Close();if(!PreExperimentOpen)RefreshTopicViews();},"primary"));
        }
        private void BuildTopicField(VisualElement parent,object target,SessionTopics.Topic topic,bool compact)
        {
            JToken current=SessionTopics.Get(target,topic);
            string text=current is JObject quantity?quantity["value"]?.ToString():current?.Type==JTokenType.Null?"":current?.ToString()??"";
            void Changed(JToken value){SessionTopics.Set(target,topic,value);UpdateStatus();}
            void Format(VisualElement field,Label label)
            {
                field.name="topic-"+topic.Scope+"-"+topic.Key;
                field.tooltip=topic.Label;
                if(compact)CompactPreparationField(field,label);
                parent.Add(field);
            }
            if(topic.Kind=="Condition"||topic.Kind=="Timestamp")
            {
                var row=Row();row.style.marginTop=row.style.marginBottom=0;
                if(compact){row.style.height=row.style.minHeight=row.style.maxHeight=20;row.style.flexShrink=0;}
                if(PreExperimentOpen&&!compact){row.style.flexDirection=FlexDirection.Column;row.style.alignItems=Align.Stretch;}
                var label=new Label(topic.Label);label.style.width=compact?100:145;label.style.minWidth=compact?100:145;label.style.fontSize=compact?11:12;row.Add(label);
                var button=new Button();button.name="topic-"+topic.Key;button.style.flexGrow=1;button.style.minWidth=0;
                button.style.height=button.style.minHeight=compact?18:30;button.style.fontSize=compact?11:12;
                button.style.marginTop=button.style.marginBottom=0;button.style.paddingTop=button.style.paddingBottom=0;
                Action refresh=()=>{
                    var value=SessionTopics.Get(target,topic);
                    string summary=topic.Kind=="Condition"?string.Join(", ",(value as JArray??new JArray()).Select(v=>v.ToString())):value?.ToString();
                    button.tooltip=summary;
                    if(topic.Kind=="Timestamp"&&SessionTopics.TryTimestamp(summary,out var date))
                    {button.text=date.ToLocalTime().ToString("dd MMM HH:mm");button.tooltip=date.ToString("o")+" · "+(topic.Key=="LastFedAt"?"Starved for ":"Without water for ")+Math.Max(0,(DateTimeOffset.Now-date).TotalHours).ToString("0.0")+" h";}
                    else button.text=string.IsNullOrWhiteSpace(summary)?(compact?"Unknown":"Unknown / not recorded"):(compact && value is JArray selected && selected.Count>1?selected.Count+" conditions":summary);
                };refresh();
                button.clicked+=()=>{
                    bool fromDetails=modal!=null&&modal.Contains(parent);
                    Action done=()=>{if(fromDetails)ShowAllTopicDetails(target,topic.Scope);else refresh();};
                    if(topic.Kind=="Condition")ShowConditionPicker(target,topic,done,parent);else ShowTimestampPicker(target,topic,done,parent);
                };
                row.Add(button);parent.Add(row);return;
            }
            if(topic.Kind=="Choice")
            {
                var options=new List<string>{"Unspecified"};options.AddRange(topic.Choices??Array.Empty<string>());options=options.Distinct().ToList();
                if(!string.IsNullOrEmpty(text)&&!options.Contains(text))options.Add(text);
                var field=TopicChoice(topic.Label,options,string.IsNullOrEmpty(text)?"Unspecified":text);
                field.RegisterValueChangedCallback(e=>Changed(e.newValue));Format(field,field.labelElement);return;
            }
            if(topic.Kind=="Checkbox")
            {
                var field=new Toggle(topic.Label){value=current?.Type==JTokenType.Boolean&&(bool)current};
                field.RegisterValueChangedCallback(e=>Changed(e.newValue));
                Format(field,field.labelElement);return;
            }
            var input=new TextField(topic.Label){value=text,isDelayed=topic.Kind=="Number"};
            input.textEdition.placeholder="Not recorded";
            if(!compact){input.AddToClassList("session-field");if(topic.Kind=="Text"&&topic.Key=="Comments"){input.multiline=true;input.AddToClassList("session-comments");}}
            string selectedUnit=current is JObject q?(string)q["unit"]??topic.Unit:topic.Unit;
            void CommitNumber(string value)
            {
                if(string.IsNullOrWhiteSpace(value)){Changed(JValue.CreateNull());input.RemoveFromClassList("warning");return;}
                if(!decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)){input.SetValueWithoutNotify(text);input.tooltip="Enter a number using a decimal point.";return;}
                text=value;
                Changed(string.IsNullOrEmpty(selectedUnit)?(JToken)new JValue(number):new JObject{{"value",number},{"unit",selectedUnit}});
            }
            input.RegisterValueChangedCallback(e=>{if(e.target!=input)return;if(topic.Kind=="Number")CommitNumber(e.newValue);else Changed(e.newValue);});
            Format(input,input.labelElement);
            if(topic.Kind=="Number"&&!string.IsNullOrEmpty(topic.Unit))
            {
                var units=topic.Key=="Pressure"?new List<string>{"hPa","mbar","kPa","mmHg"}:new List<string>{topic.Unit};
                if(!units.Contains(selectedUnit))units.Add(selectedUnit);
                var unitField=TopicChoice("Unit",units,selectedUnit);
                unitField.RegisterValueChangedCallback(e=>{selectedUnit=e.newValue;CommitNumber(input.value);});
                if(compact){input.label=topic.Label+" ("+selectedUnit+")";input.tooltip=input.label;}
                else parent.Add(unitField);
            }
        }
        private void ShowConditionPicker(object target,SessionTopics.Topic topic,Action refresh,VisualElement owner=null)
        {
            var picked=(SessionTopics.Get(target,topic) as JArray??new JArray()).Select(v=>v.ToString()).ToList();
            var editor=OpenTopicEditor("General condition","Select all that apply, and add a description if needed.",owner);
            var dialog=editor.Body;
            var toggles=new Dictionary<string,Toggle>();
            foreach(string option in SessionTopics.Conditions)
            {
                var toggle=new Toggle(option){value=picked.Contains(option)};toggles.Add(option,toggle);dialog.Add(toggle);
                toggle.RegisterValueChangedCallback(e=>{if(!e.newValue)return;foreach(var pair in toggles)if(pair.Key!=option&&(option==SessionTopics.Conditions[0]||pair.Key==SessionTopics.Conditions[0]))pair.Value.value=false;});
            }
            var custom=new TextField("Other / description"){value=string.Join("; ",picked.Where(v=>!SessionTopics.Conditions.Contains(v)))};dialog.Add(custom);
            dialog.Add(StudioButton("Apply","check",()=>{
                var values=toggles.Where(p=>p.Value.value).Select(p=>p.Key).ToList();
                if(!string.IsNullOrWhiteSpace(custom.value)){values.Remove(SessionTopics.Conditions[0]);values.Add(custom.value.Trim());}
                SessionTopics.Set(target,topic,JArray.FromObject(values));editor.Close();refresh();UpdateStatus();
            },"primary"));
        }
        private void ShowTimestampPicker(object target,SessionTopics.Topic topic,Action refresh,VisualElement owner=null)
        {
            string value=SessionTopics.Get(target,topic)?.ToString();
            var local=SessionTopics.TryTimestamp(value,out var existing)?existing.LocalDateTime:DateTime.Now;
            var editor=OpenTopicEditor(topic.Label,"Choose local date and time. The saved timestamp includes the UTC offset.",owner);
            var dialog=editor.Body;if(!PreExperimentOpen)dialog.AddToClassList("small-dialog");
            var dateRow=Row();if(PreExperimentOpen){dateRow.style.flexDirection=FlexDirection.Column;dateRow.style.alignItems=Align.Stretch;}dialog.Add(dateRow);
            var year=new IntegerField("Year"){value=local.Year};var months=Enumerable.Range(1,12).Select(m=>new DateTime(2000,m,1).ToString("MMM")).ToList();var month=TopicChoice("Month",months,months[local.Month-1]);var day=new IntegerField("Day"){value=local.Day};
            void DateControl(VisualElement field,Label label)
            {
                field.style.flexDirection=FlexDirection.Column;
                if(!PreExperimentOpen){field.style.flexGrow=1;field.style.flexBasis=0;}field.style.flexShrink=0;field.style.minWidth=0;
                label.style.width=StyleKeyword.Auto;label.style.minWidth=0;label.style.marginBottom=4;
            }
            DateControl(year,year.labelElement);DateControl(month,month.labelElement);DateControl(day,day.labelElement);
            foreach(var field in new VisualElement[]{year,month,day})dateRow.Add(field);
            var timeRow=Row();if(PreExperimentOpen){timeRow.style.flexDirection=FlexDirection.Column;timeRow.style.alignItems=Align.Stretch;}dialog.Add(timeRow);var hour=new IntegerField("Hour (0–23)"){value=local.Hour};var minute=new IntegerField("Minute"){value=local.Minute};DateControl(hour,hour.labelElement);DateControl(minute,minute.labelElement);timeRow.Add(hour);timeRow.Add(minute);
            if(PreExperimentOpen&&SessionTopics.TryTimestamp(value,out var previousTime))
                dialog.Add(new Label((topic.Key=="LastFedAt"?"Starved for ":"Without water for ")+Math.Max(0,(DateTimeOffset.Now-previousTime).TotalHours).ToString("0.0")+" h"));
            var error=new Label();dialog.Add(error);
            dialog.Add(StudioButton("Unknown / not recorded","back",()=>{SessionTopics.Set(target,topic,JValue.CreateNull());editor.Close();refresh();UpdateStatus();},"quiet"));
            dialog.Add(StudioButton("Apply","check",()=>{
                try {
                    var chosen=new DateTime(year.value,months.IndexOf(month.value)+1,day.value,hour.value,minute.value,0,DateTimeKind.Unspecified);
                    if(TimeZoneInfo.Local.IsInvalidTime(chosen)){error.text="This local time does not exist due to the clock change.";return;}
                    if(TimeZoneInfo.Local.IsAmbiguousTime(chosen)){error.text="This local time occurs twice due to the clock change. Choose an unambiguous time.";return;}
                    var stamp=new DateTimeOffset(chosen,TimeZoneInfo.Local.GetUtcOffset(chosen));
                    if(stamp>DateTimeOffset.Now){error.text="Choose a time in the past.";return;}
                    SessionTopics.Set(target,topic,stamp.ToString("o",CultureInfo.InvariantCulture));editor.Close();refresh();UpdateStatus();
                }catch(ArgumentOutOfRangeException){error.text="Enter a valid date and time.";}
            },"primary"));
        }
    }
}
