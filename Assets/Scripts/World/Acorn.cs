using UnityEngine;
using UnityEngine.SceneManagement;

public class Acorn : MonoBehaviour, ICollectible
{
    [SerializeField] private bool isGoldenAcorn = false; // Flag to indicate if this is a golden acorn
    [SerializeField] int value = 1; // define this with team
    [SerializeField] string acornId; // Unique identifier for this acorn
    public int Value => value;
    public string AcornId => acornId;

    private void Start()
    {
        // Generate unique ID if not set
        if (string.IsNullOrEmpty(acornId))
            acornId = GenerateAcornId();

        // Check if this acorn was already collected
        string currentLevel = SceneManager.GetActiveScene().name;
        if (SaveLoadManager.IsAcornCollected(currentLevel, acornId))
            gameObject.SetActive(false);

    }

    private string GenerateAcornId()
    {
        // Generate ID based on position to ensure consistency across level reloads
        Vector3 pos = transform.position;
        return $"{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
    }

    public void OnCollected()
    {
        // Mark this acorn as collected in the save system
        string currentLevel = SceneManager.GetActiveScene().name;
        SaveLoadManager.MarkAcornCollected(currentLevel, acornId, value);
    }
    
    public bool IsGoldenAcorn()
    {
        return isGoldenAcorn;
    }
}

