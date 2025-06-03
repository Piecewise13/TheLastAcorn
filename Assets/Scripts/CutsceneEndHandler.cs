using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneEndHandler : MonoBehaviour
{
    public PlayableDirector director;

    void Start()
    {
        if (director != null) director.stopped += OnCutsceneFinished;
        else Debug.LogError("PlayableDirector not assigned!");
    }

    void OnCutsceneFinished(PlayableDirector pd)
    {
        Debug.Log("Cutscene finished! Loading next scene...");
        StartCoroutine(LoadNextScene(2f));
    }

    private IEnumerator LoadNextScene(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        SceneLoader.LoadNext();
    }
}