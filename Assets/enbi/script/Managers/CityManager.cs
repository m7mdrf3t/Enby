using PetroCitySimulator.Data;
using PetroCitySimulator.Events;
using UnityEngine;

public class CityManager : MonoBehaviour
{
    public static CityManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private CityConfigSO _config;

    // ---------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------

    private float _cityGasLevel;
    private float _cityGasCapacity;
    private float _consumptionRate;
    private float _consumptionTickInterval;
    private float _consumptionTimer;

    public bool IsBlackout { get; private set; }
    public float CurrentGas => _cityGasLevel;
    public float GasCapacity => _cityGasCapacity;
    public float FillRatio => _cityGasCapacity > 0f ? _cityGasLevel / _cityGasCapacity : 0f;
    public bool IsFull => _cityGasLevel >= _cityGasCapacity;

    // ---------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_config == null)
        {
            Debug.LogWarning("[CityManager] CityConfigSO is not assigned. Using defaults.");
            _cityGasCapacity = 200f;
            _consumptionRate = 5f;
            _consumptionTickInterval = 1f;
        }
        else
        {
            _cityGasCapacity = _config.CityGasCapacity;
            _consumptionRate = _config.ConsumptionRate;
            _consumptionTickInterval = _config.ConsumptionTickInterval;
            _cityGasLevel = _cityGasCapacity * Mathf.Clamp01(_config.StartFillRatio);
        }
    }

    private void OnEnable()
    {
        EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
    }

    private void OnDisable()
    {
        EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
    }

    private void Start()
    {
        PublishCityGasChanged();
    }

    private void Update()
    {
        if (!PetroCitySimulator.Core.GameManager.Instance.IsPlaying) return;

        TickConsumption();
    }

    // ---------------------------------------------------
    //  Consumption tick
    // ---------------------------------------------------

    private void TickConsumption()
    {
        _consumptionTimer += Time.deltaTime;
        if (_consumptionTimer < _consumptionTickInterval) return;

        _consumptionTimer -= _consumptionTickInterval;

        if (_cityGasLevel <= 0f)
        {
            if (!IsBlackout) EnterBlackout();
            return;
        }

        float drain = _consumptionRate * _consumptionTickInterval;
        float prevAmount = _cityGasLevel;
        _cityGasLevel = Mathf.Max(0f, _cityGasLevel - drain);
        float actualDrain = prevAmount - _cityGasLevel;

        EventBus<OnCityConsumptionTick>.Raise(new OnCityConsumptionTick
        {
            AmountConsumed = actualDrain,
            ConsumptionRate = _consumptionRate
        });

        if (_cityGasLevel <= 0f && !IsBlackout)
            EnterBlackout();

        PublishCityGasChanged();
    }

    // ---------------------------------------------------
    //  Public methods
    // ---------------------------------------------------

    /// <summary>
    /// Add gas to the city buffer (called by UI City Button).
    /// Returns the actual amount added (clamped to capacity).
    /// </summary>
    public float AddGas(float amount)
    {
        if (amount <= 0f) return 0f;

        float prevAmount = _cityGasLevel;
        _cityGasLevel = Mathf.Min(_cityGasLevel + amount, _cityGasCapacity);
        float actualAdded = _cityGasLevel - prevAmount;

        if (IsBlackout && _cityGasLevel > 0f)
            ExitBlackout();

        PublishCityGasChanged();
        Debug.Log($"[CityManager] City received +{actualAdded:F1} gas. Level: {_cityGasLevel:F1}/{_cityGasCapacity:F1}");
        return actualAdded;
    }

    // ---------------------------------------------------
    //  Blackout state
    // ---------------------------------------------------

    private void EnterBlackout()
    {
        IsBlackout = true;
        EventBus<OnCityBlackout>.Raise(new OnCityBlackout());
        PublishCityGasChanged();
        Debug.Log("[CityManager] City entered blackout.");
    }

    private void ExitBlackout()
    {
        IsBlackout = false;
        EventBus<OnCityLightsRestored>.Raise(new OnCityLightsRestored());
        Debug.Log("[CityManager] City lights restored.");
    }

    // ---------------------------------------------------
    //  Game state
    // ---------------------------------------------------

    private void HandleGameStateChanged(OnGameStateChanged e)
    {
        if (e.NewState == GameState.Paused || e.NewState == GameState.Playing)
            _consumptionTimer = 0f;

        if (e.NewState == GameState.MainMenu || e.NewState == GameState.GameOver)
        {
            IsBlackout = false;
            _cityGasLevel = _config != null
                ? _cityGasCapacity * Mathf.Clamp01(_config.StartFillRatio)
                : _cityGasCapacity * 0.5f;
            PublishCityGasChanged();
        }
    }

    // ---------------------------------------------------
    //  Helpers
    // ---------------------------------------------------

    private void PublishCityGasChanged()
    {
        EventBus<OnCityGasChanged>.Raise(new OnCityGasChanged
        {
            CurrentAmount = _cityGasLevel,
            MaxCapacity = _cityGasCapacity,
            FillRatio = FillRatio
        });
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
