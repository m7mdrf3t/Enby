using PetroCitySimulator.Data;
using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class FactoryManager : MonoBehaviour
    {
        public static FactoryManager Instance { get; private set; }

        [Header("Identity")]
        [SerializeField] private int _factoryId = 1;

        [Header("Config")]
        [SerializeField] private FactoryConfigSO _config;
        [SerializeField] private FactoryUpgradeConfigSO _upgradeConfig;

        [Header("Visual")]
        [Tooltip("Parent transform where factory model is displayed.")]
        [SerializeField] private Transform _modelParent;

        private Timer _productionTimer;
        private float _gasBuffer;
        private float _productOutputBuffer;
        private int _currentLevel = 1;
        private GameObject _currentModelInstance;

        // Upgrade eligibility tracking
        private bool _firstUpgradeUnlocked = false;
        private bool _secondUpgradeUnlocked = false;

        public float GasBuffer => _gasBuffer;
        public float GasBufferCapacity => _config != null ? _config.GasBufferCapacity : 0f;
        public bool IsBufferFull => _config != null && _gasBuffer >= _config.GasBufferCapacity;
        public float ProductOutputBuffer => _productOutputBuffer;
        public bool HasProducts => _productOutputBuffer > 0f;
        public int CurrentLevel => _currentLevel;
        public bool CanUpgrade => _currentLevel < (_upgradeConfig != null ? _upgradeConfig.LevelCount : 1) &&
                                   ((_currentLevel == 1 && _firstUpgradeUnlocked) ||
                                    (_currentLevel == 2 && _secondUpgradeUnlocked));

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
                Debug.LogError("[FactoryManager] FactoryConfigSO is not assigned.");
                return;
            }

            _productionTimer = new Timer(GetProductionDuration());
            _productionTimer.OnCompleted += HandleProductionCycleComplete;

            // Spawn initial model
            SpawnModelForLevel(_currentLevel);
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;
            if (_productionTimer == null) return;

            // Check upgrade eligibility based on match timer
            if (_upgradeConfig != null)
            {
                float timeElapsed = Core.GameManager.Instance.MatchDurationSeconds - Core.GameManager.Instance.MainTimerRemaining;
                float timeProgress = Core.GameManager.Instance.MatchDurationSeconds > 0f 
                    ? timeElapsed / Core.GameManager.Instance.MatchDurationSeconds
                    : 1f;

                // First upgrade at 50%
                if (!_firstUpgradeUnlocked && _currentLevel == 1 && timeProgress >= _upgradeConfig.FirstUpgradeTimeGate)
                {
                    _firstUpgradeUnlocked = true;
                    RaiseUpgradeUnlockedEvent();
                }

                // Second upgrade at 75%
                if (!_secondUpgradeUnlocked && _currentLevel == 2 && timeProgress >= _upgradeConfig.SecondUpgradeTimeGate)
                {
                    _secondUpgradeUnlocked = true;
                    RaiseUpgradeUnlockedEvent();
                }
            }

            bool canProduce = _gasBuffer >= (_config.GasPerProduct * GetGasPerProductMultiplier());

            if (canProduce && !_productionTimer.IsRunning)
                _productionTimer.Start();

            _productionTimer.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Add gas to the factory buffer (called by Factory UI Button).
        /// Returns the actual amount added (clamped to capacity).
        /// </summary>
        public float AddGas(float amount)
        {
            if (_config == null || amount <= 0f) return 0f;

            float space = _config.GasBufferCapacity - _gasBuffer;
            float added = Mathf.Min(amount, space);
            _gasBuffer += added;
            Debug.Log($"[FactoryManager] +{added:F1} gas from button. Buffer: {_gasBuffer:F1}/{_config.GasBufferCapacity:F1}");
            PublishStateChanged();
            return added;
        }

        /// <summary>
        /// Fans pick up products from this output buffer.
        /// Returns the actual amount taken.
        /// </summary>
        public float TakeProducts(float amount)
        {
            if (amount <= 0f || _productOutputBuffer <= 0f) return 0f;
            float taken = Mathf.Min(amount, _productOutputBuffer);
            _productOutputBuffer -= taken;
            Debug.Log($"[FactoryManager] Fan took {taken:F1} products. Output buffer: {_productOutputBuffer:F1}");
            PublishStateChanged();
            return taken;
        }

        private void HandleProductionCycleComplete()
        {
            float gasRequired = _config.GasPerProduct * GetGasPerProductMultiplier();
            if (_gasBuffer < gasRequired) return;

            _gasBuffer -= gasRequired;
            _productOutputBuffer += _config.ProductsPerCycle * GetProductsPerCycleMultiplier();

            Debug.Log($"[FactoryManager] Produced {_config.ProductsPerCycle * GetProductsPerCycleMultiplier():F1} product(s). Gas buffer: {_gasBuffer:F1}, Product output: {_productOutputBuffer:F1}");
            PublishStateChanged();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (_productionTimer == null) return;

            if (e.NewState == GameState.Paused)
                _productionTimer.Pause();
            else if (e.NewState == GameState.Playing)
                _productionTimer.Resume();
            else if (e.NewState == GameState.GameOver)
            {
                _productionTimer.Stop();
                _gasBuffer = 0f;
                _productOutputBuffer = 0f;
                _currentLevel = 1;
                _firstUpgradeUnlocked = false;
                _secondUpgradeUnlocked = false;
                PublishStateChanged();
            }
        }

        // ---------------------------------------------------
        //  Factory Upgrades
        // ---------------------------------------------------

        /// <summary>
        /// Attempt to upgrade the factory to the next level.
        /// Requires money and current unlock status.
        /// </summary>
        public bool TryUpgradeFactory()
        {
            if (!CanUpgrade)
            {
                Debug.Log("[FactoryManager] Upgrade not available or not unlocked yet.");
                return false;
            }

            if (_upgradeConfig == null)
            {
                Debug.LogError("[FactoryManager] Upgrade config is not assigned.");
                return false;
            }

            // Get next level data
            int nextLevel = _currentLevel + 1;
            var nextLevelData = _upgradeConfig.GetLevel(nextLevel - 1);
            if (nextLevelData == null)
            {
                Debug.LogError($"[FactoryManager] No upgrade data for level {nextLevel}.");
                return false;
            }

            // Check if player has enough money
            if (MoneyManager.Instance == null || !MoneyManager.Instance.CanAfford(nextLevelData.UpgradeCost))
            {
                Debug.Log($"[FactoryManager] Not enough money for upgrade (need {nextLevelData.UpgradeCost}, have {(MoneyManager.Instance != null ? MoneyManager.Instance.CurrentMoney : 0):F0}).");
                return false;
            }

            // Deduct money
            MoneyManager.Instance.SpendMoney(nextLevelData.UpgradeCost, "FactoryUpgrade");

            // Apply upgrade
            _currentLevel = nextLevel;
            _productionTimer.Stop();
            _productionTimer = new Timer(GetProductionDuration());
            _productionTimer.OnCompleted += HandleProductionCycleComplete;

            // Update visual model
            SpawnModelForLevel(_currentLevel);

            // Publish upgrade event
            RaiseUpgradedEvent();

            Debug.Log($"[FactoryManager] Factory upgraded to level {_currentLevel}!");
            return true;
        }

        private float GetProductionDuration()
        {
            float baseDuration = _config != null ? _config.ProductionDuration : 4f;
            return baseDuration / GetProductionDurationMultiplier();
        }

        private float GetGasPerProductMultiplier()
        {
            if (_upgradeConfig == null || _currentLevel <= 1) return 1f;
            var levelData = _upgradeConfig.GetLevel(_currentLevel - 1);
            return levelData != null ? levelData.GasPerProductMultiplier : 1f;
        }

        private float GetProductsPerCycleMultiplier()
        {
            if (_upgradeConfig == null || _currentLevel <= 1) return 1f;
            var levelData = _upgradeConfig.GetLevel(_currentLevel - 1);
            return levelData != null ? levelData.ProductsPerCycleMultiplier : 1f;
        }

        private float GetProductionDurationMultiplier()
        {
            if (_upgradeConfig == null || _currentLevel <= 1) return 1f;
            var levelData = _upgradeConfig.GetLevel(_currentLevel - 1);
            return levelData != null ? levelData.ProductionDurationMultiplier : 1f;
        }

        private void SpawnModelForLevel(int level)
        {
            // Destroy old model
            if (_currentModelInstance != null)
                Destroy(_currentModelInstance);

            if (_modelParent == null || _upgradeConfig == null)
            {
                Debug.LogWarning("[FactoryManager] Model parent or upgrade config is missing.");
                return;
            }

            var levelData = _upgradeConfig.GetLevel(level - 1);
            if (levelData == null || levelData.ModelPrefab == null)
            {
                Debug.LogWarning($"[FactoryManager] No model prefab for level {level}.");
                return;
            }

            // Instantiate new model
            _currentModelInstance = Instantiate(levelData.ModelPrefab, _modelParent);
            _currentModelInstance.name = $"FactoryModel_Level{level}";
            Debug.Log($"[FactoryManager] Spawned model for level {level}.");
        }

        private void RaiseUpgradeUnlockedEvent()
        {
            if (_upgradeConfig == null) return;

            int nextLevel = _currentLevel + 1;
            var nextLevelData = _upgradeConfig.GetLevel(nextLevel - 1);
            if (nextLevelData == null) return;

            EventBus<OnFactoryUpgradeUnlocked>.Raise(new OnFactoryUpgradeUnlocked
            {
                CurrentLevel = _currentLevel,
                NextLevel = nextLevel,
                UpgradeCost = nextLevelData.UpgradeCost
            });

            Debug.Log($"[FactoryManager] Upgrade to level {nextLevel} unlocked! Cost: {nextLevelData.UpgradeCost:F0}");
        }

        private void RaiseUpgradedEvent()
        {
            if (_upgradeConfig == null) return;

            var currentLevelData = _upgradeConfig.GetLevel(_currentLevel - 1);
            if (currentLevelData == null) return;

            float gasPerProduct = _config != null ? _config.GasPerProduct * currentLevelData.GasPerProductMultiplier : 0f;
            float productsPerCycle = _config != null ? _config.ProductsPerCycle * currentLevelData.ProductsPerCycleMultiplier : 0f;
            float duration = GetProductionDuration();

            EventBus<OnFactoryUpgraded>.Raise(new OnFactoryUpgraded
            {
                NewLevel = _currentLevel,
                GasPerProduct = gasPerProduct,
                ProductsPerCycle = productsPerCycle,
                ProductionDuration = duration
            });
        }

        private void PublishStateChanged()
        {
            float capacity = _config != null ? _config.GasBufferCapacity : 0f;
            EventBus<OnFactoryStateChanged>.Raise(new OnFactoryStateChanged
            {
                GasBuffer = _gasBuffer,
                GasBufferCapacity = capacity,
                GasBufferFillRatio = capacity > 0f ? _gasBuffer / capacity : 0f,
                ProductOutputBuffer = _productOutputBuffer
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
