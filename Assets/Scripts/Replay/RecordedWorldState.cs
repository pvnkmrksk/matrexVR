using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Custom serial effects can join the same seekable snapshot contract without adding replay parsers.</summary>
public sealed class RecordedPendingDestroy : MonoBehaviour {}

public interface IReplayStateParticipant
{
    JToken CaptureReplayState();
    void RestoreReplayState(JToken state);
}

/// <summary>Marks objects created by the original controllers, separate from recorded animal populations.</summary>
public sealed class RecordedWorldStateTarget : MonoBehaviour
{
    private static readonly HashSet<RecordedWorldStateTarget> targets = new HashSet<RecordedWorldStateTarget>();
    public string Key;
    public JObject Configuration;
    private readonly Dictionary<Renderer,Material[]> replayMaterials = new Dictionary<Renderer,Material[]>();
    private RecordedAnimationCapture animation;
    private string previous;
    private readonly Dictionary<string,Texture> textureReferences = new Dictionary<string,Texture>(StringComparer.Ordinal);
    private readonly Dictionary<string,uint> textureVersions = new Dictionary<string,uint>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTargets() => targets.Clear();
    private void Awake() => targets.Add(this);
    private void OnEnable() => targets.Add(this);
    internal static void GetActiveTargets(List<RecordedWorldStateTarget> output)
    {
        output.Clear();
        foreach (var target in targets)
            if (target != null && target.gameObject.activeInHierarchy &&
                (target.hideFlags & HideFlags.DontSave) == 0 &&
                target.GetComponentInParent<RecordedPendingDestroy>(true) == null)
                output.Add(target);
        output.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
    }

    public void Invalidate() { animation = null; previous = null; textureReferences.Clear(); textureVersions.Clear(); }
    public JObject Capture(bool force = false)
    {
        var scripts = GetComponentsInChildren<MonoBehaviour>(true);
        var transforms = GetComponentsInChildren<Transform>(true);
        if(GetComponentsInChildren<ParticleSystem>(true).Length>0 && !scripts.Any(script=>script is IReplayStateParticipant))
            throw new InvalidDataException("Particle systems require a seek-state IReplayStateParticipant to record faithful replay: "+Key);
        var nodes = new JArray();
        foreach (var node in transforms)
        {
            if (node.GetComponentInParent<LocustSpawner>(true) != null || node.GetComponentInParent<BandSpawner>(true) != null || node.GetComponentInParent<RecordedPendingDestroy>(true) != null) continue;
            var value = new JObject { ["path"] = PathOf(transform, node), ["active"] = node.gameObject.activeSelf,["layer"]=node.gameObject.layer,
                ["p"] = V(node.localPosition), ["r"] = Q(node.localRotation), ["s"] = V(node.localScale) };
            var renderers = new JArray();
            foreach (var renderer in node.GetComponents<Renderer>())
            {
                var materials = new JArray(); var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                var slots = renderer.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    var material = slots[slot];
                    if (material == null) { materials.Add(JValue.CreateNull()); continue; }
                    var indexed = new MaterialPropertyBlock(); renderer.GetPropertyBlock(indexed, slot);
                    var properties = new JObject(); Shader shader = material.shader;
                    if (shader != null) for (int i = 0; i < shader.GetPropertyCount(); i++)
                    {
                        string name = shader.GetPropertyName(i); int id = Shader.PropertyToID(name);
                        var effectiveBlock = indexed.isEmpty ? block : indexed;
                        var effective = effectiveBlock.HasProperty(id) ? effectiveBlock : null;
                        switch (shader.GetPropertyType(i))
                        {
                            case ShaderPropertyType.Color: Color color = effective != null ? effective.GetColor(id) : material.GetColor(id); properties[name] = new JObject { ["c"] = new JArray(color.r,color.g,color.b,color.a) }; break;
                            case ShaderPropertyType.Vector: Vector4 vector = effective != null ? effective.GetVector(id) : material.GetVector(id); properties[name] = new JObject { ["v"] = new JArray(vector.x,vector.y,vector.z,vector.w) }; break;
                            case ShaderPropertyType.Float: case ShaderPropertyType.Range: properties[name] = new JObject { ["f"] = effective != null ? effective.GetFloat(id) : material.GetFloat(id) }; break;
                            case ShaderPropertyType.Int: properties[name] = new JObject { ["i"] = effective != null ? effective.GetInteger(id) : material.GetInteger(id) }; break;
                            case ShaderPropertyType.Texture:
                                Texture texture = effective != null ? effective.GetTexture(id) : material.GetTexture(id);
                                string textureKey = renderer.GetInstanceID()+":"+slot+":"+name;
                                bool changed = textureReferences.TryGetValue(textureKey,out var previousTexture) && previousTexture != texture;
                                if(texture != null && textureVersions.TryGetValue(textureKey,out uint version) && previousTexture == texture && version != texture.updateCount) changed = true;
                                if(changed && !scripts.Any(script=>script is IReplayStateParticipant))
                                    throw new InvalidDataException("Dynamic texture changes require IReplayStateParticipant evidence: "+Key+"/"+name);
                                textureReferences[textureKey] = texture; if(texture!=null)textureVersions[textureKey]=texture.updateCount;
                                int stId=Shader.PropertyToID(name+"_ST");
                                var stBlock=effectiveBlock.HasProperty(stId)?effectiveBlock:null;
                                Vector2 scale=material.GetTextureScale(name), offset=material.GetTextureOffset(name);
                                Vector4 st=stBlock!=null?stBlock.GetVector(stId):new Vector4(scale.x,scale.y,offset.x,offset.y);
                                properties[name]=new JObject { ["t"]=TextureIdentity(texture),["st"]=new JArray(st.x,st.y,st.z,st.w) }; break;
                        }
                    }
                    materials.Add(new JObject { ["shader"]=shader!=null?shader.name:null,["properties"] = properties, ["keywords"] = new JArray(material.shaderKeywords), ["renderQueue"] = material.renderQueue });
                }
                renderers.Add(new JObject { ["enabled"] = renderer.enabled, ["materials"] = materials });
            }
            value["renderers"] = renderers;
            var participants = new JArray();
            var ordinals = new Dictionary<string,int>(StringComparer.Ordinal);
            foreach (var script in node.GetComponents<MonoBehaviour>().Where(script => script is IReplayStateParticipant))
            {
                string type=script.GetType().FullName;ordinals.TryGetValue(type,out int ordinal);ordinals[type]=ordinal+1;
                participants.Add(new JObject { ["type"] = type,["ordinal"]=ordinal,["state"] = ((IReplayStateParticipant)script).CaptureReplayState() });
            }
            value["participants"] = participants; nodes.Add(value);
        }
        if (animation == null) animation = new RecordedAnimationCapture(gameObject);
        var state = new JObject { ["key"] = Key, ["configuration"] = Configuration?.DeepClone(), ["nodes"] = nodes, ["animation"] = animation.Capture() };
        string serialized = state.ToString(Formatting.None);
        if (!force && serialized == previous) return null;
        previous = serialized; return state;
    }
    public void Apply(JObject state)
    {
        if (!string.IsNullOrEmpty((string)state["animation"]))
        {
            var driver=GetComponent<ReplayAnimationDriver>()??gameObject.AddComponent<ReplayAnimationDriver>();
            driver.Sample((string)state["animation"],0);
            if(driver.Approximate||driver.TransitionApproximation)throw new InvalidDataException("Required configured-object animation could not be restored: "+Key);
        }
        // Controller state can recreate procedural resources/reset a transform;
        // restore it before the recorded geometry/renderer snapshot.
        foreach (var token in state["nodes"] as JArray ?? new JArray())
        {
            var node = AtPath(transform,(string)token["path"]); if(node==null)continue;
            foreach (var participant in token["participants"] as JArray ?? new JArray())
            {
                int ordinal=(int?)participant["ordinal"]??0;
                var target = node.GetComponents<MonoBehaviour>().Where(script => script is IReplayStateParticipant && script.GetType().FullName == (string)participant["type"]).Skip(ordinal).FirstOrDefault();
                if (target == null) throw new InvalidDataException("Missing replay state participant: " + participant["type"]);
                ((IReplayStateParticipant)target).RestoreReplayState(participant["state"]?.DeepClone());
            }
        }
        foreach (var token in state["nodes"] as JArray ?? new JArray())
        {
            Transform node = AtPath(transform, (string)token["path"]);
            if (node == null) throw new InvalidDataException("Recorded configured-object hierarchy differs from this build: " + Key + "/" + token["path"]);
            node.localPosition = ReadV(token["p"]); node.localRotation = ReadQ(token["r"]); node.localScale = ReadV(token["s"]);
            node.gameObject.layer=(int)token["layer"];
            node.gameObject.SetActive((bool)token["active"]);
            var renderers = node.GetComponents<Renderer>(); var recorded = token["renderers"] as JArray ?? new JArray();
            if (renderers.Length != recorded.Count) throw new InvalidDataException("Recorded renderer hierarchy differs from this build: " + Key);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i]; renderer.enabled = (bool)recorded[i]["enabled"];
                // A participant can replace generated materials on every seek. Read its
                // restored textures before reinstalling our owned material copies.
                var sourceSlots = renderer.sharedMaterials;
                if(!replayMaterials.TryGetValue(renderer,out var materials))
                {
                    materials = sourceSlots.Select(material => material != null ? new Material(material) : null).ToArray();
                    replayMaterials[renderer] = materials;
                }
                var saved = (JArray)recorded[i]["materials"];
                if(materials.Length!=saved.Count || sourceSlots.Length!=saved.Count)throw new InvalidDataException("Recorded material slot count differs from this build: "+Key);
                var originalBlock=new MaterialPropertyBlock();renderer.GetPropertyBlock(originalBlock);
                for (int j = 0; j < materials.Length; j++)
                {
                    if (!(saved[j] is JObject material)) { if(materials[j]!=null)throw new InvalidDataException("Recorded null material differs from this build: "+Key);continue; }
                    if(materials[j]==null)throw new InvalidDataException("Recorded material is missing from this build: "+Key);
                    string shaderName=(string)material["shader"];
                    if(!string.IsNullOrEmpty(shaderName)&&materials[j].shader?.name!=shaderName)
                    {
                        var shader=Shader.Find(shaderName);if(shader==null||!shader.isSupported)throw new InvalidDataException("Recorded shader is unavailable: "+shaderName);
                        materials[j].shader=shader;
                    }
                    var originalIndexed=new MaterialPropertyBlock();renderer.GetPropertyBlock(originalIndexed,j);
                    materials[j].shaderKeywords = ((JArray)material["keywords"]).Values<string>().ToArray(); materials[j].renderQueue = (int)material["renderQueue"];
                    foreach (var property in ((JObject)material["properties"]).Properties())
                    {
                        int id = Shader.PropertyToID(property.Name); var data = (JObject)property.Value;
                        if (data["f"] != null) { materials[j].SetFloat(id,(float)data["f"]); }
                        else if (data["i"] != null) materials[j].SetInteger(id,(int)data["i"]);
                        else if (data["c"] is JArray c) { var color = new Color((float)c[0],(float)c[1],(float)c[2],(float)c[3]); materials[j].SetColor(id,color); }
                        else if (data["v"] is JArray v) { var vector = new Vector4((float)v[0],(float)v[1],(float)v[2],(float)v[3]); materials[j].SetVector(id,vector); }
                        else if(data["st"] is JArray st)
                        {
                            var effectiveBlock=originalIndexed.isEmpty?originalBlock:originalIndexed;
                            var texture=effectiveBlock.HasProperty(id)?effectiveBlock.GetTexture(id):sourceSlots[j]?.GetTexture(id);
                            if(data["t"]?.Type==JTokenType.Null)texture=null;
                            else if(!JToken.DeepEquals(TextureIdentity(texture),data["t"]))
                                throw new InvalidDataException("Recorded texture or sampler differs; its procedural owner must restore IReplayStateParticipant evidence: "+Key+"/"+property.Name);
                            materials[j].SetTexture(id,texture);
                            materials[j].SetTextureScale(property.Name,new Vector2((float)st[0],(float)st[1]));materials[j].SetTextureOffset(property.Name,new Vector2((float)st[2],(float)st[3]));
                        }
                    }
                }
                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null);
                for(int j=0;j<materials.Length;j++)renderer.SetPropertyBlock(null,j);
            }

        }
    }
    private void OnDestroy(){targets.Remove(this);foreach(var materials in replayMaterials.Values)foreach(var material in materials)if(material!=null)Destroy(material);replayMaterials.Clear();}
    private static JToken TextureIdentity(Texture texture) => texture==null?JValue.CreateNull():new JObject {
        ["name"]=texture.name,["width"]=texture.width,["height"]=texture.height,["dimension"]=(int)texture.dimension,
        ["wrapU"]=(int)texture.wrapModeU,["wrapV"]=(int)texture.wrapModeV,["wrapW"]=(int)texture.wrapModeW,["filter"]=(int)texture.filterMode,["aniso"]=texture.anisoLevel,["mipBias"]=texture.mipMapBias };
    private static JArray V(Vector3 value) => new JArray(value.x,value.y,value.z);
    private static JArray Q(Quaternion value) => new JArray(value.x,value.y,value.z,value.w);
    private static Vector3 ReadV(JToken value) => new Vector3((float)value[0],(float)value[1],(float)value[2]);
    private static Quaternion ReadQ(JToken value) => new Quaternion((float)value[0],(float)value[1],(float)value[2],(float)value[3]);
    private static string PathOf(Transform root,Transform node)
    { var parts=new List<string>();while(node!=root){int index=0;for(int i=0;i<node.GetSiblingIndex();i++)if(node.parent.GetChild(i).GetComponent<RecordedPendingDestroy>()==null)index++;parts.Insert(0,index.ToString());node=node.parent;}return string.Join("/",parts); }
    private static Transform AtPath(Transform root,string path)
    { foreach(string part in string.IsNullOrEmpty(path)?Array.Empty<string>():path.Split('/')){var children=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i)).Where(child=>child.GetComponent<RecordedPendingDestroy>()==null).ToArray();if(!int.TryParse(part,out int i)||i<0||i>=children.Length)return null;root=children[i];}return root; }
}

/// <summary>Changed-target snapshots are complete checkpoints: reverse seek does not replay preceding frames.</summary>
[DefaultExecutionOrder(1400)]
public sealed class RecordedWorldStateWriter : MonoBehaviour
{
    public const string FileName = "world_state.jsonl";
    private static readonly Unity.Profiling.ProfilerMarker supportMarker = new Unity.Profiling.ProfilerMarker("MATREX.World.Support");
    private static readonly Unity.Profiling.ProfilerMarker targetsMarker = new Unity.Profiling.ProfilerMarker("MATREX.World.Targets");
    private static readonly Unity.Profiling.ProfilerMarker clockMarker = new Unity.Profiling.ProfilerMarker("MATREX.World.Clocks");
    private static readonly Unity.Profiling.ProfilerMarker journalMarker = new Unity.Profiling.ProfilerMarker("MATREX.World.Journal");
    public double CaptureMilliseconds { get; private set; }
    private readonly List<RecordedWorldStateTarget> liveTargets = new List<RecordedWorldStateTarget>();
    private ReplayRenderClock.Acquisition acquisitionClock;
    private StreamWriter writer;
    private string directory;
    private void LateUpdate()
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try { CaptureRenderedFrame(); }
        catch(Exception error)
        {
            if(MainController.Instance!=null)MainController.Instance.FailRecordingEvidence(error);
            else Debug.LogException(error);
        }
        finally { CaptureMilliseconds = 1000.0 * (System.Diagnostics.Stopwatch.GetTimestamp() - started) / System.Diagnostics.Stopwatch.Frequency; }
    }
    private void CaptureRenderedFrame()
    {
        var logger=MasterDataLogger.Instance;
        if(logger==null||!logger.AcceptingWrites||MainController.Instance?.SequenceRunning!=true||string.IsNullOrEmpty(ReplayPresentationRecorder.CurrentPresentationId))return;
        if(directory!=logger.directoryPath){Close();directory=logger.directoryPath;writer=new StreamWriter(Path.Combine(directory,FileName),false,new UTF8Encoding(false),65536);}
        using(RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.WorldSupport))
        using(supportMarker.Auto())
            ReplayEvidenceSupport.RequireSupportedScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var targets=new JArray();
        var activeTargets=new JArray();
        using(RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.WorldTargets))
        using(targetsMarker.Auto())
        {
            RecordedWorldStateTarget.GetActiveTargets(liveTargets);
            foreach(var target in liveTargets)
            {
                activeTargets.Add(target.Key);
                var state=target.Capture();if(state!=null)targets.Add(state);
            }
        }
        float shaderTime=Time.timeSinceLevelLoad,deltaTime=Time.deltaTime,smoothDeltaTime=Time.smoothDeltaTime;
        var shaderGlobals=ReplayRenderClock.Evaluate(shaderTime,deltaTime,smoothDeltaTime);
        using(RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.WorldClocks))
        using(clockMarker.Auto())
        {
            if(acquisitionClock==null)acquisitionClock=new ReplayRenderClock.Acquisition();
            acquisitionClock.Update(shaderGlobals);
        }
        using(RuntimePerformanceProbe.Measure(RuntimePerformanceProbe.CpuStage.WorldJournal))
        using(journalMarker.Auto())
        {
            var entry=new JObject { ["presentationId"]=ReplayPresentationRecorder.CurrentPresentationId,["frame"]=Time.frameCount,
                ["unityTime"]=Time.timeAsDouble,["shaderTime"]=shaderTime,["shaderGlobals"]=ReplayRenderClock.Clock(shaderGlobals),["activeTargets"]=activeTargets,["deltaTime"]=deltaTime,["smoothDeltaTime"]=smoothDeltaTime,["targets"]=targets };
            ReplayPresentationJournal.WriteEntry(writer,entry);
        }
    }
    public void Close()
    {
        acquisitionClock?.Dispose();acquisitionClock=null;
        liveTargets.Clear();
        var closing=writer;writer=null;directory=null;
        closing?.Dispose();
    }
    private void OnDestroy()=>Close();
}

public sealed class RecordedWorldStateReader : IDisposable
{
    private const int MaximumLineBytes = 64 * 1024 * 1024;
    public const int MaximumCachedRows = 16;
    // Bounds serialized payload size; JObject overhead is additional but bounded by row count.
    public const long MaximumCachedSerializedBytes = 32L * 1024 * 1024;
    private sealed class Entry { public int Frame; public long Offset; }
    private sealed class CachedRow
    {
        public JObject Value;
        public long Bytes;
        public LinkedListNode<long> Recency;
    }
    private readonly FileStream stream;
    private readonly string path;
    private readonly long sourceLength;
    private readonly DateTime sourceWriteTime;
    private readonly CancellationToken cancellation;
    private readonly Dictionary<string,List<Entry>> frames = new Dictionary<string,List<Entry>>(StringComparer.Ordinal);
    private readonly Dictionary<string,Dictionary<string,List<Entry>>> targets = new Dictionary<string,Dictionary<string,List<Entry>>>(StringComparer.Ordinal);
    private readonly Dictionary<long,CachedRow> cache = new Dictionary<long,CachedRow>();
    private readonly LinkedList<long> recency = new LinkedList<long>();
    private long cachedBytes;
    private bool disposed;
    public int CachedRows => cache.Count;
    public long CachedSerializedBytes => cachedBytes;
    public long ParsedRowCount { get; private set; }

    public RecordedWorldStateReader(string directory, CancellationToken cancellation = default)
    {
        this.cancellation = cancellation;
        cancellation.ThrowIfCancellationRequested();
        path = Path.Combine(directory, RecordedWorldStateWriter.FileName);
        if (!File.Exists(path)) throw new InvalidDataException("Same-build configured-world frame evidence is missing.");
        sourceLength = new FileInfo(path).Length;
        sourceWriteTime = File.GetLastWriteTimeUtc(path);
        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            while (stream.Position < sourceLength)
            {
                cancellation.ThrowIfCancellationRequested();
                long offset = stream.Position;
                var value = ParseVerified(ReadLine());
                string id = (string)value["presentationId"];
                if (string.IsNullOrEmpty(id)) throw new InvalidDataException("Configured-world frame has no presentation identity.");
                var entry = new Entry { Frame = (int)value["frame"], Offset = offset };
                if (!frames.TryGetValue(id, out var list)) frames[id] = list = new List<Entry>();
                if (list.Count > 0 && list[list.Count - 1].Frame >= entry.Frame)
                    throw new InvalidDataException("Configured-world frame indices must increase within each presentation.");
                list.Add(entry);
                if (!targets.TryGetValue(id, out var map)) targets[id] = map = new Dictionary<string,List<Entry>>(StringComparer.Ordinal);
                var active = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in (value["activeTargets"] as JArray ?? throw new InvalidDataException("Configured-world membership evidence is missing.")).Values<string>())
                    if (string.IsNullOrEmpty(key) || !active.Add(key)) throw new InvalidDataException("Configured-world membership contains an invalid or duplicate target.");
                var changed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var target in value["targets"] as JArray ?? throw new InvalidDataException("Configured-world target evidence is missing."))
                {
                    string key = (string)target["key"];
                    if (string.IsNullOrEmpty(key) || !active.Contains(key) || !changed.Add(key))
                        throw new InvalidDataException("Configured-world target checkpoint has invalid membership.");
                    if (!map.TryGetValue(key, out var checkpoints)) map[key] = checkpoints = new List<Entry>();
                    checkpoints.Add(entry);
                }
                foreach (string key in active)
                    if (!map.ContainsKey(key)) throw new InvalidDataException("Configured-world target has no initial checkpoint: " + key);
            }
            if (frames.Count == 0) throw new InvalidDataException("Configured-world frame evidence is empty.");
            EnsureSourceUnchanged();
        }
        catch { stream.Dispose(); throw; }
    }

    private void EnsureSourceUnchanged()
    {
        cancellation.ThrowIfCancellationRequested();
        if (disposed) throw new ObjectDisposedException(nameof(RecordedWorldStateReader));
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != sourceLength || info.LastWriteTimeUtc != sourceWriteTime)
            throw new InvalidDataException("Configured-world source changed after it was indexed; reload the recording.");
    }
    private string ReadLine()
    {
        var bytes = new List<byte>();
        int value;
        while ((value = stream.ReadByte()) >= 0 && value != '\n')
        {
            if ((bytes.Count & 65535) == 0) cancellation.ThrowIfCancellationRequested();
            if (bytes.Count >= MaximumLineBytes) throw new InvalidDataException("Configured-world frame exceeds 64 MiB.");
            bytes.Add((byte)value);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    private static JObject ParseVerified(string line)
    {
        return ReplayPresentationJournal.ParseVerified(line);
    }
    private JObject Read(Entry entry)
    {
        EnsureSourceUnchanged();
        if (cache.TryGetValue(entry.Offset, out var hit))
        { recency.Remove(hit.Recency); recency.AddLast(hit.Recency); return hit.Value; }
        stream.Position = entry.Offset;
        string line = ReadLine();
        var value = ParseVerified(line);
        ParsedRowCount++;
        long bytes = Encoding.UTF8.GetByteCount(line);
        // An unusually large row remains seekable without occupying the cache.
        if (bytes <= MaximumCachedSerializedBytes)
        {
            while (cache.Count >= MaximumCachedRows || cachedBytes + bytes > MaximumCachedSerializedBytes)
            {
                long oldest = recency.First.Value;
                cachedBytes -= cache[oldest].Bytes; cache.Remove(oldest); recency.RemoveFirst();
            }
            var row = new CachedRow { Value = value, Bytes = bytes, Recency = recency.AddLast(entry.Offset) };
            cache.Add(entry.Offset, row); cachedBytes += bytes;
        }
        return value;
    }
    private static Entry At(List<Entry> list, int frame)
    {
        int low = 0, high = list.Count - 1;
        while (low < high) { int middle = (low + high + 1) / 2; if (list[middle].Frame <= frame) low = middle; else high = middle - 1; }
        return list.Count > 0 && list[low].Frame <= frame ? list[low] : null;
    }
    private static JObject Target(JObject row, string key)
    {
        var state = (row["targets"] as JArray)?.OfType<JObject>().FirstOrDefault(value => (string)value["key"] == key);
        return state ?? throw new InvalidDataException("Configured-world target checkpoint is missing: " + key);
    }
    public JObject ConfigurationFor(string id, int frame, string key)
    {
        EnsureSourceUnchanged();
        if (!targets.TryGetValue(id, out var map) || !map.TryGetValue(key, out var checkpoints)) return null;
        var entry = At(checkpoints, frame); if (entry == null) return null;
        // Callers can configure controllers without mutating shared cached evidence.
        return Target(Read(entry), key)["configuration"]?.DeepClone() as JObject;
    }
    public void RequireFrame(string id,int frame)
    {
        cancellation.ThrowIfCancellationRequested();
        if(!frames.TryGetValue(id,out var list))throw new InvalidDataException("No configured-world frame matches this presentation.");
        var entry=At(list,frame);
        if(entry==null||entry.Frame!=frame)throw new InvalidDataException("Required configured-world rendered frame is missing: "+frame);
    }
    public JObject Apply(string id, int frame, IEnumerable<RecordedWorldStateTarget> live)
    {
        EnsureSourceUnchanged();
        if (!frames.TryGetValue(id, out var list)) throw new InvalidDataException("No configured-world frame matches this presentation.");
        var clockEntry = At(list, frame);
        if (clockEntry == null || clockEntry.Frame != frame) throw new InvalidDataException("No configured-world frame matches this presentation.");
        var clock = Read(clockEntry);
        var available = live.Where(target => target != null && target.GetComponentInParent<RecordedPendingDestroy>(true) == null).ToDictionary(target => target.Key, StringComparer.Ordinal);
        var active = new HashSet<string>(((JArray)clock["activeTargets"]).Values<string>(), StringComparer.Ordinal);
        foreach (var item in available) item.Value.gameObject.SetActive(active.Contains(item.Key));
        foreach (string key in active)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!targets[id].TryGetValue(key, out var checkpoints)) throw new InvalidDataException("Configured-world target evidence is missing: " + key);
            var checkpoint = At(checkpoints, frame);
            if (checkpoint == null) throw new InvalidDataException("Configured-world target checkpoint is missing: " + key);
            if (!available.TryGetValue(key, out var target)) throw new InvalidDataException("Original controller did not recreate recorded object target: " + key);
            target.Apply(Target(Read(checkpoint), key));
        }
        // Return clock/membership metadata only: copying all snapshots here defeats the bounded cache.
        return new JObject(clock.Properties().Where(property => property.Name != "targets" && property.Name != "sha256")
            .Select(property => new JProperty(property.Name, property.Value.DeepClone())));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; cache.Clear(); recency.Clear(); cachedBytes = 0; stream.Dispose();
    }
}
