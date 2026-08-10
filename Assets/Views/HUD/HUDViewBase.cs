using System.Collections;


public class HUDViewBase : ViewBase
{
    PlayerMove playerMove;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (playerMove != null)
            playerMove.EnableMove();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (PlayerAbilityManager.Instance == null)
            yield return null;

        playerMove = FindAnyObjectByType<PlayerMove>();

        PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;
    }

    private void OnDestroy()
    {
        if (PlayerAbilityManager.Instance == null) return;

        PlayerAbilityManager.Instance.OnAbilityUnlocked -= HandleAbilityUnlocked;
    }
}
