using System.Collections.Generic;
using PetroCitySimulator.Data;
using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class FanSpawnManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private FanConfigSO _fanConfig;

        [Header("Prefab")]
        [SerializeField] private FanController _fanPrefab;
        [SerializeField] private Transform _poolParent;

        private ObjectPool<FanController> _pool;
        private readonly Dictionary<int, FanController> _activeFans = new Dictionary<int, FanController>();

        private float _spawnTimer;
        private float _nextSpawnIn;
        private int _nextFanId = 1;
        private bool _spawnPaused = true;

        private void Awake()
        {
            ValidateReferences();

            if (_fanConfig == null || _fanPrefab == null)
                return;

            _pool = new ObjectPool<FanController>(
                _fanPrefab,
                _poolParent,
                _fanConfig.PoolInitialSize,
                _fanConfig.PoolMaxSize);
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Start()
        {
            _nextSpawnIn = _fanConfig != null ? _fanConfig.InitialSpawnDelay : 0f;
            Debug.Log($"[FanSpawnManager] Started. InitialSpawnDelay: {_nextSpawnIn}s. _spawnPaused: {_spawnPaused}. Pool: {(_pool != null ? "Ready" : "NULL")}");
        }

        private void Update()
        {
            if (_spawnPaused)
            {
                return;
            }

            if (_pool == null)
            {
                Debug.LogWarning("[FanSpawnManager] Pool is null - cannot spawn.");
                return;
            }

            if (FanRouteManager.Instance == null)
            {
                Debug.LogWarning("[FanSpawnManager] FanRouteManager.Instance is null - cannot spawn.");
                return;
            }

            if (!Core.GameManager.Instance.IsPlaying)
            {
                return;
            }

            if (_activeFans.Count >= _fanConfig.MaxActiveFans)
            {
                Debug.Log($"[FanSpawnManager] Max active fans reached ({_activeFans.Count}/{_fanConfig.MaxActiveFans})");
                return;
            }

            if (!FanRouteManager.Instance.HasAvailableHomePoint)
            {
                Debug.Log($"[FanSpawnManager] No available home points for fan spawn. Active fans: {_activeFans.Count}");
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _nextSpawnIn)
            {
                return;
            }

            _spawnTimer = 0f;
            _nextSpawnIn = _fanConfig.SpawnInterval;
            TrySpawnFan();
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
        }

        private void TrySpawnFan()
        {
            FanController fan = _pool.Get();
            if (fan == null)
            {
                Debug.LogWarning("[FanSpawnManager] Pool exhausted - spawn skipped.");
                return;
            }

            if (!FanRouteManager.Instance.TryRegisterFan(fan, out Vector3 homePoint))
            {
                _pool.Return(fan);
                Debug.Log("[FanSpawnManager] No free home points for fan spawn.");
                return;
            }

            int fanId = _nextFanId++;
            fan.Initialise(fanId, homePoint, _fanConfig);
            _activeFans[fanId] = fan;

            Debug.Log($"[FanSpawnManager] Spawned fan vehicle {fanId}. Active fans: {_activeFans.Count}/{_fanConfig.MaxActiveFans}");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            switch (e.NewState)
            {
                case GameState.Playing:
                    _spawnPaused = false;
                    Debug.Log("[FanSpawnManager] Game started - spawning enabled.");
                    break;

                case GameState.Paused:
                    _spawnPaused = true;
                    Debug.Log("[FanSpawnManager] Game paused - spawning paused.");
                    break;

                case GameState.GameOver:
                    _spawnPaused = true;
                    Debug.Log("[FanSpawnManager] Game over - spawning paused and resetting.");
                    ResetAll();
                    break;

                case GameState.MainMenu:
                    _spawnPaused = true;
                    Debug.Log("[FanSpawnManager] Main menu - spawning paused.");
                    break;
            }
        }

        private void ResetAll()
        {
            foreach (var fan in _activeFans.Values)
            {
                fan.ReleaseHomeSlot();
                _pool.Return(fan);
            }

            _activeFans.Clear();
            _spawnTimer = 0f;
            _nextSpawnIn = _fanConfig.InitialSpawnDelay;
            _nextFanId = 1;
        }

        private void ValidateReferences()
        {
            if (_fanConfig == null)
                Debug.LogError("[FanSpawnManager] FanConfigSO is not assigned.");
            if (_fanPrefab == null)
                Debug.LogError("[FanSpawnManager] Fan prefab is not assigned.");
        }
    }
}