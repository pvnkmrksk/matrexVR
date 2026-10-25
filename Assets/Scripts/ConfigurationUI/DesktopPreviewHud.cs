using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Lightweight desktop-only run controls; apparatus/LED experiments keep their own HUD.</summary>
    public sealed class DesktopPreviewHud : MonoBehaviour
    {
        internal const string WalkingDemoHelp="Walking demo plays a short recorded sample. To replay it in the same tracker window: More → Rig manager… → Start all. You can explore the world with manual controls.";
        internal const string WalkingDemoTooltip="Short recorded walking sample. Replay in HejjéTrac: More → Rig manager… → Start all. Manual exploration remains available.";
        private PanelSettings panel;
        private Label input;
        private VisualElement help,hudRoot;
        private Button forward;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { SceneManager.sceneLoaded -= Loaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name=="ControlScene" || scene.name=="ReplayScene" || MainController.Instance==null ||
                !MainController.Instance.SystemConfigs.Values.Any(r=>r.desktopPreview)) return;
            new GameObject("Desktop preview controls").AddComponent<DesktopPreviewHud>();
        }
        private void Start()
        {
            panel=ScriptableObject.CreateInstance<PanelSettings>();panel.name="Desktop preview controls";
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=DesktopDisplay.UiScale;panel.sortingOrder=700;panel.targetDisplay=MainController.Instance.SystemConfigs.Values.First(r=>r.desktopPreview).targetDisplay;
            panel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("ConfigurationUI/runtime");
            var document=gameObject.AddComponent<UIDocument>();document.panelSettings=panel;
            var root=document.rootVisualElement;hudRoot=root;root.AddToClassList("desktop-preview-hud");root.pickingMode=PickingMode.Ignore;
            root.styleSheets.Add(Resources.Load<StyleSheet>("ConfigurationUI/workbench"));WorkbenchTypography.Apply(root);
            JObject marker=null;
            try{marker=JObject.Parse(ExperimentConfigFiles.ReadAllText(ExperimentConfigFiles.Resolve(MainController.Instance.SystemConfigurationFileName)))["desktopQuickStart"] as JObject;}catch(Exception){ }
            bool demo=(bool?)marker?["demo"]??false;
            bool walkingDemo=demo&&(string)marker?["mode"]=="walking";
            var bar=new VisualElement();bar.AddToClassList("desktop-run-bar");root.Add(bar);
            var badge=new Label(demo?"DEMO":"TRACKER INPUT");badge.AddToClassList("desktop-input-badge");bar.Add(badge);
            badge.tooltip=demo?"Demonstration input is simulated or prerecorded.":"A connected publisher is not proof of a physical camera. Verify the source and tracking quality in the native tracker window.";
            input=new Label("Waiting for tracker");input.AddToClassList("desktop-input-status");bar.Add(input);
            forward=ActionButton("Auto forward",()=>{
                var keyboards=FindObjectsByType<Keyboard>(FindObjectsSortMode.None);bool on=keyboards.Any(k=>k.AutopilotEnabled);
                foreach(var keyboard in keyboards)keyboard.SetAutopilotMode(!on);Refresh();
            });forward.tooltip="Keep moving forward while you steer. Also toggled with Ctrl+Space.";bar.Add(forward);
            var plots=ActionButton("Live plots",DesktopDashboard.Open);plots.name="desktop-live-plots";plots.tooltip="Open optional external plots. This does not affect the experiment.";bar.Add(plots);
            var keys=ActionButton("Controls",()=>help.style.display=help.style.display==DisplayStyle.None?DisplayStyle.Flex:DisplayStyle.None);keys.tooltip=walkingDemo?WalkingDemoTooltip:"Show movement keys and what is being recorded.";bar.Add(keys);
            var back=ActionButton("Back to setup · Esc",()=>MainController.Instance.HandleEscape());back.name="desktop-back-to-setup";
            back.AddToClassList("desktop-back-button");bar.Add(back);
            help=new VisualElement();help.AddToClassList("desktop-control-help");help.style.display=DisplayStyle.None;root.Add(help);
            help.Add(new Label("Arrow keys  Move    ·    A / D  Turn    ·    Z / C  Down / up (flight)"));
            help.Add(new Label("Ctrl+Space  Auto forward    ·    R  Reset pose    ·    Esc  Return to setup"));
            help.Add(new Label(walkingDemo?WalkingDemoHelp:demo?"Demo input is simulated or prerecorded and labeled in the recording. Tracker windows run independently.":"Tracker input is connected over the configured transport. Verify camera source, calibration and tracking quality in its window."));
            OperatorOverlayVisibility.Changed+=Refresh;
            root.schedule.Execute(Refresh).Every(500);Refresh();
        }
        private Button ActionButton(string title, Action action)
        {
            var button=new Button(action){text=title,focusable=false};button.AddToClassList("desktop-run-button");return button;
        }
        private void Refresh()
        {
            // A mixed preview/LED configuration must protect the LED pixels too.
            var size=OperatorOverlayLayout.DisplaySize(panel.targetDisplay);
            bool hidden=OperatorOverlayVisibility.Hidden||OperatorOverlayLayout.ProtectedRects(panel.targetDisplay,(int)size.x,(int)size.y).Count>0;
            hudRoot.style.display=hidden?DisplayStyle.None:DisplayStyle.Flex;
            if(hidden)return;
            int expected=0,fresh=0;
            foreach(var rig in MainController.Instance.SystemConfigs.Values.Where(r=>r.desktopPreview))
            {
                expected++;
                var animal=RuntimeRigRegistry.Rigs(SceneManager.GetActiveScene()).FirstOrDefault(r=>RuntimeRigRegistry.ParseIndex(r.name)==RuntimeRigRegistry.ParseIndex(rig.vrId));
                var listener=animal!=null?animal.GetComponent<ZmqListener>():null;
                var sample=listener!=null?listener.ReadRawSample():default;
                if(sample.hasPose && DateTime.UtcNow.Ticks-sample.receivedUtcTicks<TimeSpan.TicksPerSecond)fresh++;
            }
            input.text=fresh==expected&&fresh>0?"Tracking input connected":"Waiting for tracking input";
            input.EnableInClassList("input-stale",fresh<expected);
            bool on=FindObjectsByType<Keyboard>(FindObjectsSortMode.None).Any(k=>k.AutopilotEnabled);
            forward.text="Auto forward "+(on?"on":"off");
            if(Mathf.Abs(panel.scale-DesktopDisplay.UiScale)>.01f)panel.scale=DesktopDisplay.UiScale;
        }
        private void OnDestroy(){OperatorOverlayVisibility.Changed-=Refresh;if(panel!=null)Destroy(panel);}
    }
}
