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
        private byte[] baseline;
        private string baselineJson;
        private readonly Stack<string> undo = new Stack<string>(), redo = new Stack<string>();
        public bool Dirty => Root.ToString(Formatting.None) != baselineJson;
        public bool Conflict { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public ConfigDocument(string path, string schemaId)
        { Path = System.IO.Path.GetFullPath(path); SchemaId = schemaId; Reload(); }
        public void Reload()
        {
            byte[] bytes = File.ReadAllBytes(Path);
            JToken parsed = LosslessJson.Parse(RunConfigArchive.Decode(bytes));
            if (!(parsed is JObject)) throw new ArgumentException("Configuration root must be an object.");
            Root = parsed; baseline = bytes; baselineJson = Root.ToString(Formatting.None);
            Conflict = false; undo.Clear(); redo.Clear();
        }
        public bool CheckExternalChange()
        {
            if (File.Exists(Path) && RunConfigArchive.Hash(File.ReadAllBytes(Path)) == RunConfigArchive.Hash(baseline)) return false;
            if (Dirty || !File.Exists(Path)) { Conflict = true; return false; }
            Reload(); return true;
        }
        public void Edit(Action<JToken> change)
        {
            string before = Root.ToString(Formatting.None);
            change(Root);
            if (Root.ToString(Formatting.None) == before) return;
            undo.Push(before); redo.Clear();
            // Bound history for large sequences; keep the most recent 32 transactions.
            if(undo.Count>32) { var recent=undo.ToArray(); undo.Clear(); for(int i=31;i>=0;i--) undo.Push(recent[i]); }
        }
        public void ApplyJson(string json)
        {
            JToken parsed = LosslessJson.Parse(json);
            if (!(parsed is JObject)) throw new ArgumentException("Configuration root must be an object.");
            Edit(_ => Root = parsed);
        }
        public void Undo() { if (undo.Count == 0) return; redo.Push(Root.ToString(Formatting.None)); Root = LosslessJson.Parse(undo.Pop()); }
        public void Redo() { if (redo.Count == 0) return; undo.Push(Root.ToString(Formatting.None)); Root = LosslessJson.Parse(redo.Pop()); }
        public void EnsureSaveable()
        {
            CheckExternalChange();
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
