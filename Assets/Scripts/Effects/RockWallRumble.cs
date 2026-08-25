using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;


public class RockWallRumble : MonoBehaviour
{

    [SerializeField] private List<Transform> rockTransforms = new List<Transform>();
    [SerializeField] private float rumbleIntensity = 1.0f;
    [SerializeField] private float rumbleRange = 1.0f;

    [Header("Ramp Up")]
    [Tooltip("Seconds the rumble takes to reach full range after StartRumble")]
    [SerializeField] private float rampDuration = 1.0f;

    [Tooltip("Scales the rumble range over the ramp. Ends at 1 for a full-strength rumble")]
    [SerializeField] private AnimationCurve rampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField, ReadOnly] private bool isRumbling;
    [SerializeField, ReadOnly] private float rampTime;

    private readonly List<float> seeds = new List<float>();
    private readonly List<Vector3> startPositions = new List<Vector3>();
    private readonly List<Vector3> appliedOffsets = new List<Vector3>();

    [Button("Start Rumble")]
    public void StartRumble()
    {
        // Only restarts the ramp on a fresh start, so repeat calls cannot drop the rumble back to zero.
        if (!isRumbling)
        {
            rampTime = 0f;
        }

        isRumbling = true;
    }

    [Button("Stop Rumble")]
    public void StopRumble()
    {
        isRumbling = false;
    }

    /// <summary>
    /// Stops the rumble and snaps every rock back to where it sat when the scene loaded.
    /// </summary>
    [Button("Reset Rocks")]
    public void ResetRocks()
    {
        isRumbling = false;
        rampTime = 0f;

        for (int i = 0; i < startPositions.Count; i++)
        {
            Transform rock = rockTransforms[i];
            if (rock == null)
            {
                continue;
            }

            rock.localPosition = startPositions[i];
            appliedOffsets[i] = Vector3.zero;
        }
    }

    void Start()
    {
        foreach (Transform rock in rockTransforms)
        {
            seeds.Add(Random.value * 1000f);
            startPositions.Add(rock != null ? rock.localPosition : Vector3.zero);
            appliedOffsets.Add(Vector3.zero);
        }
    }

    // LateUpdate so the offset is added on top of the position the animator wrote this frame.
    void LateUpdate()
    {
        if (!isRumbling)
        {
            return;
        }

        rampTime += Time.deltaTime;

        float time = Time.time * rumbleIntensity;
        float amplitude = rumbleRange * EvaluateRamp();

        for (int i = 0; i < rockTransforms.Count; i++)
        {
            Transform rock = rockTransforms[i];
            if (rock == null)
            {
                continue;
            }

            float seed = seeds[i];
            float offsetX = (Mathf.PerlinNoise(seed, time) * 2f - 1f) * amplitude;
            float offsetY = (Mathf.PerlinNoise(seed + 100f, time) * 2f - 1f) * amplitude;
            Vector3 offset = new Vector3(offsetX, offsetY, 0f);

            // Last frame's offset is backed out first, so the jitter cannot random-walk the rock away
            // on frames where the animator is not writing this position.
            rock.localPosition += offset - appliedOffsets[i];
            appliedOffsets[i] = offset;
        }
    }

    private float EvaluateRamp()
    {
        // A zero duration means no ramp at all, so read the curve's end value straight away.
        float t = rampDuration > 0f ? Mathf.Clamp01(rampTime / rampDuration) : 1f;

        return rampCurve.Evaluate(t);
    }
}
