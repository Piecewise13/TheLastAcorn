using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
/// <summary>
/// The player's own camera behaviour: the held zoom-out, the glide speed zoom, and the unlock
/// cinematic's zoom.
///
/// None of it writes to the camera directly any more. Every effect here is a claim on
/// <see cref="CameraDirector"/>, which arbitrates between them and everything else that wants the
/// camera. That is what stops a held zoom input from stealing the camera out from under a scripted
/// beat, and what lets one cave room hand off to the next.
/// </summary>
public class PlayerCameraManager : MonoBehaviour
{
    private PlayerGameControls playerMovementMap;

    public event Action OnZoomStarted;
    public event Action OnZoomEnded;

    private InputAction zoomAction;

    [SerializeField] public PlayerMoveManager playerMoveManager;

    [SerializeField] private float zoomOutAmount;
    [SerializeField] private float zoomInAmount;

    [Tooltip("How long the unlock cinematic holds its zoom before handing control back.")]
    [SerializeField] private float zoomTime = 1f;

    [Tooltip("Exponential smoothing rate for the player's own zoom. The player's zoom keeps its " +
             "spring-like feel; zone transitions use a fixed duration and curve instead.")]
    [SerializeField] private float zoomSpeed = 6f;

    [SerializeField] private CameraState cameraState = CameraState.Default;

    [Header("Feedback")]
    [SerializeField] private AudioPlayer zoomInSFX;
    [SerializeField] private AudioPlayer zoomOutSFX;

    private readonly Dictionary<CameraState, CameraClaim> claims = new();

    private bool warnedAboutMissingDirector;

    void Awake()
    {
        playerMovementMap = new PlayerGameControls();

        zoomAction = playerMovementMap.Gameplay.CameraZoom;
        zoomAction.performed += Zoom;
        zoomAction.canceled += Zoom;
    }

    void Start()
    {
        playerMoveManager = GetComponentInParent<PlayerMoveManager>();
    }

    void OnDestroy()
    {
        foreach (KeyValuePair<CameraState, CameraClaim> entry in claims)
        {
            entry.Value?.Release();
        }

        claims.Clear();
    }

    #region Zoom Unlock

    public async UniTask WaitForZoomInput(CancellationToken token)
    {
        cameraState = CameraState.Default;
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
        StartForceZoom(zoomOutAmount, CameraState.UnlockPending);
        zoomOutSFX?.Play();
        try
        {
            await UniTask.WaitForSeconds(zoomTime, cancellationToken: token);
        }
        finally
        {
            // Released here rather than left standing. This claim sits at cinematic priority, so
            // holding it past the beat would lock every other system out of the camera for good.
            EndForceZoom(CameraState.UnlockPending);
        }
    }

    #endregion

    private void Zoom(InputAction.CallbackContext context)
    {
        if (cameraState == CameraState.Disabled) return;

        // A cave room is framed to show the whole room, so there is nothing for a zoom-out to
        // reveal. Anything at or above room priority owns the framing and the input is ignored.
        if (context.performed && OwnedByHigherPriority()) return;

        if (context.performed)
        {
            cameraState = CameraState.PlayerZoomed;

            OnZoomStarted?.Invoke();
            playerMoveManager.DisableMove();
            StartForceZoom(zoomOutAmount, CameraState.PlayerZoomed);
            zoomOutSFX?.Play();
        }
        else if (context.canceled)
        {
            cameraState = CameraState.Default;
            playerMoveManager.EnableMove();
            EndForceZoom(CameraState.PlayerZoomed);

            OnZoomEnded?.Invoke();

            zoomInSFX?.Play();
        }
    }

    private static bool OwnedByHigherPriority()
    {
        CameraDirector director = CameraDirector.Current;
        return director != null && director.HasClaimAtOrAbove(CameraPriority.RoomZone);
    }

    /// <summary>
    /// Applies a zoom for the given effect, or updates it if that effect already holds one. Safe to
    /// call every frame — the glide zoom recomputes its size from the player's speed continuously.
    /// </summary>
    public void StartForceZoom(float newZoom, CameraState state)
    {
        if (cameraState == CameraState.Disabled) return;

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

        if (!claims.TryGetValue(state, out CameraClaim claim) || claim == null || claim.Released)
        {
            claim = director.Request(ToPriority(state)).WithExponentialBlend(zoomSpeed);
            claims[state] = claim;
        }

        claim.SetOrthographicSize(newZoom);
    }

    public void EndForceZoom(CameraState state)
    {
        if (!claims.TryGetValue(state, out CameraClaim claim)) return;

        claim?.Release();
        claims.Remove(state);
    }

    public void DisableZoom()
    {
        cameraState = CameraState.Disabled;
        zoomAction.Disable();
        EndForceZoom(CameraState.PlayerZoomed);
    }

    public void EnableZoom()
    {
        cameraState = CameraState.Default;
        zoomAction.Enable();
    }

    public float GetDefaultZoom()
    {
        CameraDirector director = CameraDirector.Current;
        return director != null ? director.DefaultOrthographicSize : zoomInAmount;
    }

    /// <summary>
    /// The orthographic size the camera reaches when the player zooms out.
    /// For an orthographic camera this equals the half-height of the view in world units.
    /// </summary>
    public float GetZoomOutAmount()
    {
        return zoomOutAmount;
    }

    /// <summary>
    /// Sets the orthographic size the camera reaches when the player zooms out.
    /// Used by the upgrade system to scale the zoom-out range.
    /// </summary>
    public void SetZoomOutAmount(float amount)
    {
        zoomOutAmount = amount;
    }

    private static CameraPriority ToPriority(CameraState state) => state switch
    {
        CameraState.GlideZoom => CameraPriority.GlideZoom,
        CameraState.CaveZoomed => CameraPriority.RoomZone,
        CameraState.UnlockPending => CameraPriority.Cinematic,
        _ => CameraPriority.PlayerZoom
    };

    public enum CameraState
    {
        Disabled,
        Default,
        PlayerZoomed,
        UnlockPending,
        CaveZoomed,
        GlideZoom
    }

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
