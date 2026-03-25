// ============================================================
//  StorageBarUI.cs
//  Displays the current gas storage level as a fill bar.
//  Reacts to OnStorageChanged — that is the ONLY event it needs.
//
//  Features:
//   • Smooth animated fill bar (lerped, not snapped)
//   • Colour ramp: green → amber → red as level drops
//   • Percentage label
//   • Warning pulse animation when fill drops below threshold
//   • "FULL" and "EMPTY" flash indicators
//
//  Pattern : Observer (single event subscription)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.UI
{
    public class StorageBarUI : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Bar")]
        [SerializeField] private Image _fillImage;
        [Tooltip("How fast the bar visually catches up to the real value (lerp speed).")]
        [SerializeField, Min(0.1f)] private float _lerpSpeed = 4f;

        [Header("Colour Thresholds")]
        [SerializeField] private Color _colorFull = new Color(0.25f, 0.80f, 0.35f);
        [SerializeField] private Color _colorMid = new Color(0.95f, 0.75f, 0.10f);
        [SerializeField] private Color _colorLow = new Color(0.90f, 0.25f, 0.20f);
        [SerializeField, Range(0f, 1f)] private float _midThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.25f;

        [Header("Labels")]
        [SerializeField] private TMP_Text _percentageLabel;
        [SerializeField] private TMP_Text _amountLabel;
        [SerializeField] private TMP_Text _statusFlashLabel;   // "FULL" / "EMPTY" / "LOW"

        [Header("Warning Pulse")]
        [Tooltip("Fill ratio below which the bar starts pulsing.")]
        [SerializeField, Range(0f, 1f)] private float _warningThreshold = 0.2f;
        [SerializeField, Min(0f)] private float _pulseFrequency = 2f;
        [SerializeField, Min(0f)] private float _pulseMinAlpha = 0.3f;

        [Header("Status Flash Duration")]
        [SerializeField, Min(0f)] private float _flashDuration = 1.5f;

        // ---------------------------------------------------
        //  Runtime
        // ---------------------------------------------------

        private float _targetFill = 1f;
        private float _displayFill = 1f;
        private float _currentAmount = 0f;
        private float _maxCapacity = 1f;
        private bool _isWarning = false;
        private float _pulseTimer = 0f;
        private float _flashTimer = 0f;
        private bool _flashActive = false;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void OnEnable()
        {
            EventBus<OnStorageChanged>.Subscribe(HandleStorageChanged);
        }

        private void OnDisable()
        {
            EventBus<OnStorageChanged>.Unsubscribe(HandleStorageChanged);
        }

        private void Update()
        {
            AnimateFillBar();
            AnimateWarningPulse();
            TickFlashLabel();
        }

        // ---------------------------------------------------
        //  Event handler
        // ---------------------------------------------------

        private void HandleStorageChanged(OnStorageChanged e)
        {
            _targetFill = e.FillRatio;
            _currentAmount = e.CurrentAmount;
            _maxCapacity = e.MaxCapacity;
            _isWarning = e.FillRatio <= _warningThreshold && e.FillRatio > 0f;

            UpdateLabels(e.FillRatio);
            UpdateBarColour(e.FillRatio);

            if (e.JustBecameFull) ShowFlash("FULL", _colorFull);
            if (e.JustBecameEmpty) ShowFlash("EMPTY", _colorLow);
            else if (e.FillRatio <= _warningThreshold && !_flashActive)
                ShowFlash("LOW", _colorMid);
        }

        // ---------------------------------------------------
        //  Animation helpers
        // ---------------------------------------------------

        private void AnimateFillBar()
        {
            if (_fillImage == null) return;

            _displayFill = Mathf.Lerp(_displayFill, _targetFill,
                              Time.deltaTime * _lerpSpeed);

            _fillImage.fillAmount = _displayFill;
        }

        private void AnimateWarningPulse()
        {
            if (!_isWarning || _fillImage == null) return;

            _pulseTimer += Time.deltaTime * _pulseFrequency * Mathf.PI * 2f;
            float alpha = Mathf.Lerp(_pulseMinAlpha, 1f,
                              (Mathf.Sin(_pulseTimer) + 1f) * 0.5f);

            var c = _fillImage.color;
            _fillImage.color = new Color(c.r, c.g, c.b, alpha);
        }

        private void TickFlashLabel()
        {
            if (!_flashActive || _statusFlashLabel == null) return;

            _flashTimer -= Time.deltaTime;

            float alpha = Mathf.Clamp01(_flashTimer / _flashDuration);
            var c = _statusFlashLabel.color;
            _statusFlashLabel.color = new Color(c.r, c.g, c.b, alpha);

            if (_flashTimer <= 0f)
            {
                _flashActive = false;
                _statusFlashLabel.gameObject.SetActive(false);
            }
        }

        private void ShowFlash(string message, Color colour)
        {
            if (_statusFlashLabel == null) return;

            _statusFlashLabel.text = message;
            _statusFlashLabel.color = new Color(colour.r, colour.g, colour.b, 1f);
            _statusFlashLabel.gameObject.SetActive(true);
            _flashTimer = _flashDuration;
            _flashActive = true;
        }

        // ---------------------------------------------------
        //  Label + colour updates
        // ---------------------------------------------------

        private void UpdateLabels(float fillRatio)
        {
            if (_percentageLabel != null)
                _percentageLabel.text = $"{Mathf.RoundToInt(fillRatio * 100f)}%";

            if (_amountLabel != null)
                _amountLabel.text = $"{_currentAmount:F0} / {_maxCapacity:F0}";
        }

        private void UpdateBarColour(float fillRatio)
        {
            if (_fillImage == null) return;

            Color target;
            if (fillRatio >= _midThreshold)
            {
                float t = (fillRatio - _midThreshold) / (1f - _midThreshold);
                target = Color.Lerp(_colorMid, _colorFull, t);
            }
            else if (fillRatio >= _lowThreshold)
            {
                float t = (fillRatio - _lowThreshold) / (_midThreshold - _lowThreshold);
                target = Color.Lerp(_colorLow, _colorMid, t);
            }
            else
            {
                target = _colorLow;
            }

            // Preserve current alpha (used by pulse animation)
            var current = _fillImage.color;
            _fillImage.color = new Color(target.r, target.g, target.b, current.a);
        }
    }
}