using PetroCitySimulator.Data;
using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class FactoryManager : MonoBehaviour
    {
        public static FactoryManager Instance { get; private set; }

        [Header("Identity")]
        [SerializeField] private int _factoryId = 1;

        [Header("Config")]
        [SerializeField] private FactoryConfigSO _config;

        private Timer _productionTimer;
        private float _gasBuffer;
        private float _productOutputBuffer;

        public float GasBuffer => _gasBuffer;
        public float GasBufferCapacity => _config != null ? _config.GasBufferCapacity : 0f;
        public bool IsBufferFull => _config != null && _gasBuffer >= _config.GasBufferCapacity;
        public float ProductOutputBuffer => _productOutputBuffer;
        public bool HasProducts => _productOutputBuffer > 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_config == null)
            {
                Debug.LogError("[FactoryManager] FactoryConfigSO is not assigned.");
                return;
            }

            _productionTimer = new Timer(_config.ProductionDuration);
            _productionTimer.OnCompleted += HandleProductionCycleComplete;
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;
            if (_productionTimer == null) return;

            bool canProduce = _gasBuffer >= _config.GasPerProduct;

            if (canProduce && !_productionTimer.IsRunning)
                _productionTimer.Start();

            _productionTimer.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Add gas to the factory buffer (called by Factory UI Button).
        /// Returns the actual amount added (clamped to capacity).
        /// </summary>
        public float AddGas(float amount)
        {
            if (_config == null || amount <= 0f) return 0f;

            float space = _config.GasBufferCapacity - _gasBuffer;
            float added = Mathf.Min(amount, space);
            _gasBuffer += added;
            Debug.Log($"[FactoryManager] +{added:F1} gas from button. Buffer: {_gasBuffer:F1}/{_config.GasBufferCapacity:F1}");
            PublishStateChanged();
            return added;
        }

        /// <summary>
        /// Fans pick up products from this output buffer.
        /// Returns the actual amount taken.
        /// </summary>
        public float TakeProducts(float amount)
        {
            if (amount <= 0f || _productOutputBuffer <= 0f) return 0f;
            float taken = Mathf.Min(amount, _productOutputBuffer);
            _productOutputBuffer -= taken;
            Debug.Log($"[FactoryManager] Fan took {taken:F1} products. Output buffer: {_productOutputBuffer:F1}");
            PublishStateChanged();
            return taken;
        }

        private void HandleProductionCycleComplete()
        {
            if (_gasBuffer < _config.GasPerProduct) return;

            _gasBuffer -= _config.GasPerProduct;
            _productOutputBuffer += _config.ProductsPerCycle;

            Debug.Log($"[FactoryManager] Produced {_config.ProductsPerCycle:F1} product(s). Gas buffer: {_gasBuffer:F1}, Product output: {_productOutputBuffer:F1}");
            PublishStateChanged();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (_productionTimer == null) return;

            if (e.NewState == GameState.Paused)
                _productionTimer.Pause();
            else if (e.NewState == GameState.Playing)
                _productionTimer.Resume();
            else if (e.NewState == GameState.GameOver)
            {
                _productionTimer.Stop();
                _gasBuffer = 0f;
                _productOutputBuffer = 0f;
                PublishStateChanged();
            }
        }

        private void PublishStateChanged()
        {
            float capacity = _config != null ? _config.GasBufferCapacity : 0f;
            EventBus<OnFactoryStateChanged>.Raise(new OnFactoryStateChanged
            {
                GasBuffer = _gasBuffer,
                GasBufferCapacity = capacity,
                GasBufferFillRatio = capacity > 0f ? _gasBuffer / capacity : 0f,
                ProductOutputBuffer = _productOutputBuffer
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
