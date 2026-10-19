using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// The upgrade view: a fan of lines that grows out of the squirrel's glow, one per stat, each ending in that
/// upgrade's icon, and the choice between them. Each stat has a fixed slot with a little jitter, so players
/// learn where each upgrade lives. Follows the squirrel's visible centre while it is shown.
/// <para>
/// Pushed by <see cref="PlayerUpgradeManager"/> once the glow has landed. It hands the chosen stat back through
/// <see cref="PlayerUpgradeManager.CompleteUpgradeSelection"/>, which clears the view stack and so hides this.
/// The glow, dim and camera framing belong to the player and camera, so they stay with the manager.
/// </para>
/// </summary>
public class UpgradeLineBurst : ViewBase
{
    [Serializable]
    private struct Slot
    {
        public PlayerUpgradeManager.UpgradeStat stat;
        [Tooltip("Degrees from straight up. Positive leans left, negative leans right.")]
        public float angle;
        [Tooltip("Drawn inside this stat's icon. Leave empty for a plain circle.")]
        public Sprite symbol;
        [Tooltip("Recap shown under \"<Stat> upgraded\" once this stat is chosen: what just got better.")]
        public string recap;
    }

    [SerializeField] private UpgradeLine linePrefab;
    [Tooltip("Circle that pops in at each line's tip once the line has grown.")]
    [SerializeField] private UpgradeIcon iconPrefab;
    [SerializeField] private Slot[] slots =
    {
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Climbing, angle = 50f, recap = "Climbing speed, climbing duration increased" },
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Vision, angle = 0f, recap = "View distance, scent trail length, scent range increased" },
        new Slot { stat = PlayerUpgradeManager.UpgradeStat.Gliding, angle = -50f, recap = "Glide speed, glide control increased" },
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
    [Tooltip("Icon diameter in world units.")]
    [Min(0f)] [SerializeField] private float iconDiameter = 1.5f;
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

    [Header("Prompt")]
    [Tooltip("Shown at the bottom of the screen while the player is choosing.")]
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private string gamepadPrompt = "Hold A to select";
    [SerializeField] private string keyboardPrompt = "Hold Space to select";
    [Tooltip("Gap between the prompt and the bottom of the screen, as a fraction of the camera's orthographic size.")]
    [SerializeField] private float promptMargin = 0.12f;
    [SerializeField] private float promptFade = 0.3f;

    [Header("Recap")]
    [Tooltip("Shown once the chosen upgrade reaches the squirrel: \"<Stat> upgraded\", then the slot's recap line.")]
    [SerializeField] private Transform recap;
    [SerializeField] private TMP_Text recapTitle;
    [SerializeField] private TMP_Text recapDetail;
    [Tooltip("Height of the recap above the squirrel, as a fraction of the camera's orthographic size.")]
    [SerializeField] private float recapHeight = 0.35f;
    [Tooltip("Size of the recap, as a fraction of the camera's orthographic size, so it reads the same at any zoom.")]
    [SerializeField] private float recapScale = 0.1f;
    [SerializeField] private float recapFadeIn = 0.4f;
    [Tooltip("Seconds the recap line waits after the title starts.")]
    [SerializeField] private float recapDetailDelay = 0.2f;
    [Tooltip("Seconds the recap stays up before the upgrade ends.")]
    [SerializeField] private float recapHold = 2f;
    [Tooltip("How far each line rises as it fades in, in the recap's own scaled units.")]
    [SerializeField] private float recapRise = 0.5f;
    [Tooltip("Sorting order of the recap text relative to the squirrel, above the icons.")]
    [SerializeField] private int recapSortingOffset = 10;

    [SerializeField] private float fadeOutDuration = 0.3f;

    private readonly List<UpgradeLine> lines = new List<UpgradeLine>();
    private readonly List<UpgradeIcon> icons = new List<UpgradeIcon>();
    private readonly Dictionary<UpgradeIcon, UpgradeLine> lineOf = new Dictionary<UpgradeIcon, UpgradeLine>();
    private SpriteRenderer target;
    private float frameSize;
    private bool fanFaded;
    private float promptShown;  // 0..1, eased toward promptTarget
    private float promptTarget;

    public IReadOnlyList<UpgradeLine> Lines => lines;
    public IReadOnlyList<UpgradeIcon> Icons => icons;

    /// <summary>Grows the fan, waits for the player's choice, and hands it to the upgrade manager.</summary>
    public override async UniTask RunAsync(CancellationToken token = default)
    {
        PlayerUpgradeManager manager = PlayerUpgradeManager.Instance;
        SpriteRenderer graphic = manager.PlayerGraphic;
        if (graphic == null)
        {
            Debug.LogWarning($"[{nameof(UpgradeLineBurst)}] No player graphic to grow from; ending the upgrade.", this);
            await manager.CompleteUpgradeSelection(null);
            return;
        }

        // Runs on the view's own token: the view stack shows views with CancellationToken.None, and this
        // should stop with the view, not outlive it.
        await Grow(graphic, manager.UpgradeFrameSize, manager.IsStatOffered, destroyCancellationToken);

        if (icons.Count == 0)
        {
            Debug.LogWarning($"[{nameof(UpgradeLineBurst)}] Every stat is locked or maxed; nothing to choose.", this);
            await manager.CompleteUpgradeSelection(null);
            return;
        }

        PlayerUpgradeManager.UpgradeStat chosen = await Choose(destroyCancellationToken);
        await PlayRecap(chosen, destroyCancellationToken);
        await manager.CompleteUpgradeSelection(chosen);
    }

    /// <summary>Fades every line, icon and the recap out, then destroys the view.</summary>
    public override async UniTask Hide(CancellationToken token = default)
    {
        promptTarget = 0f;
        await UniTask.WhenAll(FadeOutFan(destroyCancellationToken), FadeOutRecap(destroyCancellationToken));
        Destroy(gameObject);
    }

    /// <summary>Grows one line per slot. Stats that are not offered get a shorter line that fades out with no icon.</summary>
    private async UniTask Grow(SpriteRenderer graphic, float orthographicSize, Func<PlayerUpgradeManager.UpgradeStat, bool> isOffered, CancellationToken token)
    {
        target = graphic;
        frameSize = orthographicSize;
        gameObject.layer = graphic.gameObject.layer;
        FollowTarget();

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

            growing.Add(GrowAfter(line, slot.symbol, stagger * i, token));
        }

        await UniTask.WhenAll(growing);
    }

    /// <summary>
    /// Lets the player pick an icon. Left/right moves the highlight across the icons that can be picked, in
    /// screen order. Holding Jump reels the highlighted icon along its line toward the squirrel; letting go
    /// early springs it back out, and once it passes <see cref="commitThreshold"/> it is pulled the rest of the
    /// way in and picked. Keyboard and gamepad only. Only call when there is at least one icon.
    /// </summary>
    private async UniTask<PlayerUpgradeManager.UpgradeStat> Choose(CancellationToken token)
    {
        List<UpgradeIcon> choices = icons
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
        // Selecting is the Jump button (Space / gamepad South), matching PlayerGameControls' Jump bindings.
        using var reelIn = Button("<Keyboard>/space", "<Gamepad>/buttonSouth");

        float progress = 0f;
        choices[index].SetHighlighted(true);
        ShowPrompt(LastUsedDevice());

        while (progress < commitThreshold)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            if (Time.timeScale == 0f) continue; // paused: the pause menu owns input

            // The highlight can only move while nothing is being reeled.
            if (progress <= 0f)
            {
                // Keep the prompt on the device the player is actually using.
            InputDevice used = UsedThisFrame(previous) ?? UsedThisFrame(next) ?? UsedThisFrame(reelIn);
            if (used != null) ShowPrompt(used);

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

        // Past the threshold: finish the pull on its own, whether or not Jump is still held.
        promptTarget = 0f;
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
        UpgradeLine reeledLine = lineOf[reeled];
        reeled.SetReel(progress);
        reeledLine.SetReel(progress);

        float others = Mathf.Lerp(1f, othersPresence, Mathf.Clamp01(progress / commitThreshold));
        foreach (UpgradeIcon icon in icons)
            if (icon != reeled) icon.SetPresence(others);
        foreach (UpgradeLine line in lines)
            if (line != reeledLine) line.SetPresence(others);
    }

    private static InputAction Button(params string[] bindings)
    {
        var action = new InputAction(type: InputActionType.Button);
        foreach (string binding in bindings)
            action.AddBinding(binding);
        action.Enable();
        return action;
    }

    // Once only: each line and icon fades from its full colour, so a second fade would flash it back on first.
    private async UniTask FadeOutFan(CancellationToken token)
    {
        if (fanFaded) return;
        fanFaded = true;

        var fading = new List<UniTask>(lines.Count + icons.Count);
        foreach (UpgradeLine line in lines)
            fading.Add(line.FadeOut(fadeOutDuration, token));
        foreach (UpgradeIcon icon in icons)
            fading.Add(icon.FadeOut(fadeOutDuration, token));

        await UniTask.WhenAll(fading);
    }

    // The chosen upgrade has reached the squirrel: the rest of the fan fades away while "<Stat> upgraded" and the
    // slot's recap line rise in above it, then hold.
    private async UniTask PlayRecap(PlayerUpgradeManager.UpgradeStat stat, CancellationToken token)
    {
        UniTask fan = FadeOutFan(token);
        if (recap == null)
        {
            await fan;
            return;
        }

        Slot slot = slots.FirstOrDefault(s => s.stat == stat);
        recap.localPosition = Vector3.up * (frameSize * recapHeight);
        recap.localScale = Vector3.one * (frameSize * recapScale);
        recap.gameObject.layer = target.gameObject.layer;
        PrepareRecapText(recapTitle, $"{stat} upgraded");
        PrepareRecapText(recapDetail, slot.recap);
        recap.gameObject.SetActive(true);

        await UniTask.WhenAll(fan, RevealRecapText(recapTitle, 0f, token), RevealRecapText(recapDetail, recapDetailDelay, token));
        await UniTask.WaitForSeconds(recapHold, cancellationToken: token);
    }

    private void ShowPrompt(InputDevice device)
    {
        if (prompt == null) return;

        string content = device is Gamepad ? gamepadPrompt : keyboardPrompt;
        if (prompt.text != content || !prompt.gameObject.activeSelf)
        {
            PrepareRecapText(prompt, content);
            prompt.alpha = promptShown;
            prompt.gameObject.SetActive(true);
        }

        promptTarget = 1f;
    }

    private static InputDevice UsedThisFrame(InputAction action) =>
        action.WasPressedThisFrame() && action.activeControl != null ? action.activeControl.device : null;

    // Before the player has pressed anything here, guess from whichever of keyboard or gamepad was touched last.
    private static InputDevice LastUsedDevice()
    {
        Gamepad pad = Gamepad.current;
        Keyboard keys = Keyboard.current;
        if (pad == null) return keys;
        if (keys == null) return pad;
        return pad.lastUpdateTime >= keys.lastUpdateTime ? pad : keys;
    }

    // The prompt rides the bottom of the screen rather than the squirrel, so it is placed from the camera.
    private void PlacePrompt()
    {
        if (prompt == null || !prompt.gameObject.activeSelf) return;

        promptShown = Mathf.MoveTowards(promptShown, promptTarget, Time.deltaTime / Mathf.Max(promptFade, 0.0001f));
        prompt.alpha = promptShown;

        Camera cam = CameraRig.Current != null ? CameraRig.Current.Foreground : null;
        if (cam == null) return;

        float size = cam.orthographicSize;
        Vector3 bottom = cam.transform.position + Vector3.down * (size * (1f - promptMargin));
        prompt.transform.position = new Vector3(bottom.x, bottom.y, transform.position.z);
        prompt.transform.localScale = Vector3.one * (frameSize * recapScale);
    }

    private void PrepareRecapText(TMP_Text text, string content)
    {
        if (text == null) return;

        text.text = content;
        text.alpha = 0f;
        // On the squirrel's layer so the overlay camera draws it above the dim, and in front of the icons.
        text.gameObject.layer = target.gameObject.layer;
        Renderer textRenderer = text.GetComponent<Renderer>();
        textRenderer.sortingLayerID = target.sortingLayerID;
        textRenderer.sortingOrder = target.sortingOrder + recapSortingOffset;
    }

    private async UniTask RevealRecapText(TMP_Text text, float delay, CancellationToken token)
    {
        if (text == null || string.IsNullOrEmpty(text.text)) return;
        if (delay > 0f) await UniTask.WaitForSeconds(delay, cancellationToken: token);

        Vector3 rest = text.transform.localPosition;
        Vector3 start = rest - Vector3.up * recapRise;
        float elapsed = 0f;
        while (elapsed < recapFadeIn)
        {
            float t = elapsed / recapFadeIn;
            float eased = 1f - (1f - t) * (1f - t);
            text.transform.localPosition = Vector3.LerpUnclamped(start, rest, eased);
            text.alpha = eased;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        text.transform.localPosition = rest;
        text.alpha = 1f;
    }

    private async UniTask FadeOutRecap(CancellationToken token)
    {
        if (recap == null || !recap.gameObject.activeSelf) return;

        float title = recapTitle != null ? recapTitle.alpha : 0f;
        float detail = recapDetail != null ? recapDetail.alpha : 0f;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            float keep = 1f - elapsed / fadeOutDuration;
            if (recapTitle != null) recapTitle.alpha = title * keep;
            if (recapDetail != null) recapDetail.alpha = detail * keep;
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        recap.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        FollowTarget();
        PlacePrompt();
    }

    private void FollowTarget()
    {
        if (target != null) transform.position = target.bounds.center;
    }

    private async UniTask GrowAfter(UpgradeLine line, Sprite symbol, float delay, CancellationToken token)
    {
        if (delay > 0f) await UniTask.WaitForSeconds(delay, cancellationToken: token);
        await line.Grow(token);

        // A locked stat's line is only a wisp that fades out; it gets no icon.
        if (iconPrefab == null || line.IsLocked) return;

        // Parented to the tip so the icon drifts with it.
        UpgradeIcon icon = Instantiate(iconPrefab, line.Tip);
        icon.Setup(line.Stat, symbol, target, target.sortingOrder + iconSortingOffset, iconDiameter);
        line.AttachIcon(icon.transform, UpgradeIcon.EdgeRadius);
        icon.MaskDownTo(target.sortingOrder + sortingOffset);
        icons.Add(icon);
        lineOf[icon] = line;
        await icon.Appear(token);
    }
}
