// ============================================================
//  FanConfigSO.cs
//  ScriptableObject holding all tunable data for fan behaviour.
//  Create via: Assets → Create → PetroCity → Fan Config
// ============================================================

using UnityEngine;

namespace PetroCitySimulator.Data
{
    [CreateAssetMenu(
        fileName = "FanConfig",
        menuName = "PetroCity/Fan Config",
        order = 3)]
    public class FanConfigSO : ScriptableObject
    {
        // ---------------------------------------------------
        //  Transfer
        // ---------------------------------------------------

        [Header("Transfer")]
        [Tooltip("Gas units moved to the city per single fan activation.")]
        [SerializeField, Min(1f)] private float _transferAmount = 50f;

        [Tooltip("Seconds spent loading at the storage point.")]
        [SerializeField, Min(0.1f)] private float _loadDuration = 2f;

        [Tooltip("Seconds spent unloading at the town point.")]
        [SerializeField, Min(0.1f)] private float _unloadDuration = 2f;

        [Tooltip("Minimum gas in storage required to activate the fan.")]
        [SerializeField, Min(0f)] private float _minimumStorageThreshold = 10f;

        // ---------------------------------------------------
        //  Movement
        // ---------------------------------------------------

        [Header("Movement")]
        [Tooltip("Movement speed while travelling to storage.")]
        [SerializeField, Min(0.1f)] private float _moveToStorageSpeed = 4f;

        [Tooltip("Movement speed while travelling to town.")]
        [SerializeField, Min(0.1f)] private float _moveToTownSpeed = 4f;

        [Tooltip("Movement speed while returning to the home point.")]
        [SerializeField, Min(0.1f)] private float _returnSpeed = 5f;

        [Tooltip("Distance within which the fan snaps to a route point.")]
        [SerializeField, Min(0.05f)] private float _snapDistance = 0.35f;

        // ---------------------------------------------------
        //  Spawning
        // ---------------------------------------------------

        [Header("Spawning")]
        [Tooltip("Delay before the first fan vehicle is spawned.")]
        [SerializeField, Min(0f)] private float _initialSpawnDelay = 0f;

        [Tooltip("Seconds between spawning new fan vehicles.")]
        [SerializeField, Min(0.1f)] private float _spawnInterval = 6f;

        [Tooltip("Maximum active fan vehicles allowed at once.")]
        [SerializeField, Min(1)] private int _maxActiveFans = 3;

        [Tooltip("How many fan vehicles to prewarm in the pool.")]
        [SerializeField, Min(1)] private int _poolInitialSize = 3;

        [Tooltip("Hard cap on the pooled fan vehicle instances. 0 means unlimited.")]
        [SerializeField, Min(0)] private int _poolMaxSize = 6;

        // ---------------------------------------------------
        //  Cooldown
        // ---------------------------------------------------

        [Header("Cooldown")]
        [Tooltip("Seconds the fan must rest between activations.")]
        [SerializeField, Min(0f)] private float _cooldownDuration = 5f;

        // ---------------------------------------------------
        //  Visuals
        // ---------------------------------------------------

        [Header("Visuals")]
        [Tooltip("Rotation speed (degrees/sec) of the fan blade while pumping.")]
        [SerializeField, Min(0f)] private float _activeRotationSpeed = 360f;

        [Tooltip("Rotation speed (degrees/sec) while on cooldown (slow spin-down).")]
        [SerializeField, Min(0f)] private float _cooldownRotationSpeed = 60f;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public float TransferAmount => _transferAmount;
        public float LoadDuration => _loadDuration;
        public float UnloadDuration => _unloadDuration;
        public float MinimumStorageThreshold => _minimumStorageThreshold;
        public float MoveToStorageSpeed => _moveToStorageSpeed;
        public float MoveToTownSpeed => _moveToTownSpeed;
        public float ReturnSpeed => _returnSpeed;
        public float SnapDistance => _snapDistance;
        public float InitialSpawnDelay => _initialSpawnDelay;
        public float SpawnInterval => _spawnInterval;
        public int MaxActiveFans => _maxActiveFans;
        public int PoolInitialSize => _poolInitialSize;
        public int PoolMaxSize => _poolMaxSize;
        public float CooldownDuration => _cooldownDuration;
        public float ActiveRotationSpeed => _activeRotationSpeed;
        public float CooldownRotationSpeed => _cooldownRotationSpeed;
    }
}