using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private bool replayLibraryMode,libraryRunning;
        private int libraryGeneration;
        private float libraryNextFreshnessCheck;
        private bool libraryFreshnessRunning;
        private string libraryDirectoryToken;
        private string libraryRoot,libraryCache,librarySelected,libraryMessage;
        private readonly Dictionary<string,RecordingLibrary.Entry> libraryEntries=new Dictionary<string,RecordingLibrary.Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> libraryExternal=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> libraryChecked=new HashSet<string>(StringComparer.Ordinal);
        private readonly List<RecordingLibrary.Entry> libraryVisible=new List<RecordingLibrary.Entry>();
        private sealed class LibraryUpdate { public int Version;public List<RecordingLibrary.Entry> Initial;public RecordingLibrary.Entry Entry;public string Message;public bool Done;public bool FreshnessResult;public bool NeedsRefresh;public string DirectoryToken; }
        private readonly ConcurrentQueue<LibraryUpdate> libraryUpdates=new ConcurrentQueue<LibraryUpdate>();
        private CancellationTokenSource libraryCancellation;
        private IVisualElementScheduledItem libraryPoll;
        private Task<string> libraryPicker;
        private int libraryPickerGeneration;
        private ListView libraryList;
        private ScrollView libraryDetails;
        private RecordingLibrary.Entry libraryDetailEntry;
        private string libraryDetailPath;
        private Label libraryState,libraryEmpty;
        private TextField librarySearch;
        private Button libraryBrowse,libraryRefresh;
        private void LeaveRecordingLibrary()
        {
            replayLibraryMode=false;StopLibraryWork();libraryPoll?.Pause();
        }
        private void StopLibraryWork()
        {libraryGeneration++;libraryChecked.Clear();libraryCancellation?.Cancel();libraryCancellation?.Dispose();libraryCancellation=null;libraryRunning=false;libraryFreshnessRunning=false;}
        private void ShowRecordingLibrary()
        {
            RequireApplied();CloseModal();StopLibraryWork();replayLibraryMode=true;homeMode=experimentIndexMode=metadataMode=jsonMode=advancedMode=false;
            selected=Workspace.Sequence;path.Clear();libraryRoot=RuntimePaths.RunDataRoot;
            libraryCache=Path.Combine(Application.temporaryCachePath,"MatrexRecordingLibrary");
            BuildNavigation();BuildContent();StartLibraryScan();
        }
        private void BuildRecordingLibrary()
        {
            var sheet=Resources.Load<StyleSheet>("ConfigurationUI/recording-library");if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);
            body.Add(SessionLabel("Replay recordings","title"));
            body.Add(SessionLabel("Choose a local recording, review its saved context, and replay it. Browse opens a recording stored elsewhere.","studio-subtitle"));
            var tools=Row("recording-library-tools");body.Add(tools);
            librarySearch=new TextField {name="recording-library-search",tooltip="Filter recording names, IDs, animals, backends or recorded scenes."};librarySearch.textEdition.placeholder="Search recordings";tools.Add(librarySearch);
            librarySearch.RegisterValueChangedCallback(_=>{RefreshLibraryRows();RefreshLibraryDetails();RefreshLibraryFooter();});
            libraryRefresh=StudioButton("Refresh","loop",StartLibraryScan,"quiet");libraryRefresh.name="recording-library-refresh";tools.Add(libraryRefresh);
            libraryBrowse=StudioButton("Browse…","file",BrowseLibraryFolder,"quiet");libraryBrowse.name="recording-library-browse";tools.Add(libraryBrowse);
            var back=StudioButton("Back to Start","back",ShowSessionHome,"quiet");back.name="recording-library-back";tools.Add(back);
            libraryState=SessionLabel(libraryMessage??"Scanning local recordings…","recording-library-state");libraryState.name="recording-library-state";body.Add(libraryState);
            var split=Row("recording-library-split");body.Add(split);
            var local=new VisualElement();local.AddToClassList("recording-library-local");split.Add(local);
            local.Add(SessionLabel("LOCAL RUN DATA","apparatus-eyebrow"));
            var location=SessionLabel(libraryRoot??RuntimePaths.RunDataRoot,"recording-library-root");location.tooltip="The configured RunData directory; --data-dir overrides the default location.";local.Add(location);
            libraryEmpty=SessionLabel("No recordings found yet. Start an experiment, or Browse to a saved run folder.","recording-library-empty");local.Add(libraryEmpty);
            libraryList=new ListView(libraryVisible,90,()=>new VisualElement(),BindLibraryRow){name="recording-library-list",selectionType=SelectionType.Single,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};libraryList.style.flexGrow=1;local.Add(libraryList);
            libraryList.selectionChanged+=selection=>{var entry=selection.OfType<RecordingLibrary.Entry>().FirstOrDefault();if(entry!=null){librarySelected=entry.DirectoryPath;RefreshLibraryDetails();RefreshLibraryFooter();}};
            libraryDetailEntry=null;libraryDetailPath=null;
            libraryDetails=new ScrollView {name="recording-library-details",horizontalScrollerVisibility=ScrollerVisibility.Hidden};libraryDetails.AddToClassList("recording-library-details");split.Add(libraryDetails);
            var external=new Foldout {name="recording-library-external-path",text="Open an external folder path",value=false};body.Add(external);
            var input=Row();external.Add(input);var folder=new TextField {name="recording-library-folder-path",tooltip="Paste a complete saved run folder path. The recording is read without modifying its files."};folder.style.flexGrow=1;input.Add(folder);
            input.Add(StudioButton("Inspect folder","file",()=>InspectExternalFolder(folder.value),"quiet"));
            RefreshLibraryRows();RefreshLibraryDetails();
            libraryPoll?.Pause();libraryPoll=root.schedule.Execute(PollRecordingLibrary).Every(100);
            RefreshLibraryFooter();
        }
        private void StartLibraryScan()
        {
            if(!replayLibraryMode)return;StopLibraryWork();libraryCancellation=new CancellationTokenSource();var token=libraryCancellation.Token;int version=libraryGeneration;
            string directory=libraryRoot,cache=libraryCache,priority=librarySelected;var external=libraryExternal.ToArray();libraryRunning=true;libraryMessage="Scanning local recordings…";RefreshLibraryFooter();
            Task.Run(()=>{
                try
                {
                    var entries=RecordingLibrary.Discover(directory,token).ToList();
                    libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Initial=entries,DirectoryToken=LibraryDirectoryToken(directory)});
                    foreach(var entry in entries.OrderBy(e=>e.DirectoryPath==priority?0:1))
                    {
                        token.ThrowIfCancellationRequested();
                        libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Entry=entry.Error==null?RecordingLibrary.Inspect(entry.DirectoryPath,cache,token):entry});
                    }
                    foreach(string path in external.Where(path=>entries.All(e=>e.DirectoryPath!=path)))
                        libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Entry=RecordingLibrary.Inspect(path,cache,token)});
                    libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Done=true,Message="Recordings indexed. Choose a run to inspect its saved context."});
                }
                catch(OperationCanceledException){}
                catch(Exception error){libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Done=true,Message="Library could not finish scanning: "+error.Message});}
            });
        }
        private void BrowseLibraryFolder()
        {
            if(libraryPicker!=null)return;libraryPickerGeneration=libraryGeneration;libraryPicker=ReplayFolderPicker.ChooseAsync();RefreshLibraryFooter();
        }
        private void InspectExternalFolder(string directory)
        {
            if(!replayLibraryMode||string.IsNullOrWhiteSpace(directory))return;
            string folder=Path.GetFullPath(directory.Trim().Trim('"'));StopLibraryWork();libraryExternal.Add(folder);librarySelected=folder;
            libraryCancellation=new CancellationTokenSource();var token=libraryCancellation.Token;int version=libraryGeneration;string cache=libraryCache;
            libraryRunning=true;libraryMessage="Inspecting selected recording…";RefreshLibraryFooter();
            Task.Run(()=>{
                try{libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Entry=RecordingLibrary.Inspect(folder,cache,token),Done=true,Message="Selected recording inspected."});}
                catch(OperationCanceledException){}
                catch(Exception error){libraryUpdates.Enqueue(new LibraryUpdate{Version=version,Done=true,Message="Could not inspect recording: "+error.Message});}
            });
        }
        private void PollRecordingLibrary()
        {
            if(!replayLibraryMode)return;
            if(libraryPicker!=null&&libraryPicker.IsCompleted)
            {
                var picker=libraryPicker;libraryPicker=null;
                if(libraryPickerGeneration==libraryGeneration)
                {
                    if(picker.IsFaulted)libraryMessage=picker.Exception.GetBaseException().Message;
                    else if(!picker.IsCanceled&&!string.IsNullOrWhiteSpace(picker.Result))InspectExternalFolder(picker.Result);
                }
                RefreshLibraryFooter();
            }
            // Metadata arriving while a row is being clicked must not recycle it
            // before the pointer-up completes the selection.
            if(editPointerHeld)return;bool changed=false;
            while(libraryUpdates.TryDequeue(out var update))
            {
                if(update.Version!=libraryGeneration)continue;
                if(update.FreshnessResult){libraryFreshnessRunning=false;if(update.NeedsRefresh){StartLibraryScan();return;}continue;}
                if(update.Initial!=null)
                {
                    libraryDirectoryToken=update.DirectoryToken;
                    var keep=new HashSet<string>(update.Initial.Select(e=>e.DirectoryPath).Concat(libraryExternal),StringComparer.Ordinal);
                    foreach(string key in libraryEntries.Keys.Where(key=>!keep.Contains(key)).ToArray())libraryEntries.Remove(key);
                    foreach(var entry in update.Initial)if(!libraryEntries.ContainsKey(entry.DirectoryPath))libraryEntries[entry.DirectoryPath]=entry;
                }
                if(update.Entry!=null)
                {libraryChecked.Add(update.Entry.DirectoryPath);if(update.Entry.Omit)libraryEntries.Remove(update.Entry.DirectoryPath);else libraryEntries[update.Entry.DirectoryPath]=update.Entry;}
                if(update.Message!=null)libraryMessage=update.Message;if(update.Done)libraryRunning=false;changed=true;
            }
            if(changed){RefreshLibraryRows();RefreshLibraryDetails();RefreshLibraryFooter();}
            CheckLibraryFreshness();
        }
        private static string LibraryDirectoryToken(string directory)
        {try{return string.Join("|",Directory.EnumerateDirectories(directory).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>Path.GetFileName(p)+":"+Directory.GetLastWriteTimeUtc(p).Ticks));}catch{return "unavailable";}}
        private void CheckLibraryFreshness()
        {
            if(libraryRunning||libraryFreshnessRunning||libraryPicker!=null||Time.realtimeSinceStartup<libraryNextFreshnessCheck)return;
            libraryNextFreshnessCheck=Time.realtimeSinceStartup+5;libraryFreshnessRunning=true;
            int version=libraryGeneration;string directory=libraryRoot,previous=libraryDirectoryToken;
            var entries=libraryEntries.Values.Where(e=>e.Indexed&&e.FreshnessToken!=null).Select(e=>Tuple.Create(e.DirectoryPath,e.FreshnessToken)).ToArray();
            Task.Run(()=>{
                bool refresh=LibraryDirectoryToken(directory)!=previous;
                try{foreach(var entry in entries){if(RecordingLibrary.GetFreshnessToken(entry.Item1)!=entry.Item2){refresh=true;break;}}}catch{refresh=true;}
                libraryUpdates.Enqueue(new LibraryUpdate{Version=version,FreshnessResult=true,NeedsRefresh=refresh});
            });
        }
        private void RefreshLibraryRows()
        {
            if(libraryList==null)return;string search=librarySearch?.value?.Trim()??"";
            libraryVisible.Clear();libraryVisible.AddRange(libraryEntries.Values.Where(entry=>!entry.Omit&&MatchesLibrarySearch(entry,search)).OrderByDescending(e=>e.Id,StringComparer.Ordinal));
            libraryList.RefreshItems();int selectedIndex=libraryVisible.FindIndex(entry=>entry.DirectoryPath==librarySelected);
            if(selectedIndex<0&&libraryVisible.Count>0){selectedIndex=0;librarySelected=libraryVisible[0].DirectoryPath;}
            if(selectedIndex>=0)libraryList.SetSelectionWithoutNotify(new[]{selectedIndex});else libraryList.ClearSelection();
            libraryEmpty.style.display=libraryVisible.Count==0?DisplayStyle.Flex:DisplayStyle.None;
        }
        private static bool MatchesLibrarySearch(RecordingLibrary.Entry entry,string search)
        {
            if(search.Length==0)return true;
            string text=entry.SearchText;
            return text.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0;
        }
        private void BindLibraryRow(VisualElement holder,int index)
        {
            holder.Clear();var entry=libraryVisible[index];holder.AddToClassList("recording-library-row");
            holder.Add(SessionLabel(entry.Title,"recording-library-name"));holder.Add(SessionLabel(entry.DateLabel+" · "+entry.Id,"recording-library-date"));
            string detail=entry.Error!=null?"Unavailable · "+entry.Error:!entry.Indexed?"Indexing saved recording…":entry.ChangedDuringScan?"Recording changed during indexing · Refresh to retry":LibrarySpan(entry)+" · "+LibraryCycles(entry)+" · "+LibraryScenes(entry);
            holder.Add(SessionLabel(detail,"recording-library-row-meta"));holder.tooltip=entry.DirectoryPath;
        }
        private bool LibrarySelectionReady => SelectedLibraryEntry?.CanReplay==true&&libraryChecked.Contains(librarySelected)&&libraryPicker==null;
        private RecordingLibrary.Entry SelectedLibraryEntry => librarySelected!=null&&libraryEntries.TryGetValue(librarySelected,out var entry)?entry:null;
        private void RefreshLibraryDetails()
        {
            if(libraryDetails==null)return;var entry=SelectedLibraryEntry;
            if(ReferenceEquals(libraryDetailEntry,entry)&&libraryDetails.childCount>0)return;
            var state=entry!=null&&libraryDetailPath==entry.DirectoryPath?WorkbenchViewState.Capture(libraryDetails):null;
            libraryDetailEntry=entry;libraryDetailPath=entry?.DirectoryPath;libraryDetails.Clear();
            if(entry==null){libraryDetails.Add(SessionLabel("Select a recording","recording-library-name"));return;}
            libraryDetails.Add(SessionLabel(entry.Title,"recording-library-detail-title"));libraryDetails.Add(SessionLabel(entry.DateLabel+" · "+entry.Id,"recording-library-date"));
            if(entry.Error!=null){libraryDetails.Add(SessionLabel(entry.Error,"recording-library-warning"));return;}
            if(!entry.Indexed){libraryDetails.Add(SessionLabel("Reading recorded metadata and pose rows. Your experiment files are not changed.","recording-library-copy"));return;}
            var summary=entry.Summary;var facts=Row("recording-library-facts");libraryDetails.Add(facts);
            AddLibraryFact(facts,"Recorded span",LibrarySpan(entry));AddLibraryFact(facts,"Sequence cycles",LibraryCycles(entry,false));AddLibraryFact(facts,"Observed rigs",JoinLibraryValues(summary?["observed"]?["rigIds"]));
            libraryDetails.Add(SessionLabel(entry.Finalized?"Finalized recording summary":entry.ChangedDuringScan?"Changing recording · refresh before replay":entry.RecordingState=="error"?"Finalization reported an error · review the saved evidence":entry.RecordingState=="open"?"Recording summary is open · completion not established":"Summary from recorded files · finalization not established","recording-library-copy"));
            libraryDetails.Add(SessionHeading("Recorded scenes","sequence"));libraryDetails.Add(SessionLabel(LibraryScenes(entry),"recording-library-copy"));
            libraryDetails.Add(SessionHeading("Animals & input","person"));
            var animals=summary?["metadata"]?["configuredAnimals"] as JArray??new JArray();
            foreach(var animal in animals.OfType<JObject>())libraryDetails.Add(SessionLabel((string)animal["VR"]+" · "+LibraryOrUnknown(animal["AnimalID"],"ID unspecified")+" · "+LibraryOrUnknown(animal["SpeciesName"],"species unspecified"),"recording-library-copy"));
            if(animals.Count==0)libraryDetails.Add(SessionLabel("Animal metadata unavailable","recording-library-copy"));
            libraryDetails.Add(SessionLabel("Backend · "+JoinLibraryValues(summary?["metadata"]?["backends"],true),"recording-library-copy"));
            var presentations=summary?["observed"]?["presentationsByRig"] as JObject;
            if(presentations!=null)libraryDetails.Add(SessionLabel("Presentations · "+string.Join(" · ",presentations.Properties().Select(p=>p.Name+": "+p.Value)),"recording-library-copy"));
            var files=new Foldout {name="recording-library-files",text="Recorded files",value=false};libraryDetails.Add(files);
            foreach(var file in (summary?["files"] as JArray??new JArray()).OfType<JObject>())
            {
                files.Add(SessionLabel((string)file["relativePath"],"recording-library-file-name"));
                files.Add(SessionLabel((string)file["role"]+" · "+LibraryBytes((long?)file["bytes"]??0)+" · "+((bool?)file["rowsInspected"]==false?"row counts not indexed":file["playableRows"]+" playable · "+file["rejectedRows"]+" rejected · "+file["excludedRows"]+" excluded"),"recording-library-copy"));
            }
            var warnings=summary?["warnings"] as JArray??new JArray();var evidence=new Foldout {name="recording-library-evidence",text="Saved evidence & limitations ("+warnings.Count+")",value=false};libraryDetails.Add(evidence);
            evidence.Add(SessionLabel("Span is elapsed local-wall-clock time, including acquisition gaps; it is not active stimulus exposure. Sequence cycles are distinct recorded CurrentTrial values, not a sum across parallel animals.","recording-library-copy"));
            foreach(var warning in warnings)evidence.Add(SessionLabel(warning.ToString(),"recording-library-warning"));
            evidence.Add(SessionLabel("Planned sequence scenes · "+JoinLibraryValues(summary?["metadata"]?["plannedSceneNames"]),"recording-library-copy"));
            evidence.Add(SessionLabel("Folder · "+entry.DirectoryPath,"recording-library-root"));
            var replay=StudioButton("Save setup and replay","play",OpenLibrarySelection,"primary");replay.name="recording-library-open";replay.SetEnabled(LibrarySelectionReady);libraryDetails.Add(replay);
            state?.Restore(libraryDetails,()=>replayLibraryMode&&ReferenceEquals(libraryDetailEntry,entry));
        }
        private void OpenLibrarySelection()
        {
            var entry=SelectedLibraryEntry;if(entry==null||!LibrarySelectionReady)return;
            RequireApplied();Workspace.SaveAll();EnsureMetadata();SaveMetadata();LeaveRecordingLibrary();ReplayController.OpenSession(entry.DirectoryPath);
        }
        private void RefreshLibraryFooter(string message=null)
        {
            if(!replayLibraryMode)return;if(message!=null)libraryMessage=message;
            if(libraryState!=null)libraryState.text=libraryMessage??"Choose a recording.";
            if(status!=null){status.text=libraryMessage??"Choose a recording.";status.RemoveFromClassList("error");status.RemoveFromClassList("warning");}
            if(start!=null){var label=start.Q<Label>();if(label!=null)label.text="Replay selected";start.tooltip="Save this setup, then open the selected recording. Original recording files are read without modification.";start.SetEnabled(LibrarySelectionReady);}
            save?.SetEnabled(true);undo?.SetEnabled(false);redo?.SetEnabled(false);
            libraryBrowse?.SetEnabled(libraryPicker==null);libraryRefresh?.SetEnabled(!libraryRunning&&libraryPicker==null);
            libraryDetails?.Q<Button>("recording-library-open")?.SetEnabled(LibrarySelectionReady);
        }
        private void AddLibraryFact(VisualElement parent,string name,string value)
        {var box=new VisualElement();box.AddToClassList("recording-library-fact");box.Add(SessionLabel(name,"recording-library-date"));box.Add(SessionLabel(value,"recording-library-fact-value"));parent.Add(box);}
        private static string LibrarySpan(RecordingLibrary.Entry entry)
        {double? seconds=(double?)entry.Summary?["wallClockSpanSeconds"];return seconds.HasValue?LibraryDuration(seconds.Value):"Span unknown";}
        private static string LibraryDuration(double seconds)
        {var span=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((long)span.TotalHours).ToString("00",CultureInfo.InvariantCulture)+span.ToString(@"\:mm\:ss",CultureInfo.InvariantCulture);}
        private static string LibraryCycles(RecordingLibrary.Entry entry,bool suffix=true)
        {var value=entry.Summary?["observed"]?["sequenceCycleCount"];return value==null||value.Type==JTokenType.Null?"Cycles unknown":value+(suffix?" sequence cycles":"");}
        private static string LibraryScenes(RecordingLibrary.Entry entry)=>JoinLibraryValues(entry.Summary?["observed"]?["sceneNames"]);
        private static string JoinLibraryValues(JToken values,bool backends=false)
        {var text=(values as JArray??new JArray()).Select(v=>backends&&TrackerBackend.TryParse(v.ToString(),out var mode)?TrackerBackend.DisplayName(mode):v.ToString()).Where(v=>!string.IsNullOrWhiteSpace(v)).ToArray();return text.Length==0?"Unavailable":string.Join(" · ",text);}
        private static string LibraryOrUnknown(JToken value,string fallback)=>string.IsNullOrWhiteSpace(value?.ToString())?fallback:value.ToString();
        private static string LibraryBytes(long bytes)=>bytes>=1024*1024?(bytes/(1024d*1024)).ToString("0.0",CultureInfo.InvariantCulture)+" MiB":(bytes/1024d).ToString("0.0",CultureInfo.InvariantCulture)+" KiB";
    }
}
