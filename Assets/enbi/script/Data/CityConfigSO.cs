using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(
        fileName = "CityConfig",
        menuName = "PetroCity/City Config",
        order = 4)]
    public class CityConfigSO : ScriptableObject
    {
        [Header("Supply Thresholds")]
        [SerializeField, Range(0f, 1f)] private float _dimThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _fullLightThreshold = 0.5f;

        [Header("City Gas Buffer")]
        [Tooltip("Maximum gas the city can hold internally.")]
        [SerializeField, Min(1f)] private float _cityGasCapacity = 200f;

        [Tooltip("Gas units the city consumes per second.")]
        [SerializeField, Min(0f)] private float _consumptionRate = 5f;

        [Tooltip("How often (seconds) the consumption tick fires.")]
        [SerializeField, Min(0.1f)] private float _consumptionTickInterval = 1f;

        [Tooltip("Gas transferred from storage to city per button press.")]
        [SerializeField, Min(1f)] private float _transferAmountPerPress = 50f;

        [Tooltip("Starting gas level as a fraction of city capacity (0–1).")]
        [SerializeField, Range(0f, 1f)] private float _startFillRatio = 0.5f;

        public float DimThreshold => _dimThreshold;
        public float FullLightThreshold => _fullLightThreshold;
        public float CityGasCapacity => _cityGasCapacity;
        public float ConsumptionRate => _consumptionRate;
        public float ConsumptionTickInterval => _consumptionTickInterval;
        public float TransferAmountPerPress => _transferAmountPerPress;
        public float StartFillRatio => _startFillRatio;

        private void OnValidate()
        {
            if (_fullLightThreshold < _dimThreshold)
                _fullLightThreshold = _dimThreshold;
        }
    }
}
