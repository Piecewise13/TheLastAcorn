using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private float waitTime = 0.5f;
    
    public void LoadGame()
    {
        StartCoroutine(WaitForTransition(waitTime));
    }

    private IEnumerator WaitForTransition(float time)
    {
        yield return new WaitForSeconds(time);
        SceneLoader.LoadNext();
    }
}
