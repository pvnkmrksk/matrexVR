using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restores saved objects, built scene geometry and sky images without starting acquisition or rerunning random populations.</summary>
public class ReplayEnvironmentLoader : MonoBehaviour
{
    public string designFileName;
    public GameObject[] prefabs = Array.Empty<GameObject>();
    public Material[] materials = Array.Empty<Material>();
    public Transform environmentRoot;
    public string FidelityStatus { get; private set; } = "No session loaded";
    public bool Loading { get; private set; }
    public int ContextVersion { get; private set; }
    public IReadOnlyDictionary<string,Transform> RecordedRigs => importedRigs;

    [Serializable]
    public class CameraSpec
    {
        public string vrId;
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
        public float[] bgColor;
    }

    private readonly Dictionary<string,GameObject> prefabDict = new Dictionary<string,GameObject>();
    private readonly Dictionary<string,Material> materialDict = new Dictionary<string,Material>();
    private readonly Dictionary<string,Transform> importedRigs = new Dictionary<string,Transform>();
    private readonly Dictionary<string,Camera> importedFrontCameras = new Dictionary<string,Camera>();
    private readonly Dictionary<string,string> cameraStateKeys = new Dictionary<string,string>();
    private readonly List<string> rigIds = new List<string>();
    private readonly Dictionary<Light,bool> previewLights = new Dictionary<Light,bool>();
    private readonly Dictionary<string,JObject> legacySteps = new Dictionary<string,JObject>();
    private readonly Dictionary<string,string> asynchronousKeys = new Dictionary<string,string>();
    private readonly Dictionary<string,ReplaySessionArchive.Sample> latestAsynchronousSamples = new Dictionary<string,ReplaySessionArchive.Sample>();
    private readonly Dictionary<string,Transform> asynchronousRoots = new Dictionary<string,Transform>();
    private ReplaySessionArchive archive;
    private ReplaySessionArchive.Sample requested;
    private Scene importedScene;
    private string importedName, appliedKey;
    private JObject currentConfig;
    private Material skyMaterial, previousSky;
    private Texture2D skyTexture;
    private string currentSky;
    private Material importedDefaultSky;
    private Color importedAmbientLight, importedAmbientSky, importedAmbientEquator, importedAmbientGround, importedFogColor;
    private UnityEngine.Rendering.AmbientMode importedAmbientMode;
    private bool importedFog;
    private float importedFogDensity, importedFogStart, importedFogEnd;

    private void Awake()
    {
        IndexAssets(prefabs,materials);
        if(environmentRoot==null)
        {
            environmentRoot=new GameObject("Replay environment objects").transform;
            environmentRoot.SetParent(transform,false);
        }
        previousSky=RenderSettings.skybox;
        foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var light in root.GetComponentsInChildren<Light>(true))previewLights[light]=light.enabled;
    }

    public void BeginSession(ReplaySessionArchive session,IEnumerable<string> ids)
    {
        archive=session;SetRigIds(ids);requested=null;appliedKey=null;currentConfig=null;importedName=null;cameraStateKeys.Clear();latestAsynchronousSamples.Clear();
        ClearEnvironment();ReleaseSky();
        FidelityStatus="Saved configurations loaded; waiting for recorded scene";
    }
    public void SetRigIds(IEnumerable<string> ids){rigIds.Clear();if(ids!=null)rigIds.AddRange(ids);}
    private void IndexAssets(IEnumerable<GameObject> objects,IEnumerable<Material> sourceMaterials)
    {
        foreach(var prefab in objects??Array.Empty<GameObject>())if(prefab!=null)prefabDict[prefab.name]=prefab;
        foreach(var material in sourceMaterials??Array.Empty<Material>())if(material!=null)materialDict[material.name]=material;
    }

    public void ApplyRecordedSample(ReplaySessionArchive.Sample sample)
    {
        requested=sample;
        if(!Loading && requested!=null && (appliedKey!=requested.ContextKey || currentSky!=requested.skyboxId)) StartCoroutine(Rebuild());
    }

    private IEnumerator Rebuild()
    {
        Loading=true;
        while(requested!=null && (appliedKey!=requested.ContextKey || currentSky!=requested.skyboxId))
        {
            var target=requested;
            if(importedName!=target.scene)
            {
                ClearEnvironment();importedRigs.Clear();importedFrontCameras.Clear();cameraStateKeys.Clear();
                if(importedScene.IsValid() && importedScene.isLoaded)yield return SceneManager.UnloadSceneAsync(importedScene);
                importedScene=default;importedName=target.scene;
                foreach(string key in prefabDict.Where(p=>p.Value==null).Select(p=>p.Key).ToArray())prefabDict.Remove(key);
                foreach(string key in materialDict.Where(p=>p.Value==null).Select(p=>p.Key).ToArray())materialDict.Remove(key);
                if(!string.IsNullOrEmpty(target.scene) && target.scene!="ReplayScene" && target.scene!="ControlScene" && Application.CanStreamedLevelBeLoaded(target.scene))
                {
                    SceneManager.sceneLoaded+=FreezeLoadedScene;
                    AsyncOperation load=null;
                    try {load=SceneManager.LoadSceneAsync(target.scene,LoadSceneMode.Additive);}
                    catch(Exception error){Warn("Could not load recorded scene: "+error.Message);}
                    if(load!=null)yield return load;
                    SceneManager.sceneLoaded-=FreezeLoadedScene;
                    importedScene=SceneManager.GetSceneByName(target.scene);
                    if(importedScene.IsValid() && importedScene.isLoaded)FreezeLoadedScene(importedScene,LoadSceneMode.Additive);
                }
                else if(target.scene!="ReplayScene")Warn("Recorded scene is not included in this build: "+target.scene+". Showing trajectories and available saved objects.");
            }
            try
            {
                if(appliedKey!=target.ContextKey)
                {
                    ClearEnvironment();
                    currentConfig=archive?.ExperimentFor(target);
                    if(!(currentConfig?["steps"] is JArray))RestoreObjects(currentConfig,target);
                    else foreach(var sample in latestAsynchronousSamples.Values.Where(sample=>sample.scene==target.scene).ToArray())RestoreAsynchronousSample(sample);
                    RestoreRugosity(currentConfig);
                    currentSky=null;
                    ContextVersion++;
                    appliedKey=target.ContextKey;
                }
                RestoreSky(target.skyboxId,currentConfig);
                currentSky=target.skyboxId;
                FidelityStatus=(importedScene.IsValid()?"Built scene geometry; ":"Trajectory view; ")+
                    (currentConfig!=null?"saved experiment JSON; ":"experiment JSON unavailable; ")+
                    (!string.IsNullOrEmpty(target.skyboxId) && skyTexture!=null?"recorded sky image.":"no managed sky on this row.");
            }
            catch(Exception error)
            {
                Warn("Replay reconstruction: "+error.Message);
                appliedKey=target.ContextKey;currentSky=target.skyboxId;
                FidelityStatus="Partial reconstruction: "+error.Message;
            }
            yield return null;
        }
        Loading=false;
    }

    private void FreezeLoadedScene(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!=importedName || scene==gameObject.scene)return;
        var roots=scene.GetRootGameObjects().Where(root=>root!=null).ToArray();
        var active=roots.ToDictionary(root=>root,root=>root.activeSelf);
        // Missing scripts and optional archived metadata must never leave a later
        // rig alive long enough to start acquisition in the replay scene.
        foreach(var root in roots)root.SetActive(false);
        bool isolated=false;
        try
        {
            foreach(var root in roots)FreezeHierarchy(root,false);
            try
            {
                if(archive?.SystemConfiguration?["configs"] is JArray savedRigs)
                {
                    var configurations=savedRigs.ToObject<SystemConfig[]>().Where(config=>config!=null).ToDictionary(config=>config.vrId,config=>config);
                    RuntimeRigRegistry.Configure(scene,configurations);
                    foreach(var root in roots)if(root.GetComponent<ClosedLoop>()!=null)active[root]=root.activeSelf;
                }
            }
            catch(Exception error){Warn("Recorded rig layout unavailable: "+error.Message);}
            // Registry configuration can clone rigs. Templates were already
            // frozen, and this second pass also covers every generated child.
            foreach(var root in scene.GetRootGameObjects())FreezeHierarchy(root,false);
            isolated=true;
            foreach(var light in previewLights.Keys)if(light!=null)light.enabled=false;
            Scene previous=SceneManager.GetActiveScene();
            if(SceneManager.SetActiveScene(scene))
            {
                try
                {
                    importedDefaultSky=RenderSettings.skybox;importedAmbientMode=RenderSettings.ambientMode;
                    importedAmbientLight=RenderSettings.ambientLight;importedAmbientSky=RenderSettings.ambientSkyColor;
                    importedAmbientEquator=RenderSettings.ambientEquatorColor;importedAmbientGround=RenderSettings.ambientGroundColor;
                    importedFog=RenderSettings.fog;importedFogColor=RenderSettings.fogColor;importedFogDensity=RenderSettings.fogDensity;
                    importedFogStart=RenderSettings.fogStartDistance;importedFogEnd=RenderSettings.fogEndDistance;
                }
                finally{if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);}
            }
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root==null)continue;
                foreach(var choice in root.GetComponentsInChildren<ChoiceController>(true))if(choice!=null)IndexAssets(choice.prefabs,choice.materials);
                foreach(var dynamic in root.GetComponentsInChildren<DynamicSequenceController>(true))if(dynamic!=null)IndexAssets(dynamic.prefabs,dynamic.materials);
                foreach(var rig in root.GetComponentsInChildren<ClosedLoop>(true))
                {
                    if(rig==null)continue;
                    string id=CanonicalRig(rig.name);importedRigs[id]=rig.transform;
                    var front=rig.GetComponentsInChildren<Camera>(true).FirstOrDefault(camera=>camera!=null && camera.name=="Main Camera F");
                    if(front!=null)importedFrontCameras[id]=front;
                }
            }
        }
        catch(Exception error){Warn("Recorded scene isolation/reconstruction: "+error.Message);}
        finally
        {
            if(isolated){foreach(var root in roots)if(root!=null)root.SetActive(active[root]);}
            else foreach(var root in scene.GetRootGameObjects())if(root!=null)root.SetActive(false);
        }
    }

    private static void FreezeHierarchy(GameObject root,bool visual)
    {
        if(root==null)return;
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            if(script!=null && (!visual || !(script is RuntimeMaterialOwner)))script.enabled=false;
        foreach(var camera in root.GetComponentsInChildren<Camera>(true))if(camera!=null)camera.enabled=false;
        foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))if(listener!=null)listener.enabled=false;
        foreach(var animator in root.GetComponentsInChildren<Animator>(true))if(animator!=null)animator.enabled=false;
        foreach(var body in root.GetComponentsInChildren<Rigidbody>(true))if(body!=null)body.isKinematic=true;
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))if(collider!=null)collider.enabled=false;
        if(visual)foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer!=null)renderer.enabled=true;
    }

    public void DriveRecordedRig(string id,Vector3 position,Quaternion rotation)
    {
        if(importedRigs.TryGetValue(CanonicalRig(id),out var rig) && rig!=null)rig.SetPositionAndRotation(position,rotation);
    }

    public void ConfigureAnimalCamera(string id,Camera target,ReplaySessionArchive.Sample sample=null)
    {
        id=CanonicalRig(id);
        if(target==null || !importedFrontCameras.TryGetValue(id,out var source) || source==null || !importedRigs.TryGetValue(id,out var rig))return;
        target.transform.localPosition=rig.InverseTransformPoint(source.transform.position);
        target.transform.localRotation=Quaternion.Inverse(rig.rotation)*source.transform.rotation;
        target.fieldOfView=source.fieldOfView;target.nearClipPlane=source.nearClipPlane;target.farClipPlane=source.farClipPlane;
        target.allowHDR=source.allowHDR;target.allowMSAA=source.allowMSAA;target.renderingPath=source.renderingPath;
        var saved=(archive?.SystemConfiguration?["configs"] as JArray)?.OfType<JObject>().FirstOrDefault(config=>(string)config["vrId"]==id);
        // The configured source camera also covers desktop previews and custom
        // projections; panel dimensions alone would turn a monitor view square.
        float aspect=source.aspect;
        if(float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect<=0)
            aspect=Mathf.Max(.01f,((float?)saved?["ledPanelWidth"]??128)/Mathf.Max(1,(float?)saved?["ledPanelHeight"]??128));
        target.aspect=aspect;
        target.projectionMatrix=source.projectionMatrix;
        float available=Mathf.Max(.01f,Screen.width/(Screen.height*.8f));
        target.rect=aspect<=available?new Rect((1-aspect/available)*.5f,.20f,aspect/available,.80f):new Rect(0,.20f+(.80f-.80f*available/aspect)*.5f,1,.80f*available/aspect);
        string key=ContextVersion+"|"+sample?.StimulusKey;
        if(cameraStateKeys.TryGetValue(id,out string previous) && previous==key)return;
        cameraStateKeys[id]=key;target.clearFlags=source.clearFlags;target.backgroundColor=source.backgroundColor;
        JObject step=sample!=null?ReplayStimulusState.Step(currentConfig,sample,out _):currentConfig;
        if(step?["backgroundColor"] is JObject background)
            target.backgroundColor=new Color((float?)background["r"]??0,(float?)background["g"]??0,(float?)background["b"]??0,(float?)background["a"]??1);
        if(step?["camera"] is JArray cameras)
            foreach(var camera in cameras.OfType<JObject>().Where(camera=>(string)camera["vrId"]==id))
            {
                if(camera["clearFlags"]!=null)target.clearFlags=camera["clearFlags"].ToObject<CameraClearFlags>();
                if(camera["bgColor"] is JArray color && color.Count>=3)target.backgroundColor=ColorValue(color);
            }
        if(skyTexture!=null)target.clearFlags=CameraClearFlags.Skybox;
    }

    public void ApplyAsynchronousSample(ReplaySessionArchive.Sample sample)
    {
        latestAsynchronousSamples[sample.rig]=sample;
        if(Loading || !(currentConfig?["steps"] is JArray) || sample.scene!=importedName)return;
        RestoreAsynchronousSample(sample);
    }
    private void RestoreAsynchronousSample(ReplaySessionArchive.Sample sample)
    {
        if(asynchronousKeys.TryGetValue(sample.rig,out string previous) && previous==sample.StimulusKey)return;
        if(asynchronousRoots.TryGetValue(sample.rig,out var old) && old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
        var root=new GameObject("Recorded condition "+sample.rig).transform;root.SetParent(environmentRoot,false);
        asynchronousRoots[sample.rig]=root;asynchronousKeys[sample.rig]=sample.StimulusKey;
        try{RestoreObjects(currentConfig,sample,root);}
        catch(Exception error){Warn("Recorded asynchronous condition: "+error.Message);}
    }
    public static string CanonicalRig(string name)=>ReplaySessionArchive.CanonicalRig(name);

    public static int ObjectLayerForRig(string id)
    {
        int layer=LayerMask.NameToLayer("Choice"+CanonicalRig(id));
        return layer>=0?layer:RuntimeRigRegistry.StimulusLayer(RuntimeRigRegistry.ParseIndex(CanonicalRig(id)));
    }

    public static Vector3 ResolveRecordedPosition(JObject specification)
    {
        if(specification["polar"] is JObject polar)return PolarPosition(polar);
        if(specification["position"] is JObject position)
        {
            if(position["x"]?.Type!=null && position["x"].Type!=JTokenType.Null && position["y"]?.Type!=null && position["y"].Type!=JTokenType.Null && position["z"]?.Type!=null && position["z"].Type!=JTokenType.Null)
                return new Vector3((float)position["x"],(float)position["y"],(float)position["z"]);
            return PolarPosition(position);
        }
        return Vector(specification["pos"]);
    }
    private static Vector3 GroundRecordedPosition(Vector3 position)
    {
        foreach(Terrain terrain in Terrain.activeTerrains)
        {
            if(terrain==null || terrain.terrainData==null)continue;
            Vector3 local=position-terrain.transform.position,size=terrain.terrainData.size;
            if(local.x>=0 && local.z>=0 && local.x<=size.x && local.z<=size.z)
            {position.y+=terrain.SampleHeight(position)+terrain.transform.position.y;break;}
        }
        return position;
    }
    private static Vector3 PolarPosition(JObject position)
    {
        double angle=((double?)position["angle"]??0)*Mathf.Deg2Rad,radius=(double?)position["radius"]??0;
        return new Vector3((float)(Math.Sin(angle)*radius),(float?)position["height"]??0,(float)(Math.Cos(angle)*radius));
    }

    private void RestoreObjects(JObject config,ReplaySessionArchive.Sample sample,Transform destination=null)
    {
        if(config==null)return;
        JObject step=ReplayStimulusState.Step(config,sample,out string decisionWarning);
        if(decisionWarning!=null)Warn(decisionWarning);
        if(step==null || !(step["objects"] is JArray objects))return;
        var rendered = new Dictionary<string,List<KeyValuePair<Renderer,JObject>>>();
        foreach(var spec in objects.OfType<JObject>())
        {
            string prefabName=(string)spec["type"]??(string)spec["prefab"];
            if(string.IsNullOrEmpty(prefabName))continue;
            if(prefabName.IndexOf("band",StringComparison.OrdinalIgnoreCase)>=0)
            {Warn("Band populations use their recorded agent tracks; live random band spawning is disabled.");continue;}
            if(!prefabDict.TryGetValue(prefabName,out var prefab) || prefab==null)
            {Warn("Replay prefab not present in built scene catalog: "+prefabName);continue;}
            foreach(string rig in config["steps"] is JArray?new List<string>{sample.rig}:rigIds.Count>0?rigIds:new List<string>{"VR1"})
            {
                var go=Instantiate(prefab,destination??environmentRoot);go.name="Replay "+prefabName+" "+rig;
                FreezeVisual(go);
                go.transform.position=ResolveRecordedPosition(spec);
                if((bool?)spec["groundOnTerrain"]==true)go.transform.position=GroundRecordedPosition(go.transform.position);
                Vector3 rotation=spec["rotation"] is JObject r && r["x"]?.Type!=null && r["x"].Type!=JTokenType.Null && r["y"]?.Type!=null && r["y"].Type!=JTokenType.Null && r["z"]?.Type!=null && r["z"].Type!=JTokenType.Null?
                    new Vector3((float)r["x"],(float)r["y"],(float)r["z"]):new Vector3(0,(float?)spec["mu"]??0,0);
                go.transform.rotation=Quaternion.Euler(rotation);
                if(spec["scale"] is JObject scale)go.transform.localScale=new Vector3((float?)scale["x"]??1,(float?)scale["y"]??1,(float?)scale["z"]??1);
                if((bool?)spec["flip"]==true){var scaleValue=go.transform.localScale;scaleValue.x*=-1;go.transform.localScale=scaleValue;}
                int layer=ObjectLayerForRig(rig);if(layer>=0)SetLayer(go,layer);
                var renderer=go.GetComponent<Renderer>();
                if(renderer!=null)
                {
                    string materialName=(string)spec["material"]??(string)spec["mat"];
                    materialDict.TryGetValue(materialName??"",out var material);
                    var owned=RuntimeMaterialOwner.Assign(renderer,material);
                    if(owned!=null && spec["color"] is JArray color && color.Count>=3)owned.color=ColorValue(color);
                    if(!rendered.TryGetValue(rig,out var ordered))rendered[rig]=ordered=new List<KeyValuePair<Renderer,JObject>>();
                    ordered.Add(new KeyValuePair<Renderer,JObject>(renderer,spec));
                }
                if((bool?)spec["randomInitialRotation"]==true || (bool?)spec["useHeadingReference"]==true)
                    Warn("Randomized/heading-relative object onsets are not fully specified by static JSON. Refer to recorded onset logs; object reconstruction is approximate.");
            }
        }
        if(sample.colorsSwapped)
            foreach(var rig in rendered.Values)
            {
                bool explicitColors=rig.Any(pair=>pair.Value["swapColor"] is JArray color && color.Count>=3);
                if(explicitColors)
                {
                    foreach(var pair in rig)
                        if(pair.Key!=null && pair.Key.sharedMaterial!=null && pair.Value["swapColor"] is JArray color && color.Count>=3)
                            pair.Key.sharedMaterial.color=ColorValue(color);
                }
                else if(rig.Count>=2 && rig[0].Key!=null && rig[1].Key!=null)
                {
                    Color first=rig[0].Key.sharedMaterial.color;
                    rig[0].Key.sharedMaterial.color=rig[1].Key.sharedMaterial.color;
                    rig[1].Key.sharedMaterial.color=first;
                }
            }
    }

    private void RestoreRugosity(JObject config)
    {
        if(!(config?["rugosity"] is JObject rugosity) || !importedScene.IsValid())return;
        string visual=(string)rugosity["visualSurface"];
        if(string.IsNullOrEmpty(visual))return;
        foreach(var root in importedScene.GetRootGameObjects())
            foreach(var surface in root.GetComponentsInChildren<RugositySurface>(true))surface.SetRoles(surface.surfaceId==visual,false);
    }

    private void RestoreSky(string id,JObject config)
    {
        if(currentSky==id && (skyTexture!=null || string.IsNullOrEmpty(id)))return;
        ReleaseSky();
        RenderSettings.ambientMode=importedAmbientMode;RenderSettings.ambientLight=importedAmbientLight;
        RenderSettings.ambientSkyColor=importedAmbientSky;RenderSettings.ambientEquatorColor=importedAmbientEquator;
        RenderSettings.ambientGroundColor=importedAmbientGround;RenderSettings.fog=importedFog;
        RenderSettings.fogColor=importedFogColor;RenderSettings.fogDensity=importedFogDensity;
        RenderSettings.fogStartDistance=importedFogStart;RenderSettings.fogEndDistance=importedFogEnd;
        string path=!string.IsNullOrEmpty(id)?archive?.SkyImage(id):null;
        if(path==null && string.IsNullOrEmpty(id) && config?["skyboxPath"]?.Type==JTokenType.String)path=archive?.Resolve((string)config["skyboxPath"]);
        if(path!=null)
        {
            skyTexture=new Texture2D(2,1,TextureFormat.RGBA32,false,false){name="Recorded replay sky"};
            if(!skyTexture.LoadImage(File.ReadAllBytes(path)))throw new InvalidDataException("Saved sky image could not be decoded.");
            skyTexture.wrapModeU=TextureWrapMode.Repeat;skyTexture.wrapModeV=TextureWrapMode.Clamp;
            Shader shader=Resources.Load<Shader>("NightSky/Panorama");
            if(shader==null)throw new InvalidOperationException("Recorded sky shader is missing.");
            skyMaterial=new Material(shader){name="Recorded replay sky material"};skyMaterial.mainTexture=skyTexture;
            RenderSettings.skybox=skyMaterial;
        }
        else
        {
            RenderSettings.skybox=string.IsNullOrEmpty(id)?importedDefaultSky:null;
            if(!string.IsNullOrEmpty(id))Warn("The recorded sky could not be restored; no current-time sky is substituted.");
        }
    }
    private void ReleaseSky()
    {
        if(RenderSettings.skybox==skyMaterial)RenderSettings.skybox=previousSky;
        if(skyMaterial!=null)Destroy(skyMaterial);if(skyTexture!=null)Destroy(skyTexture);
        skyMaterial=null;skyTexture=null;currentSky=null;
    }

    public GameObject CreateAgentVisual(string kind,string name,string layer,Transform parent,float fallbackSize)
    {
        JObject swarm=currentConfig?["swarm"] as JObject??currentConfig;
        if(kind=="Swarm" && string.Equals((string)swarm?["agentVisual"],"Bogong",StringComparison.OrdinalIgnoreCase))
        {
            var agent=new GameObject("Recorded agent "+name);agent.transform.SetParent(parent,false);
            var renderHost=new GameObject("Recorded Bogong visual");renderHost.transform.SetParent(agent.transform,false);
            int agentLayer=RecordedLayer(layer);if(agentLayer>=0){agent.layer=agentLayer;renderHost.layer=agentLayer;}
            var visual=swarm["bogongVisual"]?.ToObject<BogongVisualConfig>()??new BogongVisualConfig();
            // Original wing/flicker phases were not recorded. Freeze them rather than invent a new history.
            visual.flapFrequencyHz=0;visual.flickerFrequencyHz=0;
            renderHost.AddComponent<BogongSwarmRenderer>().Configure(new[]{agent},visual,(float?)swarm["worldUnitsPerMetre"]??1);
            Warn("Recorded Bogong bodies use saved physical dimensions; historical wing/flicker phase is unavailable and is frozen.");
            return agent;
        }
        var catalog=Resources.Load<KannadiPrefabCatalog>("KannadiPrefabCatalog");
        string selection=kind=="Kannadi"?(string)currentConfig?["kannadiTilePrefab"]:(string)swarm?["locustPrefab"];
        GameObject prefab=!string.IsNullOrEmpty(selection)?catalog?.Find(selection):catalog?.Find("SimulatedLocust");
        GameObject go=prefab!=null?Instantiate(prefab,parent):GameObject.CreatePrimitive(PrimitiveType.Capsule);
        if(prefab==null){go.transform.SetParent(parent,false);go.transform.localScale=Vector3.one*fallbackSize;Warn("An agent model was unavailable; pose markers are shown.");}
        go.name="Recorded agent "+name;FreezeVisual(go);
        int layerId=RecordedLayer(layer);if(layerId>=0)SetLayer(go,layerId);
        return go;
    }
    private static int RecordedLayer(string label)
    {
        int layer=LayerMask.NameToLayer(label);
        if(layer>=0)return layer;
        if(label!=null && label.StartsWith("AgentsVR",StringComparison.Ordinal) && int.TryParse(label.Substring(8),out int index))return RuntimeRigRegistry.StimulusLayer(index);
        return -1;
    }
    public static void FreezeVisual(GameObject visual) => FreezeHierarchy(visual,true);

    private static void SetLayer(GameObject root,int layer){root.layer=layer;foreach(Transform child in root.transform)SetLayer(child.gameObject,layer);}
    private static Vector3 Vector(JToken value) => value is JArray array && array.Count>=3?new Vector3((float)array[0],(float)array[1],(float)array[2]):Vector3.zero;
    private static Color ColorValue(JArray array)=>new Color((float)array[0],(float)array[1],(float)array[2],array.Count>3?(float)array[3]:1);
    private void Warn(string message){archive?.Warn(message);}
    private void ClearEnvironment()
    {
        if(environmentRoot!=null)foreach(Transform child in environmentRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        asynchronousKeys.Clear();asynchronousRoots.Clear();
    }

    // Retain inspector/event integrations for older scenes; session replay reads saved JSON above.
    public void LoadDesign(string fileName=null)
    {
        if(!string.IsNullOrEmpty(fileName))designFileName=fileName;
        string path=archive?.Resolve(designFileName);
        if(path==null || !File.Exists(path))return;
        var document=JObject.Parse(File.ReadAllText(path));legacySteps.Clear();
        foreach(var step in (document["steps"] as JArray??new JArray()).OfType<JObject>())legacySteps[(string)step["name"]??""]=step;
    }
    public void ApplyStep(string stepName)
    {
        if(!legacySteps.TryGetValue(stepName,out var step))return;
        ClearEnvironment();RestoreObjects(step,new ReplaySessionArchive.Sample{stepName=stepName});
    }
    public CameraSpec[] GetCameraSpecs(string stepName)=>legacySteps.TryGetValue(stepName,out var step)?step["camera"]?.ToObject<CameraSpec[]>():null;
    private void OnDestroy()
    {
        SceneManager.sceneLoaded-=FreezeLoadedScene;ReleaseSky();
        foreach(var light in previewLights)if(light.Key!=null)light.Key.enabled=light.Value;
        if(importedScene.IsValid() && importedScene.isLoaded)SceneManager.UnloadSceneAsync(importedScene);
    }
}
