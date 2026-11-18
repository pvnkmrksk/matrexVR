using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    public sealed partial class ConfigWorkbench
    {
        private const string PreExperimentOverlay = "pre-experiment";
        private VisualElement preExperimentPanel;
        private Label preExperimentStatus;
        private Button preExperimentStart, preExperimentCancel;
        private readonly Dictionary<string, Label> preExperimentRigs = new Dictionary<string, Label>();
        private readonly Dictionary<VisualElement, StyleEnum<Visibility>> preExperimentHidden = new Dictionary<VisualElement, StyleEnum<Visibility>>();
        private int preExperimentOriginalDisplay;
        private string preExperimentMessage;
        private string preExperimentConfirmation;
        private bool preExperimentFilesChanged;
        public bool PreExperimentOpen => preExperimentPanel != null;
        private bool UsePreExperimentScene => ExperimentBlocks.Flag(Workspace.Sequence.Root["preExperimentScene"]);

        private void OpenPreExperiment()
        {
            var main = MainController.Instance;
            if (main == null) throw new InvalidOperationException("The experiment controller is unavailable.");
            main.BeginPreExperiment(Workspace.System.Path);
            try
            {
                preparing = false;
                body.SetEnabled(true);
                side.SetEnabled(true);
                HideHoverHelp();
                preExperimentOriginalDisplay = panel.targetDisplay;
                foreach (var element in root.Children())
                {
                    preExperimentHidden.Add(element, element.style.visibility);
                    element.style.visibility = Visibility.Hidden;
                }
                root.style.backgroundColor = Color.clear;
                root.pickingMode = PickingMode.Ignore;

                preExperimentPanel = new VisualElement { name = "pre-experiment-panel" };
                preExperimentPanel.AddToClassList("studio-dialog");
                preExperimentPanel.style.position = UnityEngine.UIElements.Position.Absolute;
                preExperimentPanel.style.paddingTop = preExperimentPanel.style.paddingBottom = 12;
                preExperimentPanel.style.paddingLeft = preExperimentPanel.style.paddingRight = 14;
                var content = new VisualElement();
                content.style.flexGrow = 1;
                preExperimentPanel.Add(content);
                var title = SessionLabel("Pre-experiment scene", "session-section-title");
                content.Add(title);
                preExperimentStatus = SessionLabel("", "session-hint");
                preExperimentStatus.name = "pre-experiment-status";
                preExperimentStatus.style.minHeight = 38;
                content.Add(preExperimentStatus);

                var rigs = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                rigs.style.flexGrow = 1;
                rigs.style.flexShrink = 1;
                rigs.style.minHeight = 38;
                foreach (string id in main.SystemConfigs.Keys.OrderBy(RuntimeRigRegistry.ParseIndex))
                {
                    var label = SessionLabel("", "session-hint");
                    label.name = "pre-experiment-" + id;
                    label.style.minHeight = 20;
                    preExperimentRigs.Add(id, label);
                    rigs.Add(label);
                }
                content.Add(rigs);

                var actions = Row();
                actions.style.justifyContent = Justify.FlexEnd;
                actions.style.marginTop = 6;
                preExperimentCancel = StudioButton("Cancel setup", "back", CancelPreExperiment, "quiet");
                preExperimentCancel.name = "cancel-pre-experiment";
                preExperimentStart = StudioButton(CurrentDemo ? "Start demo" : "Start experiment", "play", CommitPreExperiment, "primary");
                preExperimentStart.name = "commit-pre-experiment";
                actions.Add(preExperimentCancel);
                actions.Add(preExperimentStart);
                content.Add(actions);

                root.Add(preExperimentPanel);
                if (!PlacePreExperimentPanel(main.StatusOverlaySettings.targetDisplay))
                    throw new InvalidOperationException("The pre-experiment controls need free space outside the configured LED panels. Choose an operator display with space in the status overlay settings.");
                preExperimentPanel.schedule.Execute(RefreshPreExperimentPanel).Every(250);
                RefreshPreExperimentPanel();
                preExperimentCancel.Focus();
            }
            catch
            {
                main.EndPreExperiment();
                ClosePreExperimentPanel();
                throw;
            }
        }

        private void RefreshPreExperimentPanel()
        {
            var main = MainController.Instance;
            if (main == null || !main.PreExperimentActive && !preparing)
            {
                ClosePreExperimentPanel();
                return;
            }
            if (!PlacePreExperimentPanel(main.StatusOverlaySettings.targetDisplay) && !preparing)
            {
                CancelPreExperiment();
                UpdateStatus("Pre-experiment setup closed because the operator controls no longer fit outside the LED panels.");
                return;
            }
            UpdatePreExperimentPanel();
        }

        private bool PlacePreExperimentPanel(int preferredDisplay)
        {
            float scale = panel.scale;
            foreach (int display in new[] { preferredDisplay, preExperimentOriginalDisplay }.Distinct())
            {
                if (display < 0 || display >= Display.displays.Length) continue;
                if (display > 0 && !Display.displays[display].active) Display.displays[display].Activate();
                var screen = OperatorOverlayLayout.DisplaySize(display);
                float width = Mathf.Min(382 * scale, screen.x);
                float height = Mathf.Min((145 + Mathf.Clamp(preExperimentRigs.Count, 2, 12) * 20) * scale, screen.y);
                var preferred = new Rect(screen.x - width - 12, screen.y - height - 12, width, height);
                if (!OperatorOverlayLayout.Reserve(PreExperimentOverlay, display, preferred, out var bounds))
                {
                    preferred.width = Mathf.Min(330 * scale, screen.x);
                    preferred.height = Mathf.Min(190 * scale, screen.y);
                    if (!OperatorOverlayLayout.Reserve(PreExperimentOverlay, display, preferred, out bounds)) continue;
                }

                panel.targetDisplay = display;
                preExperimentPanel.style.left = bounds.x / scale;
                preExperimentPanel.style.top = (screen.y - bounds.yMax) / scale;
                preExperimentPanel.style.width = bounds.width / scale;
                preExperimentPanel.style.height = bounds.height / scale;
                preExperimentPanel.style.visibility = Visibility.Visible;
                return true;
            }
            preExperimentPanel.style.visibility = Visibility.Hidden;
            return false;
        }

        private List<string> UpdatePreExperimentPanel(string message = null)
        {
            if (message != null) preExperimentMessage = message;
            var main = MainController.Instance;
            var missing = new List<string>();
            if (main == null || !PreExperimentOpen) return missing;
            foreach (var entry in preExperimentRigs)
            {
                main.VRClosedLoops.TryGetValue(entry.Key, out var rig);
                var listener = rig != null ? rig.GetComponent<ZmqListener>() : null;
                string state = CurrentDemo ? "Demo input" : listener?.State ?? "Waiting for tracking";
                bool fresh = CurrentDemo || state == "receiving";
                double age = listener?.SecondsSinceLastPose ?? double.PositiveInfinity;
                string sampleAge = double.IsInfinity(age) ? "" : $" · {Math.Max(0, age):F2} s ago";
                string pose = fresh && rig != null ? $" · yaw {rig.transform.eulerAngles.y:0}°" : "";
                entry.Value.text = $"{entry.Key} · {state}{sampleAge}{pose}";
                entry.Value.EnableInClassList("warning", !fresh);
                if (!fresh) missing.Add(entry.Key);
            }

            string missingRigs = string.Join(", ", missing);
            if (preExperimentConfirmation != missingRigs) preExperimentConfirmation = null;
            preExperimentStatus.text = preparing ? (preparationStatus ?? "Starting experiment…") :
                preExperimentConfirmation != null ? "Start anyway with tracking still missing or stale for " + missingRigs + "?" :
                preExperimentMessage ?? (missing.Count == 0 ? "Waiting to start · Place animals and check motion feedback." :
                    $"Waiting to start · {preExperimentRigs.Count - missing.Count}/{preExperimentRigs.Count} rigs receiving tracking.");
            preExperimentStatus.tooltip = companionWarnings.Count == 0 ? preExperimentStatus.text :
                preExperimentStatus.text + "\n" + string.Join("\n", companionWarnings);
            preExperimentStatus.EnableInClassList("warning", preExperimentConfirmation != null || !preparing && preExperimentMessage != null);
            preExperimentStart.Q<Label>().text = preExperimentConfirmation != null ? "Start anyway" : CurrentDemo ? "Start demo" : "Start experiment";
            preExperimentStart.SetEnabled(!preparing && !preExperimentFilesChanged);
            preExperimentCancel.SetEnabled(!main.SequencePreparing && !main.SequenceRunning);
            return missing;
        }

        private void CommitPreExperiment()
        {
            if (!PreExperimentOpen || preparing || MainController.Instance?.PreExperimentActive != true) return;
            if (Workspace.Documents.Any(document => document.CheckExternalChange() || document.Conflict))
            {
                preExperimentFilesChanged = true;
                preExperimentConfirmation = null;
                UpdatePreExperimentPanel("Configuration files changed. Cancel setup and prepare again with the updated files.");
                return;
            }

            string missingRigs = string.Join(", ", UpdatePreExperimentPanel());
            if (missingRigs.Length > 0 && preExperimentConfirmation != missingRigs)
            {
                preExperimentConfirmation = missingRigs;
                UpdatePreExperimentPanel();
                return;
            }
            if (missingRigs.Length > 0) companionWarnings.Add("Operator started with tracking missing or stale for " + missingRigs + ".");
            preExperimentConfirmation = null;
            preExperimentMessage = null;
            StartAfterPreviousRecording();
        }

        internal void CancelPreExperiment()
        {
            var main = MainController.Instance;
            if (!PreExperimentOpen || main?.SequencePreparing == true || main?.SequenceRunning == true) return;
            if (preparing)
            {
                preparationVersion++;
                preparing = false;
                body.SetEnabled(true);
                side.SetEnabled(true);
                RefreshStartCaption();
            }
            CloseModal();
            main?.EndPreExperiment();
            ClosePreExperimentPanel();
            RebuildPreservingView(true);
            UpdateStatus("Pre-experiment setup cancelled.");
        }

        private void FinishPreExperimentStart(MainController main)
        {
            preparing = false;
            body.SetEnabled(true);
            side.SetEnabled(true);
            RefreshStartCaption();
            string message = main?.SequenceError ?? "Experiment preparation cancelled.";
            if (main?.PreExperimentActive != true) ClosePreExperimentPanel();
            UpdateStatus(message);
        }

        private void ClosePreExperimentPanel()
        {
            OperatorOverlayLayout.Release(PreExperimentOverlay);
            preExperimentPanel?.RemoveFromHierarchy();
            preExperimentPanel = null;
            preExperimentRigs.Clear();
            preExperimentConfirmation = null;
            preExperimentMessage = null;
            preExperimentFilesChanged = false;
            foreach (var element in preExperimentHidden) element.Key.style.visibility = element.Value;
            preExperimentHidden.Clear();
            root.style.backgroundColor = StyleKeyword.Null;
            root.pickingMode = PickingMode.Position;
            panel.targetDisplay = preExperimentOriginalDisplay;
            UpdateRecordingFinalizationBanner();
            ScheduleKeyboardFocus();
        }
    }
}
