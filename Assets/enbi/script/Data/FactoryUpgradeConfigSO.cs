using UnityEngine;

namespace PetroCitySimulator.Data
{
    [System.Serializable]
    public class FactoryUpgradeLevel
    {
        [Header("Level Settings")]
        [SerializeField] public int Level = 1;
        
        [Tooltip("3D model prefab for this upgrade level.")]
        [SerializeField] public GameObject ModelPrefab;

        [Header("Cost")]
        [Tooltip("Money required to upgrade to this level.")]
        [SerializeField, Min(0f)] public float UpgradeCost = 100f;

        [Header("Stats")]
        [Tooltip("Gas consumption per product (multiplier from base).")]
        [SerializeField, Min(0.1f)] public float GasPerProductMultiplier = 1f;

        [Tooltip("Products per cycle (multiplier from base).")]
        [SerializeField, Min(0.1f)] public float ProductsPerCycleMultiplier = 1f;

        [Tooltip("Production duration (multiplier from base, lower = faster).")]
        [SerializeField, Min(0.1f)] public float ProductionDurationMultiplier = 1f;
    }

    [CreateAssetMenu(fileName = "FactoryUpgradeConfig", menuName = "PetroCity/Factory Upgrade Config", order = 7)]
    public class FactoryUpgradeConfigSO : ScriptableObject
    {
        [Header("Upgrade Levels")]
        [SerializeField] private FactoryUpgradeLevel[] _levels = new FactoryUpgradeLevel[3];

        [Header("Unlock Timing")]
        [Tooltip("Fraction of match time (0..1) when first upgrade becomes available.")]
        [SerializeField, Range(0f, 1f)] private float _firstUpgradeTimeGate = 0.5f;

        [Tooltip("Fraction of match time (0..1) when second/final upgrade becomes available.")]
        [SerializeField, Range(0f, 1f)] private float _secondUpgradeTimeGate = 0.75f;

        public int LevelCount => _levels != null ? _levels.Length : 0;
        public float FirstUpgradeTimeGate => _firstUpgradeTimeGate;
        public float SecondUpgradeTimeGate => _secondUpgradeTimeGate;

        public FactoryUpgradeLevel GetLevel(int index)
        {
            if (_levels != null && index >= 0 && index < _levels.Length)
                return _levels[index];
            return null;
        }
    }
}
