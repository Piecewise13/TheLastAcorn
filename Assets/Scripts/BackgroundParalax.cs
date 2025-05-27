using UnityEngine;

public class BackgroundParalax : MonoBehaviour
{
    public Transform player; // Assign the player transform in the inspector
    public Transform[] backgrounds; // Assign the 3 tree background transforms in the inspector
    public float[] parallaxFactors; // Set 3 values (0 < value < 1), lower = slower movement

    private Vector3 previousPlayerPosition;

    void Awake()
    {
        if (backgrounds.Length != parallaxFactors.Length)
        {
            Debug.LogError("Backgrounds and parallaxFactors arrays must be the same length.");
        }
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        if (player != null)
            previousPlayerPosition = player.position;
    }

    void Update()
    {
        if (player == null) return;

        Vector3 deltaMovement = player.position - previousPlayerPosition;

        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] == null) continue;
            float parallax = parallaxFactors[i];
            Vector3 backgroundTargetPos = backgrounds[i].position + new Vector3(deltaMovement.x * parallax, 0, 0);
            backgrounds[i].position = backgroundTargetPos;
        }

        previousPlayerPosition = player.position;
    }
}
