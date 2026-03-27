using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(fileName = "FactoryConfig", menuName = "PetroCity/Factory Config", order = 6)]
    public class FactoryConfigSO : ScriptableObject
    {
        [Header("Production")]
        [SerializeField, Min(0.1f)] private float _productionDuration = 4f;
        [SerializeField, Min(1f)] private float _gasPerProduct = 25f;
        [SerializeField, Min(1f)] private float _productsPerCycle = 1f;

        [Header("Gas Buffer")]
        [Tooltip("Maximum gas the factory can hold internally.")]
        [SerializeField, Min(1f)] private float _gasBufferCapacity = 200f;

        [Tooltip("Gas transferred from storage to factory per button press.")]
        [SerializeField, Min(1f)] private float _gasTransferPerPress = 50f;

        public float ProductionDuration => _productionDuration;
        public float GasPerProduct => _gasPerProduct;
        public float ProductsPerCycle => _productsPerCycle;
        public float GasBufferCapacity => _gasBufferCapacity;
        public float GasTransferPerPress => _gasTransferPerPress;
    }
}
