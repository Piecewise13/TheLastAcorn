using System.Collections.Generic;
using UnityEngine;

public class ParalaxManager : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        [Tooltip("Parent object. Its children move with their own randomized factors.")]
        public Transform layerRoot;

        [Tooltip("Average parallax factor for the layer. Lower = slower movement.")]
        public float baseParallaxFactor = 0.5f;

        [Tooltip("Each object's factor lands somewhere in base +/- this amount.")]
        public float variance = 0.05f;

        [Tooltip("Search the whole subtree for objects with a Renderer, instead of only direct children. Use this when the layer is split into sub-groups.")]
        public bool recurseIntoChildren = false;

        public bool includeInactiveChildren = false;

        [Tooltip("Change this to re-roll the layer's randomization without renaming objects.")]
        public int seedSalt = 0;
    }

    private struct ParallaxTarget
    {
        public Transform transform;
        public float factor;
    }

    [Tooltip("What the parallax moves against. Left alone this resolves to the rig's foreground " +
             "camera at runtime; assign something here as a fallback for scenes that have no rig.")]
    public Transform player;

    [SerializeField] private ParallaxLayer[] layers;

    [HideInInspector, SerializeField] private Transform[] backgrounds; // Legacy: flat list, superseded by layers
    [HideInInspector, SerializeField] private float[] parallaxFactors; // Legacy: paired with backgrounds

    private ParallaxTarget[] targets;

    private Vector3 previousPlayerPosition;

    private bool hasPlayer = false;

    [SerializeField] private bool isLocationDependent = false; // If true, the parallax effect will only work when the player is within a certain area

    void Start()
    {
        BuildTargets();
        ResolveReference();
    }

    /// <summary>
    /// Points <see cref="player"/> at the foreground camera and takes the baseline the deltas are
    /// measured from.
    ///
    /// The rig can legitimately be absent here — a level opened on its own, or one loaded before the
    /// scene that owns the rig. Reading through it unguarded threw part-way through Start, which left
    /// the baseline at zero while the inspector's fallback transform stayed non-null, so the first
    /// frame applied the camera's entire world position as a delta and shoved every object off the
    /// authored layout. Hence the guard, and the retry from Update.
    /// </summary>
    private void ResolveReference()
    {
        CameraRig rig = CameraRig.Current;
        Camera foreground = rig != null ? rig.Foreground : null;
        if (foreground != null)
            player = foreground.transform;

        if (player != null)
            previousPlayerPosition = player.position;
    }

    private void BuildTargets()
    {
        List<ParallaxTarget> built = new List<ParallaxTarget>();

        bool hasLayers = false;
        if (layers != null)
        {
            foreach (ParallaxLayer layer in layers)
            {
                if (layer != null && layer.layerRoot != null)
                {
                    hasLayers = true;
                    break;
                }
            }
        }

        if (hasLayers)
        {
            foreach (ParallaxLayer layer in layers)
            {
                if (layer == null || layer.layerRoot == null) continue;

                int rootHash = HashString(GetHierarchyPath(layer.layerRoot));
                int countBefore = built.Count;

                for (int i = 0; i < layer.layerRoot.childCount; i++)
                {
                    CollectTargets(layer.layerRoot.GetChild(i), i, layer, rootHash, built);
                }

                if (built.Count == countBefore)
                {
                    Debug.LogWarning($"Parallax layer '{layer.layerRoot.name}' produced no targets.", layer.layerRoot);
                }
            }
        }
        else if (backgrounds != null && backgrounds.Length > 0)
        {
            if (parallaxFactors == null || backgrounds.Length != parallaxFactors.Length)
            {
                Debug.LogError("Backgrounds and parallaxFactors arrays must be the same length.", this);
            }
            else
            {
                for (int i = 0; i < backgrounds.Length; i++)
                {
                    if (backgrounds[i] == null) continue;
                    built.Add(new ParallaxTarget { transform = backgrounds[i], factor = parallaxFactors[i] });
                }
            }
        }

        targets = built.ToArray();
    }

    private void CollectTargets(Transform node, int siblingIndex, ParallaxLayer layer, int parentHash, List<ParallaxTarget> built)
    {
        if (!layer.includeInactiveChildren && !node.gameObject.activeInHierarchy) return;

        int nodeHash = CombineHash(CombineHash(parentHash, HashString(node.name)), siblingIndex);

        // When recursing, a node that draws something is the thing we move. Descending past it would
        // make its children move twice, since they already inherit the parent's movement.
        bool isTarget = !layer.recurseIntoChildren || node.GetComponent<Renderer>() != null;

        if (isTarget)
        {
            int seed = CombineHash(nodeHash, layer.seedSalt);
            System.Random rng = new System.Random(seed == int.MinValue ? 0 : seed);
            float offset = (float)(rng.NextDouble() * 2.0 - 1.0) * layer.variance;

            built.Add(new ParallaxTarget
            {
                transform = node,
                factor = layer.baseParallaxFactor + offset
            });
            return;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            CollectTargets(node.GetChild(i), i, layer, nodeHash, built);
        }
    }

    private void Update()
    {
        if (player == null)
        {
            // The rig may have arrived since Start, so keep trying rather than sitting dead for the
            // rest of the level.
            ResolveReference();
            if (player == null) return;
        }

        Vector3 deltaMovement = player.position - previousPlayerPosition;

        // Advance the baseline every frame, gated or not. Skipping it while the player was outside
        // the trigger banked the whole absence and spent it in a single frame on re-entry, which
        // read as every object popping sideways.
        previousPlayerPosition = player.position;

        if (isLocationDependent && !hasPlayer)
            return;

        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform target = targets[i].transform;
            if (target == null) continue;

            float parallax = targets[i].factor;
            target.position += new Vector3(deltaMovement.x * parallax, deltaMovement.y * parallax, 0);
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    // FNV-1a. Written out rather than using string.GetHashCode, which is not stable across runtimes.
    private static int HashString(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    private static int CombineHash(int hash, int value)
    {
        unchecked
        {
            uint combined = (uint)hash;
            combined ^= (uint)value;
            combined *= 16777619;
            return (int)combined;
        }
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        hasPlayer = true;
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        hasPlayer = false;
    }
}
