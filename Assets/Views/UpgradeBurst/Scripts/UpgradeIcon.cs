using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using TMPro;
using UnityEngine;

/// <summary>
/// The circle at the end of an upgrade line: a dark disc with a glowing gold ring, the stat's symbol, and
/// its name. Rides the line's tip, so it drifts with it. Only stats that can be picked get one; a locked
/// stat's line ends in nothing.
/// </summary>
public class UpgradeIcon : MonoBehaviour
{
    [SerializeField] private SpriteRenderer disc;
    [SerializeField] private SpriteRenderer ring;
    [Tooltip("The stat's art. Drawn large and cropped to the circle by the mask; empty for a plain circle.")]
    [SerializeField] private SpriteRenderer symbol;
    [Tooltip("Circle that crops the art. Its sprite sits just inside the ring, so the ring hides the crop edge.")]
    [SerializeField] private SpriteMask mask;
    [Tooltip("Size of the art relative to the circle. Above 1 fills the circle and lets the edges crop away.")]
    [SerializeField] private float symbolScale = 1.3f;
    [SerializeField] private TMP_Text label;

    [Header("Look")]
    [Tooltip("Tint of the frosted-glass disc. Keep it light and part see-through: the frost texture " +
             "(Art/UpgradeIconDisc.png) supplies the grain, top-left light and rim, and the world shows faintly behind it.")]
    [SerializeField] private Color discColor = new Color(0.78f, 0.84f, 0.94f, 0.45f);
    [SerializeField] private Color ringColor = new Color(1f, 0.82f, 0.35f, 1f);

    [Header("Ring Glow")]
    [Tooltip("HDR multiplier on the ring while it sits idle. The lowest glow. Bloom starts around 6 in Level1.")]
    [Min(0f)] [SerializeField] private float idleRingIntensity = 1.5f;
    [Tooltip("HDR multiplier on the ring once the selection is on it.")]
    [Min(0f)] [SerializeField] private float highlightRingIntensity = 4f;
    [Tooltip("HDR multiplier on the ring when it has been reeled all the way into the squirrel. The brightest glow.")]
    [Min(0f)] [SerializeField] private float selectedRingIntensity = 12f;
    [SerializeField] private Color labelColor = new Color(1f, 0.9f, 0.7f, 1f);

    [Header("Appear")]
    [SerializeField] private float popDuration = 0.35f;
    [Tooltip("Icon size over the pop, as a multiple of its final size.")]
    [SerializeField] private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.65f, 1.15f),
        new Keyframe(1f, 1f));

    [Header("Symbol Glow")]
    [Tooltip("HDR multiplier on the art while idle. 1 shows it as drawn.")]
    [Min(0f)] [SerializeField] private float idleSymbolIntensity = 1f;
    [Tooltip("HDR multiplier on the art when highlighted, so it blooms with the ring. Bloom starts around 6 in " +
             "Level1; higher values glow more but whiten the gold lines, since the camera has no tonemapping.")]
    [Min(0f)] [SerializeField] private float highlightSymbolIntensity = 6f;

    [Header("Reel In")]
    [Tooltip("Icon size once reeled all the way into the squirrel, as a multiple of its normal size.")]
    [Range(0f, 1f)] [SerializeField] private float reeledScale = 0.25f;
    [Tooltip("How far into the reel the name and symbol are gone, from 0 (tip) to 1 (squirrel).")]
    [Range(0.05f, 1f)] [SerializeField] private float detailFadeEnd = 0.35f;
    [Tooltip("How far into the reel the dark disc starts filling with the ring's glow, turning the icon into a ball of energy.")]
    [Range(0f, 1f)] [SerializeField] private float energizeStart = 0.2f;

    [Header("Highlight")]
    [Tooltip("Size of the highlighted icon, as a multiple of its normal size.")]
    [SerializeField] private float highlightScale = 1.25f;
    [Tooltip("How quickly the highlight eases in and out. Higher is snappier.")]
    [SerializeField] private float highlightSpeed = 7f;

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

    /// <summary>
    /// Radius of the ring's outer edge as a fraction of the icon's scale. The ring texture
    /// (Art/UpgradeIconRing.png) is drawn out to 98% of its width, and the sprite is one unit across.
    /// </summary>
    public const float EdgeRadius = 0.49f;

    public PlayerUpgradeManager.UpgradeStat Stat { get; private set; }

    /// <summary>
    /// Sizes and colours the icon. <paramref name="sortingOrder"/> is the disc's order; the ring, symbol and
    /// label stack above it.
    /// </summary>
    public void Setup(PlayerUpgradeManager.UpgradeStat stat, Sprite symbolSprite, SpriteRenderer drawWith, int sortingOrder, float worldDiameter)
    {
        Stat = stat;
        diameter = worldDiameter;

        // On the squirrel's layer so the overlay camera draws it above the dim.
        // Back to front: frosted disc, art (cropped), ring framing the crop, label.
        SetLayer(gameObject, drawWith.gameObject.layer);
        SetSorting(disc, drawWith.sortingLayerID, sortingOrder);
        SetSorting(symbol, drawWith.sortingLayerID, sortingOrder + 1);
        SetSorting(ring, drawWith.sortingLayerID, sortingOrder + 2);
        if (symbol != null)
        {
            symbol.sprite = symbolSprite;
            symbol.transform.localScale = Vector3.one * symbolScale;
            symbol.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        // Limit the mask to this icon's art, so it can't crop anything else on the same sorting layer.
        if (mask != null)
        {
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = drawWith.sortingLayerID;
            mask.backSortingLayerID = drawWith.sortingLayerID;
            mask.frontSortingOrder = sortingOrder + 1;
            mask.backSortingOrder = sortingOrder;
        }
        if (label != null)
        {
            Renderer labelRenderer = label.GetComponent<Renderer>();
            labelRenderer.sortingLayerID = drawWith.sortingLayerID;
            labelRenderer.sortingOrder = sortingOrder + 3;
            label.text = stat.ToString();
        }

        discTarget = discColor;
        ringTarget = ringColor;
        labelTarget = labelColor;

        transform.localPosition = Vector3.zero;
        pop = 0f;
        fade = 0f;
        Apply();
    }

    /// <summary>
    /// Stretches this icon's circle mask down to <paramref name="sortingOrder"/>, so renderers set to draw
    /// outside masks there (the upgrade lines) are cut away inside the circle.
    /// </summary>
    public void MaskDownTo(int sortingOrder)
    {
        if (mask != null) mask.backSortingOrder = Mathf.Min(mask.backSortingOrder, sortingOrder - 1);
    }

    /// <summary>Marks this icon as the one the player's selection is on.</summary>
    public void SetHighlighted(bool value) => highlighted = value;

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
        // nears the squirrel.
        float intensity = Mathf.Lerp(Mathf.Lerp(idleRingIntensity, highlightRingIntensity, eased), selectedRingIntensity, Smooth(reel));

        // As it's reeled in, the details drop away and the dark disc fills with the ring's glow, so the circle
        // arrives at the squirrel as a small, solid ball of energy.
        Color glow = Bright(ringTarget, intensity);
        float details = 1f - Smooth(Mathf.Clamp01(reel / detailFadeEnd));
        float energized = Smooth(Mathf.Clamp01((reel - energizeStart) / (1f - energizeStart)));

        float shown = fade * presence;
        HdrSprite.SetColor(disc, Faded(Color.Lerp(discTarget, glow, energized), shown));
        HdrSprite.SetColor(ring, Faded(glow, shown));
        float symbolGlow = Mathf.Lerp(idleSymbolIntensity, highlightSymbolIntensity, eased);
        if (symbol != null) HdrSprite.SetColor(symbol, Faded(Bright(Color.white, symbolGlow), shown * details));
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
