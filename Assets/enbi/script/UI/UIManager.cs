// ============================================================
//  UIManager.cs
//  Owns all HUD panel references and reacts to game state
//  changes (show/hide panels, trigger overlays).
//  Individual HUD components (StorageBarUI, TimerUI, etc.)
//  manage their own event subscriptions � UIManager only
//  handles top-level panel visibility and screen transitions.
//
//  Pattern : Facade (single entry point for UI state)
//            Observer (OnGameStateChanged)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using PetroCitySimulator.Data;
using PetroCitySimulator.Events;
using PetroCitySimulator.Managers;

namespace PetroCitySimulator.UI
{
    public class UIManager : MonoBehaviour
    {

        [Header("Panels")]
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _mainMenuPanel;

        [Header("Blackout Overlay")]
        [Tooltip("Full-screen dark overlay that fades in during city blackout.")]
        [SerializeField] private CanvasGroup _blackoutOverlay;

        [Tooltip("Seconds for the blackout overlay to fade in/out.")]
        [SerializeField, Min(0f)] private float _blackoutFadeDuration = 1.2f;

        [Header("HUD Components")]
        [SerializeField] private StorageBarUI _storageBarUI;
        [SerializeField] private CityLightUI _cityStatusUI;

        [Header("Action Buttons")]
        [Tooltip("Button: sends gas from storage → factory.")]
        [SerializeField] private Button _factoryButton;

        [Tooltip("Button: sends gas from storage → city.")]
        [SerializeField] private Button _cityButton;

        [Header("Action Config")]
        [SerializeField] private FactoryConfigSO _factoryConfig;
        [SerializeField] private CityConfigSO _cityConfig;

        // ---------------------------------------------------
        //  Runtime
        // ---------------------------------------------------

        private Coroutine _blackoutFade;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
            EventBus<OnCityBlackout>.Subscribe(HandleCityBlackout);
            EventBus<OnCityLightsRestored>.Subscribe(HandleCityRestored);
            EventBus<OnStorageChanged>.Subscribe(HandleStorageChangedForButtons);
            EventBus<OnCityGasChanged>.Subscribe(HandleCityGasChangedForButtons);
            EventBus<OnFactoryStateChanged>.Subscribe(HandleFactoryStateChangedForButtons);

            if (_factoryButton != null)
                _factoryButton.onClick.AddListener(OnFactoryButtonPressed);
            if (_cityButton != null)
                _cityButton.onClick.AddListener(OnCityButtonPressed);
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
            EventBus<OnCityBlackout>.Unsubscribe(HandleCityBlackout);
            EventBus<OnCityLightsRestored>.Unsubscribe(HandleCityRestored);
            EventBus<OnStorageChanged>.Unsubscribe(HandleStorageChangedForButtons);
            EventBus<OnCityGasChanged>.Unsubscribe(HandleCityGasChangedForButtons);
            EventBus<OnFactoryStateChanged>.Unsubscribe(HandleFactoryStateChangedForButtons);

            if (_factoryButton != null)
                _factoryButton.onClick.RemoveListener(OnFactoryButtonPressed);
            if (_cityButton != null)
                _cityButton.onClick.RemoveListener(OnCityButtonPressed);
        }

        private void Start()
        {
            var gm = Core.GameManager.Instance;
            ShowPanel(gm != null ? gm.CurrentState : GameState.MainMenu);

            if (_blackoutOverlay != null)
            {
                _blackoutOverlay.alpha = 0f;
                _blackoutOverlay.interactable = false;
                _blackoutOverlay.blocksRaycasts = false;
            }

            UpdateActionButtons();
        }

        // ---------------------------------------------------
        //  Game state handler
        // ---------------------------------------------------

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            ShowPanel(e.NewState);
        }

        private void ShowPanel(GameState state)
        {
            SetActive(_mainMenuPanel, state == GameState.MainMenu);
            SetActive(_hudPanel, state == GameState.Playing || state == GameState.Paused);
            SetActive(_pausePanel, state == GameState.Paused);
            SetActive(_gameOverPanel, state == GameState.GameOver);
        }

        // ---------------------------------------------------
        //  Blackout overlay
        // ---------------------------------------------------

        private void HandleCityBlackout(OnCityBlackout e)
        {
            if (_blackoutFade != null) StopCoroutine(_blackoutFade);
            _blackoutFade = StartCoroutine(FadeOverlay(targetAlpha: 0.85f));
        }

        private void HandleCityRestored(OnCityLightsRestored e)
        {
            if (_blackoutFade != null) StopCoroutine(_blackoutFade);
            _blackoutFade = StartCoroutine(FadeOverlay(targetAlpha: 0f));
        }

        private System.Collections.IEnumerator FadeOverlay(float targetAlpha)
        {
            if (_blackoutOverlay == null) yield break;

            float startAlpha = _blackoutOverlay.alpha;
            float elapsed = 0f;

            _blackoutOverlay.interactable = false;
            _blackoutOverlay.blocksRaycasts = targetAlpha > 0f;

            while (elapsed < _blackoutFadeDuration)
            {
                elapsed += Time.deltaTime;
                _blackoutOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha,
                                              elapsed / _blackoutFadeDuration);
                yield return null;
            }

            _blackoutOverlay.alpha = targetAlpha;
            _blackoutFade = null;
        }

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        // ---------------------------------------------------
        //  Public button callbacks (wired in Unity Inspector)
        // ---------------------------------------------------

        public void OnStartButtonPressed() => Core.GameManager.Instance.StartGame();
        public void OnPauseButtonPressed() => Core.GameManager.Instance.PauseGame();
        public void OnResumeButtonPressed() => Core.GameManager.Instance.ResumeGame();
        public void OnQuitButtonPressed() => Core.GameManager.Instance.EndGame();

        // ---------------------------------------------------
        //  Action buttons — Factory & City
        // ---------------------------------------------------

        public void OnFactoryButtonPressed()
        {
            if (StorageManager.Instance == null || FactoryManager.Instance == null) return;
            if (FactoryManager.Instance.IsBufferFull || StorageManager.Instance.IsEmpty) return;

            float transferAmount = _factoryConfig != null ? _factoryConfig.GasTransferPerPress : 50f;
            float drained = StorageManager.Instance.TransferGasOut(transferAmount);
            if (drained > 0f)
                FactoryManager.Instance.AddGas(drained);
        }

        public void OnCityButtonPressed()
        {
            if (StorageManager.Instance == null || CityManager.Instance == null) return;
            if (CityManager.Instance.IsFull || StorageManager.Instance.IsEmpty) return;

            float transferAmount = _cityConfig != null ? _cityConfig.TransferAmountPerPress : 50f;
            float drained = StorageManager.Instance.TransferGasOut(transferAmount);
            if (drained > 0f)
                CityManager.Instance.AddGas(drained);
        }

        // ---------------------------------------------------
        //  Button state management
        // ---------------------------------------------------

        private void HandleStorageChangedForButtons(OnStorageChanged e)
        {
            UpdateActionButtons();
        }

        private void HandleCityGasChangedForButtons(OnCityGasChanged e)
        {
            UpdateActionButtons();
        }

        private void HandleFactoryStateChangedForButtons(OnFactoryStateChanged e)
        {
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool storageHasGas = StorageManager.Instance != null && !StorageManager.Instance.IsEmpty;

            if (_factoryButton != null)
            {
                bool factoryCanAccept = FactoryManager.Instance != null && !FactoryManager.Instance.IsBufferFull;
                _factoryButton.interactable = storageHasGas && factoryCanAccept;
                Debug.Log($"[UIManager] Factory Button Interactable: {_factoryButton.interactable} (StorageHasGas: {storageHasGas}, FactoryCanAccept: {factoryCanAccept})");
            }

            if (_cityButton != null)
            {
                bool cityCanAccept = CityManager.Instance != null && !CityManager.Instance.IsFull;
                _cityButton.interactable = storageHasGas && cityCanAccept;
                Debug.Log($"[UIManager] City Button Interactable: {_cityButton.interactable} (StorageHasGas: {storageHasGas}, CityCanAccept: {cityCanAccept})");
            }
        }
    }
}