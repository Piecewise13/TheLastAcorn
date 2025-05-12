using System.Collections;
using UnityEngine;
using TMPro;

public class DisplayScore : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    
    private void OnEnable()  => StartCoroutine(WaitAndSubscribe());
    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= UpdateLabel;
    }
    
    private IEnumerator WaitAndSubscribe()
    {
        while (ScoreManager.Instance == null) yield return null;

        ScoreManager.Instance.OnScoreChanged += UpdateLabel;
        UpdateLabel(ScoreManager.Instance.CurrentScore);
    } 
    
    private void UpdateLabel(int value)
    {
        scoreText.text = $"Score: {value}";
    }
}