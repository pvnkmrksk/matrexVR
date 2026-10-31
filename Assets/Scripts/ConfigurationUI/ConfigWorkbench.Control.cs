using System;
using System.Linq;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Automation uses the same explicit Start action and preserves pending drafts.</summary>
    public sealed partial class ConfigWorkbench
    {
        private bool ControlTextEditing => root?.panel?.focusController?.focusedElement is VisualElement focused &&
            (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        public bool ControlHasUnsavedDraft => rawPending || HasUnsavedChanges || ControlTextEditing;
        public bool ControlHasModal => modal != null;
        public bool ControlHasPendingInput => rawPending||HasPendingEditorDrafts||ControlTextEditing;
        public void RefreshControlFiles()
        {
            bool changed=false;
            foreach(var doc in Workspace.Documents.ToArray())changed|=doc.CheckExternalChange();
            Workspace.RefreshReferences();if(changed)RebuildPreservingView(true);
        }
        public string ControlPreparationStatus => preparationStatus;
        public void RequireControlEditReady()
        {
            if(modal!=null||IsPreparing||rawPending||HasPendingEditorDrafts||ControlTextEditing)
                throw new InvalidOperationException("Finish the current dialog or text/JSON input before changing setup through control.");
        }
        public void SelectFilesFromControl(string system,string sequence)
        {
            RequireControlEditReady();
            if(ControlHasUnsavedDraft)throw new InvalidOperationException("Save/discard the existing GUI draft before selecting different files.");
            Workspace.SelectPrimary("system",system);Workspace.SelectPrimary("sequence",sequence);Select(Workspace.Sequence);
        }
        public void InitializeTrackingFromControl(){RequireControlEditReady();OpenQuickTracker();}
        public static string ControlRevision(ConfigDocument document)
        {return RunConfigArchive.Hash(Encoding.UTF8.GetBytes(document.Root.ToString(Formatting.None)+"\n"+document.LoadError+(document.LoadError!=null?document.SourceText:"")));}
        public ConfigDocument ControlDocument(string file)
        {
            RefreshControlFiles();
            var document=Workspace.Documents.FirstOrDefault(doc=>doc.Path==Path.GetFullPath(file));
            if(document==null)throw new ArgumentException("Select a setup which references this configuration first.");
            return document;
        }
        public JObject DescribeControlDocument(ConfigDocument doc)
        {
            return new JObject{["path"]=doc.Path,["schemaId"]=doc.SchemaId,["revision"]=ControlRevision(doc),
                ["jsonText"]=doc.Root.ToString(Formatting.Indented),["effective"]=ConfigSchema.EffectiveValue(doc.Root,Workspace.Schemas.Get(doc.SchemaId),true),
                ["dirty"]=doc.Dirty,["conflict"]=doc.Conflict,["loadError"]=doc.LoadError,["damagedSourceText"]=doc.LoadError!=null?doc.SourceText:null,
                ["canUndo"]=doc.CanUndo,["canRedo"]=doc.CanRedo};
        }
        public JObject EditDocumentFromControl(string file,string revision,string action,string json=null,bool allowInvalidDraft=false)
        {
            RequireControlEditReady();var doc=ControlDocument(file);
            if(doc.Conflict)throw new IOException("External configuration conflict; preserve the GUI draft and resolve it explicitly.");
            if(ControlRevision(doc)!=revision)throw new InvalidOperationException("Configuration revision changed; read the current GUI draft before retrying.");
            if(action=="replace")
            {
                var errors=doc.PreviewJson(json,()=>Workspace.Validate());Workspace.RefreshReferences();
                if(errors.Count>0&&!allowInvalidDraft)throw new ArgumentException("Configuration rejected: "+string.Join("; ",errors));
                doc.ApplyJson(json);
            }
            else if(action=="save")doc.Save();
            else if(action=="undo")doc.Undo();
            else if(action=="redo")doc.Redo();
            else if(action=="revert")doc.RevertToSaved();
            else throw new ArgumentException("Unknown configuration action.");
            Workspace.RefreshReferences();var validation=Workspace.Validate();RebuildPreservingView(true);
            // An explicitly invalid repair draft must remain inspectable, without
            // applying an invalid target to the live timing controller after the edit.
            if(doc==Workspace.System&&validation.Count==0)MainController.Instance?.ApplyIdleFrameTiming((JObject)Workspace.System.Root);
            var result=DescribeControlDocument(doc);result["validationErrors"]=new JArray(validation);return result;
        }
        public JObject CreateDocumentFromControl(string relative,string schemaId,string json)
        {
            RequireControlEditReady();
            if(Path.IsPathRooted(relative)||!relative.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Use a relative .json path inside the configuration root.");
            string file=Path.GetFullPath(Path.Combine(RuntimePaths.ConfigRoot,relative));
            if(!NativeRecorderClient.IsInside(file,RuntimePaths.ConfigRoot))throw new ArgumentException("Configuration path escapes the configured root.");
            for(string folder=Path.GetDirectoryName(file);folder!=RuntimePaths.ConfigRoot&&folder!=null;folder=Path.GetDirectoryName(folder))
                if(Directory.Exists(folder)&&(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Configuration destination cannot traverse a directory link.");
            if(!(Workspace.Schemas.Catalog[schemaId] is JObject schema))throw new ArgumentException("Unknown configuration schema.");
            var root=LosslessJson.Parse(json) as JObject??throw new ArgumentException("Configuration must be an object.");
            var errors=ConfigSchema.Validate(root,schema);if(errors.Count>0)throw new ArgumentException(string.Join("; ",errors));
            if(File.Exists(file))throw new IOException("Configuration already exists; inspect its revision instead of replacing it.");
            Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temporary,root.ToString(Formatting.Indented));File.Move(temporary,file);}finally{if(File.Exists(temporary))File.Delete(temporary);}
            var doc=Workspace.Open(file,schemaId);Select(doc);
            var result=DescribeControlDocument(doc);result["validationScope"]="Document schema; production cross-file validation occurs when referenced by the selected setup.";return result;
        }
        public JObject ControlMetadata(string json=null,string revision=null,bool save=false)
        {
            EnsureMetadata();string current=RunConfigArchive.Hash(Encoding.UTF8.GetBytes(MetadataFingerprint()));
            if(json!=null)
            {
                RequireControlEditReady();if(current!=revision)throw new InvalidOperationException("Session metadata changed; inspect it before retrying.");
                var value=LosslessJson.Parse(json) as JObject??throw new ArgumentException("Metadata must be an object.");
                var next=value.ToObject<AnimalData>();
                var ids=new System.Collections.Generic.HashSet<string>((Workspace.System.Root["configs"] as JArray??new JArray()).Select(rig=>(string)rig["vrId"]));
                if(next.Animals.Any(animal=>animal==null||!ids.Contains(animal.VR))||next.Animals.GroupBy(animal=>animal.VR).Any(group=>group.Count()>1))
                    throw new ArgumentException("Metadata animals must name unique configured rigs.");
                metadata=next;EnsureMetadata();RebuildPreservingView();current=RunConfigArchive.Hash(Encoding.UTF8.GetBytes(MetadataFingerprint()));
            }
            if(save){SaveMetadata();current=RunConfigArchive.Hash(Encoding.UTF8.GetBytes(MetadataFingerprint()));}
            return new JObject{["revision"]=current,["value"]=JObject.FromObject(metadata),["saved"]=save,["savedOnStart"]=true};
        }
        public void StartCurrentFromControl()
        {
            if (modal != null || IsPreparing) throw new InvalidOperationException("Finish the open setup dialog/preparation before starting.");
            // A normal button click commits a delayed text field by moving focus.
            root?.panel?.focusController?.focusedElement?.Blur();
            StartExperiment();
        }
        public void StartFilesFromControl(string system, string sequence)
        {
            if (modal != null || IsPreparing) throw new InvalidOperationException("Finish the open setup dialog/preparation before starting.");
            if (ControlTextEditing)
                throw new InvalidOperationException("Finish editing the focused text field before replacing the selected setup, or use start_current.");
            RequireApplied();
            if (ControlHasUnsavedDraft) throw new InvalidOperationException("Unsaved setup changes are preserved. Save/discard them or use start_current to run the current GUI draft.");
            SelectFilesFromControl(system,sequence);
            StartExperiment();
        }
    }
}
