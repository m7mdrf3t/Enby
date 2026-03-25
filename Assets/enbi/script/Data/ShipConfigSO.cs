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
using PetroCitySimulator.Entities.Ship;

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

        [Tooltip("Chance that a spawned ship is an export ship (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _exportShipChance = 0.35f;

        [Tooltip("Minimum product units an export ship can carry.")]
        [SerializeField, Min(1f)] private float _minExportAmount = 40f;

        [Tooltip("Maximum product units an export ship can carry.")]
        [SerializeField, Min(1f)] private float _maxExportAmount = 120f;

        [Header("Debug Spawn Overrides")]
        [Tooltip("If enabled, every spawned ship is forced to export products.")]
        [SerializeField] private bool _forceExportShips = false;

        [Tooltip("If enabled, every spawned ship is forced to import gas.")]
        [SerializeField] private bool _forceImportShips = false;

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
        public float ExportShipChance => _exportShipChance;
        public float MinExportAmount => _minExportAmount;
        public float MaxExportAmount => _maxExportAmount;
        public bool ForceExportShips => _forceExportShips;
        public bool ForceImportShips => _forceImportShips;
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

        public float GetRandomExportAmount() =>
            Random.Range(_minExportAmount, _maxExportAmount);

        public ShipCargoType GetRandomCargoType()
        {
            if (_forceExportShips) return ShipCargoType.ExportProducts;
            if (_forceImportShips) return ShipCargoType.ImportGas;

            float roll = Random.value;
            return roll < _exportShipChance
                ? ShipCargoType.ExportProducts
                : ShipCargoType.ImportGas;
        }

        private void OnValidate()
        {
            if (_maxCargoAmount < _minCargoAmount)
                _maxCargoAmount = _minCargoAmount;

            if (_maxExportAmount < _minExportAmount)
                _maxExportAmount = _minExportAmount;

            if (_forceExportShips && _forceImportShips)
                _forceImportShips = false;
        }
    }
}