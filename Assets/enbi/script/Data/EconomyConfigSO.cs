using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "PetroCity/Economy Config", order = 7)]
    public class EconomyConfigSO : ScriptableObject
    {
        [Header("Export Pricing")]
        [SerializeField, Min(0f)] private float _moneyPerProductUnit = 4f;

        public float MoneyPerProductUnit => _moneyPerProductUnit;
    }
}
