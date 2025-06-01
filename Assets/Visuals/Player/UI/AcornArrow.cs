using UnityEngine;

public class AcornArrow : MonoBehaviour
{

    [SerializeField] private GameObject[] acorns;
    [SerializeField] private GameObject[] goldAcorns;
    [SerializeField] private GameObject arrow;

    [SerializeField] private GameObject goldArrow;

    private GameObject[] spawnedArrows;

    private GameObject[] spawnedGoldArrows;

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
        for (int i = 0; i < acorns.Length; i++)
        {
            if (acorns[i] == null)
            {
                spawnedArrows[i].SetActive(false);
                continue;
            }
            

            Vector3 acornWorldPos = acorns[i].transform.position;
            Vector3 screenPos = playerCamera.WorldToViewportPoint(acornWorldPos);

            bool isBehind = screenPos.z < 0;
            bool isOnScreen = screenPos.x >= 0 && screenPos.x <= 1 && screenPos.y >= 0 && screenPos.y <= 1 && !isBehind;

            if (isOnScreen)
            {
                spawnedArrows[i].SetActive(false);
                continue;
            }
            else
            {
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

            Vector3 arrowWorldPos = playerCamera.ViewportToWorldPoint(new Vector3(clampedViewportPos.x, clampedViewportPos.y, playerCamera.nearClipPlane + 0.5f));
            spawnedArrows[i].transform.position = arrowWorldPos;

            // Point arrow towards acorn (only rotate on Z axis)
            Vector3 dir = (acornWorldPos - arrowWorldPos).normalized;
            Vector3 dirOnScreen = new Vector3(dir.x, dir.y, 0f);
            float angle = Mathf.Atan2(dirOnScreen.y, dirOnScreen.x) * Mathf.Rad2Deg;
            spawnedArrows[i].transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        
        for (int i = 0; i < goldAcorns.Length; i++)
        {
            if (goldAcorns[i] == null){
                spawnedGoldArrows[i].SetActive(false);
                continue;
            }

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

            Vector3 arrowWorldPos = playerCamera.ViewportToWorldPoint(new Vector3(clampedViewportPos.x, clampedViewportPos.y, playerCamera.nearClipPlane + 0.5f));
            spawnedGoldArrows[i].transform.position = arrowWorldPos;

            // Point arrow towards acorn (only rotate on Z axis)
            Vector3 dir = (goldAcornWorldPos - arrowWorldPos).normalized;
            Vector3 dirOnScreen = new Vector3(dir.x, dir.y, 0f);
            float angle = Mathf.Atan2(dirOnScreen.y, dirOnScreen.x) * Mathf.Rad2Deg;
            spawnedGoldArrows[i].transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
    

}
