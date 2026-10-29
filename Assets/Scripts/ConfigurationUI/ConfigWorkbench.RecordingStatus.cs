using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private Label recordingFinalizationBanner;
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
            recordingFinalizationBanner.text=preparingRecording?(main.PreparationStatus??"Preparing experiment…"):stopping?(main.PreparationStatus??"Finishing recorders… Waiting for native acknowledgement and completed files."):finishing?"Finishing recording… Saving data and closing files.":!string.IsNullOrWhiteSpace(error)?"Recording needs attention: "+error:"Recording ready · tracking data and configuration snapshots are saved with each experiment.";
            recordingFinalizationBanner.tooltip=recordingFinalizationBanner.text;
            bool changed=recordingPending!=pending||recordingBannerError!=error;
            recordingPending=pending;recordingBannerError=error;
            if(changed&&status!=null&&selected!=null)UpdateStatus();
            if(stopping)start?.SetEnabled(false);
        }
    }
}
