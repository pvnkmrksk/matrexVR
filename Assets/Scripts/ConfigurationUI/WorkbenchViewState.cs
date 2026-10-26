using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Preserves the operator's place when a structural edit regenerates a form.</summary>
    internal sealed class WorkbenchViewState
    {
        private readonly Dictionary<string,Vector2> scrolls=new Dictionary<string,Vector2>();
        private readonly Dictionary<string,bool> folds=new Dictionary<string,bool>();
        private readonly string focus;
        private WorkbenchViewState(VisualElement root)
        {
            foreach(var scroll in root.Query<ScrollView>().ToList()) scrolls[Key(scroll,root)]=scroll.scrollOffset;
            foreach(var fold in root.Query<Foldout>().ToList()) folds[Key(fold,root)]=fold.value;
            var focused=root.panel?.focusController.focusedElement as VisualElement;
            while(focused!=null && focused!=root)
            {
                if(focused is TextField || focused is IntegerField || focused is DropdownField || focused is Toggle || focused is Button)
                { focus=Key(focused,root);break; }
                focused=focused.parent;
            }
        }
        public static WorkbenchViewState Capture(VisualElement root)=>new WorkbenchViewState(root);
        // Names distinguish fields within each rig. Structural indices are used only
        // for unnamed layout wrappers, never the changing displayed text/value.
        private static string Key(VisualElement element,VisualElement root)
        {
            var parts=new List<string>();
            for(var item=element;item!=null && item!=root;item=item.parent)
                parts.Add(!string.IsNullOrEmpty(item.viewDataKey)?"@"+item.viewDataKey:!string.IsNullOrEmpty(item.name)?"#"+item.name:item.GetType().Name+":"+(item.parent?.IndexOf(item)??0));
            parts.Reverse();return string.Join("/",parts);
        }
        public void Restore(VisualElement root,Func<bool> current)
        {
            foreach(var fold in root.Query<Foldout>().ToList())
                if(folds.TryGetValue(Key(fold,root),out bool open)) fold.value=open;
            // Restore after the regenerated content has participated in layout.
            // Rechecking once handles nested foldout contents created during layout.
            void RestorePosition()
            {
                if(root.panel==null || !current())return;
                foreach(var scroll in root.Query<ScrollView>().ToList())
                    if(scrolls.TryGetValue(Key(scroll,root),out Vector2 position))
                        scroll.scrollOffset=new Vector2(Mathf.Clamp(position.x,0,Mathf.Max(0,scroll.horizontalScroller.highValue)),
                            Mathf.Clamp(position.y,0,Mathf.Max(0,scroll.verticalScroller.highValue)));
                var focused=root.panel.focusController.focusedElement as VisualElement;
                if(focus!=null && (focused==null || focused==root || !root.Contains(focused)))
                    root.Query<VisualElement>().ToList().FirstOrDefault(element=>Key(element,root)==focus)?.Focus();
            }
            root.schedule.Execute(RestorePosition);
            root.schedule.Execute(RestorePosition).StartingIn(16);
        }
    }
}
