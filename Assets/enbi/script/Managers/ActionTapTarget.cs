using PetroCitySimulator.UI;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    [RequireComponent(typeof(BoxCollider))]
    public class ActionTapTarget : MonoBehaviour
    {
        public enum ActionType
        {
            Factory,
            City,
            Export
        }

        [SerializeField] private ActionType _actionType;

        [Tooltip("Optional explicit UIManager reference. If empty, it is auto-resolved at runtime.")]
        [SerializeField] private UIManager _uiManager;

        [Tooltip("Optional explicit ShoreManager reference for export actions.")]
        [SerializeField] private ShoreManager _shoreManager;

        private void Awake()
        {
            ResolveUIManager();
        }

        public void TriggerAction()
        {
            switch (_actionType)
            {
                case ActionType.Factory:
                    if (!ResolveUIManager())
                    {
                        Debug.LogWarning($"[ActionTapTarget] UIManager not found for {gameObject.name}");
                        return;
                    }

                    _uiManager.OnFactoryButtonPressed();
                    break;
                case ActionType.City:
                    if (!ResolveUIManager())
                    {
                        Debug.LogWarning($"[ActionTapTarget] UIManager not found for {gameObject.name}");
                        return;
                    }

                    _uiManager.OnCityButtonPressed();
                    break;
                case ActionType.Export:
                    if (!ResolveShoreManager())
                    {
                        Debug.LogWarning($"[ActionTapTarget] ShoreManager not found for {gameObject.name}");
                        return;
                    }

                    _shoreManager.TryProcessNextExportAction();
                    break;
            }
        }

        private bool ResolveUIManager()
        {
            if (_uiManager != null) return true;

            _uiManager = FindFirstObjectByType<UIManager>();
            return _uiManager != null;
        }

        private bool ResolveShoreManager()
        {
            if (_shoreManager != null) return true;

            _shoreManager = FindFirstObjectByType<ShoreManager>();
            return _shoreManager != null;
        }
    }
}
