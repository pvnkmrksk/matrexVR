using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private Label recordingFinalizationBanner;
        private void AddRecordingFinalizationBanner()
        {
            recordingFinalizationBanner=new Label {name="recording-finalization-banner"};
            recordingFinalizationBanner.style.whiteSpace=WhiteSpace.Normal;
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
            var recorder=MasterDataLogger.Instance;bool finishing=recorder!=null&&recorder.IsFinalizing;
            string error=recorder?.FinalizationError;
            recordingFinalizationBanner.style.display=finishing||!string.IsNullOrWhiteSpace(error)?DisplayStyle.Flex:DisplayStyle.None;
            recordingFinalizationBanner.style.color=string.IsNullOrWhiteSpace(error)?new Color(.74f,.89f,.84f):new Color(.95f,.72f,.52f);
            recordingFinalizationBanner.text=finishing?"Finishing recording… Saving data and closing files. Setup remains available while this completes.":string.IsNullOrWhiteSpace(error)?"":"Recording finalization needs attention: "+error+". Original data remains available.";
        }
    }
}
