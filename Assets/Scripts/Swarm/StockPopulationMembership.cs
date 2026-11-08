using System;
using UnityEngine;

/// <summary>Invalidates a generated stock roster if an agent's scene membership changes.</summary>
internal sealed class StockPopulationMembership : MonoBehaviour
{
    internal event Action Changed;

    private void OnEnable() => Changed?.Invoke();
    private void OnDisable() => Changed?.Invoke();
    private void OnDestroy() => Changed?.Invoke();
    private void OnTransformParentChanged() => Changed?.Invoke();
}
