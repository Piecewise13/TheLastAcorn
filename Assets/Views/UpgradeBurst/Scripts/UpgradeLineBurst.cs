using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// The fan of upgrade lines that grows out of the squirrel's glow, one per stat. Each stat has a fixed
/// slot with a little jitter, so players learn where each upgrade lives. Follows the squirrel's visible
/// centre while it is alive.
/// </summary>
public class UpgradeLineBurst : MonoBehaviour
{
    [Serializable]
    private struct Slot
    {
        public PlayerUpgradeManager.UpgradeStat stat;
        [Tooltip("Degrees from straight up. Positive leans left, negative leans right.")]
        public float angle;
    }

    [SerializeField] private UpgradeLine linePrefab;
    [Tooltip("Circle that pops in at each line's tip once the line has grown.")]
    [SerializeField] private UpgradeIcon iconPrefab;
    [SerializeField] private Slot[] slots =
    {
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Climbing, angle = 50f },
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Vision, angle = 0f },
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Gliding, angle = -50f },
    };

    [Header("Layout")]
    [Tooltip("Random spread added to each slot's angle, in degrees either way.")]
    [SerializeField] private float angleJitter = 8f;
    [Tooltip("Line length as a fraction of the camera's orthographic size, so the fan fits the frame at any zoom.")]
    [SerializeField] private float lengthFraction = 0.45f;
    [Tooltip("Random spread on each line's length, as a fraction of it.")]
    [Range(0f, 0.5f)] [SerializeField] private float lengthJitter = 0.1f;
    [Tooltip("Seconds between one line starting and the next.")]
    [SerializeField] private float stagger = 0.12f;
    [Tooltip("Sorting order relative to the squirrel. -3 puts lines behind the halo (-2) and its core (-1) and in front of its backing (-4), so they grow out of the glow.")]
    [SerializeField] private int sortingOffset = -3;

    [Header("Icons")]
    [Tooltip("Icon diameter as a fraction of the camera's orthographic size.")]
    [SerializeField] private float iconSizeFraction = 0.12f;
    [Tooltip("Sorting order of the icon's disc relative to the squirrel. Its ring, symbol and label stack above it.")]
    [SerializeField] private int iconSortingOffset = 2;

    [Header("Choosing")]
    [Tooltip("Seconds of holding down to reel an icon all the way in.")]
    [SerializeField] private float reelDuration = 1f;
    [Tooltip("Seconds for a released icon to spring back out from fully reeled.")]
    [SerializeField] private float snapBackDuration = 0.35f;
    [Tooltip("How far in an icon must be reeled before it is picked, from 0 (tip) to 1 (squirrel).")]
    [Range(0.1f, 1f)] [SerializeField] private float commitThreshold = 0.85f;
    [Tooltip("Seconds to pull a picked icon the rest of the way in.")]
    [SerializeField] private float pullInDuration = 0.2f;
    [Tooltip("Opacity of the other lines and icons while one is reeled in.")]
    [Range(0f, 1f)] [SerializeField] private float othersPresence = 0.4f;

    [SerializeField] private float fadeOutDuration = 0.3f;

    private readonly List<UpgradeLine> lines = new List<UpgradeLine>();
    private readonly List<UpgradeIcon> icons = new List<UpgradeIcon>();
    private readonly Dictionary<UpgradeIcon, UpgradeLine> lineOf = new Dictionary<UpgradeIcon, UpgradeLine>();
    private SpriteRenderer target;

    public IReadOnlyList<UpgradeLine> Lines => lines;
    public IReadOnlyList<UpgradeIcon> Icons => icons;

    /// <summary>True when at least one icon can be picked.</summary>
    public bool HasChoice => icons.Any(icon => !icon.IsLocked);

    /// <summary>Grows one line per slot. Stats that are not offered get a broken line.</summary>
    public async UniTask Grow(SpriteRenderer graphic, float orthographicSize, Func<PlayerUpgradeManager.UpgradeStat, bool> isOffered, CancellationToken token)
    {
        target = graphic;
        gameObject.layer = graphic.gameObject.layer;
        FollowTarget();

        float iconDiameter = orthographicSize * iconSizeFraction;
        var growing = new List<UniTask>(slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            float angle = (slot.angle + Random.Range(-angleJitter, angleJitter)) * Mathf.Deg2Rad;
            float length = orthographicSize * lengthFraction * (1f + Random.Range(-lengthJitter, lengthJitter));
            Vector3 end = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f) * length;

            UpgradeLine line = Instantiate(linePrefab, transform);
            line.Setup(slot.stat, end, !isOffered(slot.stat), graphic, sortingOffset);
            lines.Add(line);

            growing.Add(GrowAfter(line, stagger * i, iconDiameter, token));
        }

        await UniTask.WhenAll(growing);
    }

    /// <summary>
    /// Lets the player pick an icon. Left/right moves the highlight across the icons that can be picked, in
    /// screen order. Holding down reels the highlighted icon along its line toward the squirrel; letting go
    /// early springs it back out, and once it passes <see cref="commitThreshold"/> it is pulled the rest of the
    /// way in and picked. Keyboard and gamepad only. Only call when <see cref="HasChoice"/> is true.
    /// </summary>
    public async UniTask<PlayerUpgradeManager.UpgradeStat> Choose(CancellationToken token)
    {
        List<UpgradeIcon> choices = icons.Where(icon => !icon.IsLocked)
            .OrderBy(icon => icon.transform.position.x)
            .ToList();

        // Start on the choice nearest the middle of the fan.
        int index = 0;
        for (int i = 1; i < choices.Count; i++)
        {
            if (Mathf.Abs(choices[i].transform.position.x - transform.position.x) <
                Mathf.Abs(choices[index].transform.position.x - transform.position.x))
                index = i;
        }

        // Defined here rather than in PlayerGameControls: the upgrade runs while PlayerMoveManager.DisableMove()
        // has the whole Gameplay map switched off.
        using var previous = Button("<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
        using var next = Button("<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
        using var reelIn = Button("<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");

        float progress = 0f;
        choices[index].SetHighlighted(true);

        while (progress < commitThreshold)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            if (Time.timeScale == 0f) continue; // paused: the pause menu owns input

            // The highlight can only move while nothing is being reeled.
            if (progress <= 0f)
            {
                int step = (next.WasPressedThisFrame() ? 1 : 0) - (previous.WasPressedThisFrame() ? 1 : 0);
                if (step != 0)
                {
                    choices[index].SetHighlighted(false);
                    index = Mathf.Clamp(index + step, 0, choices.Count - 1);
                    choices[index].SetHighlighted(true);
                }
            }

            progress = reelIn.IsPressed()
                ? Mathf.MoveTowards(progress, 1f, Time.deltaTime / reelDuration)
                : Mathf.MoveTowards(progress, 0f, Time.deltaTime / snapBackDuration);

            ShowReel(choices[index], progress);
        }

        // Past the threshold: finish the pull on its own, whether or not down is still held.
        UpgradeIcon chosen = choices[index];
        float from = progress;
        float elapsed = 0f;
        while (elapsed < pullInDuration)
        {
            ShowReel(chosen, Mathf.Lerp(from, 1f, elapsed / pullInDuration));
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        ShowReel(chosen, 1f);
        return chosen.Stat;
    }

    // Moves the reeled icon down its line and brightens its ring, and steps every other line and icon back
    // as the reel deepens.
    private void ShowReel(UpgradeIcon reeled, float progress)
    {
        reeled.SetReel(progress);
        lineOf[reeled].SetReel(progress);

        float others = Mathf.Lerp(1f, othersPresence, Mathf.Clamp01(progress / commitThreshold));
        foreach (UpgradeIcon icon in icons)
        {
            if (icon == reeled) continue;
            icon.SetPresence(others);
            lineOf[icon].SetPresence(others);
        }
    }

    private static InputAction Button(params string[] bindings)
    {
        var action = new InputAction(type: InputActionType.Button);
        foreach (string binding in bindings)
            action.AddBinding(binding);
        action.Enable();
        return action;
    }

    /// <summary>Fades every line out and destroys the burst.</summary>
    public async UniTask End(CancellationToken token)
    {
        var fading = new List<UniTask>(lines.Count + icons.Count);
        foreach (UpgradeLine line in lines)
            fading.Add(line.FadeOut(fadeOutDuration, token));
        foreach (UpgradeIcon icon in icons)
            fading.Add(icon.FadeOut(fadeOutDuration, token));

        await UniTask.WhenAll(fading);
        Destroy(gameObject);
    }

    private void LateUpdate() => FollowTarget();

    private void FollowTarget()
    {
        if (target != null) transform.position = target.bounds.center;
    }

    private async UniTask GrowAfter(UpgradeLine line, float delay, float iconDiameter, CancellationToken token)
    {
        if (delay > 0f) await UniTask.WaitForSeconds(delay, cancellationToken: token);
        await line.Grow(token);

        if (iconPrefab == null) return;

        // Parented to the tip so the icon drifts with it.
        UpgradeIcon icon = Instantiate(iconPrefab, line.Tip);
        icon.Setup(line.Stat, line.IsLocked, target, target.sortingOrder + iconSortingOffset, iconDiameter);
        icons.Add(icon);
        lineOf[icon] = line;
        await icon.Appear(token);
    }
}
