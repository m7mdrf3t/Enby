// ============================================================
//  ShipSpawnManager.cs
//  Owns the ship ObjectPool and drives the entire ship lifecycle:
//   • Ticks a spawn timer that accelerates over time
//   • Pulls ships from the pool, initialises them, releases them
//   • Tracks idle ship count to cap simultaneous waiting ships
//   • Listens for OnShipDespawned to return ships to the pool
//   • Listens for OnGameStateChanged to pause/resume/reset
//
//  This is the only class allowed to call pool.Get() / pool.Return().
//  All other systems receive ship references via EventBus only.
//
//  Pattern : Manager (owns lifecycle)
//            Object Pool (via ObjectPool<ShipController>)
//            Observer (OnShipDespawned, OnGameStateChanged)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using PetroCitySimulator.Data;
using PetroCitySimulator.Entities.Ship;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;

namespace PetroCitySimulator.Managers
{
    public class ShipSpawnManager : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Config")]
        [SerializeField] private ShipSpawnConfigSO _spawnConfig;
        [SerializeField] private ShipConfigSO _shipConfig;

        [Header("Prefab")]
        [Tooltip("Ship prefab — must have ShipController at root.")]
        [SerializeField] private ShipController _shipPrefab;

        [Tooltip("Parent transform for pooled (inactive) ships.")]
        [SerializeField] private Transform _poolParent;

        // ---------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------

        private ObjectPool<ShipController> _pool;

        private float _spawnTimer = 0f;
        private float _nextSpawnIn = 0f;
        private float _elapsedPlayTime = 0f;
        private bool _spawnPaused = false;
        private int _nextShipId = 1;

        /// <summary>Ships currently in the idle zone waiting for a tap.</summary>
        private readonly HashSet<int> _idleShipIds = new HashSet<int>();

        /// <summary>All ships currently alive (idle + docking + docked + departing).</summary>
        private readonly Dictionary<int, ShipController> _activeShips
            = new Dictionary<int, ShipController>();

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void Awake()
        {
            ValidateReferences();

            _pool = new ObjectPool<ShipController>(
                prefab: _shipPrefab,
                poolParent: _poolParent,
                initialSize: _spawnConfig.PoolInitialSize,
                maxSize: _spawnConfig.PoolMaxSize);
        }

        private void OnEnable()
        {
            EventBus<OnShipDespawned>.Subscribe(HandleShipDespawned);
            EventBus<OnShipDocked>.Subscribe(HandleShipDocked);
            EventBus<OnShipDockingStarted>.Subscribe(HandleShipDockingStarted);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnShipDespawned>.Unsubscribe(HandleShipDespawned);
            EventBus<OnShipDocked>.Unsubscribe(HandleShipDocked);
            EventBus<OnShipDockingStarted>.Unsubscribe(HandleShipDockingStarted);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Start()
        {
            ScheduleNextSpawn();
        }

        private void Update()
        {
            if (_spawnPaused) return;
            if (!Core.GameManager.Instance.IsPlaying) return;

            _elapsedPlayTime += Time.deltaTime;
            _spawnTimer += Time.deltaTime;

            if (_spawnTimer >= _nextSpawnIn)
            {
                _spawnTimer = 0f;
                TrySpawnShip();
                ScheduleNextSpawn();
            }
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
        }

        // ---------------------------------------------------
        //  Spawn logic
        // ---------------------------------------------------

        private void TrySpawnShip()
        {
            // Respect idle ship cap — don't flood the waiting zone
            if (_idleShipIds.Count >= _spawnConfig.MaxIdleShips)
            {
                Debug.Log("[ShipSpawnManager] Idle cap reached — spawn skipped.");
                return;
            }

            ShipController ship = _pool.Get();
            if (ship == null)
            {
                Debug.LogWarning("[ShipSpawnManager] Pool exhausted — spawn skipped.");
                return;
            }

            int shipId = _nextShipId++;
            float cargo = _shipConfig.GetRandomCargo();
            Vector3 spawnPos = _spawnConfig.GetSpawnPosition();
            Vector3 idlePos = _spawnConfig.GetIdlePosition();
            Vector3 departure = _spawnConfig.GetDepartureTarget(spawnPos.y);

            ship.Initialise(
                shipId: shipId,
                cargoAmount: cargo,
                spawnPosition: spawnPos,
                idleAnchor: idlePos,
                departureTarget: departure,
                config: _shipConfig);

            _activeShips[shipId] = ship;
            _idleShipIds.Add(shipId);

            Debug.Log($"[ShipSpawnManager] Spawned ship {shipId} | cargo: {cargo:F1} | " +
                      $"pool in use: {_pool.InUseCount}/{_pool.TotalCount}");
        }

        private void ScheduleNextSpawn()
        {
            _nextSpawnIn = _spawnConfig.GetSpawnInterval(_elapsedPlayTime);
            Debug.Log($"[ShipSpawnManager] Next spawn in {_nextSpawnIn:F1}s.");
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        /// <summary>
        /// Ship has moved from idle to docking — no longer counts
        /// toward the idle cap, so a new ship can spawn sooner.
        /// </summary>
        private void HandleShipDockingStarted(OnShipDockingStarted e)
        {
            _idleShipIds.Remove(e.ShipId);
        }

        /// <summary>
        /// Ship is docked — already removed from idle on DockingStarted,
        /// just kept for completeness / future analytics.
        /// </summary>
        private void HandleShipDocked(OnShipDocked e)
        {
            // Intentionally empty — handled via DockingStarted
        }

        /// <summary>
        /// Ship has finished departing and its FSM hit Despawned.
        /// Return it to the pool.
        /// </summary>
        private void HandleShipDespawned(OnShipDespawned e)
        {
            _idleShipIds.Remove(e.ShipId);

            if (_activeShips.TryGetValue(e.ShipId, out ShipController ship))
            {
                _activeShips.Remove(e.ShipId);
                _pool.Return(ship);
                Debug.Log($"[ShipSpawnManager] Ship {e.ShipId} returned to pool. " +
                          $"Pool available: {_pool.AvailableCount}");
            }
            else
            {
                Debug.LogWarning($"[ShipSpawnManager] Despawn for unknown ship id {e.ShipId}.");
            }
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            switch (e.NewState)
            {
                case GameState.Playing:
                    _spawnPaused = false;
                    break;

                case GameState.Paused:
                    _spawnPaused = true;
                    break;

                case GameState.GameOver:
                    _spawnPaused = true;
                    ResetAll();
                    break;

                case GameState.MainMenu:
                    _spawnPaused = true;
                    break;
            }
        }

        // ---------------------------------------------------
        //  Reset
        // ---------------------------------------------------

        private void ResetAll()
        {
            _pool.ReturnAll();
            _activeShips.Clear();
            _idleShipIds.Clear();
            _spawnTimer = 0f;
            _elapsedPlayTime = 0f;
            _nextShipId = 1;
            ScheduleNextSpawn();
            Debug.Log("[ShipSpawnManager] Reset complete.");
        }

        // ---------------------------------------------------
        //  Public debug helpers
        // ---------------------------------------------------

        /// <summary>Current number of ships alive (any state).</summary>
        public int ActiveShipCount => _activeShips.Count;

        /// <summary>Current number of ships in the idle waiting zone.</summary>
        public int IdleShipCount => _idleShipIds.Count;

        // ---------------------------------------------------
        //  Validation
        // ---------------------------------------------------

        private void ValidateReferences()
        {
            if (_spawnConfig == null)
                Debug.LogError("[ShipSpawnManager] ShipSpawnConfigSO is not assigned.");
            if (_shipConfig == null)
                Debug.LogError("[ShipSpawnManager] ShipConfigSO is not assigned.");
            if (_shipPrefab == null)
                Debug.LogError("[ShipSpawnManager] Ship prefab is not assigned.");
        }

        // ---------------------------------------------------
        //  Gizmos — visualise spawn and idle zones in Scene view
        // ---------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnConfig == null) return;

            // Spawn zone — blue
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.25f);
            Vector3 spawnCenter = new Vector3(
                (_spawnConfig.SpawnZoneXMin + _spawnConfig.SpawnZoneXMax) * 0.5f,
                _spawnConfig.SpawnZoneY, 
                _spawnConfig.SeaLaneZ);
            Vector3 spawnSize = new Vector3(
                _spawnConfig.SpawnZoneXMax - _spawnConfig.SpawnZoneXMin, 
                1f, 
                _spawnConfig.SeaLaneZVariance * 2f);
            Gizmos.DrawCube(spawnCenter, spawnSize);
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
            Gizmos.DrawWireCube(spawnCenter, spawnSize);

            // Idle zone — green
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.25f);
            Vector3 idleCenter = new Vector3(
                (_spawnConfig.IdleZoneXMin + _spawnConfig.IdleZoneXMax) * 0.5f,
                _spawnConfig.SpawnZoneY, 
                _spawnConfig.SeaLaneZ);
            Vector3 idleSize = new Vector3(
                _spawnConfig.IdleZoneXMax - _spawnConfig.IdleZoneXMin, 
                1f, 
                _spawnConfig.IdleZoneZVariance * 2f);
            Gizmos.DrawCube(idleCenter, idleSize);
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(idleCenter, idleSize);

            // Departure point — cyan
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                new Vector3(_spawnConfig.DepartureX, _spawnConfig.SpawnZoneY, 0f), 0.4f);

            // Labels
            UnityEditor.Handles.Label(spawnCenter + Vector3.up * 0.6f, "Spawn zone");
            UnityEditor.Handles.Label(idleCenter + Vector3.up * 0.6f, "Idle zone");
        }
#endif
    }
}