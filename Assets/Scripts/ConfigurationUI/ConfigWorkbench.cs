using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Scene-owned setup UI. No Update, panel, file polling or form binding during experiments.</summary>
    public sealed partial class ConfigWorkbench : MonoBehaviour
    {
        public static ConfigWorkbench Instance { get; private set; }
        public static bool IsOpen => Instance!=null && Instance.isActiveAndEnabled;
        public static bool ForceSetup => Environment.GetCommandLineArgs().Contains("-configure")
#if UNITY_EDITOR
            || UnityEditor.SessionState.GetBool("ConfigWorkbench.Preview",false)
#endif
            ;
        public ConfigWorkspace Workspace { get; private set; }
        public UIDocument Document { get; private set; }
        private PanelSettings panel;
        private ConfigDocument selected;
        private readonly List<string> path=new List<string>();
        private VisualElement root,side,body,inspector;
        private Label status,summary,breadcrumb;
        private ListView fields;
        private TextField search,raw;
        private Toggle omitted,inapplicable;
        private Button save,start,undo,redo;
        private bool jsonMode,rawPending,metadataMode,homeMode=true,experimentIndexMode;
        private Label pageLocation,frameRate;
        private VisualElement hoverHelp;
        private Label hoverHelpText;
        private VisualElement helpTarget;
        private IVisualElementScheduledItem helpDelay;
        private string filter="";
        private readonly List<string> rows=new List<string>();
        private FlyData metadata;
        private readonly List<UnityEngine.UI.GraphicRaycaster> legacyRaycasters=new List<UnityEngine.UI.GraphicRaycaster>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Instance=null; SceneManager.sceneLoaded-=Loaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded-=Loaded; SceneManager.sceneLoaded+=Loaded; }
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="ControlScene" || Instance!=null) return;
            // Validation suites may intentionally exercise the legacy control form.
            if(Environment.GetCommandLineArgs().Contains("-no-configuration-ui")) return;
            foreach(var logger in FindObjectsByType<UIDataLogger>(FindObjectsSortMode.None))
                if(logger.gameObject.scene==scene) logger.enabled=false;
            new GameObject("Experiment configuration workbench").AddComponent<ConfigWorkbench>();
        }
        private void Start()
        {
            if(MainController.Instance==null || MainController.Instance.SequenceRunning) { Destroy(gameObject); return; }
            Instance=this;
            try
            {
                Workspace=new ConfigWorkspace(MainController.Instance.SystemConfigurationFileName,MainController.Instance.SequenceConfigurationFileName,ConfigSchema.Load());
                selected=Workspace.Sequence;
                panel=ScriptableObject.CreateInstance<PanelSettings>(); panel.name="Setup only";
                panel.scaleMode=PanelScaleMode.ConstantPixelSize; panel.scale=1; panel.targetDisplay=0; panel.sortingOrder=500;
                panel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("ConfigurationUI/runtime");
                Document=gameObject.AddComponent<UIDocument>(); Document.panelSettings=panel;
                root=Document.rootVisualElement; root.AddToClassList("workbench");
                root.styleSheets.Add(Resources.Load<StyleSheet>("ConfigurationUI/workbench"));
                root.RegisterCallback<GeometryChangedEvent>(evt=>{
                    ApplyResponsiveLayout(evt.newRect.width);
                });
                BuildShell(); BuildNavigation(); BuildContent();
                // Cover the previous form and stop its raycasts; metadata is generated for the actual rig list.
                foreach(var raycaster in FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None))
                    if(raycaster.gameObject.scene==gameObject.scene && raycaster.enabled) { legacyRaycasters.Add(raycaster); raycaster.enabled=false; }
                root.schedule.Execute(PollFiles).Every(1000);
            }
            catch(Exception error)
            {
                Debug.LogError("Experiment setup could not open: "+error.Message);
                if(root!=null) { root.Clear(); root.Add(new Label("Setup failed: "+error.Message)); }
            }
        }
        private Button Button(string label,Action action,string css=null)
        { var button=new Button(()=>Guard(()=>{ FlushNumbers(); action(); })) { text=label }; if(css!=null) button.AddToClassList(css); return button; }
        private void Guard(Action action)
        { try { action(); } catch(Exception error) { if(status!=null) { status.text=error.GetBaseException().Message; status.AddToClassList("error"); } } }
        private VisualElement Row(string css="row") { var row=new VisualElement(); row.AddToClassList("row"); if(css!="row") row.AddToClassList(css); return row; }
        private void BuildShell()
        {
            var header=Row("header");
            var identity=new VisualElement(); identity.AddToClassList("identity"); header.Add(identity);
            var brand=new Label("MATREX"); brand.AddToClassList("brand"); identity.Add(brand);
            var descriptor=new Label("EXPERIMENT CONTROL"); descriptor.AddToClassList("brand-detail"); identity.Add(descriptor);
            pageLocation=new Label(); pageLocation.AddToClassList("page-location"); header.Add(pageLocation);
            var badge=new Label("●  SETUP"); badge.AddToClassList("badge"); badge.tooltip="Setup is open. The experiment starts when you choose Start experiment."; header.Add(badge);
            frameRate=new Label("— FPS"){name="setup-frame-rate"}; frameRate.AddToClassList("frame-rate"); header.Add(frameRate);
            root.Add(header);
            var main=Row("main"); root.Add(main);
            side=new ScrollView { horizontalScrollerVisibility=ScrollerVisibility.Hidden }; side.name="setup-navigation"; side.AddToClassList("sidebar"); main.Add(side);
            body=new VisualElement(); body.AddToClassList("body"); main.Add(body);
            inspector=new ScrollView { horizontalScrollerVisibility=ScrollerVisibility.Hidden }; inspector.AddToClassList("inspector"); main.Add(inspector);
            var footer=Row("footer"); root.Add(footer);
            status=new Label(); status.AddToClassList("status"); footer.Add(status);
            undo=Button("Undo",()=>{ RequireApplied(); selected.Undo(); BuildContent(); }); footer.Add(undo);
            redo=Button("Redo",()=>{ RequireApplied(); selected.Redo(); BuildContent(); }); footer.Add(redo);
            save=StudioButton("Save setup","file",()=>{ RequireApplied(); Workspace.SaveAll(); SaveMetadata(); UpdateStatus("Setup and session details saved."); }); footer.Add(save);
            start=StudioButton("Start experiment","play",StartExperiment,"primary"); footer.Add(start);
            save.tooltip="Save the system, sequence, edited experiment files and subject metadata. Omitted values and unknown JSON fields are preserved.";
            start.tooltip="Validate and save this setup, archive the run's configuration files, then start the experiment.";
            undo.tooltip="Undo the last change in the current configuration file."; redo.tooltip="Redo the last undone change in the current configuration file.";
            InstallHoverHelp();
            root.schedule.Execute(UpdateFrameRate).Every(500); UpdateFrameRate();
        }
        private void BuildNavigation() { Workspace.RefreshReferences(); BuildStudioNavigation(); }
        private void Select(ConfigDocument doc)
        { RequireApplied(); if(selected!=doc) { activePhase="stimulus"; settingsTab="Essentials"; } selected=doc; path.Clear(); homeMode=false; experimentIndexMode=false; metadataMode=false; jsonMode=false; advancedMode=false; filter=""; BuildNavigation(); BuildContent(); }
        private void RequireApplied() { FlushNumbers(); if(rawPending) throw new InvalidOperationException("Apply the JSON draft or discard it before saving, navigating or starting."); }
        private JToken Current => ConfigDocument.At(selected.Root,path);
        private JObject Context => new JObject { ["flight"]=(Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>().Any(r=>string.Equals(r["closedLoopMode"]?.ToString(),"Kinefly",StringComparison.OrdinalIgnoreCase) || r["closedLoopMode"]?.ToString()=="1") };
        private JObject CurrentSchema
        {
            get
            {
                JObject schema=Workspace.Schemas.Get(selected.SchemaId); JToken token=selected.Root;
                foreach(string key in path)
                {
                    if(key=="parameters" && selected.SchemaId=="sequence" && token is JObject step)
                    {
                        schema=ConfigSchema.Child(schema,key);
                        string id=ConfigSchema.ForScene((string)step["sceneName"]);
                        if(id=="swarm" || id=="kannadi")
                        {
                            schema=(JObject)schema.DeepClone();
                            var properties=(JObject)schema["properties"];
                            foreach(var property in ((JObject)Workspace.Schemas.Get(id)["properties"]).Properties())
                                if(!new[]{"preStimulus","stimulus","postStimulus","autopilotEnabled","autopilotSpeed","headingReference"}.Contains(property.Name)) properties[property.Name]=property.Value.DeepClone();
                        }
                    }
                    else schema=ConfigSchema.Child(schema,key,token is JArray);
                    token=token is JArray a?a[int.Parse(key)]:token?[key];
                }
                return schema;
            }
        }
        private void BuildContent()
        {
            numberFlushes.Clear(); blockList=null;
            enableUpdates.Clear();
            body.Clear(); inspector.Clear(); rawPending=false;
            HideHoverHelp();
            body.EnableInClassList("session-body",homeMode || experimentIndexMode);
            side.style.display=DisplayStyle.Flex;
            inspector.style.display=homeMode || experimentIndexMode || root.resolvedStyle.width<1220?DisplayStyle.None:DisplayStyle.Flex;
            pageLocation.text=homeMode?"Session / Setup":experimentIndexMode?"Experiment configs / Library":selected==Workspace.System?"System config / Hardware":selected==Workspace.Sequence?"Sequence config / Run plan":"Experiment configs / "+ExperimentBlocks.Title(selected.Path);
            undo.style.display=redo.style.display=homeMode?DisplayStyle.None:DisplayStyle.Flex;
            if(homeMode) { BuildSessionHome(); return; }
            if(experimentIndexMode) { BuildExperimentConfigs(); return; }
            if(metadataMode) { BuildMetadata(); return; }
            if(Current==null) path.Clear();
            if(!jsonMode && !advancedMode && path.Count==0) { BuildStudio(); return; }
            var title=new Label(selected.SchemaId=="system"?"System & VR rigs":selected.SchemaId=="sequence"?"Experiment sequence":Path.GetFileNameWithoutExtension(selected.Path)); title.AddToClassList("title"); body.Add(title);
            var location=new Label(selected.Path); location.AddToClassList("location"); body.Add(location);
            var toolbar=Row(); body.Add(toolbar);
            toolbar.Add(Button("← Builder",()=>{ RequireApplied(); jsonMode=false; advancedMode=false; path.Clear(); BuildContent(); }));
            toolbar.Add(Button("JSON",()=>{ RequireApplied(); jsonMode=true; BuildContent(); }));
            toolbar.Add(Button("Reload disk",()=>{ selected.Reload(); path.Clear(); BuildContent(); BuildNavigation(); }));
            if(jsonMode)
            {
                raw=new TextField { multiline=true,value=selected.Root.ToString(Formatting.Indented) }; raw.AddToClassList("raw-json"); body.Add(raw);
                raw.RegisterValueChangedCallback(e=>{ rawPending=true; status.text="JSON draft — apply to synchronize the form."; save.SetEnabled(false); start.SetEnabled(false); });
                var actions=Row(); actions.Add(Button("Apply JSON",()=>{ selected.ApplyJson(raw.value); rawPending=false; Workspace.RefreshReferences(); BuildNavigation(); UpdateStatus(); }));
                actions.Add(Button("Discard draft",()=>BuildContent())); body.Add(actions);
            }
            else
            {
                breadcrumb=new Label("$"+(path.Count==0?"":" / "+string.Join(" / ",path))); breadcrumb.AddToClassList("breadcrumb"); body.Add(breadcrumb);
                var tools=Row(); body.Add(tools);
                tools.Add(Button("↑ Parent",()=>{ if(path.Count>0) { path.RemoveAt(path.Count-1); BuildContent(); } }));
                search=new TextField { value=filter }; search.AddToClassList("search"); tools.Add(search);
                search.RegisterValueChangedCallback(e=>{ filter=e.newValue; RefreshRows(); });
                omitted=new Toggle("Omitted fields") { value=false }; tools.Add(omitted); omitted.RegisterValueChangedCallback(_=>RefreshRows());
                inapplicable=new Toggle("All modes") { value=false }; tools.Add(inapplicable); inapplicable.RegisterValueChangedCallback(_=>RefreshRows());
                fields=new ListView(rows,64,()=>new VisualElement(),BindRow) { selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight };
                fields.AddToClassList("fields"); body.Add(fields); RefreshRows();
                var add=Row(); body.Add(add);
                if(Current is JArray)
                    add.Add(Button("+ Add item",()=>Change(node=>{
                        var array=(JArray)node; var value=ConfigSchema.NewValue(ConfigSchema.Child(CurrentSchema,"",true));
                        if(value is JObject obj && obj["vrId"]!=null) { int id=1; while(array.OfType<JObject>().Any(o=>(string)o["vrId"]=="VR"+id)) id++; obj["vrId"]="VR"+id; }
                        array.Add(value);
                    })));
                else
                {
                    var key=new TextField { value="newSetting" }; add.Add(key);
                    var type=new DropdownField(new List<string>{"string","number","boolean","object","array","null"},0); add.Add(type);
                    add.Add(Button("+ Add property",()=>Change(node=>{
                        var obj=(JObject)node; if(obj.Property(key.value)!=null || string.IsNullOrWhiteSpace(key.value)) throw new ArgumentException("Use a unique nonempty JSON key.");
                        obj[key.value]=type.value=="null"?JValue.CreateNull():ConfigSchema.NewValue(new JObject { ["type"]=type.value });
                    })));
                }
                // Disable the content of an inherited disabled block, but leave its own enable switch available.
                for(int i=0;i<path.Count;i++)
                {
                    var parent=ConfigDocument.At(selected.Root,path.Take(i).ToArray());
                    if(parent is JObject obj && obj["enabled"]?.Type==JTokenType.Boolean && !(bool)obj["enabled"]) { fields.SetEnabled(false); add.SetEnabled(false); break; }
                }
            }
            inspector.Add(new Label("CONFIGURATION CHECK")); summary=new Label(); inspector.Add(summary);
            inspector.Add(new Label("Lossless JSON editing"));
            inspector.Add(new Label("Existing keys and numeric precision are retained. Missing, null, false and zero remain distinct. Omitted fields are added only when you choose them."));
            inspector.Add(new Label("Run behavior"));
            inspector.Add(new Label("Start saves and validates the referenced files, snapshots a fresh session, then removes this setup panel. Python analysis remains optional."));
            if(selected.SchemaId=="system") inspector.Add(new Label("You can add any number of rig entries here. Runtime scenes currently author VR1–VR4; extra entries require matching scene rigs and controller support."));
            inspector.Add(new Label("Flight / walking"));
            inspector.Add(new Label("Kinefly selects wing-angle flight input. FicTrac selects ball tracking. Choose the corresponding scene and complete experiment template. Scene units: terrain metres, walking centimetres."));
            UpdateStatus();
        }
        private void RefreshRows()
        {
            rows.Clear(); var current=Current; var schema=CurrentSchema;
            if(current is JObject obj)
            {
                var keys=obj.Properties().Select(p=>p.Name).ToList();
                if(omitted.value) keys.AddRange((schema["properties"] as JObject??new JObject()).Properties().Select(p=>p.Name).Where(k=>obj[k]==null));
                rows.AddRange(keys.Where(k=>(inapplicable.value || ConfigSchema.Visible(obj,schema,k,Context)) && (filter.Length==0 || (k+" "+obj[k]?.ToString(Formatting.None)).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0)));
            }
            else if(current is JArray array) rows.AddRange(Enumerable.Range(0,array.Count).Select(i=>i.ToString()).Where(k=>filter.Length==0 || (k+" "+array[int.Parse(k)].ToString(Formatting.None)).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0));
            fields.Rebuild();
        }
        private void Change(Action<JToken> action)
        { int count=Workspace.Documents.Count; selected.Edit(_=>action(Current)); RefreshRows(); UpdateStatus(); if(Workspace.Documents.Count!=count) BuildNavigation(); }
        private static string Human(string key) => Regex.Replace(key,@"([a-z0-9])([A-Z])","$1 $2");
        private void BindRow(VisualElement row,int index)
        {
            row.Clear(); row.AddToClassList("field-row");
            string key=rows[index]; bool array=Current is JArray;
            JToken value=array?Current[int.Parse(key)]:Current[key]; JObject schema=ConfigSchema.Child(CurrentSchema,key,array);
            var caption=new VisualElement(); caption.AddToClassList("caption"); row.Add(caption);
            caption.Add(new Label(array?ItemLabel(value,int.Parse(key)):Pretty(key)));
            var hint=new Label(array?"item "+key:key+(schema["x-ui"]?["unit"]!=null?" · "+schema["x-ui"]["unit"]:"")); hint.AddToClassList("key-hint"); caption.Add(hint);
            row.tooltip=ConfigParameterHelp.Description(key,path.Concat(new[]{key}).ToArray(),schema);
            var edit=Row("field-controls"); row.Add(edit);
            bool enabled=array || ConfigSchema.Enabled(Current,CurrentSchema,key,Context);
            edit.SetEnabled(enabled);
            Action<JToken> replace=v=>Change(node=>{ if(array) ((JArray)node)[int.Parse(key)]=v; else node[key]=v; });
            if(value==null) edit.Add(Button("+ Set value",()=>replace(ConfigSchema.NewValue(schema)),"quiet"));
            else if(value is JContainer && !(value is JValue))
            {
                // JRaw is a scalar JValue; object/array forms are opened lazily.
                edit.Add(Button(value is JArray a?a.Count+" items  →":"Configure  →",()=>{ path.Add(key); BuildContent(); }));
            }
            else if(value.Type==JTokenType.Null)
            { edit.Add(new Label("null · unset / inherited")); edit.Add(Button("Set",()=>replace(ConfigSchema.NewValue(schema)))); }
            else if(value.Type==JTokenType.Boolean)
            {
                var toggle=new Toggle { value=(bool)value }; edit.Add(toggle);
                toggle.RegisterValueChangedCallback(e=>replace(new JValue(e.newValue)));
            }
            else if(schema["enum"] is JArray options && value.Type==JTokenType.String)
            {
                var choices=options.Where(v=>v.Type==JTokenType.String).Select(v=>v.ToString()).ToList(); if(!choices.Contains(value.ToString())) choices.Add(value.ToString());
                var dropdown=new DropdownField(choices,choices.IndexOf(value.ToString())); edit.Add(dropdown); dropdown.RegisterValueChangedCallback(e=>replace(new JValue(e.newValue)));
            }
            else
            {
                var text=new TextField { value=value.ToString(),isDelayed=true }; edit.Add(text);
                text.RegisterValueChangedCallback(e=>Guard(()=>{
                    try
                    {
                        JToken v=value.Type==JTokenType.String?new JValue(e.newValue):LosslessJson.Parse(e.newValue);
                        string kind=LosslessJson.Kind(v); if(value.Type!=JTokenType.String && kind!="number" && kind!="integer") throw new ArgumentException("Enter a JSON number; use the JSON view to change its type.");
                        replace(v);
                    }
                    catch { text.SetValueWithoutNotify(value.ToString()); throw; }
                }));
            }
            if(value!=null)
            {
                if(ConfigSchema.Types(schema).Contains("null") && value.Type!=JTokenType.Null) edit.Add(Button("∅",()=>replace(JValue.CreateNull()),"mini"));
                if(array)
                {
                    int position=int.Parse(key);
                    edit.Add(Button("Copy",()=>Change(node=>((JArray)node).Insert(position+1,node[position].DeepClone())),"mini"));
                    edit.Add(Button("↑",()=>Move(position,-1),"mini")); edit.Add(Button("↓",()=>Move(position,1),"mini"));
                }
                edit.Add(Button("×",()=>Change(node=>{ if(array) ((JArray)node).RemoveAt(int.Parse(key)); else ((JObject)node).Property(key).Remove(); }),"mini"));
            }
        }
        private void Move(int position,int offset)
        { Change(node=>{ var array=(JArray)node; int target=position+offset; if(target<0 || target>=array.Count) return; var value=array[position]; array.RemoveAt(position); array.Insert(target,value); }); }
        private static string ItemLabel(JToken value,int index)
        { return value is JObject obj?(string)(obj["vrId"]??obj["sceneName"]??obj["name"]??obj["type"])??"Item "+(index+1):"Item "+(index+1); }
        private void UpdateStatus(string message=null)
        {
            List<string> errors=Workspace.Validate();
            var warnings=new List<string>();
            if(errors.Count==0) foreach(var step in BuilderSteps.OfType<JObject>())
            {
                var parameters=step["parameters"] as JObject;string file=(string)parameters?["configFile"];
                if(file==null) continue;
                var doc=Workspace.Documents.FirstOrDefault(d=>d.Path==Path.GetFullPath(ExperimentConfigFiles.Resolve(file)));
                if(doc?.Root is JObject payload) warnings.AddRange(ExperimentBlocks.TimingWarnings(payload,ExperimentBlocks.Number(step["duration"])));
            }
            if(summary!=null) summary.text=errors.Count==0?"✓  Configuration ready\n\n"+Workspace.Documents.Count+" files loaded\n"+(Workspace.System.Root["configs"] as JArray)?.Count+" VR entries\n"+(Workspace.Sequence.Root["sequences"] as JArray)?.Count+" sequence steps":string.Join("\n\n",errors.Take(12));
            status.text=message??(errors.Count>0?string.Join(" · ",errors.Take(2)):warnings.Count>0?warnings[0]:Workspace.Documents.Any(d=>d.Dirty)?"Unsaved config changes · saved when you start":"Ready to run · session details saved when you start");
            status.EnableInClassList("error",errors.Count>0);
            status.EnableInClassList("warning",errors.Count==0 && warnings.Count>0);
            start.SetEnabled(errors.Count==0 && !rawPending); save.SetEnabled(!rawPending); undo.SetEnabled(selected.CanUndo && !rawPending); redo.SetEnabled(selected.CanRedo && !rawPending);
        }
        private void PollFiles()
        {
            if(MainController.Instance==null || MainController.Instance.SequenceRunning) { Destroy(gameObject); return; }
            bool changed=false;
            Guard(()=>{
                foreach(var doc in Workspace.Documents.ToArray())
                    if(!(rawPending && doc==selected)) changed|=doc.CheckExternalChange();
                if(changed) { if(!rawPending) BuildContent(); BuildNavigation(); UpdateStatus("External JSON changes loaded."); }
                else if(Workspace.Documents.Any(d=>d.Conflict)) UpdateStatus("External edit conflict — copy your draft, then reload disk.");
            });
        }
        private void BuildMetadata()
        {
            var title=new Label("Session metadata"); title.AddToClassList("title"); body.Add(title);
            EnsureMetadata();
            var scroll=new ScrollView(); scroll.AddToClassList("metadata"); body.Add(scroll);
            var experimenter=new TextField("Experimenter") { value=metadata.ExperimenterName }; scroll.Add(experimenter); experimenter.RegisterValueChangedCallback(e=>metadata.ExperimenterName=e.newValue);
            var comments=new TextField("Comments") { multiline=true,value=metadata.Comments }; scroll.Add(comments); comments.RegisterValueChangedCallback(e=>metadata.Comments=e.newValue);
            foreach(var rig in (Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>())
            {
                string id=(string)rig["vrId"]; var fly=metadata.Flies.FirstOrDefault(f=>f.VR==id);
                if(fly==null) { fly=new Fly { VR=id,Sex="Unspecified" }; metadata.Flies.Add(fly); }
                scroll.Add(new Label(id));
                var f=fly;
                foreach(string key in new[]{"FlyID","AgeDays","StarvedSinceHours"})
                {
                    var property=typeof(Fly).GetProperty(key); var field=new TextField(Human(key)) { value=(string)property.GetValue(f) }; scroll.Add(field); field.RegisterValueChangedCallback(e=>property.SetValue(f,e.newValue));
                }
                var sex=new DropdownField("Sex",new List<string>{"Unspecified","Female","Male"},0) { value=string.IsNullOrEmpty(f.Sex)?"Unspecified":f.Sex }; scroll.Add(sex); sex.RegisterValueChangedCallback(e=>f.Sex=e.newValue);
            }
            scroll.Add(Button("Save metadata",()=>{ SaveMetadata(); UpdateStatus("Session metadata saved."); }));
            UpdateStatus();
        }
        private string SaveMetadata()
        {
            EnsureMetadata();
            var ids=new HashSet<string>((Workspace.System.Root["configs"] as JArray??new JArray()).Select(v=>(string)v["vrId"]));
            var data=new FlyData { ExperimentName=metadata.ExperimentName,ExperimenterName=metadata.ExperimenterName,Comments=metadata.Comments,Extensions=metadata.Extensions,Flies=metadata.Flies.Where(f=>ids.Contains(f.VR)).ToList(),UsedFlyIDs=metadata.UsedFlyIDs??new List<string>() };
            foreach(var fly in data.Flies) if(!string.IsNullOrWhiteSpace(fly.FlyID) && !data.UsedFlyIDs.Contains(fly.FlyID)) data.UsedFlyIDs.Add(fly.FlyID);
            string json=JsonConvert.SerializeObject(data,Formatting.Indented);
            string backup=Path.Combine(Application.dataPath,"RunData/Backup"); Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup,"FlyMetaData.json"),json);
            // Setup edits belong to the draft backup. A completed run's metadata
            // is written only once, into the fresh folder prepared by Start.
            return json;
        }
        private void EnsureMetadata()
        {
            if(metadata==null)
            {
                string backup=Path.Combine(Application.dataPath,"RunData/Backup/FlyMetaData.json");
                metadata=File.Exists(backup)?JsonConvert.DeserializeObject<FlyData>(File.ReadAllText(backup)):new FlyData();
                metadata=metadata??new FlyData(); metadata.Flies=metadata.Flies??new List<Fly>();
            }
            foreach(var rig in (Workspace.System.Root["configs"] as JArray??new JArray()).OfType<JObject>())
            {
                string id=(string)rig["vrId"];
                if(!metadata.Flies.Any(f=>f.VR==id)) metadata.Flies.Add(new Fly { VR=id,Sex="Unspecified" });
                var fly=metadata.Flies.First(f=>f.VR==id); if(string.IsNullOrEmpty(fly.Sex)) fly.Sex="Unspecified";
            }
        }
        public void StartExperiment()
        {
            RequireApplied(); var errors=Workspace.Validate();
            if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            Workspace.SaveAll();
            var main=MainController.Instance;
            main.PrepareConfiguredRun(Workspace.System.Path,Workspace.Sequence.Path);
            string metadataJson=SaveMetadata();
            File.WriteAllText(Path.Combine(MasterDataLogger.Instance.directoryPath,"FlyMetaData.json"),metadataJson);
            // Detach immediately; Destroy alone is deferred until end of frame.
            Document.enabled=false; root.Clear(); enabled=false;
            main.StartSequence();
            if(!main.SequenceRunning) { enabled=true; Document.enabled=true; root=Document.rootVisualElement; root.AddToClassList("workbench"); root.styleSheets.Add(Resources.Load<StyleSheet>("ConfigurationUI/workbench")); BuildShell(); BuildNavigation(); BuildContent(); return; }
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            foreach(var raycaster in legacyRaycasters) if(raycaster!=null) raycaster.enabled=true;
            if(panel!=null) Destroy(panel);
            if(Instance==this) Instance=null;
        }
    }
}
