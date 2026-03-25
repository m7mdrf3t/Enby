// ============================================================
//  TimerUI.cs
//  Two responsibilities in one component:
//
//  1. DockTimerUI  � shows the 20-second dock countdown for a
//     specific socket. One instance per socket in the scene.
//     Subscribes to OnSocketTimerTick filtered by SocketIndex.
//
//  2. FanCooldownUI � shows a radial fill ring draining to zero
//     as a fan's cooldown expires. One instance per fan.
//     Subscribes to OnFanActivated / OnFanCompleted.
//
//  Both are on the same script to keep the UI folder lean.
//  Assign _mode in the Inspector to choose which behaviour
//  this instance uses.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.UI
{
    public class TimerUI : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Mode selection
        // ---------------------------------------------------

        public enum TimerMode { DockCountdown, FanCooldown }

        [Header("Mode")]
        [SerializeField] private TimerMode _mode = TimerMode.DockCountdown;

        // ---------------------------------------------------
        //  Dock countdown settings
        // ---------------------------------------------------

        [Header("Dock Countdown (if mode = DockCountdown)")]
        [Tooltip("Which socket index this timer display belongs to.")]
        [SerializeField] private int _socketIndex = 0;

        [SerializeField] private TMP_Text _countdownLabel;
        [SerializeField] private Image _countdownRing;

        [Tooltip("Total dock duration � used to normalise the ring fill.")]
        [SerializeField, Min(1f)] private float _dockDuration = 20f;

        // ---------------------------------------------------
        //  Fan cooldown settings
        // ---------------------------------------------------

        [Header("Fan Cooldown (if mode = FanCooldown)")]
        [Tooltip("Which fan ID this cooldown ring belongs to.")]
        [SerializeField] private int _fanId = 0;

        [SerializeField] private Image _cooldownRing;
        [SerializeField] private TMP_Text _cooldownLabel;
        [SerializeField] private GameObject _readyIndicator;

        [Tooltip("Total cooldown duration � used to normalise the ring fill.")]
        [SerializeField, Min(0.1f)] private float _cooldownDuration = 5f;

        // ---------------------------------------------------
        //  Runtime
        // ---------------------------------------------------

        private float _ringTarget = 0f;
        private float _ringDisplay = 0f;
        private bool _active = false;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void OnEnable()
        {
            if (_mode == TimerMode.DockCountdown)
            {
                EventBus<OnSocketTimerTick>.Subscribe(HandleSocketTick);
                EventBus<OnSocketFreed>.Subscribe(HandleSocketFreed);
            }
            else
            {
                EventBus<OnFanActivated>.Subscribe(HandleFanActivated);
                EventBus<OnFanCompleted>.Subscribe(HandleFanCompleted);
                EventBus<OnStorageChanged>.Subscribe(HandleStorageChanged);
            }
        }

        private void OnDisable()
        {
            if (_mode == TimerMode.DockCountdown)
            {
                EventBus<OnSocketTimerTick>.Unsubscribe(HandleSocketTick);
                EventBus<OnSocketFreed>.Unsubscribe(HandleSocketFreed);
            }
            else
            {
                EventBus<OnFanActivated>.Unsubscribe(HandleFanActivated);
                EventBus<OnFanCompleted>.Unsubscribe(HandleFanCompleted);
                EventBus<OnStorageChanged>.Unsubscribe(HandleStorageChanged);
            }
        }

        private void Start()
        {
            SetVisible(false);
            SetReadyIndicator(false);
        }

        private void Update()
        {
            // Smooth ring fill toward target
            if (_mode == TimerMode.DockCountdown && _countdownRing != null)
            {
                _ringDisplay = Mathf.Lerp(_ringDisplay, _ringTarget, Time.deltaTime * 8f);
                _countdownRing.fillAmount = _ringDisplay;
            }
            else if (_mode == TimerMode.FanCooldown && _cooldownRing != null)
            {
                _ringDisplay = Mathf.Lerp(_ringDisplay, _ringTarget, Time.deltaTime * 8f);
                _cooldownRing.fillAmount = _ringDisplay;
            }
        }

        // ---------------------------------------------------
        //  Dock countdown handlers
        // ---------------------------------------------------

        private void HandleSocketTick(OnSocketTimerTick e)
        {
            if (e.SocketIndex != _socketIndex) return;

            _active = true;
            SetVisible(true);

            float remaining = e.SecondsRemaining;
            _ringTarget = remaining / _dockDuration;

            if (_countdownLabel != null)
                _countdownLabel.text = FormatTime(remaining);
        }

        private void HandleSocketFreed(OnSocketFreed e)
        {
            if (e.SocketIndex != _socketIndex) return;

            _active = false;
            _ringTarget = 0f;
            _ringDisplay = 0f;
            SetVisible(false);

            if (_countdownLabel != null) _countdownLabel.text = string.Empty;
        }

        // ---------------------------------------------------
        //  Fan cooldown handlers
        // ---------------------------------------------------

        private void HandleFanActivated(OnFanActivated e)
        {
            if (e.FanId != _fanId) return;

            _active = true;
            _ringTarget = 1f;
            _ringDisplay = 1f;
            SetReadyIndicator(false);
            SetVisible(true);

            if (_cooldownLabel != null)
                _cooldownLabel.text = $"{_cooldownDuration:F0}s";
        }

        private void HandleFanCompleted(OnFanCompleted e)
        {
            if (e.FanId != _fanId) return;

            // Ring drains to zero over cooldown � driven by StorageChanged
            // for simplicity; a dedicated tick event could also be used.
            StartCoroutine(DrainCooldownRing());
        }

        private void HandleStorageChanged(OnStorageChanged e)
        {
            if (_mode != TimerMode.FanCooldown) return;

            if (!_active && e.CurrentAmount > 0f)
                SetReadyIndicator(true);
        }

        private System.Collections.IEnumerator DrainCooldownRing()
        {
            float elapsed = 0f;
            while (elapsed < _cooldownDuration)
            {
                elapsed += Time.deltaTime;
                _ringTarget = 1f - Mathf.Clamp01(elapsed / _cooldownDuration);

                if (_cooldownLabel != null)
                    _cooldownLabel.text =
                        $"{Mathf.Max(0f, _cooldownDuration - elapsed):F0}s";

                yield return null;
            }

            _ringTarget = 0f;
            _active = false;

            if (_cooldownLabel != null) _cooldownLabel.text = string.Empty;

            SetReadyIndicator(true);
        }

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        private void SetVisible(bool visible)
        {
            if (_mode == TimerMode.DockCountdown)
            {
                SetObjectActive(_countdownLabel, visible);
                SetObjectActive(_countdownRing, visible);
                return;
            }

            SetObjectActive(_cooldownLabel, visible);
            SetObjectActive(_cooldownRing, visible);
        }

        private void SetReadyIndicator(bool ready)
        {
            if (_readyIndicator != null)
                _readyIndicator.SetActive(ready);
        }

        private static void SetObjectActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return s >= 60
                ? $"{s / 60}:{s % 60:D2}"
                : $"{s}s";
        }
    }
}