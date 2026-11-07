using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private Label recordingFinalizationBanner;
        private VisualElement recordingSavingScreen;
        private Label recordingSavingStatus;
        private VisualElement focusBeforeSaving;
        private readonly List<VisualElement> disabledForSaving = new List<VisualElement>();
        private bool SavingScreenVisible => recordingSavingScreen != null && recordingSavingScreen.parent == root &&
            recordingSavingScreen.style.display == DisplayStyle.Flex;
        private bool recordingPending;
        private string recordingBannerError;
        private void AddRecordingFinalizationBanner()
        {
            recordingFinalizationBanner=new Label {name="recording-finalization-banner"};
            recordingFinalizationBanner.style.whiteSpace=WhiteSpace.NoWrap;
            recordingFinalizationBanner.style.height=32;recordingFinalizationBanner.style.minHeight=32;recordingFinalizationBanner.style.maxHeight=32;
            recordingFinalizationBanner.style.flexShrink=0;recordingFinalizationBanner.style.overflow=Overflow.Hidden;recordingFinalizationBanner.style.textOverflow=TextOverflow.Ellipsis;
            recordingFinalizationBanner.style.paddingLeft=24;recordingFinalizationBanner.style.paddingRight=24;
            recordingFinalizationBanner.style.paddingTop=7;recordingFinalizationBanner.style.paddingBottom=7;
            recordingFinalizationBanner.style.fontSize=12;
            recordingFinalizationBanner.style.backgroundColor=new Color(.12f,.18f,.21f);
            root.Add(recordingFinalizationBanner);UpdateRecordingFinalizationBanner();
            root.schedule.Execute(UpdateRecordingFinalizationBanner).Every(300);
        }
        private void AddRecordingSavingScreen()
        {
            focusBeforeSaving=null;
            disabledForSaving.Clear();
            recordingSavingScreen=new VisualElement {name="recording-saving-screen",focusable=true,tabIndex=-1};
            recordingSavingScreen.style.position=UnityEngine.UIElements.Position.Absolute;
            recordingSavingScreen.style.left=recordingSavingScreen.style.right=recordingSavingScreen.style.top=recordingSavingScreen.style.bottom=0;
            recordingSavingScreen.style.justifyContent=Justify.Center;
            recordingSavingScreen.style.alignItems=Align.Center;
            recordingSavingScreen.style.backgroundColor=new Color(.04f,.07f,.10f);
            recordingSavingScreen.style.color=new Color(.9f,.95f,.98f);
            var title=new Label("Saving data…");
            title.style.fontSize=34;
            recordingSavingScreen.Add(title);
            recordingSavingStatus=new Label();
            recordingSavingStatus.style.fontSize=15;
            recordingSavingScreen.Add(recordingSavingStatus);
            recordingSavingScreen.style.display=DisplayStyle.None;
            root.Add(recordingSavingScreen);
            UpdateRecordingFinalizationBanner();
        }

        private void UpdateRecordingFinalizationBanner()
        {
            if(recordingFinalizationBanner==null)return;
            var recorder=MasterDataLogger.Instance;var main=MainController.Instance;
            bool stopping=main?.SequenceStopping==true;
            bool finishing=recorder!=null&&recorder.IsFinalizing;
            string error=main?.RecordingTransitionError??recorder?.FinalizationError;
            bool preparingRecording=main?.SequencePreparing==true;
            bool pending=stopping||finishing||preparingRecording;
            recordingFinalizationBanner.style.display=DisplayStyle.Flex;
            recordingFinalizationBanner.style.color=string.IsNullOrWhiteSpace(error)?new Color(.74f,.89f,.84f):new Color(.95f,.72f,.52f);
            string[] warnings=(main?.RecordingWarnings??Array.Empty<string>()).Concat(companionWarnings).Distinct().ToArray();
            string ready=warnings.Length==0?"VR ready":warnings.Any(warning=>warning.IndexOf("record",StringComparison.OrdinalIgnoreCase)>=0)?"VR ready · recording unavailable":"VR ready · tracking unavailable";
            recordingFinalizationBanner.text=preparingRecording?(main.PreparationStatus??"Preparing…"):stopping?(main.PreparationStatus??"Finishing recorders…"):finishing?"Saving recording…":!string.IsNullOrWhiteSpace(error)?"Recording needs attention":ready;
            recordingFinalizationBanner.tooltip=!string.IsNullOrWhiteSpace(error)?error:warnings.Length>0?string.Join("\n",warnings):"Tracking data and configuration snapshots are saved with each experiment. Tracker availability does not prevent the VR from starting.";
            bool changed=recordingPending!=pending||recordingBannerError!=error;
            recordingPending=pending;recordingBannerError=error;
            if(changed&&status!=null&&selected!=null)UpdateStatus();
            if(stopping)start?.SetEnabled(false);
            if(recordingSavingScreen==null||recordingSavingScreen.parent!=root)return;
            bool saving=stopping||finishing;
            bool shown=SavingScreenVisible;
            recordingSavingScreen.style.display=saving?DisplayStyle.Flex:DisplayStyle.None;
            if(saving)recordingSavingStatus.text=(main?.PreparationStatus??"Finishing recording…")+" Setup will return when this finishes.";
            if(saving==shown)return;
            if(saving)
            {
                focusBeforeSaving=root.panel?.focusController.focusedElement as VisualElement;
                foreach(var element in root.Children().ToList())
                {
                    if(element==recordingSavingScreen||!element.enabledSelf)continue;
                    disabledForSaving.Add(element);
                    element.SetEnabled(false);
                }
                var screen=recordingSavingScreen;
                screen.schedule.Execute(()=>{if(screen==recordingSavingScreen&&SavingScreenVisible)screen.Focus();});
            }
            else
            {
                foreach(var element in disabledForSaving)
                    if(element.parent==root)element.SetEnabled(true);
                disabledForSaving.Clear();
                if(focusBeforeSaving!=null&&focusBeforeSaving.panel==root.panel&&focusBeforeSaving.enabledInHierarchy)
                    focusBeforeSaving.Focus();
                else ScheduleKeyboardFocus();
                focusBeforeSaving=null;
            }
        }
    }
}
