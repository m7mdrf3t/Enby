// ============================================================
//  FactoryUpgradeTarget.cs
//  3D UI interaction point for factory upgrades.
//  Player taps/clicks this 3D object to trigger factory upgrade.
//  Shows upgrade availability and cost via visual feedback.
//
//  Requires a BoxCollider on the same GameObject.
// ============================================================

using UnityEngine;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.Managers
{
    [RequireComponent(typeof(BoxCollider))]
    public class FactoryUpgradeTarget : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [Tooltip("Renderer to tint when upgrade is available.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Color when upgrade is available.")]
        [SerializeField] private Color _availableColor = new Color(0.2f, 1f, 0.2f, 0.8f);

        [Tooltip("Color when upgrade is locked.")]
        [SerializeField] private Color _lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private Material _material;
        private bool _upgradeAvailable = false;

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            if (_renderer != null)
                _material = _renderer.material;
        }

        private void OnEnable()
        {
            EventBus<OnFactoryUpgradeUnlocked>.Subscribe(HandleUpgradeUnlocked);
            EventBus<OnFactoryUpgraded>.Subscribe(HandleUpgradeCompleted);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnFactoryUpgradeUnlocked>.Unsubscribe(HandleUpgradeUnlocked);
            EventBus<OnFactoryUpgraded>.Unsubscribe(HandleUpgradeCompleted);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Start()
        {
            UpdateVisuals();
        }

        private void OnMouseDown()
        {
            TriggerUpgrade();
        }

        /// <summary>
        /// Manually trigger upgrade attempt. Called by click or external action.
        /// </summary>
        public void TriggerUpgrade()
        {
            if (FactoryManager.Instance == null)
            {
                Debug.LogWarning("[FactoryUpgradeTarget] FactoryManager not found.");
                return;
            }

            if (!FactoryManager.Instance.CanUpgrade)
            {
                Debug.Log("[FactoryUpgradeTarget] Factory upgrade not available or unlocked yet.");
                return;
            }

            if (FactoryManager.Instance.TryUpgradeFactory())
            {
                Debug.Log("[FactoryUpgradeTarget] Factory upgraded successfully!");
            }
        }

        private void HandleUpgradeUnlocked(OnFactoryUpgradeUnlocked e)
        {
            _upgradeAvailable = true;
            UpdateVisuals();
        }

        private void HandleUpgradeCompleted(OnFactoryUpgraded e)
        {
            _upgradeAvailable = false;
            UpdateVisuals();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (e.NewState == GameState.GameOver || e.NewState == GameState.MainMenu)
            {
                _upgradeAvailable = false;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (_material == null)
                return;

            Color targetColor = _upgradeAvailable ? _availableColor : _lockedColor;
            _material.color = targetColor;
        }
    }
}
