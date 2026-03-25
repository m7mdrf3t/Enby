// ============================================================
//  StorageManager.cs
//  Owns the gas storage tank level.
//  - Receives cargo from SocketController (via EventBus)
//  - Drains gas over time (city consumption tick)
//  - Publishes OnStorageChanged every time the level moves
//  - Publishes OnStorageEmpty / OnStorageRestored on transitions
//
//  Pattern : Manager (single owner of shared resource)
//            Observer (subscribes to OnCargoDelivered, OnFanCompleted)
//            Publisher (raises OnStorageChanged, OnStorageEmpty, ...)
// ============================================================

using UnityEngine;
using PetroCitySimulator.Data;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.Managers
{
    public class StorageManager : MonoBehaviour
    {
        public static StorageManager Instance { get; private set; }

        // ---------------------------------------------------
        //  Inspector configuration
        // ---------------------------------------------------

        [Header("Tank Settings")]
        [SerializeField] private StorageConfigSO _config;

        [Tooltip("Maximum gas units the tank can hold.")]
        [SerializeField] private float _maxCapacity = 1000f;

        [Tooltip("Starting gas level as a fraction of max capacity (0�1).")]
        [SerializeField, Range(0f, 1f)] private float _startFillRatio = 0.5f;

        [Header("Consumption")]
        [Tooltip("Gas units drained per second by the city when running normally.")]
        [SerializeField] private float _baseConsumptionRate = 5f;

        [Tooltip("How often (seconds) the consumption tick fires and publishes events.")]
        [SerializeField] private float _consumptionTickInterval = 1f;

        // ---------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------

        private float _currentAmount;
        private bool _isEmpty;
        private float _consumptionTimer;

        // ---------------------------------------------------
        //  Public read-only accessors
        // ---------------------------------------------------

        public float CurrentAmount => _currentAmount;
        public float MaxCapacity => _maxCapacity;
        public float FillRatio => _maxCapacity > 0f ? _currentAmount / _maxCapacity : 0f;
        public bool IsEmpty => _isEmpty;
        public bool IsFull => _currentAmount >= _maxCapacity;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ApplyConfigOverrides();
            _currentAmount = _maxCapacity * Mathf.Clamp01(_startFillRatio);
            _isEmpty = _currentAmount <= 0f;
        }

        private void OnEnable()
        {
            EventBus<OnCargoDelivered>.Subscribe(HandleCargoDelivered);
            EventBus<OnFanCompleted>.Subscribe(HandleFanCompleted);
            EventBus<OnFanTransferRequested>.Subscribe(HandleFanTransferRequested);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnCargoDelivered>.Unsubscribe(HandleCargoDelivered);
            EventBus<OnFanCompleted>.Unsubscribe(HandleFanCompleted);
            EventBus<OnFanTransferRequested>.Unsubscribe(HandleFanTransferRequested);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Start()
        {
            // Broadcast initial state so all UIs can initialise correctly
            PublishStorageChanged(justBecameEmpty: false, justBecameFull: false);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;

            TickConsumption();
        }

        // ---------------------------------------------------
        //  Consumption tick
        // ---------------------------------------------------

        private void TickConsumption()
        {
            _consumptionTimer += Time.deltaTime;

            if (_consumptionTimer < _consumptionTickInterval) return;

            _consumptionTimer -= _consumptionTickInterval;

            float drain = _baseConsumptionRate * _consumptionTickInterval;
            DrainGas(drain, isConsumptionTick: true);
        }

        // ---------------------------------------------------
        //  Public methods (called by FanController or tests)
        // ---------------------------------------------------

        /// <summary>
        /// Add gas to the tank (e.g. cargo delivery, cheat/debug).
        /// Clamped to MaxCapacity.
        /// </summary>
        public void AddGas(float amount)
        {
            if (amount <= 0f) return;

            bool wasEmpty = _isEmpty;
            float prevAmount = _currentAmount;

            _currentAmount = Mathf.Min(_currentAmount + amount, _maxCapacity);

            bool justBecameFull = !Mathf.Approximately(prevAmount, _maxCapacity) && IsFull;
            bool justWasRestored = wasEmpty && _currentAmount > 0f;

            _isEmpty = false;

            PublishStorageChanged(justBecameEmpty: false, justBecameFull: justBecameFull);

            if (justWasRestored)
            {
                EventBus<OnStorageRestored>.Raise(new OnStorageRestored
                {
                    RestoredAmount = _currentAmount
                });
                Debug.Log($"[StorageManager] Storage restored. Level: {_currentAmount:F1}");
            }
        }

        /// <summary>
        /// Remove gas from the tank (e.g. city consumption, fan transfer).
        /// Clamped to zero. Returns the actual amount drained.
        /// </summary>
        public float DrainGas(float amount, bool isConsumptionTick = false)
        {
            if (amount <= 0f) return 0f;
            if (_isEmpty) return 0f;

            float prevAmount = _currentAmount;
            _currentAmount = Mathf.Max(_currentAmount - amount, 0f);
            float actualDrain = prevAmount - _currentAmount;

            bool justBecameEmpty = prevAmount > 0f && _currentAmount <= 0f;

            if (justBecameEmpty)
            {
                _isEmpty = true;
                EventBus<OnStorageEmpty>.Raise(new OnStorageEmpty());
                Debug.Log("[StorageManager] Storage is now EMPTY.");
            }

            PublishStorageChanged(justBecameEmpty: justBecameEmpty, justBecameFull: false);

            if (isConsumptionTick)
            {
                EventBus<OnCityConsumptionTick>.Raise(new OnCityConsumptionTick
                {
                    AmountConsumed = actualDrain,
                    ConsumptionRate = _baseConsumptionRate
                });
            }

            return actualDrain;
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        private void HandleCargoDelivered(OnCargoDelivered e)
        {
            Debug.Log($"[StorageManager] Cargo received: +{e.AmountDelivered:F1} units from ship {e.ShipId}.");
            AddGas(e.AmountDelivered);
        }

        private void HandleFanCompleted(OnFanCompleted e)
        {
            // Fan already transferred gas directly; this is for any
            // secondary book-keeping if needed later.
            Debug.Log($"[StorageManager] Fan {e.FanId} transfer acknowledged: {e.AmountTransferred:F1} units.");
        }

        private void HandleFanTransferRequested(OnFanTransferRequested e)
        {
            float drained = DrainGas(e.TransferAmount);
            Debug.Log($"[StorageManager] Fan {e.FanId} drained {drained:F1} units from storage.");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            // Reset consumption timer on pause/resume to avoid a spike
            if (e.NewState == GameState.Paused || e.NewState == GameState.Playing)
                _consumptionTimer = 0f;
        }

        // ---------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------

        private void PublishStorageChanged(bool justBecameEmpty, bool justBecameFull)
        {
            EventBus<OnStorageChanged>.Raise(new OnStorageChanged
            {
                CurrentAmount = _currentAmount,
                MaxCapacity = _maxCapacity,
                FillRatio = FillRatio,
                JustBecameEmpty = justBecameEmpty,
                JustBecameFull = justBecameFull
            });
        }

        private void ApplyConfigOverrides()
        {
            if (_config == null) return;

            _maxCapacity = _config.MaxCapacity;
            _startFillRatio = _config.StartFillRatio;
            _baseConsumptionRate = _config.BaseConsumptionRate;
            _consumptionTickInterval = _config.ConsumptionTickInterval;
        }

        // ---------------------------------------------------
        //  Editor helpers
        // ---------------------------------------------------

#if UNITY_EDITOR
        [ContextMenu("Debug: Fill Tank")]
        private void DebugFillTank()   => AddGas(_maxCapacity);

        [ContextMenu("Debug: Empty Tank")]
        private void DebugEmptyTank()  => DrainGas(_maxCapacity);

        [ContextMenu("Debug: Add 100 Units")]
        private void DebugAdd100()     => AddGas(100f);

        [ContextMenu("Debug: Drain 100 Units")]
        private void DebugDrain100()   => DrainGas(100f);
#endif

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}