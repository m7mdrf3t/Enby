using PetroCitySimulator.Events;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class ProductStorageManager : MonoBehaviour
    {
        public static ProductStorageManager Instance { get; private set; }

        [Header("Product Storage")]
        [SerializeField, Min(1f)] private float _maxCapacity = 250f;
        [SerializeField, Min(0f)] private float _startAmount = 0f;

        private float _currentAmount;

        public float CurrentAmount => _currentAmount;
        public float MaxCapacity => _maxCapacity;
        public float FillRatio => _maxCapacity > 0f ? _currentAmount / _maxCapacity : 0f;
        public bool IsFull => _currentAmount >= _maxCapacity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentAmount = Mathf.Clamp(_startAmount, 0f, _maxCapacity);
        }

        private void Start()
        {
            PublishChanged();
        }

        private void OnEnable()
        {
            EventBus<OnProductExportRequested>.Subscribe(HandleExportRequested);
        }

        private void OnDisable()
        {
            EventBus<OnProductExportRequested>.Unsubscribe(HandleExportRequested);
        }

        public float AddProducts(float amount)
        {
            if (amount <= 0f) return 0f;
            float previous = _currentAmount;
            _currentAmount = Mathf.Min(_maxCapacity, _currentAmount + amount);
            float added = _currentAmount - previous;
            if (added > 0f) PublishChanged();
            return added;
        }

        public float TakeProducts(float amount)
        {
            if (amount <= 0f) return 0f;
            float previous = _currentAmount;
            _currentAmount = Mathf.Max(0f, _currentAmount - amount);
            float taken = previous - _currentAmount;
            if (taken > 0f) PublishChanged();
            return taken;
        }

        private void PublishChanged()
        {
            EventBus<OnProductStorageChanged>.Raise(new OnProductStorageChanged
            {
                CurrentAmount = _currentAmount,
                MaxCapacity = _maxCapacity,
                FillRatio = FillRatio
            });
        }

        private void HandleExportRequested(OnProductExportRequested e)
        {
            float exported = TakeProducts(e.RequestedAmount);

            EventBus<OnProductsExported>.Raise(new OnProductsExported
            {
                ShipId = e.ShipId,
                SocketIndex = e.SocketIndex,
                AmountExported = exported
            });

            Debug.Log($"[ProductStorageManager] Ship {e.ShipId} exported {exported:F1}/{e.RequestedAmount:F1} products.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
