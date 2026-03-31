
using System.Collections.Generic;

namespace InSceneSequence
{
    public interface IInSceneSequencer : ISceneController
    {
        /// Called by MainController when the *next* SequenceStep
        ///   wants to reuse the active scene.
        void AdvanceStep(Dictionary<string, object> parameters);
    }
}