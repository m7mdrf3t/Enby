using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "PetroCity/Economy Config", order = 7)]
    public class EconomyConfigSO : ScriptableObject
    {
        [Header("Export Pricing")]
        [SerializeField, Min(0f)] private float _moneyPerProductUnit = 4f;

        [Header("Ship Docking")]
        [Tooltip("Flat cost to dock one import ship.")]
        [SerializeField, Min(0f)] private float _dockingCostPerShip = 10f;

        public float MoneyPerProductUnit => _moneyPerProductUnit;
        public float DockingCostPerShip  => _dockingCostPerShip;
    }
}
