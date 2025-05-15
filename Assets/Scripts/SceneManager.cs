using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public void SceneChanger(int id)
    {
        //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
}
