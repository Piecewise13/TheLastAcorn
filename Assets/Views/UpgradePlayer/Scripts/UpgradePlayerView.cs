using System.Threading;
using Player;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UpgradePlayerView : ViewBase
{
    [SerializeField] private UpgradeOptionContainer[] optionsContainers = null!;

    // Ability required to reveal each container, mapped by index.
    // Climbing is always available; Vision and Gliding require their matching abilities.
    private static readonly PlayerAbilityManager.Abilities?[] RequiredAbility =
    {
        null,                                  // Climbing
        PlayerAbilityManager.Abilities.Zoom,   // Vision
        PlayerAbilityManager.Abilities.Glide,  // Gliding
    };

    public override async UniTask Setup()
    {
        for (int i = 0; i < optionsContainers.Length; i++)
        {
            var container = optionsContainers[i];
            var required = i < RequiredAbility.Length ? RequiredAbility[i] : null;

            bool unlocked = required == null
                || PlayerAbilityManager.Instance.IsAbilityUnlocked(required.Value);

            if (unlocked)
            {
                int captured = i;
                container.SetButtonCallback(() => UpgradeSelected(captured));
                await container.Setup();
            }
            else
            {
                container.SetupAsHidden();
            }
        }
        
        await OverlayCameraController.Current.RequestPlayerOverlay();
    }

    public override async UniTask RunAsync(CancellationToken token = default)
    {
        for (int i = 0; i < optionsContainers.Length; i++)
        {
            await optionsContainers[i].RunAsync();
        }
    }

    public void UpgradeSelected(int upgradeIndex)
    {
        if (upgradeIndex < 0 || upgradeIndex >= optionsContainers.Length) return;
        
        Debug.Log("[UpgradePlayerView] Upgrade selected: ]"+upgradeIndex);

        var stat = optionsContainers[upgradeIndex].UpgradeStat;

        if (!PlayerUpgradeManager.Instance.CanAfford(stat))
        {
            Debug.Log($"Cannot upgrade {stat}: already maxed.");
            return;
        }

        PlayerUpgradeManager.Instance.CompleteUpgradeSelection(stat).Forget();
    }
}
