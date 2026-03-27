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
using PetroCitySimulator.Managers;

namespace PetroCitySimulator.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Main Timer")]
        [Tooltip("Total match time in seconds. When it reaches zero, the game ends.")]
        [SerializeField, Min(1f)] private float _matchDurationSeconds = 180f;

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
            // Keep MainMenu active until the player presses Start.
        }

        // ---------------------------------------------------
        //  State
        // ---------------------------------------------------

        private GameState _currentState = GameState.MainMenu;
        private float _mainTimerRemaining;

        public GameState CurrentState => _currentState;

        public bool IsPlaying => _currentState == GameState.Playing;
        public bool IsPaused => _currentState == GameState.Paused;
        public float MatchDurationSeconds => _matchDurationSeconds;
        public float MainTimerRemaining => _mainTimerRemaining;

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

            EventBusUtil.Register(EventBus<OnShipTapped>.Clear);
            EventBusUtil.Register(EventBus<OnGameStateChanged>.Clear);
            EventBusUtil.Register(EventBus<OnCityGasChanged>.Clear);
            EventBusUtil.Register(EventBus<OnMainTimerTick>.Clear);
            EventBusUtil.Register(EventBus<OnGameFinishedSummary>.Clear);
            EventBusUtil.Register(EventBus<OnFactoryUpgradeUnlocked>.Clear);
            EventBusUtil.Register(EventBus<OnFactoryUpgraded>.Clear);

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

            _mainTimerRemaining = _matchDurationSeconds;
            PublishMainTimerTick();
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
            PublishGameFinishedSummary();
            TransitionTo(GameState.GameOver);
        }

        private void Update()
        {
            if (_currentState != GameState.Playing)
                return;

            if (_mainTimerRemaining <= 0f)
                return;

            _mainTimerRemaining = Mathf.Max(0f, _mainTimerRemaining - Time.deltaTime);
            PublishMainTimerTick();

            if (_mainTimerRemaining <= 0f)
                EndGame();
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

        private void PublishMainTimerTick()
        {
            float duration = Mathf.Max(0.01f, _matchDurationSeconds);
            EventBus<OnMainTimerTick>.Raise(new OnMainTimerTick
            {
                RemainingSeconds = _mainTimerRemaining,
                DurationSeconds = _matchDurationSeconds,
                NormalizedRemaining = Mathf.Clamp01(_mainTimerRemaining / duration)
            });
        }

        private void PublishGameFinishedSummary()
        {
            EventBus<OnGameFinishedSummary>.Raise(new OnGameFinishedSummary
            {
                MoneyAmount = MoneyManager.Instance != null ? MoneyManager.Instance.CurrentMoney : 0f,
                ProductAmount = ProductStorageManager.Instance != null ? ProductStorageManager.Instance.CurrentAmount : 0f,
                GasAmount = CityManager.Instance != null ? CityManager.Instance.CurrentGas : 0f
            });
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