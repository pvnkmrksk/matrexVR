using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    /// <summary>A JSON DOM is the source of truth. Schemas never coerce or inject defaults on load.</summary>
    public sealed class ConfigDocument
    {
        public string Path { get; }
        public string SchemaId { get; set; }
        public JToken Root { get; private set; }
        public string LoadError { get; private set; }
        public string SourceText { get; private set; }
        private byte[] baseline;
        private string baselineJson;
        private bool repairedSource;
        private readonly struct DocumentSnapshot
        {
            public readonly string Json, Error, Source;
            public readonly bool Repaired;
            public DocumentSnapshot(string json,string error,string source,bool repaired)
            {Json=json;Error=error;Source=source;Repaired=repaired;}
            public bool SameAs(DocumentSnapshot other) => Json==other.Json && Error==other.Error && Source==other.Source && Repaired==other.Repaired;
        }
        private DocumentSnapshot baselineState;
        private readonly Stack<DocumentSnapshot> undo = new Stack<DocumentSnapshot>(), redo = new Stack<DocumentSnapshot>();
        public bool Dirty => repairedSource || Root.ToString(Formatting.None) != baselineJson ||
            LoadError!=baselineState.Error || LoadError!=null && SourceText!=baselineState.Source;
        public bool Conflict { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public ConfigDocument(string path, string schemaId)
        { Path = System.IO.Path.GetFullPath(path); SchemaId = schemaId; Reload(); }
        public void Reload(bool preserveHistory = false)
        {
            byte[] bytes = File.ReadAllBytes(Path);
            var before=Root==null?(DocumentSnapshot?)null:Capture();
            string source=RunConfigArchive.Decode(bytes),errorText=null;
            JToken parsed;
            try
            {
                parsed=string.IsNullOrWhiteSpace(source)?new JObject():LosslessJson.Parse(source);
                if(!(parsed is JObject))throw new ArgumentException("Configuration root must be an object.");
            }
            catch(Exception error) when(error is JsonException || error is ArgumentException)
            {
                // Keep the screen usable, with inherited controls visible. Never
                // save over damaged input merely because it was opened.
                parsed=new JObject();errorText=error.Message;
            }
            SourceText=source;LoadError=errorText;repairedSource=string.IsNullOrWhiteSpace(source);
            Root = parsed; baseline = bytes; baselineJson = Root.ToString(Formatting.None);
            baselineState=Capture();
            if(preserveHistory && before.HasValue && !before.Value.SameAs(baselineState))Remember(before.Value);
            Conflict = false;
            if (!preserveHistory) { undo.Clear(); redo.Clear(); }
        }
        public bool CheckExternalChange()
        {
            if (File.Exists(Path) && RunConfigArchive.Hash(File.ReadAllBytes(Path)) == RunConfigArchive.Hash(baseline)) { Conflict = false; return false; }
            if (Dirty || !File.Exists(Path)) { Conflict = true; return false; }
            Reload(true); return true;
        }
        public void Edit(Action<JToken> change)
        {
            var before=Capture();
            try { change(Root); }
            catch
            {
                // A failed callback must never leave an untracked partial edit.
                Restore(before);
                throw;
            }
            if(Capture().SameAs(before))return;
            Remember(before);
        }
        private DocumentSnapshot Capture() => new DocumentSnapshot(Root.ToString(Formatting.None),LoadError,SourceText,repairedSource);
        private void Restore(DocumentSnapshot snapshot)
        {Root=LosslessJson.Parse(snapshot.Json);LoadError=snapshot.Error;SourceText=snapshot.Source;repairedSource=snapshot.Repaired;}
        private void Remember(DocumentSnapshot before)
        {
            undo.Push(before); redo.Clear();
            // Bound history for large sequences; keep the most recent 32 transactions.
            if(undo.Count>32) { var recent=undo.ToArray(); undo.Clear(); for(int i=31;i>=0;i--) undo.Push(recent[i]); }
        }
        public void RevertToSaved() => Edit(_ => Restore(baselineState));
        public void ApplyJson(string json)
        {
            JToken parsed = LosslessJson.Parse(json);
            if (!(parsed is JObject)) throw new ArgumentException("Configuration root must be an object.");
            Edit(_ => {if(LoadError!=null)repairedSource=true;Root=parsed;LoadError=null;});
        }
        public void Undo() { if (undo.Count == 0) return; redo.Push(Capture()); Restore(undo.Pop()); }
        public void Redo() { if (redo.Count == 0) return; undo.Push(Capture()); Restore(redo.Pop()); }
        public void EnsureSaveable()
        {
            CheckExternalChange();
            if(LoadError!=null)throw new IOException("Repair the JSON draft before saving "+Path+": "+LoadError);
            if (Conflict) throw new IOException("External edit conflict: " + Path + ". Copy your JSON draft before reloading; disk changes will not be overwritten.");
        }
        public void Save()
        {
            EnsureSaveable(); if (!Dirty) return; // Byte-identical no-op, including BOM and whitespace.
            byte[] bytes = new UTF8Encoding(false).GetBytes(Root.ToString(Formatting.Indented) + "\n");
            string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                // Atomic replace on desktop. Preserve the previous config as a recoverable backup.
                File.Replace(temporary, Path, Path + ".bak");
                baseline = bytes; baselineJson = Root.ToString(Formatting.None); Conflict = false;
                repairedSource=false;
                SourceText=Root.ToString(Formatting.Indented)+"\n";
                baselineState=Capture();
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static JToken At(JToken root, IReadOnlyList<string> parts)
        {
            foreach (string part in parts)
            {
                if(root is JArray array) root=int.TryParse(part,out int index) && index>=0 && index<array.Count?array[index]:null;
                else root=root is JObject obj?obj[part]:null;
                if (root == null) break;
            }
            return root;
        }
    }
}
