using PetroCitySimulator.Events;
using UnityEngine;

public class CityManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private PetroCitySimulator.Data.CityConfigSO _config;

    public bool IsBlackout { get; private set; }

    private void Awake()
    {
        if (_config == null)
            Debug.LogWarning("[CityManager] CityConfigSO is not assigned. Using default blackout behavior.");
    }

    private void OnEnable()
    {
        EventBus<OnStorageEmpty>.Subscribe(HandleStorageEmpty);
        EventBus<OnStorageRestored>.Subscribe(HandleStorageRestored);
        EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
    }

    private void OnDisable()
    {
        EventBus<OnStorageEmpty>.Unsubscribe(HandleStorageEmpty);
        EventBus<OnStorageRestored>.Unsubscribe(HandleStorageRestored);
        EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
    }

    private void HandleStorageEmpty(OnStorageEmpty e)
    {
        if (IsBlackout) return;

        IsBlackout = true;
        EventBus<OnCityBlackout>.Raise(new OnCityBlackout());
        Debug.Log("[CityManager] City entered blackout.");
    }

    private void HandleStorageRestored(OnStorageRestored e)
    {
        if (!IsBlackout) return;

        IsBlackout = false;
        EventBus<OnCityLightsRestored>.Raise(new OnCityLightsRestored());
        Debug.Log($"[CityManager] City lights restored with {e.RestoredAmount:F1} units available.");
    }

    private void HandleGameStateChanged(OnGameStateChanged e)
    {
        if (e.NewState == GameState.MainMenu || e.NewState == GameState.GameOver)
            IsBlackout = false;
    }
}
