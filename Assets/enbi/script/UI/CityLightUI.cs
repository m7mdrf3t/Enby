using UnityEngine;
using UnityEngine.UI;
using TMPro;using PetroCitySimulator.Data;using PetroCitySimulator.Data;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.UI
{
    public class CityLightUI : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Status Icon")]
        [Tooltip("Image that switches sprite to reflect city supply state.")]
        [SerializeField] private Image _statusIcon;
        [SerializeField] private Sprite _iconLit;
        [SerializeField] private Sprite _iconDimming;
        [SerializeField] private Sprite _iconBlackout;

        [Header("Consumption Rate")]
        [SerializeField] private TMP_Text _consumptionRateLabel;
        [SerializeField] private TMP_Text _consumptionAmountLabel;

        [Header("Blackout Warning Banner")]
        [SerializeField] private GameObject _blackoutBanner;
        [Tooltip("Pulse frequency of the blackout banner (cycles/sec).")]
        [SerializeField, Min(0f)] private float _bannerPulseFrequency = 1.5f;

        [Header("Supply Health Bar")]
        [SerializeField] private Image _healthBarFill;
        [SerializeField, Min(0.1f)] private float _healthBarLerpSpeed = 3f;

        [SerializeField] private Color _healthColorGood = new Color(0.25f, 0.80f, 0.35f);
        [SerializeField] private Color _healthColorWarning = new Color(0.95f, 0.75f, 0.10f);
        [SerializeField] private Color _healthColorCritical = new Color(0.90f, 0.25f, 0.20f);

        [Header("Thresholds (mirrors CityConfigSO)")]
        [SerializeField] private CityConfigSO _config;
        [SerializeField, Range(0f, 1f)] private float _dimThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _fullLightThreshold = 0.50f;

        // ---------------------------------------------------
        //  Runtime
        // ---------------------------------------------------

        private bool _isBlackout = false;
        private float _pulseTimer = 0f;
        private float _healthBarTarget = 1f;
        private float _healthBarDisplay = 1f;
        private float _lastConsumptionRate = 0f;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void OnEnable()
        {
            EventBus<OnCityGasChanged>.Subscribe(HandleCityGasChanged);
            EventBus<OnCityBlackout>.Subscribe(HandleCityBlackout);
            EventBus<OnCityLightsRestored>.Subscribe(HandleCityRestored);
            EventBus<OnCityConsumptionTick>.Subscribe(HandleConsumptionTick);
        }

        private void OnDisable()
        {
            EventBus<OnCityGasChanged>.Unsubscribe(HandleCityGasChanged);
            EventBus<OnCityBlackout>.Unsubscribe(HandleCityBlackout);
            EventBus<OnCityLightsRestored>.Unsubscribe(HandleCityRestored);
            EventBus<OnCityConsumptionTick>.Unsubscribe(HandleConsumptionTick);
        }

        private void Start()
        {
            ApplyConfigOverrides();
            SetBlackoutBanner(false);
        }

        private void Update()
        {
            AnimateHealthBar();
            AnimateBlackoutBanner();
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        private void HandleCityGasChanged(OnCityGasChanged e)
        {
            _healthBarTarget = e.FillRatio;
            UpdateHealthBarColour(e.FillRatio);
            UpdateStatusIcon(e.FillRatio);
        }

        private void HandleCityBlackout(OnCityBlackout e)
        {
            _isBlackout = true;
            SetBlackoutBanner(true);
            SetStatusIcon(_iconBlackout);
        }

        private void HandleCityRestored(OnCityLightsRestored e)
        {
            _isBlackout = false;
            SetBlackoutBanner(false);
            SetStatusIcon(_iconLit);
        }

        private void HandleConsumptionTick(OnCityConsumptionTick e)
        {
            _lastConsumptionRate = e.ConsumptionRate;

            if (_consumptionRateLabel != null)
                _consumptionRateLabel.text = $"{e.ConsumptionRate:F1} u/s";

            if (_consumptionAmountLabel != null)
                _consumptionAmountLabel.text = $"-{e.AmountConsumed:F1}";
        }

        // ---------------------------------------------------
        //  Animation
        // ---------------------------------------------------

        private void AnimateHealthBar()
        {
            if (_healthBarFill == null) return;

            _healthBarDisplay = Mathf.Lerp(
                _healthBarDisplay, _healthBarTarget,
                Time.deltaTime * _healthBarLerpSpeed);

            _healthBarFill.fillAmount = _healthBarDisplay;
        }

        private void AnimateBlackoutBanner()
        {
            if (!_isBlackout || _blackoutBanner == null) return;

            _pulseTimer += Time.deltaTime * _bannerPulseFrequency * Mathf.PI * 2f;
            float alpha = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;   // [0, 1]
            alpha = Mathf.Lerp(0.4f, 1f, alpha);

            var cg = _blackoutBanner.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
        }

        // ---------------------------------------------------
        //  Status icon
        // ---------------------------------------------------

        private void UpdateStatusIcon(float fillRatio)
        {
            if (_isBlackout) return;   // blackout icon takes priority

            if (fillRatio >= _fullLightThreshold)
                SetStatusIcon(_iconLit);
            else if (fillRatio > 0f)
                SetStatusIcon(_iconDimming);
            else
                SetStatusIcon(_iconBlackout);
        }

        private void SetStatusIcon(Sprite sprite)
        {
            if (_statusIcon != null && sprite != null)
                _statusIcon.sprite = sprite;
        }

        // ---------------------------------------------------
        //  Health bar colour
        // ---------------------------------------------------

        private void UpdateHealthBarColour(float fillRatio)
        {
            if (_healthBarFill == null) return;

            Color target;
            if (fillRatio >= _fullLightThreshold)
                target = _healthColorGood;
            else if (fillRatio >= _dimThreshold)
            {
                float t = (fillRatio - _dimThreshold) /
                          (_fullLightThreshold - _dimThreshold);
                target = Color.Lerp(_healthColorWarning, _healthColorGood, t);
            }
            else
                target = Color.Lerp(
                    _healthColorCritical, _healthColorWarning,
                    fillRatio / _dimThreshold);

            var current = _healthBarFill.color;
            _healthBarFill.color = new Color(target.r, target.g, target.b, current.a);
        }

        // ---------------------------------------------------
        //  Banner helper
        // ---------------------------------------------------

        private void SetBlackoutBanner(bool active)
        {
            if (_blackoutBanner == null) return;

            _blackoutBanner.SetActive(active);

            if (!active)
            {
                var cg = _blackoutBanner.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        private void ApplyConfigOverrides()
        {
            if (_config == null) return;

            _dimThreshold = _config.DimThreshold;
            _fullLightThreshold = _config.FullLightThreshold;
        }
    }
}