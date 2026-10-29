using System;
using System.Linq;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Automation uses the same explicit Start action and preserves pending drafts.</summary>
    public sealed partial class ConfigWorkbench
    {
        private bool ControlTextEditing => root?.panel?.focusController?.focusedElement is VisualElement focused &&
            (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        public bool ControlHasUnsavedDraft => rawPending || HasUnsavedChanges || ControlTextEditing;
        public bool ControlHasModal => modal != null;
        public void StartCurrentFromControl()
        {
            if (modal != null || IsPreparing) throw new InvalidOperationException("Finish the open setup dialog/preparation before starting.");
            // A normal button click commits a delayed text field by moving focus.
            root?.panel?.focusController?.focusedElement?.Blur();
            StartExperiment();
        }
        public void StartFilesFromControl(string system, string sequence)
        {
            if (modal != null || IsPreparing) throw new InvalidOperationException("Finish the open setup dialog/preparation before starting.");
            if (ControlTextEditing)
                throw new InvalidOperationException("Finish editing the focused text field before replacing the selected setup, or use start_current.");
            RequireApplied();
            if (ControlHasUnsavedDraft) throw new InvalidOperationException("Unsaved setup changes are preserved. Save/discard them or use start_current to run the current GUI draft.");
            Workspace.SelectPrimary("system", system); Workspace.SelectPrimary("sequence", sequence);
            Select(Workspace.Sequence);
            StartExperiment();
        }
    }
}
