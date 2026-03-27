using PetroCitySimulator.Data;
using PetroCitySimulator.Events;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class MoneyManager : MonoBehaviour
    {
        public static MoneyManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private EconomyConfigSO _config;

        [Header("Starting Money")]
        [SerializeField, Min(0f)] private float _startingMoney = 0f;

        private float _currentMoney;

        public float CurrentMoney => _currentMoney;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentMoney = _startingMoney;
        }

        private void OnEnable()
        {
            EventBus<OnProductsExported>.Subscribe(HandleProductsExported);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnProductsExported>.Unsubscribe(HandleProductsExported);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Start()
        {
            PublishMoneyChanged(0f, "Init");
        }

        private void HandleProductsExported(OnProductsExported e)
        {
            if (_config == null)
            {
                Debug.LogError("[MoneyManager] EconomyConfigSO is not assigned.");
                return;
            }

            float delta = Mathf.Max(0f, e.AmountExported) * _config.MoneyPerProductUnit;
            if (delta <= 0f)
                return;

            _currentMoney += delta;
            PublishMoneyChanged(delta, "ProductExport");

            Debug.Log($"[MoneyManager] +{delta:F1} money from ship {e.ShipId} export ({e.AmountExported:F1} units).");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (e.NewState == GameState.GameOver)
            {
                _currentMoney = _startingMoney;
                PublishMoneyChanged(0f, "Reset");
            }
        }

        private void PublishMoneyChanged(float delta, string source)
        {
            EventBus<OnMoneyChanged>.Raise(new OnMoneyChanged
            {
                CurrentMoney = _currentMoney,
                Delta = delta,
                Source = source
            });
        }

        public bool CanAfford(float amount) => _currentMoney >= amount;

        public bool SpendMoney(float amount, string source)
        {
            if (amount <= 0f) return true;
            if (_currentMoney < amount) return false;

            _currentMoney -= amount;
            PublishMoneyChanged(-amount, source);
            Debug.Log($"[MoneyManager] -{amount:F1} money ({source}). Remaining: {_currentMoney:F1}");
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
