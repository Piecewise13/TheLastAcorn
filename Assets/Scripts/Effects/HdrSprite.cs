using UnityEngine;

/// <summary>
/// Sets an HDR colour on a SpriteRenderer so it survives to the screen and can bloom.
/// <para>
/// <see cref="SpriteRenderer.color"/> is clamped to 1 on some render paths (imported sprites, and any sprite
/// drawn through a <see cref="SpriteMask"/>), so a colour of 6 comes out the same as 1. The URP sprite shaders
/// also multiply by a <c>_Color</c> material property, which is not clamped. This keeps the renderer colour in
/// normal range for hue and alpha, and carries the brightness above 1 through <c>_Color</c>.
/// </para>
/// </summary>
public static class HdrSprite
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static MaterialPropertyBlock block;

    public static void SetColor(SpriteRenderer renderer, Color color)
    {
        if (renderer == null) return;

        float boost = Mathf.Max(1f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
        renderer.color = new Color(color.r / boost, color.g / boost, color.b / boost, color.a);

        // Both colours are converted to linear before they multiply, and that conversion is a power curve, so
        // splitting the colour this way lands on the same value as setting it in one piece.
        block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(ColorId, new Color(boost, boost, boost, 1f));
        renderer.SetPropertyBlock(block);
    }
}
