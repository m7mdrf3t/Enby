// ============================================================
//  ShipSpawnConfigSO.cs
//  ScriptableObject holding all tunable spawn settings.
//  Create via: Assets → Create → PetroCity → Ship Spawn Config
// ============================================================

using UnityEngine;
using UnityEngine.Serialization;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(
        fileName = "ShipSpawnConfig",
        menuName  = "PetroCity/Ship Spawn Config",
        order     = 1)]
    public class ShipSpawnConfigSO : ScriptableObject
    {
        // ---------------------------------------------------
        //  Pool
        // ---------------------------------------------------

        [Header("Object Pool")]
        [Tooltip("How many ship instances to pre-warm at startup.")]
        [SerializeField, Min(1)] private int _poolInitialSize = 6;

        [Tooltip("Hard cap on simultaneous ship instances. 0 = unlimited.")]
        [SerializeField, Min(0)] private int _poolMaxSize = 12;

        // ---------------------------------------------------
        //  Spawn timing
        // ---------------------------------------------------

        [Header("Spawn Timing")]
        [Tooltip("Seconds between ship spawns at game start (easiest rate).")]
        [SerializeField, Min(1f)] private float _initialSpawnInterval = 12f;

        [Tooltip("Minimum seconds between spawns (hardest rate, reached at MaxDifficultyTime).")]
        [SerializeField, Min(1f)] private float _minimumSpawnInterval = 4f;

        [Tooltip("Seconds of play time over which spawn interval ramps from initial to minimum.")]
        [SerializeField, Min(1f)] private float _maxDifficultyTime = 180f;

        [Tooltip("Random jitter added to each spawn interval (±seconds). Keeps spawning from feeling mechanical.")]
        [SerializeField, Min(0f)] private float _spawnIntervalJitter = 1.5f;

        // ---------------------------------------------------
        //  Spawn positions
        // ---------------------------------------------------

        [Header("Spawn Zone")]
        [Tooltip("Ships spawn along this horizontal band. X range is randomised between min and max.")]
        [SerializeField] private float _spawnZoneXMin = -12f;
        [SerializeField] private float _spawnZoneXMax = -8f;

        [Tooltip("Z position of the sea lane where ships travel (center line).")]
        [FormerlySerializedAs("_spawnZoneY")]
        [SerializeField] private float _seaLaneZ = 0f;

        [Tooltip("How far from the sea lane center ships can spawn (±Z variance).")]
        [SerializeField] private float _seaLaneZVariance = 2f;

        [Tooltip("Idle zone X — ships move here after spawning and wait to be tapped.")]
        [SerializeField] private float _idleZoneXMin = -6f;
        [SerializeField] private float _idleZoneXMax = -2f;

        [Tooltip("How far from the sea lane ships can drift while idle (±Z variance).")]
        [SerializeField] private float _idleZoneZVariance = 2f;

        [Tooltip("X position ships sail toward after departure (off-screen right).")]
        [SerializeField] private float _departureX = 14f;

        // ---------------------------------------------------
        //  Limits
        // ---------------------------------------------------

        [Header("Limits")]
        [Tooltip("Maximum ships allowed in the idle zone at once. New spawns pause if this is reached.")]
        [SerializeField, Min(1)] private int _maxIdleShips = 3;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public int   PoolInitialSize        => _poolInitialSize;
        public int   PoolMaxSize            => _poolMaxSize;
        public float InitialSpawnInterval   => _initialSpawnInterval;
        public float MinimumSpawnInterval   => _minimumSpawnInterval;
        public float MaxDifficultyTime      => _maxDifficultyTime;
        public float SpawnIntervalJitter    => _spawnIntervalJitter;
        public float SpawnZoneXMin          => _spawnZoneXMin;
        public float SpawnZoneXMax          => _spawnZoneXMax;
        public float SpawnZoneY             => _seaLaneZ;
        public float SeaLaneZ               => _seaLaneZ;
        public float SeaLaneZVariance       => _seaLaneZVariance;
        public float IdleZoneXMin           => _idleZoneXMin;
        public float IdleZoneXMax           => _idleZoneXMax;
        public float IdleZoneZVariance      => _idleZoneZVariance;
        public float DepartureX             => _departureX;
        public int   MaxIdleShips           => _maxIdleShips;

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        /// <summary>
        /// Returns the spawn interval for a given elapsed play time,
        /// lerped along an ease-out curve from initial to minimum.
        /// </summary>
        public float GetSpawnInterval(float elapsedSeconds)
        {
            float t        = Mathf.Clamp01(elapsedSeconds / _maxDifficultyTime);
            float eased    = 1f - Mathf.Pow(1f - t, 2f);   // ease-out quad
            float interval = Mathf.Lerp(_initialSpawnInterval, _minimumSpawnInterval, eased);
            float jitter   = Random.Range(-_spawnIntervalJitter, _spawnIntervalJitter);
            return Mathf.Max(_minimumSpawnInterval, interval + jitter);
        }

        /// <summary>Returns a random spawn-zone world position.</summary>
        public Vector3 GetSpawnPosition() =>
            new Vector3(
                Random.Range(_spawnZoneXMin, _spawnZoneXMax),
                0f,
                _seaLaneZ + Random.Range(-_seaLaneZVariance, _seaLaneZVariance));

        /// <summary>Returns a random idle-anchor world position.</summary>
        public Vector3 GetIdlePosition() =>
            new Vector3(
                Random.Range(_idleZoneXMin, _idleZoneXMax),
                0f,
                _seaLaneZ + Random.Range(-_idleZoneZVariance, _idleZoneZVariance));

        /// <summary>Returns the world-space departure target for a ship at the given lane Z.</summary>
        public Vector3 GetDepartureTarget(float worldZ) =>
            new Vector3(_departureX, 0f, worldZ);

        private void OnValidate()
        {
            if (_minimumSpawnInterval > _initialSpawnInterval)
                _minimumSpawnInterval = _initialSpawnInterval;
            if (_spawnZoneXMax < _spawnZoneXMin)
                _spawnZoneXMax = _spawnZoneXMin;
            if (_idleZoneXMax < _idleZoneXMin)
                _idleZoneXMax = _idleZoneXMin;
        }
    }
}
