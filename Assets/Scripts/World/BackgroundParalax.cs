using System.Collections.Generic;
using UnityEngine;

public class BackgroundParalax : MonoBehaviour
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

    public Transform player; // Assign the player transform in the inspector

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

        player = CameraRig.Instance.Foreground.transform;

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
        if (player == null) return;

        if (isLocationDependent && !hasPlayer)
        {
            return;
        }

        Vector3 deltaMovement = player.position - previousPlayerPosition;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform target = targets[i].transform;
            if (target == null) continue;

            float parallax = targets[i].factor;
            target.position += new Vector3(deltaMovement.x * parallax, deltaMovement.y * parallax, 0);
        }

        previousPlayerPosition = player.position;
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
