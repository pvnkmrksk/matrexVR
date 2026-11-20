#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ConfigurationUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

public static class SessionTopicsChecks
{
    public static void Run()
    {
        int checks=0;
        void Click(Button button)
        {
            var handlers=(Action)typeof(Clickable).GetField("clicked",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(button.clickable);
            handlers();
        }
        void Check(bool result,string message){if(!result)throw new Exception(message);checks++;}
        var at=new DateTimeOffset(2026,11,20,15,0,0,TimeSpan.FromHours(1));
        var data=new AnimalData {ExperimentName="Fixture",Animals={new AnimalMetadata{VR="VR1",AnimalID="animal-7",Sex="Female",AgeDays="4",Comments="keep draft"}},UsedAnimalIDs={"animal-7"}};
        var topics=SessionTopics.Definitions(data);
        SessionTopics.Topic Find(string key,string scope="Animal")=>topics.First(t=>t.Key==key&&t.Scope==scope);
        Find("LastFedAt").Enabled=Find("LastWateredAt").Enabled=Find("Condition").Enabled=true;
        Find("Pressure","Recording").Enabled=true;
        Find("Comments").Enabled=false;
        SessionTopics.StoreDefinitions(data,topics);
        var animal=data.Animals[0];
        SessionTopics.Set(animal,Find("LastFedAt"),at.AddHours(-18).ToString("o"));
        SessionTopics.Set(animal,Find("LastWateredAt"),at.AddHours(-2).ToString("o"));
        SessionTopics.Set(animal,Find("Condition"),new JArray("Missing left hind leg","Soft thorax","custom condition"));
        SessionTopics.Set(data,Find("Pressure","Recording"),new JObject{{"value",1012.5m},{"unit","hPa"}});
        var custom=new SessionTopics.Topic{Key="custom_fixture",Scope="Animal",Label="Cohort",Kind="Choice",Choices=new[]{"A","B"},Enabled=true};
        topics.Add(custom);SessionTopics.StoreDefinitions(data,topics);SessionTopics.Set(animal,custom,"B");
        var numeric=new SessionTopics.Topic {Key="custom_zero",Scope="Animal",Kind="Number",Label="Zero",Enabled=true};
        var checkbox=new SessionTopics.Topic {Key="custom_false",Scope="Animal",Kind="Checkbox",Label="False",Enabled=true};
        topics.Add(numeric);topics.Add(checkbox);SessionTopics.StoreDefinitions(data,topics);
        SessionTopics.Set(animal,numeric,0);SessionTopics.Set(animal,checkbox,false);
        SessionTopics.Set(data,Find("LastFedAt","Recording"),at.AddHours(-24).ToString("o"));
        SessionTopics.Set(data,Find("LastWateredAt","Recording"),at.AddHours(-8).ToString("o"));
        SessionTopics.Set(data,Find("LifeStage","Recording"),"L5");
        var reloaded=JsonConvert.DeserializeObject<AnimalData>(JsonConvert.SerializeObject(data));
        Check(SessionTopics.Definitions(reloaded).Any(t=>t.Key==custom.Key&&t.Enabled),"Custom definition did not reload");
        Check(SessionTopics.TryTimestamp(SessionTopics.Get(reloaded.Animals[0],Find("LastFedAt")).ToString(),out var stamp)&&stamp==at.AddHours(-18),"Reload changed timestamp instant");
        var run=JObject.Parse(SessionTopics.Snapshot(reloaded,at));var subject=(JObject)run["Animals"][0];
        Check((decimal)subject["StarvedSinceHours"]==18m,"Starvation duration not captured");
        Check((decimal)run["StarvedSinceHours"]==24m&&(decimal)run["SinceWateredHours"]==8m,"Recording-wide timestamp durations wrong");
        Check((string)run["Topics"]["LifeStage"]=="L5","Recording-wide instar missing");
        Check(SessionTopics.Definitions(reloaded).Count(t=>t.Scope=="Recording"&&t.Enabled&&new[]{"LastFedAt","LastWateredAt","LifeStage"}.Contains(t.Key))==3,"Recording topics not enabled");
        Check((decimal)subject["SinceWateredHours"]==2m,"Watering duration not captured");
        Check(subject["Comments"]==null&&reloaded.Animals[0].Comments=="keep draft","Unchecked draft lost or run leaked value");
        Check((string)subject["Topics"][custom.Key]=="B","Custom value missing");
        Check((int)subject["Topics"][numeric.Key]==0&&!(bool)subject["Topics"][checkbox.Key],"Zero/false custom values lost");
        Check(((JArray)subject["Topics"]["Condition"]).Count==3,"Multiple and custom conditions lost");
        Check((decimal)run["Topics"]["Pressure"]["value"]==1012.5m&&(string)run["Topics"]["Pressure"]["unit"]=="hPa","Pressure value/unit lost");
        Check(!SessionTopics.Conditions.Any(c=>c.Contains("Damaged"))&&SessionTopics.Conditions.Count(c=>c.StartsWith("Missing"))==6,"Condition options wrong");
        var loadedTopics=SessionTopics.Definitions(reloaded);
        loadedTopics.First(t=>t.Key=="AnimalID").Enabled=false;
        loadedTopics.First(t=>t.Key=="LastWateredAt").Enabled=false;
        SessionTopics.StoreDefinitions(reloaded,loadedTopics);
        reloaded.Animals[0].Extensions["SinceWateredHours"]=999;
        run=JObject.Parse(SessionTopics.Snapshot(reloaded,at));subject=(JObject)run["Animals"][0];
        Check(subject["AnimalID"]==null&&subject["FlyID"]==null&&run["UsedAnimalIDs"]==null&&run["UsedFlyIDs"]==null,"Unchecked identifiers leaked");
        Check(subject["SinceWateredHours"]==null&&subject["Topics"]["LastWateredAt"]==null,"Disabled watering leaked stale duration");
        SessionTopics.Set(reloaded.Animals[0],Find("LastFedAt"),JValue.CreateNull());
        run=JObject.Parse(SessionTopics.Snapshot(reloaded,at));
        Check(run["Animals"][0]["StarvedSinceHours"]==null,"Unknown feeding acquired an elapsed value");
        SessionTopics.Set(reloaded.Animals[0],Find("LastFedAt"),at.AddHours(1).ToString("o"));
        bool rejected=false;try{SessionTopics.Snapshot(reloaded,at);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Future timestamp not rejected");
        // The same actual UI builder renders launcher and compact preparation controls.
        var go=new UnityEngine.GameObject("Metadata checks");
        try {
            var wb=go.AddComponent<ConfigWorkbench>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ConfigWorkbench).GetField("metadata",flags).SetValue(wb,data);
            var build=typeof(ConfigWorkbench).GetMethod("BuildTopicFields",flags);
            var regular=new VisualElement();build.Invoke(wb,new object[]{regular,animal,"Animal",false});
            var compact=new VisualElement();build.Invoke(wb,new object[]{compact,animal,"Animal",true});
            Check(regular.Q("topic-LastFedAt")!=null,"Launcher lacks selected timestamp");
            Check(regular.Q("topic-Animal-Comments")==null,"Launcher shows unchecked field");
            Check(compact.Query<Button>().ToList().Any(b=>b.text.StartsWith("All ")),"Large compact forms lack full details action");
            var uiRoot=new VisualElement();var card=new VisualElement {name="preparation-animal-VR1"};uiRoot.Add(card);
            typeof(ConfigWorkbench).GetField("root",flags).SetValue(wb,uiRoot);
            typeof(ConfigWorkbench).GetField("preExperimentPanel",flags).SetValue(wb,new VisualElement());
            var recording=new VisualElement();uiRoot.Add(recording);
            typeof(ConfigWorkbench).GetField("preExperimentRecording",flags).SetValue(wb,recording);
            var cards=(System.Collections.Generic.Dictionary<string,VisualElement>)typeof(ConfigWorkbench).GetField("preExperimentAnimalCards",flags).GetValue(wb);cards.Add("VR1",card);
            typeof(ConfigWorkbench).GetMethod("ShowAllTopicDetails",flags).Invoke(wb,new object[]{animal,"Animal",card});
            Check(card.Q("contained-topic-editor")!=null&&uiRoot.childCount==2,"All details escaped the animal card");
            Check(card.Query<DropdownField>().ToList().Count==0,"Preparation still uses native popup dropdowns");
            int refreshed=0;
            typeof(ConfigWorkbench).GetMethod("ShowConditionPicker",flags).Invoke(wb,new object[]{animal,Find("Condition"),(Action)(()=>refreshed++),card});
            var condition=card.Children().Last();
            Check(condition.Query<Toggle>().ToList().Count==9,"Condition options missing in contained editor");
            condition.Query<Toggle>().ToList().First(t=>t.label=="Missing right hind leg").value=true;
            Click(condition.Query<Button>().ToList().Last());
            Check(refreshed==1&&(SessionTopics.Get(animal,Find("Condition")) as JArray).Any(v=>v.ToString()=="Missing right hind leg"),"Contained condition Apply failed");
            typeof(ConfigWorkbench).GetMethod("ShowTimestampPicker",flags).Invoke(wb,new object[]{animal,Find("LastFedAt"),(Action)(()=>refreshed++),card});
            var dateEditor=card.Children().Last();
            Check(dateEditor.Query<IntegerField>().ToList().Count==4&&dateEditor.Query<DropdownField>().ToList().Count==0,"Date editor escaped containment or used popup month menu");
            var monthField=dateEditor.Query<BaseField<string>>().ToList().First(f=>f.label=="Month");
            Click(monthField.Q<Button>());
            var monthEditor=card.Children().Last();
            Check(monthEditor.Query<Button>().ToList().Count==13&&uiRoot.childCount==2,"Month options escaped the card");
            Click(monthEditor.Query<Button>().ToList().First(b=>b.text=="Jan"));
            Check(monthField.value=="Jan"&&dateEditor.style.display.value!=DisplayStyle.None,"Month choice did not restore date editor");
            Check(wb.TryHandleSetupEscape()&&wb.PreExperimentOpen&&dateEditor.parent==null,"Escape cancelled preparation instead of closing date editor");
            Check(uiRoot.Query<VisualElement>().ToList().All(e=>!e.ClassListContains("modal-shade")),"Preparation opened a screen modal");

        }finally{UnityEngine.Object.DestroyImmediate(go);}
        var output=Path.Combine(UnityEngine.Application.dataPath,"../session-topics-checks.json");
        File.WriteAllText(output,new JObject{{"passed",true},{"checks",checks}}.ToString());
        UnityEngine.Debug.Log("Session topic checks passed: "+checks);
        UnityEditor.EditorApplication.Exit(0);
    }
}
#endif
