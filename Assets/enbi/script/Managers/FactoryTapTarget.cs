using UnityEngine;

namespace PetroCitySimulator.Managers
{
    [RequireComponent(typeof(Collider))]
    public class FactoryTapTarget : MonoBehaviour
    {
        [SerializeField] private int _factoryId = 1;

        public int FactoryId => _factoryId;
    }
}
