using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(
        fileName = "StorageConfig",
        menuName = "PetroCity/Storage Config",
        order = 2)]
    public class StorageConfigSO : ScriptableObject
    {
        [Header("Tank Settings")]
        [SerializeField, Min(1f)] private float _maxCapacity = 1000f;
        [SerializeField, Range(0f, 1f)] private float _startFillRatio = 0.5f;

        [Header("Consumption")]
        [SerializeField, Min(0f)] private float _baseConsumptionRate = 5f;
        [SerializeField, Min(0.1f)] private float _consumptionTickInterval = 1f;

        public float MaxCapacity => _maxCapacity;
        public float StartFillRatio => _startFillRatio;
        public float BaseConsumptionRate => _baseConsumptionRate;
        public float ConsumptionTickInterval => _consumptionTickInterval;
    }
}
