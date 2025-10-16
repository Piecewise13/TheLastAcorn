using UnityEngine;

public class FoxBush : MonoBehaviour
{
    [Header("Fox Settings")]
    static bool foxSpawned = false;
    [SerializeField] private GameObject foxPrefab;

    private static GameObject currentFox;


    [Header("Bush Management")]
    [SerializeField] private static float maxDistance = 20f;
    [SerializeField] private static float minDistance = 10f;

    private static FoxBush[] allBushes = null;




    void Awake()
    {
        if (allBushes == null)
        {
            allBushes = FindObjectsByType<FoxBush>(FindObjectsSortMode.None);
            System.Array.Sort(allBushes, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SpawnFox()
    {
        if (foxSpawned)
        {
            return;
        }

        foxSpawned = true;
        currentFox = Instantiate(foxPrefab, transform.position, Quaternion.identity);
    }

    public static void ResetFoxSpawn()
    {
        foxSpawned = false;
    }

    public static void TrySpawnFoxAtPlayer(Vector2 playerPosition)
    {
        if (foxSpawned)
        {
            return;
        }

        FoxBush[] bushes = GetClosestBushes(playerPosition);

        if (bushes.Length == 0)
            return;

        float dist0 = Vector2.Distance(bushes[0].transform.position, playerPosition);
        float dist1 = bushes.Length > 1 ? Vector2.Distance(bushes[1].transform.position, playerPosition) : float.MaxValue;

        if ((dist0 <= dist1 && dist0 > minDistance) || bushes.Length == 1)
        {
            bushes[0].SpawnFox();
        }
        else
        {
            bushes[1].SpawnFox();
        }
    }

    static public FoxBush[] GetClosestBushes(Vector2 position)
    {
        if (allBushes == null || allBushes.Length == 0)
            return new FoxBush[0];

        int n = allBushes.Length;
        if (n == 1)
            return new FoxBush[] { allBushes[0], allBushes[0] };

        float x = position.x;

        int lo = 0;
        int hi = n - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            float midX = allBushes[mid].transform.position.x;
            if (midX < x)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        int left = lo - 1;
        int right = lo;

        FoxBush[] result = new FoxBush[2];
        int idx = 0;
        while (idx < 2)
        {
            if (left < 0)
            {
                result[idx++] = allBushes[right++];
                continue;
            }

            if (right >= n)
            {
                result[idx++] = allBushes[left--];
                continue;
            }

            float dl = Mathf.Abs(allBushes[left].transform.position.x - x);
            float dr = Mathf.Abs(allBushes[right].transform.position.x - x);

            if (dl <= dr)
                result[idx++] = allBushes[left--];
            else
                result[idx++] = allBushes[right++];
        }

        return result;
    }
    
}
