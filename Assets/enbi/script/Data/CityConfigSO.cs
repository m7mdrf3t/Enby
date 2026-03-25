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

        public float DimThreshold => _dimThreshold;
        public float FullLightThreshold => _fullLightThreshold;

        private void OnValidate()
        {
            if (_fullLightThreshold < _dimThreshold)
                _fullLightThreshold = _dimThreshold;
        }
    }
}
