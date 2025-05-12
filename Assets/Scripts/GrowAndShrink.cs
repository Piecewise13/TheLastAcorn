using UnityEngine;
using System.Collections;

public class GrowAndShrink : MonoBehaviour
{
    public enum ScaleEase
    {
        Linear,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseInBounce,
        EaseOutBounce
    }

    [Header("Default Scale Values")]
    [Tooltip("The scale to grow to (can be overridden by passing parameters).")]
    public Vector3 grownScale = new Vector3(1.2f, 1.2f, 1f);

    [Tooltip("The scale to shrink to (can be overridden by passing parameters).")]
    public Vector3 shrunkScale = Vector3.one;

    [Tooltip("The duration for the scale animation (seconds).")]
    public float duration = 0.5f;

    [Header("Animation Settings")]
    [Tooltip("Select the easing type for the scale animation.")]
    public ScaleEase easeType = ScaleEase.EaseInCubic;

    [Tooltip("If true, this GameObject will automatically grow to 'grownScale' on OnEnable.")]
    public bool growOnEnable = false;

    private Coroutine currentAnim;

    private void OnEnable()
    {
        if (growOnEnable) Grow();
    }

    public void Grow() => Grow(grownScale, duration);

    public void Grow(Vector3 targetScale, float time)
    {
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateScale(targetScale, time));
    }

    public void Shrink() => Shrink(shrunkScale, duration);

    public void Shrink(Vector3 targetScale, float time)
    {
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateScale(targetScale, time));
    }

    public void ShrinkAndDisable()
    {
        if (!gameObject.activeInHierarchy) return;
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateScaleAndDisable(shrunkScale, duration));
    }

    private IEnumerator AnimateScale(Vector3 targetScale, float time)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / time);
            float easedT = EvaluateEase(t, easeType);
            transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator AnimateScaleAndDisable(Vector3 targetScale, float time)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / time);
            float easedT = EvaluateEase(t, easeType);
            transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            yield return null;
        }

        transform.localScale = targetScale;
        gameObject.SetActive(false);
    }

    private float EvaluateEase(float t, ScaleEase ease)
    {
        switch (ease)
        {
            case ScaleEase.Linear:
                return t;

            case ScaleEase.EaseInCubic:
                return t * t * t;

            case ScaleEase.EaseOutCubic:
                float tInv = 1f - t;
                return 1f - tInv * tInv * tInv;

            case ScaleEase.EaseInOutCubic:
                return t < 0.5f
                    ? 4f * t * t * t
                    : 0.5f * Mathf.Pow((2f * t) - 2f, 3) + 1f;

            case ScaleEase.EaseInBounce:
                return 1f - EaseOutBounce(1f - t);

            case ScaleEase.EaseOutBounce:
                return EaseOutBounce(t);

            default:
                return t;
        }
    }

    private float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
            return n1 * t * t;
        else if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }
        else
        {
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
