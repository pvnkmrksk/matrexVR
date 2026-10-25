using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    /// <summary>Hardware choices live in the same reversible system JSON as the renderer and tracker inputs.</summary>
    public static class DesktopApparatusProfile
    {
        public const string MarkerName="apparatusConfiguration";
        public const string FaceLetters="DRBLFU";
        public const int MaximumRigCount=28;
        public static IEnumerable<JObject> Rigs(JToken root) => (root?["configs"] as JArray??new JArray()).OfType<JObject>();
        private static JObject Profile(JToken root) => (root as JObject)?[MarkerName] as JObject;
        public static bool IsConfigured(JToken root) => Integer(Profile(root)?["version"],0)==1&&Flag(Profile(root)?["configured"]);
        public static string Presentation(JToken root)
        {
            string saved=(string)Profile(root)?["presentation"];
            if(saved=="led"||saved=="desktop")return saved;
            return Rigs(root).Any()&&Rigs(root).All(r=>Flag(r["desktopPreview"]))?"desktop":"led";
        }
        public static int MonitorCount(JToken root) => Math.Max(1,Integer(Profile(root)?["monitorCount"],Rigs(root).Select(r=>Integer(r["targetDisplay"],0)+1).DefaultIfEmpty(1).Max()));
        public static bool ShouldPrepareCompanions(JToken root) => IsConfigured(root)&&Flag(Profile(root)?["launchBundledTrackers"]);
        public static bool ShouldLaunchBundledForRig(JToken root,JObject rig)
        {
            if(!ShouldPrepareCompanions(root)||rig==null||!string.IsNullOrWhiteSpace((string)rig["tracker"]?["executablePath"])||!string.IsNullOrWhiteSpace((string)rig["tracker"]?["configFile"]))return false;
            string mode=(rig["closedLoopMode"]?.ToString()??"").ToLowerInvariant();
            if(mode!="rekkefly"&&mode!="hejjetrac"&&mode!="hejjé trac"&&mode!="hejjétrac"&&mode!="hejjetrak")return false;
            return LocalAddress((string)rig["zmqAddress"]);
        }
        public static bool LocalAddress(string address)
        {
            string host=(address??"localhost").Trim().Trim('[',']').ToLowerInvariant();
            return host=="localhost"||host=="127.0.0.1"||host=="::1";
        }
        public static JObject Marker(JObject root)
        {
            if(!(root[MarkerName] is JObject marker)){marker=new JObject();root[MarkerName]=marker;}
            return marker;
        }
        public static void Begin(JObject root,string presentation,bool fromDemo)
        {
            if(presentation!="led"&&presentation!="desktop")throw new ArgumentException("Choose LED panels or desktop preview.");
            var marker=Marker(root);marker["version"]=1;marker["configured"]=false;marker["presentation"]=presentation;
            if(marker["monitorCount"]==null)marker["monitorCount"]=Math.Max(1,Rigs(root).Select(r=>Integer(r["targetDisplay"],0)+1).DefaultIfEmpty(1).Max());
            if(marker["launchBundledTrackers"]==null)marker["launchBundledTrackers"]=fromDemo;
            root.Remove("desktopQuickStart");
            foreach(var rig in Rigs(root))
            {
                rig["desktopPreview"]=presentation=="desktop";
                if(!fromDemo)continue;
                if(presentation=="led")
                {
                    rig["displayOrder"]="DRBLFU";rig["ledPanelWidth"]=128;rig["ledPanelHeight"]=128;
                    rig["startRow"]=0;rig["startCol"]=0;
                    if(rig["manualControls"] is JObject manual)manual["enabled"]=false;
                }
                else rig["displayOrder"]="F";
            }
        }
        public static void Complete(JObject root)
        {
            var errors=Validate(root);
            if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            var marker=Marker(root);marker["version"]=1;marker["configured"]=true;marker["presentation"]=Presentation(root);
            if(marker["monitorCount"]==null)marker["monitorCount"]=MonitorCount(root);
            if(marker["launchBundledTrackers"]==null)marker["launchBundledTrackers"]=false;
            root.Remove("desktopQuickStart");
        }
        public static void ResizeRigs(JObject root,int count)
        {
            if(count<1||count>MaximumRigCount)throw new ArgumentException("Use 1–"+MaximumRigCount+" VRs; runtime layer availability is also checked before Start.");
            if(!(root["configs"] is JArray rigs)){rigs=new JArray();root["configs"]=rigs;}
            while(rigs.Count>count)rigs.RemoveAt(rigs.Count-1);
            while(rigs.Count<count)
            {
                var rig=rigs.OfType<JObject>().LastOrDefault()?.DeepClone() as JObject??NewRig();
                int id=1;while(rigs.OfType<JObject>().Any(r=>(string)r["vrId"]=="VR"+id))id++;
                rig["vrId"]="VR"+id;
                // Camera calibration belongs to an individual rig. Existing rigs are
                // untouched; a new rig must not silently reuse its neighbour's file.
                rig.Remove("tracker");
                var ports=new HashSet<int>(rigs.OfType<JObject>().Select(r=>Integer(r["zmqPort"],9871)));
                int port=9871;while(port<=65535&&ports.Contains(port))port++;
                if(port>65535)throw new ArgumentException("No unused local tracker port is available.");
                rig["zmqPort"]=port;
                int display=Integer(rig["targetDisplay"],0);bool horizontal=rig["horizontal"]==null||Flag(rig["horizontal"]);
                int width=Math.Max(1,Integer(rig["ledPanelWidth"],128)),height=Math.Max(1,Integer(rig["ledPanelHeight"],128));
                var same=rigs.OfType<JObject>().Where(r=>Integer(r["targetDisplay"],0)==display).ToList();
                if(horizontal)
                {
                    int edge=same.Select(r=>(Integer(r["startRow"],0)+(r["horizontal"]==null||Flag(r["horizontal"])?1:Order(r).Length))*Math.Max(1,Integer(r["ledPanelHeight"],128))).DefaultIfEmpty(0).Max();
                    rig["startRow"]=(int)Math.Ceiling((double)edge/height);rig["startCol"]=0;
                }
                else
                {
                    int edge=same.Select(r=>(Integer(r["startCol"],0)+(r["horizontal"]!=null&&!Flag(r["horizontal"])?1:Order(r).Length))*Math.Max(1,Integer(r["ledPanelWidth"],128))).DefaultIfEmpty(0).Max();
                    rig["startCol"]=(int)Math.Ceiling((double)edge/width);rig["startRow"]=0;
                }
                rigs.Add(rig);
            }
        }
        private static JObject NewRig() => new JObject { ["vrId"]="VR1",["speciesName"]="",["closedLoopMode"]="Rekkefly",["desktopPreview"]=false,["displayOrder"]="DRBLFU",["ledPanelWidth"]=128,["ledPanelHeight"]=128,["startRow"]=0,["startCol"]=0,["horizontal"]=true,["zmqAddress"]="127.0.0.1",["zmqPort"]=9871,["targetDisplay"]=0,["sphereDiameter"]=2.6 };
        public static string Order(JObject rig) => rig?["displayOrder"]?.ToString()??"";
        public static void SetOrder(JObject rig,string order)
        {
            string text=(order??"").Trim().ToUpperInvariant();
            if(text.Length<1||text.Length>6||text.Any(c=>FaceLetters.IndexOf(c)<0)||text.Distinct().Count()!=text.Length)
                throw new ArgumentException("Panel order uses each D/R/B/L/F/U at most once, with at least one face.");
            rig["displayOrder"]=text;
        }
        public static string FaceName(char face)
        {
            switch(face){case 'D':return "Down";case 'R':return "Right";case 'B':return "Back";case 'L':return "Left";case 'F':return "Front";case 'U':return "Up";default:return "Unknown";}
        }
        public static void SetMovement(JObject root,string mode)
        {
            if(mode!="walking"&&mode!="flying")throw new ArgumentException("Choose walking or flying.");
            foreach(var rig in Rigs(root))rig["closedLoopMode"]=mode=="walking"?"Hejjetrac":"Rekkefly";
        }
        public static void SetPresentation(JObject root,string presentation)
        {
            if(presentation!="led"&&presentation!="desktop")throw new ArgumentException("Choose LED panels or desktop preview.");
            Marker(root)["presentation"]=presentation;
            foreach(var rig in Rigs(root)){rig["desktopPreview"]=presentation=="desktop";if(presentation=="desktop")rig["displayOrder"]="F";}
        }
        public static List<string> Validate(JToken root)
        {
            var errors=new List<string>();var rigs=Rigs(root).ToList();
            if(!(root?["configs"] is JArray array)||rigs.Count!=array.Count||rigs.Count<1||rigs.Count>MaximumRigCount)
                errors.Add("Configure 1–"+MaximumRigCount+" valid VR entries.");
            var ids=new HashSet<string>(StringComparer.Ordinal);var inputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var rig in rigs)
            {
                string id=(string)rig["vrId"]??"";
                if(!System.Text.RegularExpressions.Regex.IsMatch(id,@"^VR[1-9][0-9]*$")||!ids.Add(id))errors.Add("Each rig needs a unique ID such as VR1 or VR2.");
                string order=Order(rig);
                if(order.Length<1||order.Length>6||order.Any(c=>FaceLetters.IndexOf(c)<0)||order.Distinct().Count()!=order.Length)errors.Add(id+": use unique D/R/B/L/F/U face letters.");
                if(Flag(rig["desktopPreview"])&&order.Length!=1)errors.Add(id+": desktop preview uses one face per display.");
                if(Integer(rig["ledPanelWidth"],0)<=0||Integer(rig["ledPanelHeight"],0)<=0)errors.Add(id+": panel pixel dimensions must be positive.");
                if(Integer(rig["startRow"],-1)<0||Integer(rig["startCol"],-1)<0)errors.Add(id+": panel row and column must be zero or greater.");
                if(Integer(rig["targetDisplay"],-1)<0)errors.Add(id+": choose an output display index of zero or greater.");
                int port=Integer(rig["zmqPort"],0);string host=((string)rig["zmqAddress"]??"").Trim();
                if(string.IsNullOrEmpty(host)||port<1||port>65535)errors.Add(id+": enter a host and a tracker port from 1–65535.");
                string input=(LocalAddress(host)?"localhost":host)+":"+port;
                if(!inputs.Add(input))errors.Add(id+": another rig uses the same tracker endpoint.");
            }
            return errors;
        }
        public static int Integer(JToken token,int fallback)
        {return int.TryParse(token?.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)?value:fallback;}
        public static bool Flag(JToken token) => token?.Type==JTokenType.Boolean&&(bool)token;
    }
}
