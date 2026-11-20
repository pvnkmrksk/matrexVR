using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private sealed class TopicEditor
        {
            public VisualElement Body;
            public Action Close;
        }

        private readonly List<TopicEditor> containedTopicEditors=new List<TopicEditor>();
        private bool CloseContainedTopicEditor()
        {
            if(containedTopicEditors.Count==0)return false;
            containedTopicEditors[containedTopicEditors.Count-1].Close();
            return true;
        }

        // Preparation editors replace the card's contents, never its reserved display bounds.
        private TopicEditor OpenTopicEditor(string title,string description,VisualElement owner=null)
        {
            if(!PreExperimentOpen)return new TopicEditor {Body=OpenModal(title,description),Close=CloseModal};
            var card=PreparationOwner(owner)??preExperimentRecording;
            var focused=root?.panel?.focusController.focusedElement as VisualElement;
            var previous=new Dictionary<VisualElement,StyleEnum<DisplayStyle>>();
            foreach(var child in card.Children()){previous.Add(child,child.style.display);child.style.display=DisplayStyle.None;}
            var surface=new VisualElement {name="contained-topic-editor"};
            surface.style.position=UnityEngine.UIElements.Position.Absolute;
            surface.style.left=surface.style.right=surface.style.top=surface.style.bottom=0;
            surface.style.paddingLeft=surface.style.paddingRight=4;
            surface.style.paddingTop=surface.style.paddingBottom=3;
            surface.style.backgroundColor=(Color)new Color32(27,27,27,255);
            surface.style.overflow=Overflow.Hidden;
            var result=new TopicEditor();
            result.Close=()=>{
                if(!containedTopicEditors.Remove(result))return;
                surface.RemoveFromHierarchy();
                foreach(var child in previous)child.Key.style.display=child.Value;
                if(focused!=null&&card.Contains(focused))focused.Focus();
                UpdatePreExperimentPanel();
            };
            var back=new Button(()=>result.Close()){text="‹ Back · "+title,name="contained-editor-back"};
            back.style.fontSize=11;back.style.height=back.style.minHeight=20;
            back.style.marginTop=back.style.marginBottom=0;
            back.style.paddingTop=back.style.paddingBottom=0;
            surface.Add(back);back.schedule.Execute(()=>back.Focus());
            var scroll=new ScrollView {name="contained-editor-scroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            scroll.style.flexGrow=1;scroll.style.flexShrink=1;scroll.style.minHeight=0;
            scroll.style.overflow=Overflow.Hidden;
            surface.Add(scroll);result.Body=scroll;
            containedTopicEditors.Add(result);
            card.Add(surface);
            UpdatePreExperimentPanel();
            // Controls added by the caller are styled on the next UI turn, before interaction.
            scroll.schedule.Execute(()=>FitContainedTopicControls(scroll));
            return result;
        }

        private VisualElement PreparationOwner(VisualElement element)
        {
            for(var current=element;current!=null;current=current.parent)
                if(current==preExperimentRecording||preExperimentAnimalCards.ContainsValue(current))return current;
            return null;
        }

        private static void FitContainedTopicControls(VisualElement body)
        {
            body.Query<VisualElement>().ForEach(element=>{
                element.style.minWidth=0;
                if(element.ClassListContains("unity-base-field"))
                {
                    element.style.flexDirection=element is Toggle?FlexDirection.Row:FlexDirection.Column;
                    element.style.flexShrink=0;
                    element.style.marginLeft=element.style.marginRight=1;
                }
                if(element is Toggle toggle)
                {
                    toggle.style.height=toggle.style.minHeight=toggle.style.maxHeight=22;
                    toggle.labelElement.style.flexGrow=1;
                    var input=toggle.Q<VisualElement>(className:"unity-base-field__input");
                    if(input!=null){input.style.height=input.style.minHeight=input.style.maxHeight=16;input.style.width=input.style.minWidth=input.style.maxWidth=16;input.style.paddingTop=input.style.paddingBottom=input.style.paddingLeft=input.style.paddingRight=0;}
                }
                if(element is Label)
                {
                    element.style.whiteSpace=WhiteSpace.Normal;
                    element.style.fontSize=11;
                }
                if(element.ClassListContains("unity-base-field__label"))
                {element.style.width=StyleKeyword.Auto;element.style.minWidth=0;}
                if(element is Button)
                {
                    element.style.whiteSpace=WhiteSpace.Normal;
                    element.style.fontSize=11;
                    element.style.flexShrink=0;
                }
            });
        }

        private sealed class ContainedChoiceField : BaseField<string>
        {
            public ContainedChoiceField(string label,string current,Action<ContainedChoiceField> open):base(label,new Button())
            {
                var button=this.Q<Button>();
                button.style.flexGrow=1;button.style.minWidth=0;
                button.clicked+=()=>open(this);
                value=current;button.text=current;
                this.RegisterValueChangedCallback(e=>button.text=e.newValue);
            }
        }

        private BaseField<string> TopicChoice(string label,List<string> options,string current)
        {
            if(!PreExperimentOpen)return new DropdownField(label,options,0){value=current};
            return new ContainedChoiceField(label,current,field=>{
                var editor=OpenTopicEditor(label,"",field);
                foreach(string option in options)
                {
                    string chosen=option;
                    var button=new Button(()=>{field.value=chosen;editor.Close();}){text=chosen,name="contained-choice-option"};
                    button.style.height=StyleKeyword.Auto;button.style.minHeight=20;
                    button.style.marginTop=button.style.marginBottom=0;
                    editor.Body.Add(button);
                }
            });
        }
    }
}
