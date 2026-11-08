#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Same-build input/state contract regressions using production controllers, renderer and disk reader.</summary>
public static class SameBuildReplayValidation
{
    private static readonly List<string> checks=new List<string>();
    private static string output;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
    private static void Reject(Action action,string label)
    {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,label);}
    private static void Invoke(object target,string name)=>target.GetType().GetMethod(name,Hidden)?.Invoke(target,null);
    private static JObject Row(int frame,JArray targets,JArray members)=>new JObject { ["presentationId"]="a",["frame"]=frame,
        ["shaderTime"]=frame*.02,["deltaTime"]=.02,["smoothDeltaTime"]=.02,["shaderGlobals"]=ReplayRenderClock.CaptureFrameClock(),["activeTargets"]=members,["targets"]=targets };
    private static void SaveRows(string folder,IEnumerable<JObject> rows)
    {File.WriteAllLines(Path.Combine(folder,RecordedWorldStateWriter.FileName),rows.Select(row=>{row["sha256"]=ReplayPresentationJournal.HashEntry(row);return row.ToString(Formatting.None);}));}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"../KANNADI_VALIDATION_COPY")))throw new Exception("Disposable project only.");
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../same-build-replay-validation"));Directory.CreateDirectory(output);checks.Clear();
        string failure=null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            InputsAndRandom();ConfigurationNulls();WorldSnapshots();WorldTargetRegistry();AnimationBlend();OriginalControllers();OriginalPopulationFactory();AsynchronousParentFrame();UnsupportedEffects();RenderClock();
            Check((DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(ExperimentPhases),typeof(DefaultExecutionOrder)) is DefaultExecutionOrder phase && phase.order<1200,"Phase transitions complete before pose/population/world evidence");
        }
        catch(Exception error){failure=error.ToString();Debug.LogError(failure);}
        File.WriteAllText(Path.Combine(output,"results.json"),JsonConvert.SerializeObject(new {checks=checks.Count,passed=checks,failure,unity=Application.unityVersion,
            scope="Production same-build journal/hash/build gate/RNG checkpoints, realized world seek state and cache, original drum/dynamic controller configuration, transition bones, GPU shader clock. Physical displays and third-party trackers are outside this regression."},Formatting.Indented));
        EditorApplication.Exit(failure==null?0:1);
    }
    private static void InputsAndRandom()
    {
        string folder=Path.Combine(output,"contract");Directory.CreateDirectory(folder);
        var identity=PlayerBuildIdentity.Capture();
        var worker=Task.Run(()=>PlayerBuildIdentity.Capture());worker.Wait();
        Check(JToken.DeepEquals(identity,worker.Result),"Build identity is immutable and safe to read from archive workers");
        worker.Result["applicationVersion"]="changed";Check((string)PlayerBuildIdentity.Capture()["applicationVersion"]!=(string)worker.Result["applicationVersion"],"Workers receive independent identity clones");
        var config=JObject.Parse("{\"title\":\"Future configuration\",\"newField\":{\"arbitrary\":[1,2,3]}}");
        var contract=new JObject{["schemaVersion"]=1,["player"]=identity};File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.ContractName),contract.ToString());
        var a=new JObject{["id"]="a",["onsetUtc"]=DateTimeOffset.UtcNow.ToString("O"),["authoredRow"]=4,["config"]=config};
        a["sha256"]=ReplayPresentationJournal.HashEntry(a);var b=(JObject)a.DeepClone();b["id"]="b";b["authoredRow"]=1;b["sha256"]=ReplayPresentationJournal.HashEntry(b);
        File.WriteAllLines(Path.Combine(folder,ReplayPresentationJournal.FileName),new[]{a.ToString(Formatting.None),b.ToString(Formatting.None)});
        var journal=new ReplayPresentationJournal(folder,identity);
        Check(JToken.DeepEquals(journal.For("a")["config"],config)&& (int)journal.For("a")["authoredRow"]==4&&(int)journal.For("b")["authoredRow"]==1,"Whole future configuration and repeated/randomized authored rows survive exact presentation identity");
        Check(ReplayPresentationJournal.HashEntry(ReplayPresentationJournal.Parse(a.ToString(Formatting.None)))==(string)a["sha256"],"UTC journal integrity survives local non-UTC timezone parsing");
        var other=(JObject)identity.DeepClone();other["buildGuid"]="a-different-build-guid";other["runtimeSourceFingerprintSha256"]="other";other["editorRuntimeIdentity"]="other";
        Reject(()=>ReplayPresentationJournal.RequireSameBuild(identity,other),"Foreign build is rejected explicitly");
        UnityEngine.Random.InitState(3921);var checkpoint=ExperimentRandom.CaptureCheckpoint();
        float random=UnityEngine.Random.value;int named=ExperimentRandom.NextSeed("validation-new-stream");
        float after=UnityEngine.Random.value;var prior=UnityEngine.Random.state;float nextCaller=UnityEngine.Random.value;UnityEngine.Random.state=prior;
        using(ExperimentRandom.RestoreCheckpoint(checkpoint))
        {Check(UnityEngine.Random.value==random&&ExperimentRandom.NextSeed("validation-new-stream")==named,"Runtime RNG state and named-stream counters restore exact initialization");}
        Check(UnityEngine.Random.value==nextCaller,"Replay RNG scope restores the caller rather than leaving replay state active");
        a["authoredRow"]=9;File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.FileName),a.ToString(Formatting.None));
        Reject(()=>new ReplayPresentationJournal(folder),"Valid JSON with altered presentation evidence fails integrity");
    }
    private static void ConfigurationNulls()
    {
        var stepType=typeof(DynamicSequenceController).GetNestedType("Step",BindingFlags.NonPublic);
        var config=JObject.Parse("{\"initialPosition\":{\"x\":1,\"y\":2,\"z\":3},\"objects\":[{\"type\":\"Cube\",\"pos\":[4,5,6],\"polar\":null,\"scale\":null}],\"trigger\":null}");
        var step=config.ToObject(stepType);var saved=ReplayConfigurationJson.Capture(step);
        Check(saved["objects"][0]["polar"].Type==JTokenType.Null&&saved["objects"][0]["scale"].Type==JTokenType.Null&&saved["trigger"].Type==JTokenType.Null,"Dynamic replay configuration preserves null polar/scale/trigger branches");
        Check(((JObject)saved["initialPosition"]).Count==3&&(float)saved["initialPosition"]["x"]==1&&(float)saved["initialPosition"]["y"]==2&&(float)saved["initialPosition"]["z"]==3,"Unity Vector3 configuration captures exactly public xyz fields without computed recursion");
        Check(JToken.DeepEquals(saved,ReplayConfigurationJson.Capture(saved.ToObject(stepType))),"Dynamic configuration fields survive typed capture/restore without changing null branch semantics");
    }
    private static void WorldSnapshots()
    {
        string folder=Path.Combine(output,"world");Directory.CreateDirectory(folder);
        var root=new GameObject("source");var target=root.AddComponent<RecordedWorldStateTarget>();target.Key="fixture";
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);
        var renderer=cube.GetComponent<Renderer>();var material=new Material(Shader.Find("Standard"));var texture=new Texture2D(2,2){name="immutable fixture texture"};material.mainTexture=texture;var secondMaterial=new Material(material);renderer.sharedMaterials=new[]{material,secondMaterial};
        var global=new MaterialPropertyBlock();global.SetColor("_Color",Color.red);renderer.SetPropertyBlock(global);
        var indexed=new MaterialPropertyBlock();indexed.SetFloat("_Glossiness",.25f);renderer.SetPropertyBlock(indexed,0);
        var first=root.AddComponent<SameBuildReplayProbe>();first.value=11;var second=root.AddComponent<SameBuildReplayProbe>();second.value=22;
        var state=target.Capture();Check(target.Capture()==null,"Unchanged complete target snapshots are deduplicated");
        material.mainTextureScale=new Vector2(3,4);var next=target.Capture();Check(next!=null,"Material-only changes are captured with a static transform hierarchy");
        var clone=Object.Instantiate(root);clone.transform.GetChild(0).gameObject.layer=8;var restored=clone.GetComponent<RecordedWorldStateTarget>();restored.Apply(state);
        Check(clone.transform.GetChild(0).gameObject.layer==cube.layer,"Configured-object snapshot restores original visibility layer after seek isolation changes");
        var probes=clone.GetComponents<SameBuildReplayProbe>();Check(probes[0].value==11&&probes[1].value==22,"Two participants of the same component type restore by ordinal");
        var restoredMaterials=clone.GetComponentInChildren<Renderer>().sharedMaterials;
        Check(restoredMaterials[0].mainTextureScale==Vector2.one,"Texture scale and offset restore without modifying source texture");
        Check(restoredMaterials[0].color==Color.white&&restoredMaterials[1].color==Color.red&&Mathf.Abs(restoredMaterials[0].GetFloat("_Glossiness")-.25f)<.00001f,"Indexed material blocks supersede global blocks per Unity rendering semantics");
        var future=new GameObject("future");var futureTarget=future.AddComponent<RecordedWorldStateTarget>();futureTarget.Key="future";
        var rows=new List<JObject>();for(int frame=1;frame<=25;frame++)rows.Add(Row(frame,new JArray(frame==1?state:null,frame==12?futureTarget.Capture(true):null).Where(token=>token.Type!=JTokenType.Null).ToJArray(),new JArray(frame>=12?new[]{"fixture","future"}:new[]{"fixture"})));
        SaveRows(folder,rows);string path=Path.Combine(folder,RecordedWorldStateWriter.FileName),before=RunConfigArchive.Hash(File.ReadAllBytes(path));
        using(var reader=new RecordedWorldStateReader(folder))
        {
            reader.Apply("a",25,new[]{restored,futureTarget});Check(future.activeSelf,"Seek after creation restores target membership");
            reader.Apply("a",1,new[]{restored,futureTarget});Check(!future.activeSelf,"Backward seek hides targets that did not yet exist");
            long parses=reader.ParsedRowCount;reader.Apply("a",1,new[]{restored,futureTarget});Check(reader.ParsedRowCount==parses,"Repeated seek shares parsed rows instead of reparsing each target");
            for(int frame=1;frame<=25;frame++)reader.Apply("a",frame,new[]{restored,futureTarget});
            Check(reader.CachedRows<=RecordedWorldStateReader.MaximumCachedRows&&reader.CachedSerializedBytes<=RecordedWorldStateReader.MaximumCachedSerializedBytes,"Seek cache remains bounded across many frames");
            Reject(()=>reader.Apply("a",26,new[]{restored,futureTarget}),"Missing exact rendered frame fails instead of reusing a preceding row");
        }
        Check(before==RunConfigArchive.Hash(File.ReadAllBytes(path)),"Read/seek/dispose leaves original world journal bytes untouched");
        var canceled=new CancellationTokenSource();canceled.Cancel();bool cancel=false;try{new RecordedWorldStateReader(folder,canceled.Token);}catch(OperationCanceledException){cancel=true;}Check(cancel,"Canceled imports stop before indexing state evidence");
        rows[0]["deltaTime"]=3;File.WriteAllLines(path,rows.Select(row=>row.ToString(Formatting.None)));Reject(()=>new RecordedWorldStateReader(folder),"Corrupt valid-JSON world evidence is rejected by hash");
        Object.DestroyImmediate(root);Object.DestroyImmediate(clone);Object.DestroyImmediate(future);Object.DestroyImmediate(material);Object.DestroyImmediate(secondMaterial);Object.DestroyImmediate(texture);
    }
    private static JArray ToJArray(this IEnumerable<JToken> values)=>new JArray(values);
    private static void AnimationBlend()
    {
        var prefab=Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog").Find("SimulatedLocust");var original=Object.Instantiate(prefab);var animator=original.GetComponentInChildren<Animator>();animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
        int state=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;animator.Play(state,0,.1f);animator.Update(0);animator.CrossFade(state,.4f,0,.7f);animator.Update(.05f);
        string evidence=new RecordedAnimationCapture(original).Capture();Check(JObject.Parse(evidence)["realizedPose"] is JArray,"Animator transition archives realized bone pose");
        var replay=Object.Instantiate(prefab);var driver=replay.AddComponent<ReplayAnimationDriver>();driver.Sample(evidence,123);
        var a=original.GetComponentsInChildren<Transform>(true);var b=replay.GetComponentsInChildren<Transform>(true);
        Check(a.Length==b.Length&&a.Zip(b,(x,y)=>Vector3.Distance(x.localPosition,y.localPosition)<.00001f&&Quaternion.Angle(x.localRotation,y.localRotation)<.001f).All(value=>value)&&!driver.TransitionApproximation,"Forward/reverse independent transition replay restores exact realized bones");Object.DestroyImmediate(original);Object.DestroyImmediate(replay);
    }
    private static void OriginalControllers()
    {
        string folder=Path.Combine(output,"native");Directory.CreateDirectory(folder);var archive=new ReplaySessionArchive(folder);
        var host=new GameObject("original optomotor");var controller=host.AddComponent<OptomotorSceneController>();typeof(OptomotorSceneController).GetField("drumPrefab",Hidden).SetValue(controller,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/OptomotorDrum.prefab"));Invoke(controller,"Awake");
        var target=Object.FindObjectsByType<RecordedWorldStateTarget>(FindObjectsSortMode.None).Single(value=>value.Key=="optomotor-drum");Invoke(target.GetComponent<SinusoidalGrating>(),"Awake");Invoke(target.GetComponent<DrumRotator>(),"Awake");
        var config=JObject.Parse("{\"stimuli\":[{\"frequency\":3,\"contrast\":0.9,\"dutyCycle\":0.3,\"color1\":\"#FF0000\",\"color2\":\"#FFFFFF\"},{\"frequency\":11,\"contrast\":0.6,\"dutyCycle\":0.7,\"color1\":\"#0000FF\",\"color2\":\"#FFFFFF\"}]}");
        using(new ReplayExecutionContext(archive,new JObject(),new Dictionary<string,string>(),new Dictionary<string,Texture2D>()))
        {
            controller.InitializeScene(new Dictionary<string,object>{{"configFile","recorded.json"},{"__experimentPhase",config}});target.transform.rotation=Quaternion.Euler(0,37,0);
            var first=target.Capture(true);string pixels=RunConfigArchive.Hash(((Texture2D)target.GetComponent<Renderer>().sharedMaterial.mainTexture).GetRawTextureData());
            controller.ApplyReplayStimulus(1);target.transform.rotation=Quaternion.Euler(0,123,0);var second=target.Capture(true);
            Check(pixels!=RunConfigArchive.Hash(((Texture2D)target.GetComponent<Renderer>().sharedMaterial.mainTexture).GetRawTextureData()),"Original drum controller generates distinct textures for real stimuli");
            target.Apply(first);Check(controller.ReplayStimulusIndex==0&&Quaternion.Angle(target.transform.rotation,Quaternion.Euler(0,37,0))<.001f&&pixels==RunConfigArchive.Hash(((Texture2D)target.GetComponent<Renderer>().sharedMaterial.mainTexture).GetRawTextureData()),"Original drum controller and state participant restore texture and realized rotation on backward seek");
            target.Apply(second);Check(controller.ReplayStimulusIndex==1,"Forward seek reinstates second native drum stimulus");
            var nativeSequence=(System.Collections.IEnumerator)typeof(OptomotorSceneController).GetMethod("RunStimulusSequence",Hidden).Invoke(controller,null);
            nativeSequence.MoveNext();nativeSequence.MoveNext();bool running=nativeSequence.MoveNext();
            var completed=target.Capture(true);
            Check(!running&&controller.ReplayStimulusIndex==1,"Non-loop drum completion retains the actually displayed last stimulus rather than next sequence pointer");
            string finalPixels=RunConfigArchive.Hash(((Texture2D)target.GetComponent<Renderer>().sharedMaterial.mainTexture).GetRawTextureData());
            target.Apply(first);target.Apply(completed);
            Check(finalPixels==RunConfigArchive.Hash(((Texture2D)target.GetComponent<Renderer>().sharedMaterial.mainTexture).GetRawTextureData()),"Completed multi-stimulus sequence restores last realized texture after reverse seek");
        }
        Object.DestroyImmediate(target.gameObject);Object.DestroyImmediate(host);
    }
    private static void OriginalPopulationFactory()
    {
        string folder=Path.Combine(output,"population-factory");Directory.CreateDirectory(folder);
        var identity=PlayerBuildIdentity.Capture();File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.ContractName),new JObject{["schemaVersion"]=1,["player"]=identity}.ToString());
        var entry=new JObject{["id"]="population",["config"]=new JObject()};entry["sha256"]=ReplayPresentationJournal.HashEntry(entry);File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.FileName),entry.ToString(Formatting.None));
        var archive=new ReplaySessionArchive(folder);var catalog=Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        var rig=new GameObject("VR1");var original=rig.AddComponent<Kannadi>();original.tilePrefab=catalog.Find("SimulatedLocust");
        var selected=original.ResolvePrefab(new KannadiConfig {kannadiTilePrefab="BrownCylinder",populationMode="Swarm"});
        Check(selected!=null&&selected!=original.tilePrefab,"Original Kannadi Swarm factory resolves a nondefault selected model");
        var loader=new GameObject("factory replay").AddComponent<ReplayEnvironmentLoader>();
        typeof(ReplayEnvironmentLoader).GetField("archive",Hidden).SetValue(loader,archive);
        typeof(ReplayEnvironmentLoader).GetField("currentConfig",Hidden).SetValue(loader,new JObject{["populationMode"]="Swarm"});
        typeof(ReplayEnvironmentLoader).GetField("currentPresentation",Hidden).SetValue(loader,new JObject{["populationVisuals"]=new JArray(new JObject{["family"]="Kannadi",["kind"]="Swarm",["rig"]="VR1",["layer"]="SimulatedLocustsVR1",["prefab"]=selected.name})});
        ((Dictionary<string,Transform>)typeof(ReplayEnvironmentLoader).GetField("importedRigs",Hidden).GetValue(loader))["VR1"]=rig.transform;
        var visual=loader.CreateAgentVisual("Swarm","recorded","SimulatedLocustsVR1",loader.transform,.1f);
        var actualMeshes=visual.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh).ToArray();
        var expectedMeshes=selected.GetComponentsInChildren<MeshFilter>(true).Select(filter=>filter.sharedMesh).ToArray();
        Check(actualMeshes.Length>0&&actualMeshes.SequenceEqual(expectedMeshes),"Swarm replay uses actual journaled Kannadi model even when authored phase omits the preserved selection");
        Object.DestroyImmediate(visual);Object.DestroyImmediate(loader.gameObject);Object.DestroyImmediate(rig);
    }
    private static void AsynchronousParentFrame()
    {
        string folder=Path.Combine(output,"parent-frame");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.ContractName),new JObject{["schemaVersion"]=1,["player"]=PlayerBuildIdentity.Capture()}.ToString());
        var journal=new JObject{["id"]="a",["config"]=new JObject()};journal["sha256"]=ReplayPresentationJournal.HashEntry(journal);File.WriteAllText(Path.Combine(folder,ReplayPresentationJournal.FileName),journal.ToString(Formatting.None));
        var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.name="Cube";
        var controller=new GameObject("Native controller parent").AddComponent<DynamicSequenceController>();controller.prefabs=new[]{prefab};controller.materials=Array.Empty<Material>();Invoke(controller,"Awake");
        controller.transform.SetPositionAndRotation(new Vector3(6,8,10),Quaternion.Euler(0,20,0));controller.transform.localScale=Vector3.one*1.5f;
        var sourceRoot=new GameObject("live container");sourceRoot.transform.SetParent(controller.transform,true);
        var source=sourceRoot.AddComponent<RecordedWorldStateTarget>();source.Key="dynamic:VR1";
        var step=JObject.Parse("{\"objects\":[{\"type\":\"Cube\",\"pos\":[-4,1,12],\"scale\":{\"x\":2,\"y\":8,\"z\":2}}]}");source.Configuration=step;
        var objectSource=Object.Instantiate(prefab,sourceRoot.transform);objectSource.transform.position=new Vector3(-4,1,12);objectSource.transform.localScale=new Vector3(2,8,2);
        var first=source.Capture(true);Vector3 expectedFirst=objectSource.transform.position;objectSource.transform.position=new Vector3(8,1,12);var second=source.Capture(true);Vector3 expectedSecond=objectSource.transform.position;
        SaveRows(folder,new[]{Row(1,new JArray(first),new JArray("dynamic:VR1")),Row(2,new JArray(second),new JArray("dynamic:VR1"))});
        var archive=new ReplaySessionArchive(folder);var frames=new RecordedWorldStateReader(folder);
        var loader=new GameObject("Nonzero operator loader").AddComponent<ReplayEnvironmentLoader>();loader.transform.position=new Vector3(79,33,21);Invoke(loader,"Awake");
        typeof(ReplayEnvironmentLoader).GetField("archive",Hidden).SetValue(loader,archive);typeof(ReplayEnvironmentLoader).GetField("worldFrames",Hidden).SetValue(loader,frames);
        typeof(ReplayEnvironmentLoader).GetField("importedScene",Hidden).SetValue(loader,controller.gameObject.scene);typeof(ReplayEnvironmentLoader).GetField("currentPresentation",Hidden).SetValue(loader,new JObject());typeof(ReplayEnvironmentLoader).GetField("currentConfig",Hidden).SetValue(loader,new JObject{["steps"]=new JArray(step)});
        var roots=(Dictionary<string,Transform>)typeof(ReplayEnvironmentLoader).GetField("asynchronousRoots",Hidden).GetValue(loader);
        foreach(int frame in new[]{1,2,1})
        {
            var sample=new ReplaySessionArchive.Sample{rig="VR1",presentationId="a",unityFrame=frame,colorsSwapped=frame==2};
            typeof(ReplayEnvironmentLoader).GetMethod("RestoreAsynchronousSample",Hidden).Invoke(loader,new object[]{sample});
            var target=roots["VR1"].GetComponentInChildren<RecordedWorldStateTarget>();frames.Apply("a",frame,new[]{target});
            Check(Vector3.Distance(target.GetComponentInChildren<Renderer>().transform.position,frame==1?expectedFirst:expectedSecond)<.0001f,"Async forward/reverse seek restores native parent coordinate frame with nonzero operator/controller transforms: "+frame);
        }
        typeof(ReplayEnvironmentLoader).GetField("importedScene",Hidden).SetValue(loader,default(UnityEngine.SceneManagement.Scene));
        Object.DestroyImmediate(loader.gameObject);Object.DestroyImmediate(controller.gameObject);Object.DestroyImmediate(prefab);
    }
    private static void UnsupportedEffects()
    {
        var wind=new GameObject("unsupported native wind").AddComponent<WindZone>();
        Reject(()=>ReplayEvidenceSupport.RequireSupportedScene(wind.gameObject.scene),"Unowned native wind is rejected before acquisition evidence can imply faithful replay");
        wind.gameObject.SetActive(false);ReplayEvidenceSupport.RequireSupportedScene(wind.gameObject.scene);Check(true,"Disabled native wind remains an admissible static scene");
        Object.DestroyImmediate(wind.gameObject);
    }
    private static void WorldTargetRegistry()
    {
        var root=new GameObject("registry parent");
        var first=new GameObject("registry first").AddComponent<RecordedWorldStateTarget>();first.Key="z";first.transform.SetParent(root.transform);
        var second=new GameObject("registry second").AddComponent<RecordedWorldStateTarget>();second.Key="a";
        var collect=typeof(RecordedWorldStateTarget).GetMethod("GetActiveTargets",BindingFlags.NonPublic|BindingFlags.Static);
        var targets=new List<RecordedWorldStateTarget>();
        void Collect()=>collect.Invoke(null,new object[]{targets});
        try
        {
            // The editor runner is outside Play Mode, so exercise the runtime
            // registration callbacks explicitly.
            Invoke(first,"Awake");Invoke(second,"Awake");Invoke(first,"OnEnable");first.enabled=false;
            Collect();Check(targets.SequenceEqual(new[]{second,first}),"Registry includes live targets once in current Key order");
            first.Key="0";Collect();Check(targets.SequenceEqual(new[]{first,second}),"Registry observes changed target keys without rediscovery");
            root.SetActive(false);Collect();Check(targets.SequenceEqual(new[]{second}),"Registry excludes a target under an inactive parent");
            root.SetActive(true);Collect();Check(targets.Contains(first),"Registry observes reactivated hierarchies");
            root.AddComponent<RecordedPendingDestroy>();Collect();Check(targets.SequenceEqual(new[]{second}),"Registry excludes pending-destroy ancestors");
            Invoke(second,"OnDestroy");Collect();Check(targets.Count==0,"Registry releases destroyed targets");
        }
        finally
        {
            Invoke(first,"OnDestroy");Invoke(second,"OnDestroy");
            Object.DestroyImmediate(root);Object.DestroyImmediate(second.gameObject);
        }
    }
    private static void RenderClock()
    {
        var camera=new GameObject("clock camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-3);camera.orthographic=true;camera.orthographicSize=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);var material=new Material(Shader.Find("Hidden/MATREX/ValidationClock"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var rt=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);camera.targetTexture=rt;var texture=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
        var clock=camera.gameObject.AddComponent<ReplayRenderClock>();clock.SetClock(2.5f,.125f,.25f);camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,32,32),0,0);texture.Apply();Color first=texture.GetPixel(16,16);
        Check(Mathf.Abs(first.r-.25f)<.002f&&Mathf.Abs(first.g-.125f)<.002f,"GPU original shader receives defined acquisition/replay clock vectors");
        clock.SetClock(7,.2f,.2f);camera.Render();clock.SetClock(2.5f,.125f,.25f);camera.Render();texture.ReadPixels(new Rect(0,0,32,32),0,0);texture.Apply();Check((first-texture.GetPixel(16,16)).maxColorComponent<.001f,"GPU shader time-dependent appearance restores after backward seek");
        ShadowClock(camera,()=>clock.SetClock(2.5f,.125f,.25f));
        // The edit-mode fixture installed buffers without running MonoBehaviour activation.
        Invoke(clock,"OnDestroy");Object.DestroyImmediate(clock);
        AcquisitionClock(camera,rt,texture,first);
        RenderTexture.active=prior;camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(quad);Object.DestroyImmediate(material);Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);
    }
    private static void AcquisitionClock(Camera first,RenderTexture rt,Texture2D texture,Color expected)
    {
        var type=typeof(ReplayRenderClock).GetNestedType("Acquisition",BindingFlags.NonPublic);
        var evaluate=typeof(ReplayRenderClock).GetMethod("Evaluate",BindingFlags.Static|BindingFlags.NonPublic);
        var update=type.GetMethod("Update");
        var second=new GameObject("second acquisition clock camera").AddComponent<Camera>();second.CopyFrom(first);second.transform.SetPositionAndRotation(first.transform.position,first.transform.rotation);second.targetTexture=rt;
        Camera manual=null;
        int firstPrior=first.commandBufferCount,secondPrior=second.commandBufferCount,manualPrior=-1;
        void CheckPixel(Camera camera,string message)
        {
            camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,32,32),0,0);texture.Apply();
            Check((texture.GetPixel(16,16)-expected).maxColorComponent<.001f&&(expected-texture.GetPixel(16,16)).maxColorComponent<.001f,message);
        }
        try
        {
            using(var clock=(IDisposable)Activator.CreateInstance(type))
            {
                void Install()=>update.Invoke(clock,new[]{evaluate.Invoke(null,new object[]{2.5f,.125f,.25f})});
                Install();
                CheckPixel(first,"First acquisition camera uses the shared shader clock");
                CheckPixel(second,"Second acquisition camera renders the same recorded shader clock");
                manual=new GameObject("new disabled manual clock camera").AddComponent<Camera>();manual.CopyFrom(first);manual.transform.SetPositionAndRotation(first.transform.position,first.transform.rotation);manual.targetTexture=rt;manual.enabled=false;
                manualPrior=manual.commandBufferCount;
                CheckPixel(manual,"A newly created disabled manual camera gets the clock on its first render");
                ShadowClock(first,Install);
            }
            Check(first.commandBufferCount==firstPrior&&second.commandBufferCount==secondPrior&&manual.commandBufferCount==manualPrior,
                $"Sealing releases acquisition buffers from enabled and manual cameras ({firstPrior}/{first.commandBufferCount}, {secondPrior}/{second.commandBufferCount}, {manualPrior}/{manual.commandBufferCount})");
        }
        finally
        {
            if(manual!=null)Object.DestroyImmediate(manual.gameObject);
            Object.DestroyImmediate(second.gameObject);
        }
    }
    private static void ShadowClock(Camera camera,Action installClock)
    {
        var sun=new GameObject("shadow clock sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Hard;sun.transform.rotation=Quaternion.Euler(40,-30,0);sun.shadowCustomResolution=256;
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.localScale=Vector3.one*.4f;cube.transform.position=new Vector3(.5f,0,-.5f);cube.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard"));
        var previous=QualitySettings.shadows;float distance=QualitySettings.shadowDistance;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=30;
        var rt=new RenderTexture(8,8,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();var read=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);
        var material=new Material(Shader.Find("Hidden/MATREX/ValidationShadowClock"));var probe=new UnityEngine.Rendering.CommandBuffer{name="Validate actual shadow pass clock"};
        installClock();
        probe.Blit(Texture2D.blackTexture,rt,material);sun.AddCommandBuffer(UnityEngine.Rendering.LightEvent.BeforeShadowMap,probe);
        camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,8,8),0,0);read.Apply();Color value=read.GetPixel(4,4);
        Check(Mathf.Abs(value.r-.25f)<.002f&&Mathf.Abs(value.g-.125f)<.002f,"GPU directional shadow-map pass consumes the recorded clock before caster rendering");
        RenderTexture.active=active;sun.RemoveCommandBuffer(UnityEngine.Rendering.LightEvent.BeforeShadowMap,probe);probe.Release();QualitySettings.shadows=previous;QualitySettings.shadowDistance=distance;
        Object.DestroyImmediate(cube.GetComponent<Renderer>().sharedMaterial);Object.DestroyImmediate(cube);Object.DestroyImmediate(sun.gameObject);Object.DestroyImmediate(material);Object.DestroyImmediate(rt);Object.DestroyImmediate(read);
    }
}
public sealed class SameBuildReplayProbe:MonoBehaviour,IReplayStateParticipant
{
    public int value;
    public JToken CaptureReplayState()=>new JValue(value);
    public void RestoreReplayState(JToken token)=>value=(int)token;
}
#endif
