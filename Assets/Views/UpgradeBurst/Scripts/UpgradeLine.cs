using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using UnityEngine;

/// <summary>
/// One gold line growing out of the squirrel's glow toward where an upgrade's icon will sit. A locked
/// or maxed stat gets a wisp instead: it grows the same way, but grey, faint, and fading out before its end.
/// Drawn in the burst's local space, so it follows the squirrel.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class UpgradeLine : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    // One soft-edge texture per softness/brightness combination, shared by every line that uses it.
    private static readonly Dictionary<(int, int, int), Texture2D> SoftEdgeTextures = new Dictionary<(int, int, int), Texture2D>();

    [SerializeField] private LineRenderer line;
    [Tooltip("Sits at the end of the line; the upgrade's icon attaches here.")]
    [SerializeField] private Transform tip;

    [Header("Look")]
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.35f, 1f);
    [Tooltip("HDR multiplier on the line colour so it blooms like the halo. Bloom starts around 6 in Level1.")]
    [Min(0f)] [SerializeField] private float intensity = 6f;
    [SerializeField] private int pointCount = 24;

    [Header("Width")]
    [Tooltip("Line width at the glow end, in world units. Overrides the LineRenderer's own width curve.")]
    [Min(0f)] [SerializeField] private float rootWidth = 0.8f;
    [Tooltip("Line width at the icon end, in world units.")]
    [Min(0f)] [SerializeField] private float tipWidth = 0.25f;

    [Header("Edge Softness")]
    [Tooltip("How feathered the edges are at the glow end. 1 fades all the way from the centre of the line.")]
    [Range(0.05f, 1f)] [SerializeField] private float rootSoftness = 1f;
    [Tooltip("How feathered the edges are at the icon end. Small values give a crisp edge.")]
    [Range(0.02f, 1f)] [SerializeField] private float tipSoftness = 0.15f;

    [Header("Wiggle")]
    [Tooltip("Sideways sway as a fraction of the line's length. Keep it small; 0 turns the wiggle off.")]
    [Range(0f, 0.1f)] [SerializeField] private float wiggleAmount = 0.015f;
    [Tooltip("How many waves fit along the line.")]
    [SerializeField] private float wiggleWaves = 1.5f;
    [Tooltip("Wave cycles per second travelling out along the line.")]
    [SerializeField] private float wiggleSpeed = 0.8f;

    [Header("Drift")]
    [Tooltip("How far the tip (and the icon on it) wanders, as a fraction of the line's length. 0 holds it still.")]
    [Range(0f, 0.2f)] [SerializeField] private float driftAmount = 0.04f;
    [Tooltip("How fast the tip wanders. Low values read as floating rather than shaking.")]
    [SerializeField] private float driftSpeed = 0.15f;

    [Header("Grow")]
    [SerializeField] private float growDuration = 0.6f;
    [SerializeField] private AnimationCurve growCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.5f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("Locked")]
    [Tooltip("How far a locked line reaches, as a fraction of a full line's length.")]
    [Range(0f, 1f)] [SerializeField] private float brokenLength = 0.75f;
    [Tooltip("Where along a locked line it has faded to fully transparent, as a fraction of its own length. " +
             "Below 1, it vanishes before it ends, so a locked stat reads as a wisp rather than a line.")]
    [Range(0.1f, 1f)] [SerializeField] private float lockedFadeEnd = 0.85f;
    [Tooltip("A locked line's colour. Not HDR, and kept faint so it reads as a hint rather than a choice.")]
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.5f, 0.42f, 0.2f);

    private MaterialPropertyBlock block;
    private Vector3 end;
    private float reach;
    private Color shownColor;
    private float presence = 1f;
    private float wigglePhase;
    private Vector2 appliedWidth = new Vector2(-1f, -1f);
    private Transform icon;
    private float iconEdge;
    private Vector2 driftSeed;
    private Vector3 drift;

    public PlayerUpgradeManager.UpgradeStat Stat { get; private set; }
    public bool IsLocked { get; private set; }
    public Transform Tip => tip;

    /// <summary>
    /// Pulls the line back toward the squirrel as its icon is reeled in: 0 is fully out, 1 is all the way in.
    /// The icon rides the tip, so it slides down the line as it shortens.
    /// </summary>
    public void SetReel(float progress)
    {
        reach = 1f - Mathf.Clamp01(progress);
        Redraw();
    }

    /// <summary>
    /// Ends the drawn line at the edge of the icon riding its tip rather than at the icon's centre.
    /// <paramref name="edgeFraction"/> is the icon's radius as a fraction of its scale. Read every frame, so the
    /// line follows the icon as it pops in, highlights and shrinks while reeled.
    /// </summary>
    public void AttachIcon(Transform iconTransform, float edgeFraction)
    {
        icon = iconTransform;
        iconEdge = edgeFraction;
    }

    /// <summary>Scales the line's opacity, so it can step back while another line is reeled in.</summary>
    public void SetPresence(float value)
    {
        presence = Mathf.Clamp01(value);
        if (block != null) SetColor(shownColor);
    }

    public void Setup(PlayerUpgradeManager.UpgradeStat stat, Vector3 localEnd, bool locked, SpriteRenderer drawWith, int sortingOffset)
    {
        Stat = stat;
        IsLocked = locked;
        end = localEnd;
        // Each line starts at a different point in the wave so the fan doesn't sway in step.
        wigglePhase = Random.value * 2f * Mathf.PI;
        driftSeed = new Vector2(Random.value * 100f, Random.value * 100f);

        // On the squirrel's layer so the overlay camera draws it above the dim.
        gameObject.layer = drawWith.gameObject.layer;
        tip.gameObject.layer = drawWith.gameObject.layer;
        line.sortingLayerID = drawWith.sortingLayerID;
        line.sortingOrder = drawWith.sortingOrder + sortingOffset;
        line.useWorldSpace = false;
        // Hidden inside any icon's circle mask, so a line never shows through the frosted disc.
        line.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        line.colorGradient = AlphaAlongLine(locked ? lockedFadeEnd : 1f);

        block ??= new MaterialPropertyBlock();
        // A locked line is grey from the start; its colour isn't HDR, so the feather needs no dimming.
        Color shown = locked ? lockedColor : Bright(color, intensity);
        SetSoftEdge(LinearBrightness(shown));
        SetColor(shown);
        SetReach(0f);
    }

    public async UniTask Grow(CancellationToken token)
    {
        float target = IsLocked ? brokenLength : 1f;
        float elapsed = 0f;

        while (elapsed < growDuration)
        {
            SetReach(growCurve.Evaluate(elapsed / growDuration) * target);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetReach(target);
    }

    /// <summary>Fades the line out. Temporary until the chosen/unchosen dissipate exists.</summary>
    public async UniTask FadeOut(float duration, CancellationToken token)
    {
        Color start = IsLocked ? lockedColor : Bright(color, intensity);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            SetColor(Color.Lerp(start, Clear(start), elapsed / duration));
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        SetColor(Clear(start));
    }

    private void LateUpdate()
    {
        // Redrawn every frame so the wiggle keeps moving after the line has finished growing.
        if (line.positionCount > 0) Redraw();
    }

    private void SetReach(float value)
    {
        reach = value;
        line.positionCount = pointCount;
        Redraw();
    }

    private void Redraw()
    {
        ApplyWidth();

        // Smooth noise rather than a sine, so the tip wanders instead of tracing a visible loop.
        float t = Time.time * driftSpeed;
        drift = new Vector3(
            Mathf.PerlinNoise(driftSeed.x + t, 0f) * 2f - 1f,
            Mathf.PerlinNoise(0f, driftSeed.y + t) * 2f - 1f,
            0f) * (driftAmount * end.magnitude);

        // The tip (and the icon on it) sits at the full reach; the visible line stops short of it by the icon's
        // current radius, so it meets the circle's rim.
        float gap = icon != null ? Mathf.Abs(icon.lossyScale.x) * iconEdge / Mathf.Max(end.magnitude, 0.0001f) : 0f;
        float drawn = Mathf.Max(0f, reach - gap);
        for (int i = 0; i < pointCount; i++)
            line.SetPosition(i, PointAt(drawn * i / (pointCount - 1f)));

        tip.localPosition = PointAt(reach);
    }

    // A straight run from the origin to the end with a slow wave travelling out along it. The wave fades to
    // nothing at both ends, so the line stays rooted in the glow. The drift is scaled by distance along the
    // line, so the root stays put, the tip carries the full drift, and the line stays straight as it swings.
    private Vector3 PointAt(float u)
    {
        Vector3 perpendicular = new Vector3(-end.y, end.x, 0f).normalized;
        float envelope = Mathf.Sin(u * Mathf.PI);
        float wiggle = envelope * wiggleAmount *
                       Mathf.Sin((u * wiggleWaves - Time.time * wiggleSpeed) * 2f * Mathf.PI + wigglePhase);
        return end * u + perpendicular * (wiggle * end.magnitude) + drift * u;
    }

    private void SetColor(Color value)
    {
        shownColor = value;
        block.SetColor(BaseColorId, new Color(value.r, value.g, value.b, value.a * presence));
        line.SetPropertyBlock(block);
    }

    // Only rebuilds the curve when a width changes, so tuning in the Inspector shows up live without
    // allocating a curve every frame.
    private void ApplyWidth()
    {
        var width = new Vector2(rootWidth, tipWidth);
        if (width == appliedWidth) return;

        appliedWidth = width;
        line.widthMultiplier = 1f;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, rootWidth), new Keyframe(1f, tipWidth));
    }

    // How bright the HDR colour ends up once Unity converts it to linear (a colour of 6 lands near 51), so the
    // soft-edge texture can bring the feathered band back into displayable range.
    private static float LinearBrightness(Color c)
    {
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        return Mathf.Max(1f, Mathf.Pow(peak, 2.2f));
    }

    private void SetSoftEdge(float brightness)
    {
        block.SetTexture(BaseMapId, SoftEdgeTexture(rootSoftness, tipSoftness, brightness));
        line.SetPropertyBlock(block);
    }

    // A texture that feathers the line's edges. Across the texture (V) is across the line; along it (U) is
    // along the line, because the LineRenderer stretches the texture over its full length. The feather
    // widens from tip to root, so the line is blurred at the glow and sharp at the icon.
    //
    // Alpha alone can't do this: the line colour is HDR, so even a 5%-alpha edge pixel is still brighter than
    // the screen can show and clips to solid. So the colour is dimmed in the feathered band to 1/brightness,
    // landing the band in normal range where the alpha fade is visible. Only the solid core keeps the full
    // HDR value, which is what blooms.
    private static Texture2D SoftEdgeTexture(float root, float tip, float brightness)
    {
        var key = (Mathf.RoundToInt(root * 100f), Mathf.RoundToInt(tip * 100f), Mathf.RoundToInt(brightness));
        if (SoftEdgeTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        const int length = 128;
        const int across = 64;
        var texture = new Texture2D(length, across, TextureFormat.RGBAHalf, false, true)
        {
            name = "UpgradeLineSoftEdge",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float dimmed = 1f / brightness;
        var pixels = new Color[length * across];
        for (int y = 0; y < across; y++)
        {
            float fromCentre = Mathf.Abs((y + 0.5f) / across * 2f - 1f);
            for (int x = 0; x < length; x++)
            {
                float softness = Mathf.Lerp(root, tip, x / (length - 1f));
                float core = 1f - softness; // half-width of the solid centre, as a fraction of the half-width

                float k = Mathf.Clamp01((fromCentre - core) / softness);
                float alpha = 1f - Smooth(k);

                // Full HDR at the very centre, easing down to the dimmed value at the edge of the core.
                float toCoreEdge = core > 0f ? Mathf.Clamp01(fromCentre / core) : 1f;
                float rgb = Mathf.Lerp(1f, dimmed, Smooth(toCoreEdge));

                pixels[y * length + x] = new Color(rgb, rgb, rgb, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        SoftEdgeTextures[key] = texture;
        return texture;
    }

    // Opacity along the line, on top of its colour. 1 keeps it solid end to end; below 1 it holds solid near
    // the glow, then fades to nothing at that fraction of its length.
    private static Gradient AlphaAlongLine(float fadeEnd)
    {
        var gradient = new Gradient();
        GradientAlphaKey[] alpha = fadeEnd >= 1f
            ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeEnd * 0.25f), new GradientAlphaKey(0f, fadeEnd) };
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
        return gradient;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

    private static Color Bright(Color c, float scale) => new Color(c.r * scale, c.g * scale, c.b * scale, c.a);
}
