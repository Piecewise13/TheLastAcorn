using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

namespace ScriptEffects {
public class ShakeEffect : MonoBehaviour
{
    [Tooltip("Leave null to use parent object's transform")]
    [SerializeField] private Transform shakeTarget = null!;

    [Header("Shake Settins")] 
    [SerializeField] private float shakeRange;
    [SerializeField] private float shakeSpeed;

    [Tooltip("Shake each child of the target separately instead of the target itself")]
    [SerializeField] private bool shakeChildren;

    [Tooltip("Also shake grandchildren and deeper. Offsets of nested targets add up with their shaking parents")]
    [ShowIf(nameof(shakeChildren))]
    [SerializeField] private bool includeNestedChildren;

    [Header("Debug")]
    [SerializeField] private float testShakeDuration = 1f;

    private readonly List<ShakeTarget> targets = new List<ShakeTarget>();
    private readonly Dictionary<Transform, ShakeTarget> targetsByTransform = new Dictionary<Transform, ShakeTarget>();
    private bool isShaking;
    private float globalAmplitudeScale = 1f;
    private CancellationTokenSource testShakeCts;

    private class ShakeTarget
    {
        public Transform Transform = null!;
        public Vector3 RestPosition;
        public float NoiseSeedX;
        public float NoiseSeedY;
        public float AmplitudeScale;
    }

    /// <summary>
    /// Scales every target's shake at once. Ramp from 0 to 1 to fade a rumble in.
    /// </summary>
    public float GlobalAmplitudeScale
    {
        get => globalAmplitudeScale;
        set => globalAmplitudeScale = value;
    }

    // Resolved in Awake so callers can StartShake() before this component's own Start runs.
    void Awake()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform;
        }
    }

    void Start()
    {
        CollectTargets();
    }

    void Update()
    {
        if (!isShaking)
        {
            return;
        }

        Shake();
    }

    private void CollectTargets()
    {
        targets.Clear();
        targetsByTransform.Clear();

        if (!shakeChildren)
        {
            AddTarget(shakeTarget);
            return;
        }

        if (includeNestedChildren)
        {
            AddDescendants(shakeTarget);
            return;
        }

        foreach (Transform child in shakeTarget)
        {
            AddTarget(child);
        }
    }

    private void AddDescendants(Transform parent)
    {
        foreach (Transform child in parent)
        {
            AddTarget(child);
            AddDescendants(child);
        }
    }

    private void AddTarget(Transform target)
    {
        // Each target gets its own seeds so they shake independently rather than in lockstep.
        ShakeTarget entry = new ShakeTarget
        {
            Transform = target,
            RestPosition = target.localPosition,
            NoiseSeedX = Random.value * 1000f,
            NoiseSeedY = Random.value * 1000f,
            AmplitudeScale = 1f
        };

        targets.Add(entry);
        targetsByTransform[target] = entry;
    }
    
    private void Shake(){
        float time = Time.time * shakeSpeed;

        foreach (ShakeTarget target in targets)
        {
            float amplitude = shakeRange * globalAmplitudeScale * target.AmplitudeScale;

            // Perlin noise is remapped from 0..1 to -1..1 so the object shakes around its rest position.
            float offsetX = (Mathf.PerlinNoise(target.NoiseSeedX, time) * 2f - 1f) * amplitude;
            float offsetY = (Mathf.PerlinNoise(target.NoiseSeedY, time) * 2f - 1f) * amplitude;

            target.Transform.localPosition = target.RestPosition + new Vector3(offsetX, offsetY, 0f);
        }
    }

    public bool IsTracking(Transform target)
    {
        return targetsByTransform.ContainsKey(target);
    }

    public Vector3 GetRestPosition(Transform target)
    {
        return targetsByTransform.TryGetValue(target, out ShakeTarget entry)
            ? entry.RestPosition
            : target.localPosition;
    }

    /// <summary>
    /// Moves the position a target shakes around. Drive a shaking target through this rather than
    /// writing to its transform, which Shake() would overwrite on the next frame.
    /// </summary>
    public void SetRestPosition(Transform target, Vector3 localRestPosition)
    {
        if (targetsByTransform.TryGetValue(target, out ShakeTarget entry))
        {
            entry.RestPosition = localRestPosition;
        }
    }

    /// <summary>
    /// Scales one target's shake, so it can settle while the others keep shaking.
    /// </summary>
    public void SetAmplitudeScale(Transform target, float scale)
    {
        if (targetsByTransform.TryGetValue(target, out ShakeTarget entry))
        {
            entry.AmplitudeScale = scale;
        }
    }

    public void StartShake()
    {
        if (isShaking)
        {
            return;
        }

        CollectTargets();
        isShaking = true;
    }

    public void StopShake()
    {
        if (!isShaking)
        {
            return;
        }

        isShaking = false;

        foreach (ShakeTarget target in targets)
        {
            target.Transform.localPosition = target.RestPosition;
        }
    }

    [Button("Test Shake (Debug)")]
    private void DebugTestShake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter play mode to test the shake.", this);
            return;
        }

        // Cancelling restarts the timer when the button is pressed mid-test.
        testShakeCts?.Cancel();
        testShakeCts?.Dispose();
        testShakeCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        TestShake(testShakeCts.Token).Forget();
    }

    private async UniTask TestShake(CancellationToken cancellationToken)
    {
        StartShake();
        await UniTask.WaitForSeconds(testShakeDuration, cancellationToken: cancellationToken);
        StopShake();
    }

    void OnDestroy()
    {
        testShakeCts?.Cancel();
        testShakeCts?.Dispose();
    }
}
}
