using PetroCitySimulator.Data;
using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class FactoryManager : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private int _factoryId = 1;

        [Header("Config")]
        [SerializeField] private FactoryConfigSO _config;

        private Timer _productionTimer;
        private float _gasBuffer;

        private void Awake()
        {
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
            EventBus<OnFactoryTapped>.Subscribe(HandleFactoryTapped);
            EventBus<OnFactoryGasDelivered>.Subscribe(HandleFactoryGasDelivered);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnFactoryTapped>.Unsubscribe(HandleFactoryTapped);
            EventBus<OnFactoryGasDelivered>.Unsubscribe(HandleFactoryGasDelivered);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;
            if (_productionTimer == null) return;

            bool canProduce = _gasBuffer >= _config.GasPerProduct &&
                              ProductStorageManager.Instance != null &&
                              !ProductStorageManager.Instance.IsFull;

            if (canProduce && !_productionTimer.IsRunning)
                _productionTimer.Start();

            _productionTimer.Tick(Time.deltaTime);
        }

        private void HandleFactoryTapped(OnFactoryTapped e)
        {
            if (e.FactoryId != _factoryId) return;
            TryDispatchIdleFanToFactory();
        }

        private void HandleFactoryGasDelivered(OnFactoryGasDelivered e)
        {
            if (e.FactoryId != _factoryId) return;
            _gasBuffer += Mathf.Max(0f, e.GasAmount);
            Debug.Log($"[FactoryManager] Received gas from fan {e.FanId}: +{e.GasAmount:F1}. Buffer: {_gasBuffer:F1}");
        }

        private void HandleProductionCycleComplete()
        {
            if (ProductStorageManager.Instance == null) return;
            if (_gasBuffer < _config.GasPerProduct) return;

            _gasBuffer -= _config.GasPerProduct;
            float produced = ProductStorageManager.Instance.AddProducts(_config.ProductsPerCycle);

            Debug.Log($"[FactoryManager] Produced {produced:F1} product(s). Gas buffer: {_gasBuffer:F1}");
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
            }
        }

        private void TryDispatchIdleFanToFactory()
        {
            FanController[] fans = FindObjectsByType<FanController>(FindObjectsSortMode.None);
            if (fans == null || fans.Length == 0)
            {
                Debug.Log("[FactoryManager] No fans found to dispatch.");
                return;
            }

            for (int i = 0; i < fans.Length; i++)
            {
                FanController fan = fans[i];
                if (fan != null && fan.IsTappable)
                {
                    bool sent = fan.TryActivateToFactory();
                    if (sent)
                    {
                        Debug.Log($"[FactoryManager] Dispatched fan {fan.FanId} to factory.");
                        return;
                    }
                }
            }

            Debug.Log("[FactoryManager] No eligible idle fan available for factory dispatch.");
        }
    }
}
