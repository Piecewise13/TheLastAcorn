using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD side of the life system: swaps the life meter icon to match PlayerLifeManager's life count
/// and plays the UI sound when a life is lost. The manager is persistent and the HUD is not, so
/// the icon reference lives here rather than being serialized on the manager.
/// </summary>
public class LifeMeterDisplay : MonoBehaviour
{
    [SerializeField] private Image lifeIcon;

    [Tooltip("Indexed by lives remaining: element 0 is the empty meter, the last is full.")]
    [SerializeField] private Sprite[] lifeIcons;

    [SerializeField] private AudioPlayer lifeLostSfx;

    private int displayedLives = -1;
    private bool subscribed;

    private void OnEnable()
    {
        SubscribeWhenReady(destroyCancellationToken).Forget();
    }

    private void OnDisable()
    {
        if (PlayerLifeManager.Instance != null)
            PlayerLifeManager.Instance.OnLivesChanged -= HandleLivesChanged;

        subscribed = false;
    }

    // The HUD can come online before GameManager has spawned the persistent managers.
    private async UniTask SubscribeWhenReady(CancellationToken cancellationToken)
    {
        bool canceled = await UniTask.WaitUntil(() => PlayerLifeManager.Instance != null,
                                                cancellationToken: cancellationToken)
                                     .SuppressCancellationThrow();
        if (canceled || subscribed) return;

        PlayerLifeManager.Instance.OnLivesChanged += HandleLivesChanged;
        subscribed = true;

        var manager = PlayerLifeManager.Instance;
        displayedLives = manager.CurrentLives;
        ShowLives(manager.CurrentLives);
    }

    private void HandleLivesChanged(int currentLives, int maxLives)
    {
        if (currentLives < displayedLives) lifeLostSfx?.Play();

        displayedLives = currentLives;
        ShowLives(currentLives);
    }

    private void ShowLives(int currentLives)
    {
        if (lifeIcon == null || lifeIcons == null || lifeIcons.Length == 0) return;

        lifeIcon.sprite = lifeIcons[Mathf.Clamp(currentLives, 0, lifeIcons.Length - 1)];
    }
}
