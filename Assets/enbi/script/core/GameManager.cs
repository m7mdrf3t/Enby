// ============================================================
//  GameManager.cs
//  Singleton. Owns the top-level game state machine and
//  bootstraps / tears down the EventBus lifecycle.
//
//  Pattern  : Singleton (persistent across scenes)
//             + State Machine (enum-driven)
//             + Facade (single entry point for pause / resume)
// ============================================================

using UnityEngine;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool _autoStartInPlayMode = true;

        // ---------------------------------------------------
        //  Singleton
        // ---------------------------------------------------

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            // Enforce a single instance across scene loads
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitialiseGame();
        }

        private void Start()
        {
            if (_autoStartInPlayMode && _currentState == GameState.MainMenu)
                StartGame();
        }

        // ---------------------------------------------------
        //  State
        // ---------------------------------------------------

        private GameState _currentState = GameState.MainMenu;

        public GameState CurrentState => _currentState;

        public bool IsPlaying => _currentState == GameState.Playing;
        public bool IsPaused => _currentState == GameState.Paused;

        // ---------------------------------------------------
        //  Initialisation
        // ---------------------------------------------------

        private void InitialiseGame()
        {
            // Seed the EventBus clear registry for every event type
            // we know the game uses so ClearAll() works correctly.
            EventBusUtil.Register(EventBus<OnShipSpawned>.Clear);
            EventBusUtil.Register(EventBus<OnShipDockingStarted>.Clear);
            EventBusUtil.Register(EventBus<OnShipDocked>.Clear);
            EventBusUtil.Register(EventBus<OnCargoDelivered>.Clear);
            EventBusUtil.Register(EventBus<OnShipDeparting>.Clear);
            EventBusUtil.Register(EventBus<OnShipDespawned>.Clear);
            EventBusUtil.Register(EventBus<OnSocketFreed>.Clear);
            EventBusUtil.Register(EventBus<OnSocketTimerTick>.Clear);
            EventBusUtil.Register(EventBus<OnStorageChanged>.Clear);
            EventBusUtil.Register(EventBus<OnStorageEmpty>.Clear);
            EventBusUtil.Register(EventBus<OnStorageRestored>.Clear);
            EventBusUtil.Register(EventBus<OnCityBlackout>.Clear);
            EventBusUtil.Register(EventBus<OnCityLightsRestored>.Clear);
            EventBusUtil.Register(EventBus<OnCityConsumptionTick>.Clear);
            EventBusUtil.Register(EventBus<OnFanActivated>.Clear);
            EventBusUtil.Register(EventBus<OnFanCompleted>.Clear);
            EventBusUtil.Register(EventBus<OnFanTransferRequested>.Clear);
            EventBusUtil.Register(EventBus<OnShipTapped>.Clear);
            EventBusUtil.Register(EventBus<OnGameStateChanged>.Clear);

            Debug.Log("[GameManager] Initialised. Awaiting StartGame().");
        }

        // ---------------------------------------------------
        //  Public state transitions
        // ---------------------------------------------------

        /// <summary>Transition from MainMenu → Playing.</summary>
        public void StartGame()
        {
            if (_currentState != GameState.MainMenu)
            {
                Debug.LogWarning("[GameManager] StartGame called in wrong state.");
                return;
            }
            TransitionTo(GameState.Playing);
        }

        /// <summary>Pause the simulation. Time scale set to 0.</summary>
        public void PauseGame()
        {
            if (_currentState != GameState.Playing) return;
            Time.timeScale = 0f;
            TransitionTo(GameState.Paused);
        }

        /// <summary>Resume from pause. Time scale restored to 1.</summary>
        public void ResumeGame()
        {
            if (_currentState != GameState.Paused) return;
            Time.timeScale = 1f;
            TransitionTo(GameState.Playing);
        }

        /// <summary>End the simulation and return to MainMenu.</summary>
        public void EndGame()
        {
            Time.timeScale = 1f;
            TransitionTo(GameState.GameOver);
        }

        // ---------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------

        private void TransitionTo(GameState newState)
        {
            var previousState = _currentState;
            _currentState = newState;

            EventBus<OnGameStateChanged>.Raise(new OnGameStateChanged
            {
                PreviousState = previousState,
                NewState = newState
            });

            Debug.Log($"[GameManager] {previousState} → {newState}");
        }

        // ---------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------

        private void OnDestroy()
        {
            // Only clean up when the true singleton is destroyed
            if (Instance != this) return;

            EventBusUtil.ClearAll();
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            Time.timeScale = 1f;
        }
    }
}