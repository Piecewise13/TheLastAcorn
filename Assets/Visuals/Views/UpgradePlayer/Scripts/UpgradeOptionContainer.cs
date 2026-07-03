using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeOptionContainer : MonoBehaviour
{
    [SerializeField] private PlayerUpgradeManager.UpgradeStat upgradeStat;
    [SerializeField] private MMF_Player revealFeedback;
    [SerializeField] private GameObject lockedPanel;
    [SerializeField] private Button upgradeButton;

    private bool isHidden;

    public PlayerUpgradeManager.UpgradeStat UpgradeStat => upgradeStat;

    public void SetButtonCallback(Action onClicked)
    {
        if (upgradeButton == null) return;
        upgradeButton.onClick.AddListener(() => onClicked?.Invoke());
    }

    public async UniTask Setup()
    {
        await revealFeedback.PlayFeedbacksAsync(CancellationToken.None);
        lockedPanel.SetActive(false);
    }

    public void SetupAsHidden()
    {
        isHidden = true;
        lockedPanel.SetActive(true);

        if (upgradeButton != null)
            upgradeButton.interactable = false;
    }
}
