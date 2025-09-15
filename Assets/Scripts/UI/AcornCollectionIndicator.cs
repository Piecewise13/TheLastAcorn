using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using System;
using UnityEditor.MPE;

public class AcornCollectionIndicator : MonoBehaviour
{

    private PlayerCamera playerCamera;

    [Header("Discrete Indicator")]

    public GameObject discreteObject;

    public GameObject acornIndicator;

    public GameObject goldenAcornIndicator;

    public Sprite acornIndicatorEmpty;

    public Sprite acornIndicatorFull;

    public Sprite goldenAcornIndicatorEmpty;

    public Sprite goldenAcornIndicatorFull;

    public GameObject[] acornIndicators;
    public GameObject[] goldenAcornIndicators;


    [Header("Progress Bar")]
    public GameObject progressBarObject;
    public Slider minCollectedSlider;

    public GameObject slotIndicator;

    public GameObject slotManager;

    //The total acorns collected in the scene
    private int collectedAcornScore = 0;

    // Track acorns collected in current scene session since scene load or checkpoint reached
    private int collectedSceneAcornCount = 0;

    public int sceneAcornCount => collectedSceneAcornCount;


    //The total number of Acorns in the scene regardless of collection status
    private int totalAcornsInScene = 0;
    //The number of collected Acorns
    private int collectedAcornsInScene = 0;


    //The total number of Golden Acorns in the scene regardless of collection status
    private int totalGoldenAcornsInScene = 0;
    //number of collected Golden Acorns
    private int collectedGoldenAcornsInScene = 0;

    //A map of the AcornID's and the Acorn script so that we can access the info when an acorn is collected
    private Dictionary<string, Acorn> sceneAcorns = new Dictionary<string, Acorn>();

    //An array of the all the Acorns in the scene
    private Acorn[] acorns;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        discreteObject.SetActive(false);
        progressBarObject.SetActive(false);

        playerCamera = GetComponentInParent<PlayerCamera>();


        ScoreManager.Instance.OnScoreChanged += UpdateUI;


        playerCamera.OnZoomChanged += UpdateVisibility;


        //minCollectedSlider = GetComponentInChildren<Slider>();

        acorns = FindObjectsByType<Acorn>(UnityEngine.FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var item in acorns)
        {
            sceneAcorns[item.AcornId] = item;
        }

        ParseCollectedAcorns();


        acornIndicators = new GameObject[totalAcornsInScene];
        goldenAcornIndicators = new GameObject[totalGoldenAcornsInScene];


        if (collectedAcornScore < ScoreManager.Instance.GetMinimumScore())
        {
            progressBarObject.SetActive(true);
            discreteObject.SetActive(false);
            SpawnSlots();
        }
        else
        {
            progressBarObject.SetActive(false);
            discreteObject.SetActive(true);
            SpawnDiscreteAcorns();
        }



    }



    /// <summary>
    /// When the player zooms out, we want to display the relevent UI
    /// </summary>
    /// <param name="isVisible">whether the UI should be visible</param>
    public void UpdateVisibility(bool isVisible)
    {

        if (collectedAcornScore < ScoreManager.Instance.GetMinimumScore())
        {
            progressBarObject.SetActive(isVisible);
            discreteObject.SetActive(!isVisible);
        }
        else
        {
            progressBarObject.SetActive(!isVisible);
            discreteObject.SetActive(isVisible);
        }

        gameObject.SetActive(isVisible);

    }

    /// <summary>
    /// Called when the score changes. We can use this to display the relevent UI for a short time
    /// </summary>
    /// <param name="score"></param>
    public void UpdateUI(int score)
    {

        if (score == 0)
        {
            return;
        }

        ParseCollectedAcorns();

        ClearDiscreteAcorns();
        SpawnDiscreteAcorns();

        gameObject.SetActive(true);



        if (collectedAcornScore <= ScoreManager.Instance.GetMinimumScore())
        {

            progressBarObject.SetActive(true);
            discreteObject.SetActive(false);

            float targetValue = (float)collectedAcornScore / ScoreManager.Instance.GetMinimumScore();
            StartCoroutine(FlashProgressBar(progressBarObject, targetValue));

            minCollectedSlider.value = targetValue;
        }
        else
        {

            progressBarObject.SetActive(false);
            discreteObject.SetActive(true);
        }

    }

    IEnumerator FlashProgressBar(GameObject indicator, float targetValue)
    {
        progressBarObject.SetActive(true);

        float flashDuration = 2f;
        float holdDuration = 1.5f;
        float elapsedTime = 0f;

        float originalVal = minCollectedSlider.value;

        while (elapsedTime < flashDuration + holdDuration)
        {
            minCollectedSlider.value = Mathf.Lerp(originalVal, targetValue, elapsedTime / flashDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        progressBarObject.SetActive(false);
        gameObject.SetActive(false);
    }

    IEnumerator FlashDiscreteIndicator(GameObject indicator)
    {
        float flashDuration = 0.1f;
        float elapsedTime = 0f;

        Image img = indicator.GetComponent<Image>();
        Color originalColor = img.color;
        Color flashColor = Color.yellow; // Change to desired flash color

        while (elapsedTime < flashDuration)
        {
            img.color = Color.Lerp(originalColor, flashColor, (elapsedTime / flashDuration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        img.color = originalColor;
    }

    /// <summary>
    /// Parse the acorns in the scene to determine which have been collected and update the relevant counts
    /// </summary>
    private void ParseCollectedAcorns()
    {

        collectedAcornScore = 0;

        collectedAcornsInScene = 0;
        collectedGoldenAcornsInScene = 0;

        totalAcornsInScene = 0;
        totalGoldenAcornsInScene = 0;

        foreach (var acorn in acorns)
        {

            if (acorn.IsGoldenAcorn())
            {
                totalGoldenAcornsInScene++;
            }
            else
            {
                totalAcornsInScene++;
            }

            if (!SaveLoadManager.IsAcornCollected(SceneManager.GetActiveScene().name, acorn.AcornId))
            {
                continue;
            }

            collectedAcornScore += acorn.Value;

            if (acorn.IsGoldenAcorn())
            {
                collectedGoldenAcornsInScene++;
            }
            else
            {
                collectedAcornsInScene++;
            }
        }



    }


    /// <summary>
    /// Helper Function: Spawns the indicators for the progress bar with even spacing
    /// </summary>
    void SpawnSlots()
    {

        RectTransform slotRect = slotManager.GetComponent<RectTransform>();
        float width = slotRect.rect.width;
        float spacing = width / (ScoreManager.Instance.GetMinimumScore());

        for (int i = 1; i < ScoreManager.Instance.GetMinimumScore(); i++)
        {
            GameObject indicator = Instantiate(slotIndicator, slotRect);
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();

            // Set anchored position for even horizontal spacing
            indicatorRect.anchoredPosition = new Vector2(spacing * (i) - (width / 2) - indicatorRect.rect.width, 0);
        }
    }


    void ClearDiscreteAcorns()
    {
        foreach (var item in acornIndicators)
        {
            Destroy(item);
        }

        foreach (var item in goldenAcornIndicators)
        {
            Destroy(item);
        }
    }  

    void SpawnDiscreteAcorns()
    {
        for (int i = 0; i < totalAcornsInScene; i++)
        {
            acornIndicators[i] = Instantiate(acornIndicator, discreteObject.transform);

            if (i < collectedAcornsInScene)
            {
                acornIndicators[i].GetComponent<Image>().sprite = acornIndicatorFull;
            }
            else
            {
                acornIndicators[i].GetComponent<Image>().sprite = acornIndicatorEmpty;
            }
        }

        for (int i = 0; i < totalGoldenAcornsInScene; i++)
        {
            goldenAcornIndicators[i] = Instantiate(goldenAcornIndicator, discreteObject.transform);

            if (i < collectedGoldenAcornsInScene)
            {
                goldenAcornIndicators[i].GetComponent<Image>().sprite = goldenAcornIndicatorFull;
            }
            else
            {
                goldenAcornIndicators[i].GetComponent<Image>().sprite = goldenAcornIndicatorEmpty;
            }
        }
    }
}
