using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using TMPro;
using UnityEngine;

/// <summary>
/// The circle at the end of an upgrade line: a dark disc with a glowing gold ring, the stat's symbol, and
/// its name. Rides the line's tip, so it drifts with it. A locked or maxed stat's icon is dim and grey.
/// </summary>
public class UpgradeIcon : MonoBehaviour
{
    [SerializeField] private SpriteRenderer disc;
    [SerializeField] private SpriteRenderer ring;
    [Tooltip("The stat's symbol. Left empty until the icon art exists.")]
    [SerializeField] private SpriteRenderer symbol;
    [SerializeField] private TMP_Text label;

    [Header("Look")]
    [Tooltip("Covers the end of the line, so keep it mostly opaque.")]
    [SerializeField] private Color discColor = new Color(0.06f, 0.05f, 0.09f, 0.9f);
    [SerializeField] private Color ringColor = new Color(1f, 0.82f, 0.35f, 1f);

    [Header("Ring Glow")]
    [Tooltip("HDR multiplier on the ring while it sits idle. The lowest glow. Bloom starts around 6 in Level1.")]
    [Min(0f)] [SerializeField] private float idleRingIntensity = 1.5f;
    [Tooltip("HDR multiplier on the ring once the selection is on it.")]
    [Min(0f)] [SerializeField] private float highlightRingIntensity = 4f;
    [Tooltip("HDR multiplier on the ring when it has been reeled all the way into the squirrel. The brightest glow.")]
    [Min(0f)] [SerializeField] private float selectedRingIntensity = 12f;
    [SerializeField] private Color labelColor = new Color(1f, 0.9f, 0.7f, 1f);

    [Header("Locked")]
    [SerializeField] private Color lockedRingColor = new Color(0.55f, 0.5f, 0.42f, 0.5f);
    [SerializeField] private Color lockedDiscColor = new Color(0.06f, 0.05f, 0.09f, 0.6f);
    [SerializeField] private Color lockedLabelColor = new Color(0.6f, 0.57f, 0.52f, 0.6f);

    [Header("Appear")]
    [SerializeField] private float popDuration = 0.35f;
    [Tooltip("Icon size over the pop, as a multiple of its final size.")]
    [SerializeField] private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.65f, 1.15f),
        new Keyframe(1f, 1f));

    [Header("Reel In")]
    [Tooltip("Icon size once reeled all the way into the squirrel, as a multiple of its normal size.")]
    [Range(0f, 1f)] [SerializeField] private float reeledScale = 0.25f;
    [Tooltip("How far into the reel the name and symbol are gone, from 0 (tip) to 1 (squirrel).")]
    [Range(0.05f, 1f)] [SerializeField] private float detailFadeEnd = 0.35f;
    [Tooltip("How far into the reel the dark disc starts filling with the ring's glow, turning the icon into a ball of energy.")]
    [Range(0f, 1f)] [SerializeField] private float energizeStart = 0.2f;

    [Header("Highlight")]
    [Tooltip("Size of the highlighted icon, as a multiple of its normal size.")]
    [SerializeField] private float highlightScale = 1.2f;
    [Tooltip("How quickly the highlight eases in and out. Higher is snappier.")]
    [SerializeField] private float highlightSpeed = 12f;

    private float diameter;
    private float pop;       // size from the appear animation, as a multiple of diameter
    private float fade;      // 0 invisible, 1 fully shown
    private float highlight; // eased toward 1 while highlighted
    private bool highlighted;
    private float reel;      // 0 at the line's tip, 1 reeled into the squirrel
    private float presence = 1f; // dims the icon while another one is being reeled in
    private Color discTarget;
    private Color ringTarget;
    private Color labelTarget;

    public PlayerUpgradeManager.UpgradeStat Stat { get; private set; }
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Sizes and colours the icon. <paramref name="sortingOrder"/> is the disc's order; the ring, symbol and
    /// label stack above it.
    /// </summary>
    public void Setup(PlayerUpgradeManager.UpgradeStat stat, bool locked, SpriteRenderer drawWith, int sortingOrder, float worldDiameter)
    {
        Stat = stat;
        IsLocked = locked;
        diameter = worldDiameter;

        // On the squirrel's layer so the overlay camera draws it above the dim.
        SetLayer(gameObject, drawWith.gameObject.layer);
        SetSorting(disc, drawWith.sortingLayerID, sortingOrder);
        SetSorting(ring, drawWith.sortingLayerID, sortingOrder + 1);
        SetSorting(symbol, drawWith.sortingLayerID, sortingOrder + 2);
        if (label != null)
        {
            Renderer labelRenderer = label.GetComponent<Renderer>();
            labelRenderer.sortingLayerID = drawWith.sortingLayerID;
            labelRenderer.sortingOrder = sortingOrder + 3;
            label.text = stat.ToString();
        }

        discTarget = locked ? lockedDiscColor : discColor;
        ringTarget = locked ? lockedRingColor : ringColor;
        labelTarget = locked ? lockedLabelColor : labelColor;

        transform.localPosition = Vector3.zero;
        pop = 0f;
        fade = 0f;
        Apply();
    }

    /// <summary>Marks this icon as the one the player's selection is on. Locked icons ignore it.</summary>
    public void SetHighlighted(bool value) => highlighted = value && !IsLocked;

    /// <summary>How far the icon has been reeled in, 0 at the tip to 1 at the squirrel. Drives the ring's glow.</summary>
    public void SetReel(float progress) => reel = Mathf.Clamp01(progress);

    /// <summary>Scales the icon's opacity, so the others can step back while one is reeled in.</summary>
    public void SetPresence(float value) => presence = Mathf.Clamp01(value);

    public async UniTask Appear(CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            float t = elapsed / popDuration;
            pop = popCurve.Evaluate(t);
            fade = Mathf.Clamp01(t * 2f);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        pop = popCurve.Evaluate(1f);
        fade = 1f;
    }

    /// <summary>Fades the icon out. Temporary until the chosen/unchosen dissipate exists.</summary>
    public async UniTask FadeOut(float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fade = 1f - elapsed / duration;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        fade = 0f;
    }

    private void LateUpdate()
    {
        highlight = Mathf.MoveTowards(highlight, highlighted ? 1f : 0f, highlightSpeed * Time.deltaTime);
        Apply();
    }

    // Size and colour come from independent animations (pop, fade, highlight, reel, presence), combined here
    // once a frame so none of them overwrites another.
    private void Apply()
    {
        float eased = Smooth(highlight);
        float reeled = Smooth(reel);
        transform.localScale = Vector3.one *
                               (diameter * pop * Mathf.Lerp(1f, highlightScale, eased) * Mathf.Lerp(1f, reeledScale, reeled));

        // Idle is the dimmest; the highlight lifts it; reeling in carries it up to the brightest as the icon
        // nears the squirrel. Locked rings stay their flat grey.
        float intensity = IsLocked
            ? 1f
            : Mathf.Lerp(Mathf.Lerp(idleRingIntensity, highlightRingIntensity, eased), selectedRingIntensity, Smooth(reel));

        // As it's reeled in, the details drop away and the dark disc fills with the ring's glow, so the circle
        // arrives at the squirrel as a small, solid ball of energy.
        Color glow = Bright(ringTarget, intensity);
        float details = 1f - Smooth(Mathf.Clamp01(reel / detailFadeEnd));
        float energized = Smooth(Mathf.Clamp01((reel - energizeStart) / (1f - energizeStart)));

        float shown = fade * presence;
        disc.color = Faded(Color.Lerp(discTarget, glow, energized), shown);
        ring.color = Faded(glow, shown);
        if (symbol != null) symbol.color = Faded(IsLocked ? lockedLabelColor : Color.white, shown * details);
        if (label != null) label.color = Faded(labelTarget, shown * details);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static Color Faded(Color c, float amount) => new Color(c.r, c.g, c.b, c.a * amount);

    private static Color Bright(Color c, float scale) => new Color(c.r * scale, c.g * scale, c.b * scale, c.a);

    private static void SetSorting(SpriteRenderer renderer, int sortingLayerId, int order)
    {
        if (renderer == null) return;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;
    }

    private static void SetLayer(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayer(child.gameObject, layer);
    }
}
