using System;
using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{

    public enum PlayerState
    {
        Grounded,
        Climb,
        Glide,
        Fall,
        RidingOwl,
        VineSwinging,
        STUNNED
    }

    public static PlayerStateManager Instance { get; private set; }

    public PlayerState CurrentState { get; private set; }

    public event Action<PlayerState, PlayerState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple instances of PlayerStateManager detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CurrentState = PlayerState.Grounded; // Default state
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeState(PlayerState newState)
    {
        if (CurrentState == newState) return;

        Debug.Log($"Changing player state from {CurrentState} to {newState}");
        PlayerState previousState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(previousState, newState);
    }
}
