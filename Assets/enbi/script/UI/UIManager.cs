// ============================================================
//  UIManager.cs
//  Owns all HUD panel references and reacts to game state
//  changes (show/hide panels, trigger overlays).
//  Individual HUD components (StorageBarUI, TimerUI, etc.)
//  manage their own event subscriptions — UIManager only
//  handles top-level panel visibility and screen transitions.
//
//  Pattern : Facade (single entry point for UI state)
//            Observer (OnGameStateChanged)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using PetroCitySimulator.Events;

namespace PetroCitySimulator.UI
{
    public class UIManager : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector — Panel roots
        // ---------------------------------------------------

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
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
            EventBus<OnCityBlackout>.Unsubscribe(HandleCityBlackout);
            EventBus<OnCityLightsRestored>.Unsubscribe(HandleCityRestored);
        }

        private void Start()
        {
            ShowPanel(GameState.MainMenu);

            if (_blackoutOverlay != null)
            {
                _blackoutOverlay.alpha = 0f;
                _blackoutOverlay.interactable = false;
                _blackoutOverlay.blocksRaycasts = false;
            }
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
    }
}