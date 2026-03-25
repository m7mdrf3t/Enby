// ============================================================
//  ShipConfigSO.cs
//  ScriptableObject that holds all tunable data for ship behaviour.
//  Create via: Assets → Create → PetroCity → Ship Config
//
//  Pattern : Data Object (ScriptableObject)
//            Keeps magic numbers out of code and lets designers
//            tweak values without touching any script.
// ============================================================

using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(
        fileName = "ShipConfig",
        menuName = "PetroCity/Ship Config",
        order = 0)]
    public class ShipConfigSO : ScriptableObject
    {
        // ---------------------------------------------------
        //  Cargo
        // ---------------------------------------------------

        [Header("Cargo")]
        [Tooltip("Minimum gas units a ship can carry.")]
        [SerializeField, Min(1f)] private float _minCargoAmount = 80f;

        [Tooltip("Maximum gas units a ship can carry.")]
        [SerializeField, Min(1f)] private float _maxCargoAmount = 200f;

        // ---------------------------------------------------
        //  Docking
        // ---------------------------------------------------

        [Header("Docking")]
        [Tooltip("Seconds the ship stays docked before cargo is delivered and it departs.")]
        [SerializeField, Min(1f)] private float _dockDuration = 20f;

        [Tooltip("How close (world units) the ship must be to the socket to snap into the docked position.")]
        [SerializeField, Min(0.1f)] private float _dockSnapDistance = 0.5f;

        // ---------------------------------------------------
        //  Movement
        // ---------------------------------------------------

        [Header("Movement")]
        [Tooltip("Speed (world units/sec) while sailing toward the socket.")]
        [SerializeField, Min(0.1f)] private float _dockingSpeed = 4f;

        [Tooltip("Speed (world units/sec) while departing off-screen.")]
        [SerializeField, Min(0.1f)] private float _departureSpeed = 6f;

        [Tooltip("Speed (world units/sec) while idling / bobbing in the waiting zone.")]
        [SerializeField, Min(0.1f)] private float _idleSpeed = 1f;

        [Tooltip("How far off-screen the ship must travel before it is despawned.")]
        [SerializeField, Min(1f)] private float _despawnDistance = 20f;

        // ---------------------------------------------------
        //  Visuals / feedback
        // ---------------------------------------------------

        [Header("Visuals")]
        [Tooltip("Vertical bob amplitude while idle (world units).")]
        [SerializeField, Min(0f)] private float _idleBobAmplitude = 0.15f;

        [Tooltip("Vertical bob frequency while idle (cycles per second).")]
        [SerializeField, Min(0f)] private float _idleBobFrequency = 0.8f;

        // ---------------------------------------------------
        //  Public read-only properties
        // ---------------------------------------------------

        public float MinCargoAmount => _minCargoAmount;
        public float MaxCargoAmount => _maxCargoAmount;
        public float DockDuration => _dockDuration;
        public float DockSnapDistance => _dockSnapDistance;
        public float DockingSpeed => _dockingSpeed;
        public float DepartureSpeed => _departureSpeed;
        public float IdleSpeed => _idleSpeed;
        public float DespawnDistance => _despawnDistance;
        public float IdleBobAmplitude => _idleBobAmplitude;
        public float IdleBobFrequency => _idleBobFrequency;

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        /// <summary>Returns a random cargo amount within the configured range.</summary>
        public float GetRandomCargo() =>
            Random.Range(_minCargoAmount, _maxCargoAmount);

        private void OnValidate()
        {
            if (_maxCargoAmount < _minCargoAmount)
                _maxCargoAmount = _minCargoAmount;
        }
    }
}