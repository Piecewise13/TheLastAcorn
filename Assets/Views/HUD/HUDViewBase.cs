using System.Collections;


public class HUDViewBase : ViewBase
{
    PlayerMoveManager playerMoveManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void HandleAbilityUnlocked(PlayerAbilityManager.Abilities ability)
    {
        if (playerMoveManager != null)
            playerMoveManager.EnableMove();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (PlayerAbilityManager.Instance == null)
            yield return null;

        playerMoveManager = FindAnyObjectByType<PlayerMoveManager>();

        PlayerAbilityManager.Instance.OnAbilityUnlocked += HandleAbilityUnlocked;
    }

    private void OnDestroy()
    {
        if (PlayerAbilityManager.Instance == null) return;

        PlayerAbilityManager.Instance.OnAbilityUnlocked -= HandleAbilityUnlocked;
    }
}
