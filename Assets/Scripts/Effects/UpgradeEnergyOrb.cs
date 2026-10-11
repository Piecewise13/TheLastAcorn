using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The gold energy that leaves the acorn collection bar when a segment completes and flies to the
/// player. Spawned in world space so its trail picks up bloom.
/// </summary>
public class UpgradeEnergyOrb : MonoBehaviour
{
    [SerializeField] private TrailRenderer trail;
    [Tooltip("Optional sparkles that follow the orb. Stopped on arrival with the trail.")]
    [SerializeField] private ParticleSystem sparkles;

    [Header("Flight")]
    [SerializeField] private float flightDuration = 2.2f;
    [Tooltip("Remaps flight progress along the spiral.")]
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("How far around the player the orb circles before landing. Positive is counter-clockwise, negative is clockwise.")]
    [SerializeField] private float spiralTurns = -2f;
    [Tooltip("Distance from the player over the flight, as a multiple of the starting distance. " +
             "Rising above 1 early makes the orb swing wide before it closes in.")]
    [SerializeField] private AnimationCurve radiusCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.3f, 1.15f),
        new Keyframe(1f, 0f));
    [Tooltip("Offset from the player's pivot that the orb lands on.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

    /// <summary>How long the trail lingers. The bar drains over this so bar and trail read as one stream.</summary>
    public float TrailTime => trail != null ? trail.time : 0f;

    /// <summary>
    /// Spirals in onto the target and completes on impact, so the player's glow can start right then.
    /// The trail keeps fading out after that and the orb destroys itself. The spiral is centred on the
    /// target's live position, so the orb keeps finding the player if they move.
    /// </summary>
    public async UniTask FlyTo(Transform target, CancellationToken token)
    {
        Vector3 startOffset = transform.position - (target.position + targetOffset);
        float startRadius = new Vector2(startOffset.x, startOffset.y).magnitude;
        float startAngle = Mathf.Atan2(startOffset.y, startOffset.x);
        float sweep = spiralTurns * 2f * Mathf.PI;
        float elapsed = 0f;

        while (elapsed < flightDuration)
        {
            float t = travelCurve.Evaluate(elapsed / flightDuration);
            float angle = startAngle + sweep * t;
            float radius = startRadius * radiusCurve.Evaluate(t);
            Vector3 centre = target.position + targetOffset;
            transform.position = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        transform.position = target.position + targetOffset;

        if (trail != null) trail.emitting = false;
        if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        FadeOutAndDestroy(destroyCancellationToken).Forget();
    }

    private async UniTask FadeOutAndDestroy(CancellationToken token)
    {
        await UniTask.WaitForSeconds(TrailTime, cancellationToken: token);
        Destroy(gameObject);
    }
}
