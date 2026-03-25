using PetroCitySimulator.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PetroCitySimulator.UI
{
    public class EconomyPanelUI : MonoBehaviour
    {
        [Header("Product Storage")]
        [SerializeField] private Image _productFill;
        [SerializeField] private TMP_Text _productAmountLabel;

        [Header("Money")]
        [SerializeField] private TMP_Text _moneyLabel;
        [SerializeField] private TMP_Text _moneyDeltaLabel;
        [SerializeField, Min(0f)] private float _deltaLabelDuration = 1.6f;

        [Header("Formatting")]
        [SerializeField] private string _moneyPrefix = "$";

        private float _deltaTimer;

        private void OnEnable()
        {
            EventBus<OnProductStorageChanged>.Subscribe(HandleProductStorageChanged);
            EventBus<OnMoneyChanged>.Subscribe(HandleMoneyChanged);
        }

        private void OnDisable()
        {
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

        private void HandleProductStorageChanged(OnProductStorageChanged e)
        {
            if (_productFill != null)
                _productFill.fillAmount = e.FillRatio;

            if (_productAmountLabel != null)
                _productAmountLabel.text = $"{e.CurrentAmount:F0} / {e.MaxCapacity:F0}";
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
