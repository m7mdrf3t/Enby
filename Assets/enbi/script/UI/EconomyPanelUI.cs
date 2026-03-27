using PetroCitySimulator.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PetroCitySimulator.UI
{
    public class EconomyPanelUI : MonoBehaviour
    {
        [Header("Factory Gas Buffer")]
        [SerializeField] private Image _factoryGasFill;
        [SerializeField] private TMP_Text _factoryGasLabel;
        [SerializeField] private TMP_Text _factoryProductLabel;

        [Header("Product Storage")]
        [SerializeField] private Image _productFill;
        [SerializeField] private TMP_Text _productAmountLabel;
        [Tooltip("Shows only the current product count as a plain number.")]
        [SerializeField] private TMP_Text _productCountLabel;

        [Header("Money")]
        [SerializeField] private TMP_Text _moneyLabel;
        [SerializeField] private TMP_Text _moneyDeltaLabel;
        [SerializeField, Min(0f)] private float _deltaLabelDuration = 1.6f;

        [Header("Formatting")]
        [SerializeField] private string _moneyPrefix = "$";

        private float _deltaTimer;

        private void OnEnable()
        {
            EventBus<OnFactoryStateChanged>.Subscribe(HandleFactoryStateChanged);
            EventBus<OnProductStorageChanged>.Subscribe(HandleProductStorageChanged);
            EventBus<OnMoneyChanged>.Subscribe(HandleMoneyChanged);
        }

        private void OnDisable()
        {
            EventBus<OnFactoryStateChanged>.Unsubscribe(HandleFactoryStateChanged);
            EventBus<OnProductStorageChanged>.Unsubscribe(HandleProductStorageChanged);
            EventBus<OnMoneyChanged>.Unsubscribe(HandleMoneyChanged);
        }

        private void Update()
        {
            if (_moneyDeltaLabel == null || ! _moneyDeltaLabel.gameObject.activeSelf)
                return;

            _deltaTimer -= Time.deltaTime;
            if (_deltaTimer <= 0f)
                _moneyDeltaLabel.gameObject.SetActive(false);
        }

        private void HandleFactoryStateChanged(OnFactoryStateChanged e)
        {
            if (_factoryGasFill != null)
                _factoryGasFill.fillAmount = e.GasBufferFillRatio;

            if (_factoryGasLabel != null)
                _factoryGasLabel.text = $"{e.GasBuffer:F0} / {e.GasBufferCapacity:F0}";

            if (_factoryProductLabel != null)
                _factoryProductLabel.text = $"{e.ProductOutputBuffer:F0}";
        }

        private void HandleProductStorageChanged(OnProductStorageChanged e)
        {
            if (_productFill != null)
                _productFill.fillAmount = e.FillRatio;

            if (_productAmountLabel != null)
                _productAmountLabel.text = $"{e.CurrentAmount:F0} / {e.MaxCapacity:F0}";

            if (_productCountLabel != null)
                _productCountLabel.text = $"{e.CurrentAmount:F0}";
        }

        private void HandleMoneyChanged(OnMoneyChanged e)
        {
            if (_moneyLabel != null)
                _moneyLabel.text = $"{_moneyPrefix}{e.CurrentMoney:F0}";

            if (_moneyDeltaLabel != null && e.Delta > 0f)
            {
                _moneyDeltaLabel.text = $"+{_moneyPrefix}{e.Delta:F0}";
                _moneyDeltaLabel.gameObject.SetActive(true);
                _deltaTimer = _deltaLabelDuration;
            }
        }
    }
}
