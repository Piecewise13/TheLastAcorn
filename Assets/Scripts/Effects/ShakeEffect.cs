using System.Collections;
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

    [Header("Debug")]
    [SerializeField] private float testShakeDuration = 1f;

    private bool isShaking;
    private Vector3 restPosition;
    private float noiseSeedX;
    private float noiseSeedY;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform;
        }

        restPosition = shakeTarget.localPosition;
        noiseSeedX = Random.value * 1000f;
        noiseSeedY = Random.value * 1000f;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isShaking)
        {
            return;
        }

        Shake();
    }

    private void Shake(){
        float time = Time.time * shakeSpeed;

        // Perlin noise is remapped from 0..1 to -1..1 so the object shakes around its rest position.
        float offsetX = (Mathf.PerlinNoise(noiseSeedX, time) * 2f - 1f) * shakeRange;
        float offsetY = (Mathf.PerlinNoise(noiseSeedY, time) * 2f - 1f) * shakeRange;

        shakeTarget.localPosition = restPosition + new Vector3(offsetX, offsetY, 0f);
    }

    public void StartShake()
    {
        if (isShaking)
        {
            return;
        }

        restPosition = shakeTarget.localPosition;
        isShaking = true;
    }

    public void StopShake()
    {
        if (!isShaking)
        {
            return;
        }

        isShaking = false;
        shakeTarget.localPosition = restPosition;
    }

    [Button("Test Shake (Debug)")]
    private void DebugTestShake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter play mode to test the shake.", this);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(TestShakeRoutine());
    }

    private IEnumerator TestShakeRoutine()
    {
        StartShake();
        yield return new WaitForSeconds(testShakeDuration);
        StopShake();
    }
}
}
