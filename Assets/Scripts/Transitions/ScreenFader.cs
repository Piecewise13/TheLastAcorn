using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A persistent, top-of-everything white cover that survives scene loads
/// (<see cref="Object.DontDestroyOnLoad"/>, built in code so nothing needs wiring).
///
/// Its only job is the <b>load bridge</b>: a full-white cover snapped on for the instant a
/// single-mode scene swap tears down one scene's overlay white before the next has rendered its
/// own. It is white-on-white against the <c>OverlayCameraController</c> fade on either side, so the
/// bridge is invisible. Because it is a plain <see cref="RenderMode.ScreenSpaceOverlay"/> canvas it
/// draws over everything (including the player), which is exactly why it is only used for that
/// invisible instant and never for the "player visible on white" beats — those belong to the
/// overlay camera. Transition text is a pushed view, not this object.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;

    public static ScreenFader Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(ScreenFader));
                instance = go.AddComponent<ScreenFader>();
            }

            return instance;
        }
    }

    [SerializeField] private Color coverColor = Color.white;

    private Image cover;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (cover == null)
        {
            Build();
        }
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        gameObject.AddComponent<GraphicRaycaster>();

        var coverGo = new GameObject("Cover", typeof(RectTransform));
        coverGo.transform.SetParent(transform, false);
        cover = coverGo.AddComponent<Image>();
        cover.raycastTarget = false;

        RectTransform rt = cover.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        SetCoverAlpha(0f);
    }

    private void SetCoverAlpha(float a)
    {
        Color c = coverColor;
        c.a = Mathf.Clamp01(a);
        cover.color = c;
    }

    /// <summary>Snaps the bridge cover fully white this frame. Used the instant before a scene swap.</summary>
    public void ShowCoverInstant() => SetCoverAlpha(1f);

    /// <summary>Snaps the bridge cover fully transparent this frame. Used once the new scene's overlay white is up.</summary>
    public void HideCoverInstant() => SetCoverAlpha(0f);

    /// <summary>Fades the cover up to fully opaque white. Fallback for when no OverlayCameraController exists.</summary>
    public UniTask FadeToWhite(float duration, CancellationToken token = default) => FadeCover(1f, duration, token);

    /// <summary>Fades the cover back out to fully transparent. Fallback for when no OverlayCameraController exists.</summary>
    public UniTask FadeFromWhite(float duration, CancellationToken token = default) => FadeCover(0f, duration, token);

    private async UniTask FadeCover(float target, float duration, CancellationToken token)
    {
        float start = cover.color.a;

        if (duration <= 0f)
        {
            SetCoverAlpha(target);
            return;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetCoverAlpha(Mathf.Lerp(start, target, elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetCoverAlpha(target);
    }
}
