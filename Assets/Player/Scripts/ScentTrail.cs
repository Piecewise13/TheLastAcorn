using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// A single scent-trail instance. Lives on the scent-trail prefab and drives a Visual Effect
/// Graph that draws a trail between the acorn and the player. It feeds the VFX two exposed
/// Vector3 (Position) properties each frame: "Player" and "Acorn".
/// </summary>
public class ScentTrail : MonoBehaviour
{
    [SerializeField] private VisualEffect visualEffect;

    [Tooltip("Name of the exposed Vector3/Position property on the VFX for the player location.")]
    [SerializeField] private string playerProperty = "Player";
    [Tooltip("Name of the exposed Vector3/Position property on the VFX for the acorn location.")]
    [SerializeField] private string acornProperty = "Acorn";

    // Set the trail's source acorn and the player it points toward.
    private Transform playerGraphics;
    private Transform acornTransform;

    private int playerPropertyId;
    private int acornPropertyId;

    public Acorn Target { get; private set; }

    private void Awake()
    {
        if (visualEffect == null)
        {
            visualEffect = GetComponent<VisualEffect>();
        }

        playerPropertyId = Shader.PropertyToID(playerProperty);
        acornPropertyId = Shader.PropertyToID(acornProperty);
    }

    /// <summary>
    /// Configures the trail between the given acorn and the player's graphics.
    /// </summary>
    /// <param name="origin">The player graphics transform.</param>
    /// <param name="acorn">The acorn this trail belongs to.</param>
    public void SetEndpoints(Transform origin, Acorn acorn)
    {
        playerGraphics = origin;
        Target = acorn;
        acornTransform = acorn != null ? acorn.transform : null;
    }

    private void Update()
    {
        if (playerGraphics == null || acornTransform == null || visualEffect == null)
        {
            return;
        }

        if (visualEffect.HasVector3(playerPropertyId))
        {
            visualEffect.SetVector3(playerPropertyId, playerGraphics.position);
        }

        if (visualEffect.HasVector3(acornPropertyId))
        {
            visualEffect.SetVector3(acornPropertyId, acornTransform.position);
        }
    }
}
