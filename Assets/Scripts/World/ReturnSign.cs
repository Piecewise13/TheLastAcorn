using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class ReturnSign : MonoBehaviour
{

    private PlayerGameControls playerMovementMap;

    /// <summary>
    /// Input action for player movement.
    /// </summary>
    private InputAction returnAction;

    private GrowAndShrink growAndShrink;

    [Header("Components")]
    [SerializeField] GameObject returnSignIndicator;

    [Header("Scenes")]
    [SerializeField] private string nextSceneName;

    const float DELAY_BEFORE_LOAD = 3f;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();
        returnAction = playerMovementMap.Gameplay.Interact;
        returnAction.performed += InteractInput;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        growAndShrink = GetComponentInChildren<GrowAndShrink>();

    }

    // Update is called once per frame
    void Update()
    {

    }

        IEnumerator ReadySequence()
    {
       //TODO: Mateo do your thing man

        yield return new WaitForSeconds(DELAY_BEFORE_LOAD);
        SaveLoadManager.SaveCurrentLevelName(nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }

    void InteractInput(InputAction.CallbackContext context)
    {
        StartCoroutine(ReadySequence());
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        growAndShrink.Grow();
        returnSignIndicator.SetActive(true);
        returnAction.Enable();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        growAndShrink.Shrink();
        returnSignIndicator.SetActive(false);
        returnAction.Disable();
    }
}
