using System.Collections.Generic;
using UnityEngine;

public class CloudManager : MonoBehaviour
{

    [SerializeField] private List<GameObject> cloudPrefabs;

    private List<CloudData> activeClouds = new List<CloudData>();

    [SerializeField] private Transform cloudSpawnHeightMax, cloudSpawnHeightMin, cloudSpawnTravelMax;


    [SerializeField] private float cloudSpeedMin, cloudSpeedMax;

    [SerializeField] private float cloudSpawnTimeMin, cloudSpawnTimeMax;


    [SerializeField] private int initialCloudCountMax, initialCloudCountMin;

    private float cloudSpawnTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int initialCloudCount = Random.Range(initialCloudCountMin, initialCloudCountMax);

        for (int i = 0; i < initialCloudCount; i++)
        {

            float spawnX = Random.Range(transform.position.x, cloudSpawnTravelMax.position.x);

            SpawnCloud(spawnX);
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (cloudSpawnTimer <= 0f)
        {
            SpawnCloud(transform.position.x);
        }

        cloudSpawnTimer -= Time.deltaTime;

        UpdateCloudPosition();

    }


    private void UpdateCloudPosition()
    {
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            var cloudData = activeClouds[i];
            if (cloudData.cloudPrefab != null)
            {
                cloudData.cloudPrefab.transform.Translate(Vector3.right * cloudData.speed * Time.deltaTime);
                if (cloudData.cloudPrefab.transform.position.x > cloudSpawnTravelMax.position.x)
                {
                    Destroy(cloudData.cloudPrefab);
                    activeClouds.RemoveAt(i);
                }
            }
        }
    }

    private void SpawnCloud(float spawnX)
    {
        float spawnY = Random.Range(cloudSpawnHeightMin.position.y, cloudSpawnHeightMax.position.y);
        Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0f);

        GameObject cloudPrefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Count)];

        GameObject cloudInstance = Instantiate(cloudPrefab, spawnPosition, Quaternion.identity);

        cloudInstance.transform.SetParent(transform); // Set parent to manager for better organization

        float scale = cloudInstance.transform.localScale.x; // Get original scale
        if (Random.Range(0f, 1f) > 0.5f)
        {
            scale = scale * -1f; // Randomly flip the cloud horizontally
        }

        cloudInstance.transform.localScale = new Vector3(scale, cloudInstance.transform.localScale.y, 1f); // Reset scale to avoid prefab scale issues

        float speed = Random.Range(cloudSpeedMin, cloudSpeedMax);
        activeClouds.Add(new CloudData { cloudPrefab = cloudInstance, speed = speed });


        cloudSpawnTimer = Random.Range(cloudSpawnTimeMin, cloudSpawnTimeMax);

    }



    private struct CloudData
    {
        public GameObject cloudPrefab;
        public float speed;
    }
}
