using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hooks a UI Slider for smooth scrubbing without fighting playback updates.
/// </summary>
[RequireComponent(typeof(Slider))]
public class ReplayScrubSlider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public ReplayController replay;

    private Slider _slider;
    private bool _dragging;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(OnValueChanged);
    }

    public void OnPointerDown(PointerEventData eventData) => _dragging = true;

    public void OnPointerUp(PointerEventData eventData)
    {
        _dragging = false;
        if (replay != null)
            replay.OnScrubReleased(_slider.value);
    }

    private void OnValueChanged(float value)
    {
        if (_dragging && replay != null)
            replay.OnScrubDrag(value);
    }

    public void SetNormalized(float value, bool fromPlayback)
    {
        if (_dragging && !fromPlayback)
            return;
        _slider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }
}
