using UnityEngine;
using System.Collections; // This is necessary for IEnumerator

public class LuminanceChanger : MonoBehaviour
{
    public Material targetMaterial; // Assign this in the inspector
    public float duration = 0.5f; // Duration of one fade in or fade out

    private void Start()
    {
        StartCoroutine(FadeInAndOut());
    }

    private IEnumerator FadeInAndOut()
    {
        while (true)
        {
            // Fade out (transparent)
            yield return StartCoroutine(Fade(1f, 0f));
            // Fade in (opaque)
            yield return StartCoroutine(Fade(0f, 1f));
        }
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0.0f;
        Color color = targetMaterial.color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
            targetMaterial.color = new Color(color.r, color.g, color.b, newAlpha);
            yield return null;
        }
        targetMaterial.color = new Color(color.r, color.g, color.b, endAlpha);
    }
}
