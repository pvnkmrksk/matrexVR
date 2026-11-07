using UnityEngine;

/// <summary>Submit native locust batches after capture, before the cameras render.</summary>
[DefaultExecutionOrder(1450)]
public sealed class NativeLocustBatchSubmission : MonoBehaviour
{
    private void LateUpdate() => NativeLocustBatchRenderer.SubmitFrame();
}
