using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    /// <summary>Forks only QuickStart-owned documents. Imported experiment references and existing files remain untouched.</summary>
    public static class ApparatusStarterFiles
    {
        public sealed class ForkResult
        {
            public string SequencePath,ExperimentReference;
            public string SequenceFingerprint,ExperimentFingerprint;
            public bool FactoryStarter;
            public readonly List<string> CopiedSources=new List<string>();
        }
        public static string Fingerprint(JToken root)
        {
            // Compact text retains raw numeric lexemes, including values outside double precision.
            string normalized=root.ToString(Formatting.None);
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-","").ToLowerInvariant();
        }
        public static ForkResult Fork(JObject sourceSequence,string configRoot,Func<string,string,JObject> read,JObject factorySequence,JObject factoryExperiment)
        {
            string root=Path.GetFullPath(configRoot),folder=Path.Combine(root,"Apparatus");Directory.CreateDirectory(folder);
            var result=new ForkResult{SequencePath=Path.Combine(folder,"sequence.json")};
            var sequence=(JObject)sourceSequence.DeepClone();var references=new Dictionary<string,string>(StringComparer.Ordinal);
            var sourceSteps=(sourceSequence["sequences"] as JArray??new JArray()).OfType<JObject>().ToList();
            bool factory=Fingerprint(sourceSequence)==Fingerprint(factorySequence)&&sourceSteps.Count==1;
            var quickRoot=Path.GetFullPath(Path.Combine(root,"QuickStart"))+Path.DirectorySeparatorChar;
            int copied=0;
            foreach(var step in (sequence["sequences"] as JArray??new JArray()).OfType<JObject>())
            {
                if(!(step["parameters"] is JObject parameters))continue;
                string key=parameters["configFile"]?.Type==JTokenType.String?"configFile":parameters["design"]?.Type==JTokenType.String?"design":null;
                if(key==null)continue;
                string reference=(string)parameters[key];
                if(string.IsNullOrWhiteSpace(reference))continue;
                string source=Path.GetFullPath(Path.IsPathRooted(reference)?reference:Path.Combine(root,reference));
                if(!source.StartsWith(quickRoot,StringComparison.Ordinal)){factory=false;continue;}
                if(!references.TryGetValue(source,out string destination))
                {
                    JObject experiment=read(reference,(string)step["sceneName"]);
                    if(factory&&Fingerprint(experiment)!=Fingerprint(factoryExperiment))factory=false;
                    string name=(++copied).ToString("00")+"-"+Path.GetFileName(source);
                    destination="Apparatus/Experiments/"+name;references.Add(source,destination);
                    WriteMissing(Path.Combine(root,destination),experiment);
                    if(factory&&Fingerprint(LosslessJson.Parse(File.ReadAllText(Path.Combine(root,destination))))!=Fingerprint(factoryExperiment))factory=false;
                    result.CopiedSources.Add(source);
                }
                parameters[key]=destination;
                result.ExperimentReference=destination;
            }
            WriteMissing(result.SequencePath,sequence);
            var savedSequence=LosslessJson.Parse(File.ReadAllText(result.SequencePath));
            result.FactoryStarter=factory&&copied==1&&Fingerprint(savedSequence)==Fingerprint(sequence);
            result.SequenceFingerprint=Fingerprint(savedSequence);
            if(result.ExperimentReference!=null)result.ExperimentFingerprint=Fingerprint(LosslessJson.Parse(File.ReadAllText(Path.Combine(root,result.ExperimentReference))));
            return result;
        }
        private static void WriteMissing(string path,JObject root)
        {
            if(File.Exists(path))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(file,new UTF8Encoding(false)))writer.Write(root.ToString(Formatting.Indented)+"\n");
        }
    }
}
