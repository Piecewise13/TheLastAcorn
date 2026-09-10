using System;
using UnityEngine;

namespace Player
{
    public enum PlayerState
    {
        Locked,
        Grounded,
        Climb,
        Glide,
        Fall,
        RidingOwl,
        VineSwinging,
        STUNNED
    }

    public class PlayerStateManager : MonoBehaviour
    {

        public static PlayerStateManager Instance { get; private set; }

        public PlayerState CurrentState { get; private set; }

        public event Action<PlayerState, PlayerState> OnStateChanged;

        /// <summary>
        /// True if the player lock has been aquired, False if released. 
        /// </summary>
        public event Action<bool> OnLockChange;

        /// <summary>
        /// Raised whenever a scene's player body comes online, carrying that player's root GameObject.
        /// Persistent, game-wide services (ability/upgrade progression) that are no longer attached to
        /// the player use this to re-bind to whatever player is current, rather than a one-time
        /// GetComponent on themselves. Static so late-spawned or persistent listeners can subscribe
        /// without holding a reference to the per-scene instance.
        /// </summary>
        public static event Action<GameObject> OnPlayerRegistered;

        public GameObject playerGameObject { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple instances of PlayerStateManager detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            playerGameObject = transform.gameObject;
            CurrentState = PlayerState.Grounded; // Default state

            OnPlayerRegistered?.Invoke(playerGameObject);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void LockPlayer()
        {
            //IF needed, could use a lock queue if multiple objects want to own the lock on the player
            if (CurrentState == PlayerState.Locked)
            {
                return;
            }

            CurrentState = PlayerState.Locked;
            OnLockChange?.Invoke(true);
        }

        public void UnlockPlayer()
        {
            if (CurrentState != PlayerState.Locked)
            {
                return;
            }
            
            CurrentState = PlayerState.Grounded;
            ChangeState(PlayerState.Fall);
            OnLockChange?.Invoke(false);
        }

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == PlayerState.Locked)
            {
                return;
            }
            
            if (CurrentState == newState) return;

            Debug.Log($"[PlayerStateManager] Changing player state from {CurrentState} to {newState}");
            PlayerState previousState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(previousState, newState);
        }
    }
}
