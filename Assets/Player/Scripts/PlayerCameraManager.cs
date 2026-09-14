using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player
{
/// <summary>
/// The player's side of the camera. Three separate behaviours live here, and they are deliberately
/// kept apart:
///
/// <list type="bullet">
/// <item>the <b>zoom-out ability</b> — a held input that pulls the camera back and freezes the
/// player while it is held (this file)</item>
/// <item>the <b>velocity zoom</b> — the player's normal framing, driven continuously by glide speed
/// (PlayerCameraManager.VelocityZoom.cs)</item>
/// <item>the <b>unlock cinematic</b> — a one-shot scripted pull-back</item>
/// </list>
///
/// None of them writes to the camera directly. Each is a <see cref="CameraClaim"/> on
/// <see cref="CameraDirector"/>, which arbitrates between them and everything else that wants the
/// camera. That is what stops a held zoom input from stealing the camera out from under a scripted
/// beat, and what lets one cave room hand off to the next.
///
/// Two enums keep the two axes from getting confused: <see cref="ZoomInputState"/> is about whether
/// the ability's input is accepted, and <see cref="ZoomEffect"/> is about which behaviour owns a
/// claim. They used to be one enum, which meant the input gate and the claim keys shared a
/// namespace and neither read clearly.
/// </summary>
public partial class PlayerCameraManager : MonoBehaviour
{
    /// <summary>Whether the zoom-out ability's input is accepted, and whether it is held.</summary>
    public enum ZoomInputState
    {
        /// <summary>Input is off entirely — end-game, cutscenes. Nothing here responds.</summary>
        Disabled = 0,
        /// <summary>Input is accepted but not held. Kept at 1 to match the old serialized value.</summary>
        Ready = 1,
        /// <summary>The ability is held: camera pulled back, player movement frozen.</summary>
        Held = 2
    }

    /// <summary>
    /// The behaviours that can hold a zoom claim through this component. One claim per effect, so
    /// two of them can be live at once and the director resolves which is seen.
    /// </summary>
    public enum ZoomEffect
    {
        /// <summary>The player's normal speed-driven framing. Lowest priority; any zone beats it.</summary>
        VelocityZoom,
        /// <summary>The held zoom-out ability.</summary>
        PlayerZoomOut,
        /// <summary>The owl's flight framing, held for as long as the player is riding.</summary>
        OwlRide,
        /// <summary>The ability-unlock cinematic's pull-back.</summary>
        UnlockCinematic
    }

    // ── Wiring ────────────────────────────────────────────────────────────────

    [SerializeField] public PlayerMoveManager playerMoveManager;

    // ── Zoom-out ability ──────────────────────────────────────────────────────

    [Header("Zoom-out ability")]
    [Tooltip("Orthographic half-height the camera pulls back to while the ability is held. Raised " +
             "by the Vision upgrade.")]
    [FormerlySerializedAs("zoomOutAmount")]
    [SerializeField] private float playerZoomOutSize;

    [Tooltip("Exponential smoothing rate for the ability's pull-back. Kept separate from the " +
             "velocity zoom's rate so the ability can stay snappy while the speed ramp eases.")]
    [FormerlySerializedAs("zoomSpeed")]
    [SerializeField] private float playerZoomBlendRate = 6f;

    [Header("Zoom-out ability feedback")]
    [SerializeField] private AudioPlayer zoomInSFX;
    [SerializeField] private AudioPlayer zoomOutSFX;

    public event Action OnZoomStarted;
    public event Action OnZoomEnded;

    // ── Unlock cinematic ──────────────────────────────────────────────────────

    [Header("Unlock cinematic")]
    [Tooltip("How long the unlock cinematic holds its pull-back before handing control back.")]
    [FormerlySerializedAs("zoomTime")]
    [SerializeField] private float unlockZoomHoldSeconds = 1f;

    // ── Fallback ──────────────────────────────────────────────────────────────

    [Header("Fallback")]
    [Tooltip("Size used as the baseline when there is no CameraDirector in the scene. Normally the " +
             "director's own default wins, so this only matters in a scene missing the camera rig.")]
    [FormerlySerializedAs("zoomInAmount")]
    [SerializeField] private float fallbackDefaultSize = 20f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private PlayerGameControls playerMovementMap;
    private InputAction zoomAction;

    // Runtime state, not authored data — the old serialized `cameraState` was never overridden by
    // any scene, so nothing is lost by keeping it in memory only.
    private ZoomInputState inputState = ZoomInputState.Ready;

    private readonly Dictionary<ZoomEffect, CameraClaim> claims = new();

    private bool warnedAboutMissingDirector;

    private void Awake()
    {
        playerMovementMap = new PlayerGameControls();

        zoomAction = playerMovementMap.Gameplay.CameraZoom;
        zoomAction.performed += HandleZoomInput;
        zoomAction.canceled += HandleZoomInput;
    }

    private void Start()
    {
        playerMoveManager = GetComponentInParent<PlayerMoveManager>();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<ZoomEffect, CameraClaim> entry in claims)
        {
            entry.Value?.Release();
        }

        claims.Clear();
    }

    #region Zoom-out ability

    private void HandleZoomInput(InputAction.CallbackContext context)
    {
        if (inputState == ZoomInputState.Disabled) return;

        // A cave room is framed to show the whole room, so there is nothing for a zoom-out to
        // reveal. Anything at or above room priority owns the framing and the input is ignored.
        if (context.performed && OwnedByHigherPriority()) return;

        if (context.performed)
        {
            inputState = ZoomInputState.Held;

            OnZoomStarted?.Invoke();
            playerMoveManager.DisableMove();
            ApplyZoom(playerZoomOutSize, ZoomEffect.PlayerZoomOut);
            zoomOutSFX?.Play();
        }
        else if (context.canceled)
        {
            inputState = ZoomInputState.Ready;

            playerMoveManager.EnableMove();
            ReleaseZoom(ZoomEffect.PlayerZoomOut);

            OnZoomEnded?.Invoke();
            zoomInSFX?.Play();
        }
    }

    public void DisableZoom()
    {
        inputState = ZoomInputState.Disabled;
        zoomAction.Disable();
        ReleaseZoom(ZoomEffect.PlayerZoomOut);
    }

    public void EnableZoom()
    {
        inputState = ZoomInputState.Ready;
        zoomAction.Enable();
    }

    /// <summary>
    /// The orthographic size the camera reaches when the player zooms out.
    /// For an orthographic camera this equals the half-height of the view in world units.
    /// </summary>
    public float GetZoomOutAmount() => playerZoomOutSize;

    /// <summary>
    /// Sets the orthographic size the ability pulls back to. Used by the Vision upgrade.
    /// </summary>
    public void SetZoomOutAmount(float amount)
    {
        playerZoomOutSize = amount;
    }

    #endregion

    #region Unlock cinematic

    public async UniTask WaitForZoomInput(CancellationToken token)
    {
        inputState = ZoomInputState.Ready;
        var completionSource = new UniTaskCompletionSource();
        void OnPerformed(InputAction.CallbackContext _) => completionSource.TrySetResult();
        zoomAction.Enable();
        zoomAction.performed += OnPerformed;
        try
        {
            await completionSource.Task.AttachExternalCancellation(token);
        }
        finally
        {
            zoomAction.performed -= OnPerformed;
        }
    }

    public async UniTask PerformUnlockZoom(CancellationToken token)
    {
        ApplyZoom(playerZoomOutSize, ZoomEffect.UnlockCinematic);
        zoomOutSFX?.Play();
        try
        {
            await UniTask.WaitForSeconds(unlockZoomHoldSeconds, cancellationToken: token);
        }
        finally
        {
            // Released here rather than left standing. This claim sits at cinematic priority, so
            // holding it past the beat would lock every other system out of the camera for good.
            ReleaseZoom(ZoomEffect.UnlockCinematic);
        }
    }

    #endregion

    #region Claims

    /// <summary>
    /// Applies a zoom for one effect, or resizes it if that effect already holds a claim. Safe to
    /// call every frame — the velocity zoom recomputes its size from the player's speed continuously.
    /// </summary>
    public void ApplyZoom(float size, ZoomEffect effect)
    {
        if (inputState == ZoomInputState.Disabled) return;

        CameraDirector director = CameraDirector.Current;
        if (director == null)
        {
            if (!warnedAboutMissingDirector)
            {
                warnedAboutMissingDirector = true;
                Debug.LogWarning($"[{nameof(PlayerCameraManager)}] No {nameof(CameraDirector)} in the scene, so " +
                                 "zoom does nothing. Add it to the camera rig alongside CameraRig.", this);
            }

            return;
        }

        if (!claims.TryGetValue(effect, out CameraClaim claim) || claim == null || claim.Released)
        {
            claim = director.Request(PriorityFor(effect));
            claims[effect] = claim;
        }

        // Re-applied every call, not just on creation. The velocity zoom swaps its rate by travel
        // direction, and its claim is held for the whole session, so a rate set once would freeze
        // on whatever direction happened to be first.
        claim.WithExponentialBlend(BlendRateFor(effect));
        claim.SetOrthographicSize(size);
    }

    /// <summary>Drops one effect's claim, letting whatever sits underneath frame the camera again.</summary>
    public void ReleaseZoom(ZoomEffect effect)
    {
        if (!claims.TryGetValue(effect, out CameraClaim claim)) return;

        claim?.Release();
        claims.Remove(effect);
    }

    private static CameraPriority PriorityFor(ZoomEffect effect) => effect switch
    {
        ZoomEffect.VelocityZoom => CameraPriority.GlideZoom,
        ZoomEffect.OwlRide => CameraPriority.GlideZoom,
        ZoomEffect.UnlockCinematic => CameraPriority.Cinematic,
        _ => CameraPriority.PlayerZoom
    };

    /// <summary>
    /// Exponential smoothing rate per effect. The whole point of splitting these is that the
    /// ability wants a fast, springy response while the speed ramp wants a slow drift — one shared
    /// rate could only ever satisfy one of them.
    /// </summary>
    private float BlendRateFor(ZoomEffect effect) => effect switch
    {
        ZoomEffect.VelocityZoom => VelocityZoomBlendRate,
        _ => playerZoomBlendRate
    };

    private static bool OwnedByHigherPriority()
    {
        CameraDirector director = CameraDirector.Current;
        return director != null && director.HasClaimAtOrAbove(CameraPriority.RoomZone);
    }

    /// <summary>
    /// The size the camera rests at when nothing is claiming zoom. Owned by the director so every
    /// system agrees on one baseline; the serialized fallback only applies with no rig in the scene.
    /// </summary>
    private float DefaultSize
    {
        get
        {
            CameraDirector director = CameraDirector.Current;
            return director != null ? director.DefaultOrthographicSize : fallbackDefaultSize;
        }
    }

    #endregion

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private UnlockZoomView zoomView;

    [NaughtyAttributes.Button("Ability Unlock Test")]
    private async void UnlockAbilityTest()
    {
        await OverlayCameraController.Current.RequestPlayerOverlay();
        ViewManager.Instance.PushView(zoomView).Forget();
    }
#endif
}
}
