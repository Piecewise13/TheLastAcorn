using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private float waitTime = 2f;
    [SerializeField] private AudioPlayer music;
    [SerializeField] private AudioPlayer ambience;

    public void LoadGame()
    {
        StartCoroutine(WaitForTransition(waitTime));
    }

    private IEnumerator WaitForTransition(float time)
    {
        FadeAudio();

        yield return new WaitForSeconds(time);
        SceneLoader.LoadNext();
    }

    private void FadeAudio()
    {
        music.FadeOut(1f);
        ambience.FadeOut(1f);
    }
}
