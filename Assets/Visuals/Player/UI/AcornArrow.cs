using UnityEngine;

public class AcornArrow : MonoBehaviour
{

    [SerializeField] private GameObject[] acorns;
    [SerializeField] private GameObject[] goldAcorns;
    [SerializeField] private GameObject arrow;

    [SerializeField] private GameObject goldArrow;

    [SerializeField] private float arrowScreenDistance;

    [SerializeField] private float maxAcornDistance = 50f;

    private GameObject[] spawnedArrows;

    private GameObject[] spawnedGoldArrows;

    private int numAcorns;

    private bool allCollected;

    [SerializeField]private GameObject statueArrowPrefab;

    private GameObject statueArrow;



    private Camera playerCamera;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerCamera = Camera.main;

        acorns = GameObject.FindGameObjectsWithTag("Acorn");
        goldAcorns = GameObject.FindGameObjectsWithTag("GoldAcorn");

        spawnedArrows = new GameObject[acorns.Length];
        for (int i = 0; i < acorns.Length; i++)
        {
            spawnedArrows[i] = Instantiate(arrow, transform);
        }


        spawnedGoldArrows = new GameObject[goldAcorns.Length];
        for (int i = 0; i < goldAcorns.Length; i++)
        {
            spawnedGoldArrows[i] = Instantiate(goldArrow, transform);
        }

    }

    // Update is called once per frame
    void Update()
    {

        if (allCollected)
        {
            UpdateStatueArrow();
            return;
        }
        UpdateAcornArrows();
    }

    private void UpdateAcornArrows()
    {
        numAcorns = 0;
        for (int i = 0; i < acorns.Length; i++)
        {
            if (acorns[i] == null)
            {
                spawnedArrows[i].SetActive(false);
                continue;
            }

            numAcorns++;

            Vector3 acornWorldPos = acorns[i].transform.position;
            Vector3 screenPos = playerCamera.WorldToViewportPoint(acornWorldPos);

            bool isBehind = screenPos.z < 0;
            bool isOnScreen = screenPos.x >= 0 && screenPos.x <= 1 && screenPos.y >= 0 && screenPos.y <= 1 && !isBehind;
            
            float acornDistance = Vector2.Distance(playerCamera.transform.position, acornWorldPos);



            if (isOnScreen || acornDistance > maxAcornDistance)
                // If the acorn is on screen or too far away, hide the arrow
            {
                spawnedArrows[i].SetActive(false);
                continue;
            }
            else
            {
                spawnedArrows[i].transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.3f, acornDistance/maxAcornDistance); // Reset scale in case it was changed
                spawnedArrows[i].SetActive(true);
            }

            // Clamp to edge of viewport
            Vector3 clampedViewportPos = screenPos;

            if (isBehind)
            {
                clampedViewportPos.x = 1f - clampedViewportPos.x;
                clampedViewportPos.y = 1f - clampedViewportPos.y;
                clampedViewportPos.z = Mathf.Abs(clampedViewportPos.z);
            }

            clampedViewportPos.x = Mathf.Clamp(clampedViewportPos.x, 0.05f, 0.95f);
            clampedViewportPos.y = Mathf.Clamp(clampedViewportPos.y, 0.05f, 0.95f);
            // Calculate the screen position at the edge (in pixels)
            Vector3 screenEdgePos = playerCamera.ViewportToScreenPoint(new Vector3(clampedViewportPos.x, clampedViewportPos.y, playerCamera.nearClipPlane + 0.5f));

            // Move arrowDistance pixels towards the center of the screen
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, screenEdgePos.z);
            Vector3 dirToCenter = (screenCenter - screenEdgePos).normalized;
            // Standardize arrow distance based on screen size (use a fraction of the smaller screen dimension)
            float distanceFraction = arrowScreenDistance / 1080f; // 1080 is a reference resolution
            float standardizedDistance = Mathf.Min(Screen.width, Screen.height) * distanceFraction;
            Vector3 arrowScreenPos = screenEdgePos + dirToCenter * standardizedDistance;

            // Convert back to world position
            Vector3 arrowWorldPos = playerCamera.ScreenToWorldPoint(arrowScreenPos);
            spawnedArrows[i].transform.position = arrowWorldPos;


            // Point arrow towards acorn (only rotate on Z axis)
            Vector3 dir = (acornWorldPos - arrowWorldPos).normalized;
            Vector3 dirOnScreen = new Vector3(dir.x, dir.y, 0f);
            float angle = Mathf.Atan2(dirOnScreen.y, dirOnScreen.x) * Mathf.Rad2Deg;
            spawnedArrows[i].transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        for (int i = 0; i < goldAcorns.Length; i++)
        {
            if (goldAcorns[i] == null)
            {
                spawnedGoldArrows[i].SetActive(false);
                continue;
            }
            numAcorns++;

            Vector3 goldAcornWorldPos = goldAcorns[i].transform.position;
            Vector3 screenPos = playerCamera.WorldToViewportPoint(goldAcornWorldPos);

            bool isBehind = screenPos.z < 0;
            bool isOnScreen = screenPos.x >= 0 && screenPos.x <= 1 && screenPos.y >= 0 && screenPos.y <= 1 && !isBehind;

            if (isOnScreen)
            {
                spawnedGoldArrows[i].SetActive(false);
                continue;
            }
            else
            {
                spawnedGoldArrows[i].SetActive(true);
            }

            // Clamp to edge of viewport
            Vector3 clampedViewportPos = screenPos;

            if (isBehind)
            {
                clampedViewportPos.x = 1f - clampedViewportPos.x;
                clampedViewportPos.y = 1f - clampedViewportPos.y;
                clampedViewportPos.z = Mathf.Abs(clampedViewportPos.z);
            }

            clampedViewportPos.x = Mathf.Clamp(clampedViewportPos.x, 0.05f, 0.95f);
            clampedViewportPos.y = Mathf.Clamp(clampedViewportPos.y, 0.05f, 0.95f);

            Vector3 screenEdgePos = playerCamera.ViewportToScreenPoint(new Vector3(clampedViewportPos.x, clampedViewportPos.y, playerCamera.nearClipPlane + 0.5f));

            // Move arrowDistance pixels towards the center of the screen
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, screenEdgePos.z);
            Vector3 dirToCenter = (screenCenter - screenEdgePos).normalized;
            // Standardize arrow distance based on screen size (use a fraction of the smaller screen dimension)
            float distanceFraction = arrowScreenDistance / 1080f; // 1080 is a reference resolution
            float standardizedDistance = Mathf.Min(Screen.width, Screen.height) * distanceFraction;
            Vector3 arrowScreenPos = screenEdgePos + dirToCenter * standardizedDistance;

            // Convert back to world position
            Vector3 arrowWorldPos = playerCamera.ScreenToWorldPoint(arrowScreenPos);
            spawnedGoldArrows[i].transform.position = arrowWorldPos;

            // Point arrow towards acorn (only rotate on Z axis)
            Vector3 dir = (goldAcornWorldPos - arrowWorldPos).normalized;
            Vector3 dirOnScreen = new Vector3(dir.x, dir.y, 0f);
            float angle = Mathf.Atan2(dirOnScreen.y, dirOnScreen.x) * Mathf.Rad2Deg;
            spawnedGoldArrows[i].transform.rotation = Quaternion.Euler(0, 0, angle);
            
        }

        if (numAcorns == 0)
        {
            allCollected = true;
            SpawnStatueArrow();
        }

    }

    private void SpawnStatueArrow()
    {
        if (statueArrowPrefab != null)
        {
            Destroy(statueArrowPrefab);
        }

        statueArrow = Instantiate(statueArrowPrefab, transform);
        Vector3 statuePos = GameObject.FindGameObjectWithTag("Statue").transform.position;
        statueArrow.transform.position = statuePos;

        Vector3 dir = (statuePos - playerCamera.transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        statueArrow.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    
    private void UpdateStatueArrow()
    {
        if (statueArrow == null)
        {
            SpawnStatueArrow();
            return;
        }

            Vector3 statuePos = GameObject.FindGameObjectWithTag("Statue").transform.position;
            Vector3 screenPos = playerCamera.WorldToViewportPoint(statuePos);

            bool isBehind = screenPos.z < 0;
            bool isOnScreen = screenPos.x >= 0 && screenPos.x <= 1 && screenPos.y >= 0 && screenPos.y <= 1 && !isBehind;

            if (isOnScreen)
            {
                statueArrow.SetActive(false);
                return;
            }
            else
            {
                statueArrow.SetActive(true);
            }

            // Clamp to edge of viewport
            Vector3 clampedViewportPos = screenPos;

            if (isBehind)
            {
                clampedViewportPos.x = 1f - clampedViewportPos.x;
                clampedViewportPos.y = 1f - clampedViewportPos.y;
                clampedViewportPos.z = Mathf.Abs(clampedViewportPos.z);
            }

            clampedViewportPos.x = Mathf.Clamp(clampedViewportPos.x, 0.05f, 0.95f);
            clampedViewportPos.y = Mathf.Clamp(clampedViewportPos.y, 0.05f, 0.95f);

            // Calculate the screen position at the edge (in pixels)
            Vector3 screenEdgePos = playerCamera.ViewportToScreenPoint(new Vector3(clampedViewportPos.x, clampedViewportPos.y, playerCamera.nearClipPlane + 0.5f));

            // Move arrowDistance pixels towards the center of the screen
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, screenEdgePos.z);
            Vector3 dirToCenter = (screenCenter - screenEdgePos).normalized;
            Vector3 arrowScreenPos = screenEdgePos + dirToCenter * arrowScreenDistance;

            // Convert back to world position
            Vector3 arrowWorldPos = playerCamera.ScreenToWorldPoint(arrowScreenPos);
            statueArrow.transform.position = arrowWorldPos;

            // Point arrow towards statue (only rotate on Z axis)
            Vector3 dir = (statuePos - arrowWorldPos).normalized;
            Vector3 dirOnScreen = new Vector3(dir.x, dir.y, 0f);
            float angle = Mathf.Atan2(dirOnScreen.y, dirOnScreen.x) * Mathf.Rad2Deg;
            statueArrow.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    

}
