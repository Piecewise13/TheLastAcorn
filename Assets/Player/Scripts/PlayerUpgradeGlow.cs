using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The gold glow the squirrel takes on when the upgrade energy lands: a halo that swells behind it and
/// stays lit while the upgrade is chosen, plus a short gold flash over the squirrel itself. Spawned
/// onto the player's graphic for the duration of the upgrade, then destroyed.
/// </summary>
public class PlayerUpgradeGlow : MonoBehaviour
{
    [Tooltip("Soft outer glow at normal brightness, so its whole falloff shows.")]
    [SerializeField] private SpriteRenderer halo;
    [Tooltip("Small HDR centre of the halo that carries the bloom. Its sprite is generated at runtime.")]
    [SerializeField] private SpriteRenderer haloCore;
    [Tooltip("Copies the squirrel's current frame so the flash is the squirrel's silhouette.")]
    [SerializeField] private SpriteRenderer flash;
    [Tooltip("Dark disc behind the halo that blocks out the world, so the gold reads the same on any background.")]
    [SerializeField] private SpriteRenderer backing;

    [Header("Halo")]
    [SerializeField] private Color haloColor = new Color(1f, 0.82f, 0.35f, 1f);
    [Tooltip("HDR multiplier on the halo's core. Bloom only picks up pixels above the scene volume's " +
             "threshold (5.42 in Level1), so the core needs to push past it. The outer halo stays at normal " +
             "brightness: HDR there would clip its soft edge into a hard disc.")]
    [Min(0f)] [SerializeField] private float haloIntensity = 8f;
    [Tooltip("Core diameter as a fraction of the halo's.")]
    [Range(0.05f, 1f)] [SerializeField] private float coreSize = 0.6f;
    [Tooltip("Halo diameter in world units.")]
    [SerializeField] private float haloSize = 4f;
    [SerializeField] private float swellDuration = 0.45f;
    [Tooltip("Halo size over the swell, as a multiple of haloSize. Overshooting 1 gives the impact a pop.")]
    [SerializeField] private AnimationCurve swellCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 1.2f),
        new Keyframe(1f, 1f));
    [Tooltip("How much the halo's size pulses while it waits, as a fraction of haloSize.")]
    [SerializeField] private float breatheAmount = 0.06f;
    [SerializeField] private float breatheSpeed = 2f;

    [Header("Backing")]
    [SerializeField] private Color backingColor = new Color(0.02f, 0.02f, 0.05f, 0.9f);
    [Tooltip("Backing diameter as a multiple of the halo's, so the halo's soft edge fades onto dark instead of onto the world.")]
    [SerializeField] private float backingScale = 1.5f;

    [Header("Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.4f, 0.65f);
    [Tooltip("HDR multiplier on the flash colour, so its peak catches the bloom for a moment.")]
    [Min(0f)] [SerializeField] private float flashIntensity = 7f;
    [SerializeField] private float flashDuration = 0.5f;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    private static Sprite coreSprite;
    private static float coreSpriteBrightness;

    private SpriteRenderer target;
    private float haloScale; // multiple of haloSize, driven by the swell, breathe and fade
    private bool breathing;
    private bool ending;
    private CancellationTokenSource impactCts;

    /// <summary>
    /// Attaches to the squirrel's graphic and plays the impact: halo swells in, squirrel flashes gold.
    /// Completes once the impact settles; the halo then keeps breathing until <see cref="End"/>.
    /// </summary>
    public async UniTask Begin(SpriteRenderer graphic, CancellationToken token)
    {
        target = graphic;
        transform.SetParent(graphic.transform, false);

        // Same layer as the squirrel so the overlay camera draws the glow above the dim with it.
        // Back to front: backing, upgrade lines (-3, set by UpgradeLineBurst), halo, halo core, squirrel, flash.
        SetLayer(gameObject, graphic.gameObject.layer);
        backing.sortingLayerID = graphic.sortingLayerID;
        backing.sortingOrder = graphic.sortingOrder - 4;
        halo.sortingLayerID = graphic.sortingLayerID;
        halo.sortingOrder = graphic.sortingOrder - 2;
        haloCore.sortingLayerID = graphic.sortingLayerID;
        haloCore.sortingOrder = graphic.sortingOrder - 1;
        haloCore.sprite = CoreSprite(LinearBrightness(Bright(haloColor, haloIntensity)));
        flash.sortingLayerID = graphic.sortingLayerID;
        flash.sortingOrder = graphic.sortingOrder + 1;

        await PlayImpact(token);
    }

    /// <summary>Fades the halo out and destroys the glow.</summary>
    public async UniTask End(CancellationToken token)
    {
        ending = true;
        impactCts?.Cancel();
        breathing = false;
        float startScale = haloScale;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            float t = elapsed / fadeOutDuration;
            haloScale = Mathf.Lerp(startScale, 0.6f, t);
            halo.color = Color.Lerp(haloColor, Clear(haloColor), t);
            haloCore.color = Color.Lerp(Bright(haloColor, haloIntensity), Clear(Bright(haloColor, haloIntensity)), t);
            backing.color = Color.Lerp(backingColor, Clear(backingColor), t);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        impactCts?.Cancel();
        impactCts?.Dispose();
    }

    /// <summary>
    /// Resets the halo and flash and plays the swell and flash from the start. Starting a new impact cancels
    /// one already in progress, so the debug restart can be pressed mid-impact.
    /// </summary>
    private async UniTask PlayImpact(CancellationToken token)
    {
        impactCts?.Cancel();
        impactCts?.Dispose();
        impactCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        breathing = false;
        halo.color = haloColor;
        haloCore.color = Bright(haloColor, haloIntensity);
        backing.color = backingColor;
        flash.color = Clear(flashColor);
        haloScale = 0f;

        bool replaced = await UniTask.WhenAll(Swell(impactCts.Token), Flash(impactCts.Token))
            .SuppressCancellationThrow();

        // Cancelled by a newer impact (or End): that one now owns the halo, so leave breathing to it.
        if (replaced)
        {
            token.ThrowIfCancellationRequested();
            return;
        }

        breathing = true;
    }

    [Button("Debug: Restart Glow")]
    private void DebugRestartGlow()
    {
        if (!Application.isPlaying || target == null || ending)
        {
            Debug.LogWarning($"[{nameof(PlayerUpgradeGlow)}] Restart only works in Play mode while the glow is on the player.", this);
            return;
        }

        PlayImpact(destroyCancellationToken).Forget();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        flash.sprite = target.sprite;
        flash.flipX = target.flipX;
        flash.flipY = target.flipY;

        float scale = haloScale;
        if (breathing)
            scale += Mathf.Sin(Time.time * breatheSpeed * 2f * Mathf.PI) * breatheAmount;

        // Centred on the squirrel's visible bounds rather than its pivot, and sized in world units so the
        // graphic's own scale (or a flip done through scale) doesn't distort it.
        Vector3 centre = target.bounds.center;
        halo.transform.position = centre;
        halo.transform.localScale = WorldToLocalScale(halo, haloSize * scale);
        haloCore.transform.position = centre;
        haloCore.transform.localScale = WorldToLocalScale(haloCore, haloSize * scale * coreSize);
        backing.transform.position = centre;
        backing.transform.localScale = WorldToLocalScale(backing, haloSize * scale * backingScale);
    }

    private async UniTask Swell(CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < swellDuration)
        {
            haloScale = swellCurve.Evaluate(elapsed / swellDuration);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        haloScale = swellCurve.Evaluate(1f);
    }

    private async UniTask Flash(CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            // Up fast, down slow: peaks a fifth of the way in.
            float t = elapsed / flashDuration;
            float strength = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            Color peak = Bright(flashColor, flashIntensity);
            flash.color = Color.Lerp(Clear(peak), peak, strength);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        flash.color = Clear(flashColor);
    }

    private static Vector3 WorldToLocalScale(SpriteRenderer renderer, float worldDiameter)
    {
        Vector2 spriteSize = renderer.sprite != null ? (Vector2)renderer.sprite.bounds.size : Vector2.one;
        Vector3 parentScale = renderer.transform.parent.lossyScale;
        return new Vector3(
            worldDiameter / (spriteSize.x * Mathf.Abs(parentScale.x)),
            worldDiameter / (spriteSize.y * Mathf.Abs(parentScale.y)),
            1f);
    }

    // How bright an HDR colour ends up once Unity converts it to linear (a colour of 8 lands near 97).
    private static float LinearBrightness(Color c)
    {
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        return Mathf.Max(1f, Mathf.Pow(peak, 2.2f));
    }

    // A round, soft sprite for the HDR core. Alpha alone can't make an HDR glow soft: a faint edge pixel is
    // still brighter than the screen can show and clips to solid, so the colour also eases from full HDR at
    // the centre down to 1/brightness by halfway out, where the alpha fade takes over and stays visible.
    // Same approach as the upgrade lines' soft edges.
    private static Sprite CoreSprite(float brightness)
    {
        if (coreSprite != null && Mathf.Approximately(coreSpriteBrightness, brightness)) return coreSprite;

        const int size = 128;
        const float hdrRadius = 0.5f; // fraction of the radius where the colour reaches normal range
        float dimmed = 1f / brightness;
        var texture = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true)
        {
            name = "UpgradeGlowCore",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float rgb = Mathf.Lerp(1f, dimmed, Smooth(Mathf.Clamp01(r / hdrRadius)));
                float alpha = 1f - Smooth(Mathf.Clamp01((r - hdrRadius) / (1f - hdrRadius)));
                pixels[y * size + x] = new Color(rgb, rgb, rgb, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        coreSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        coreSprite.name = "UpgradeGlowCore";
        coreSpriteBrightness = brightness;
        return coreSprite;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static Color Clear(Color color) => new Color(color.r, color.g, color.b, 0f);

    // Scales the colour past 1 for bloom, leaving alpha alone so the blend amount is unchanged.
    private static Color Bright(Color color, float intensity) =>
        new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);

    private static void SetLayer(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayer(child.gameObject, layer);
    }
}
