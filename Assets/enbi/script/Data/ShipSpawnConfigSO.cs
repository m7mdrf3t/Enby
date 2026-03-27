// ============================================================
//  ShipSpawnConfigSO.cs
//  ScriptableObject holding all tunable spawn settings.
//  Create via: Assets → Create → PetroCity → Ship Spawn Config
// ============================================================

using UnityEngine;

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
        [Tooltip("World-space center of the spawn zone rectangle (X/Z plane).")]
        [SerializeField] private Vector2 _spawnZoneCenter = new Vector2(-10f, 0f);

        [Tooltip("Spawn zone size in world units: X width and Z depth.")]
        [SerializeField] private Vector2 _spawnZoneSize = new Vector2(4f, 4f);

        [Tooltip("World-space center of the idle zone rectangle (X/Z plane).")]
        [SerializeField] private Vector2 _idleZoneCenter = new Vector2(-4f, 0f);

        [Tooltip("Idle zone size in world units: X width and Z depth.")]
        [SerializeField] private Vector2 _idleZoneSize = new Vector2(4f, 4f);

        [Tooltip("World Y height for ship spawn, idle and departure targets.")]
        [SerializeField] private float _zoneWorldY = 0f;

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
        public Vector2 SpawnZoneCenter      => _spawnZoneCenter;
        public Vector2 SpawnZoneSize        => _spawnZoneSize;
        public Vector2 IdleZoneCenter       => _idleZoneCenter;
        public Vector2 IdleZoneSize         => _idleZoneSize;
        public float ZoneWorldY             => _zoneWorldY;
        public float SpawnZoneXMin          => _spawnZoneCenter.x - (_spawnZoneSize.x * 0.5f);
        public float SpawnZoneXMax          => _spawnZoneCenter.x + (_spawnZoneSize.x * 0.5f);
        public float SpawnZoneY             => _zoneWorldY;
        public float SeaLaneZ               => _spawnZoneCenter.y;
        public float SeaLaneZVariance       => _spawnZoneSize.y * 0.5f;
        public float IdleZoneXMin           => _idleZoneCenter.x - (_idleZoneSize.x * 0.5f);
        public float IdleZoneXMax           => _idleZoneCenter.x + (_idleZoneSize.x * 0.5f);
        public float IdleZoneZVariance      => _idleZoneSize.y * 0.5f;
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
                Random.Range(_spawnZoneCenter.x - (_spawnZoneSize.x * 0.5f), _spawnZoneCenter.x + (_spawnZoneSize.x * 0.5f)),
                _zoneWorldY,
                Random.Range(_spawnZoneCenter.y - (_spawnZoneSize.y * 0.5f), _spawnZoneCenter.y + (_spawnZoneSize.y * 0.5f)));

        /// <summary>Returns a random idle-anchor world position.</summary>
        public Vector3 GetIdlePosition() =>
            new Vector3(
                Random.Range(_idleZoneCenter.x - (_idleZoneSize.x * 0.5f), _idleZoneCenter.x + (_idleZoneSize.x * 0.5f)),
                _zoneWorldY,
                Random.Range(_idleZoneCenter.y - (_idleZoneSize.y * 0.5f), _idleZoneCenter.y + (_idleZoneSize.y * 0.5f)));

        /// <summary>Returns the world-space departure target for a ship at the given lane Z.</summary>
        public Vector3 GetDepartureTarget(float worldZ) =>
            new Vector3(_departureX, _zoneWorldY, worldZ);

        private void OnValidate()
        {
            if (_minimumSpawnInterval > _initialSpawnInterval)
                _minimumSpawnInterval = _initialSpawnInterval;
            _spawnZoneSize.x = Mathf.Max(0.01f, _spawnZoneSize.x);
            _spawnZoneSize.y = Mathf.Max(0.01f, _spawnZoneSize.y);
            _idleZoneSize.x = Mathf.Max(0.01f, _idleZoneSize.x);
            _idleZoneSize.y = Mathf.Max(0.01f, _idleZoneSize.y);
        }
    }
}
