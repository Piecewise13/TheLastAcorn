using Cysharp.Threading.Tasks;
using UnityEngine;

public class UpgradePlayerView : MonoBehaviour
{
    [SerializeField] private UpgradeOptionContainer[] optionsContainers = null!;

    // Ability required to reveal each container, mapped by index.
    // Index 0 (ClimbTime) is always available; indices 1+ require the matching ability.
    private static readonly PlayerAbilityManager.Abilities?[] RequiredAbility =
    {
        null,                                  // ClimbTime — always visible
        PlayerAbilityManager.Abilities.Zoom,   // ZoomOut
        PlayerAbilityManager.Abilities.Glide,  // GlideSpeed
    };

    public async UniTask SetUp()
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
    }

    public void UpgradeSelected(int upgradeIndex)
    {
        if (upgradeIndex < 0 || upgradeIndex >= optionsContainers.Length) return;

        var stat = optionsContainers[upgradeIndex].UpgradeStat;

        if (!PlayerUpgradeManager.Instance.CanAfford(stat))
        {
            Debug.Log($"Cannot afford upgrade for {stat}: not enough acorns or already maxed.");
            return;
        }

        PlayerUpgradeManager.Instance.ApplyUpgrade(stat);
    }
}
