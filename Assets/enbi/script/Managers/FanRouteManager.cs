using PetroCitySimulator.Entities.Fan;
using System.Collections.Generic;
using UnityEngine;

namespace PetroCitySimulator.Managers
{
    public class FanRouteManager : MonoBehaviour
    {
        public static FanRouteManager Instance { get; private set; }

        [Header("Home Points")]
        [SerializeField] private List<Transform> _homePoints = new List<Transform>();

        [Header("Shared Route Points")]
        [SerializeField] private Transform _storageLoadPoint;
        [SerializeField] private Transform _townUnloadPoint;

        [Header("Waypoints")]
        [SerializeField] private List<Transform> _toStorageWaypoints = new List<Transform>();
        [SerializeField] private List<Transform> _toTownWaypoints = new List<Transform>();
        [SerializeField] private List<Transform> _returnWaypoints = new List<Transform>();

        // Storage queue: fans waiting to load, or actively loading
        private readonly Queue<FanController> _storageQueue = new Queue<FanController>();
        private FanController _fanLoadingAtStorage;

        // Town queue: similar to storage
        private readonly Queue<FanController> _townQueue = new Queue<FanController>();
        private FanController _fanUnloadingAtTown;

        // Home slot tracking: maps each fan to its assigned home point index
        private readonly Dictionary<FanController, int> _fanHomeSlots = new Dictionary<FanController, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            // Debug: Log the route manager state
            Debug.Log($"[FanRouteManager] Initialized:");
            Debug.Log($"  Home Points: {_homePoints.Count} total");
            for (int i = 0; i < _homePoints.Count; i++)
            {
                Debug.Log($"    [{i}] {(_homePoints[i] != null ? _homePoints[i].name : "NULL")}");
            }
            Debug.Log($"  Storage Load Point: {(_storageLoadPoint != null ? _storageLoadPoint.name : "NOT ASSIGNED")}");
            Debug.Log($"  Town Unload Point: {(_townUnloadPoint != null ? _townUnloadPoint.name : "NOT ASSIGNED")}");
            Debug.Log($"  To Storage Waypoints: {_toStorageWaypoints.Count}");
            Debug.Log($"  To Town Waypoints: {_toTownWaypoints.Count}");
            Debug.Log($"  Return Waypoints: {_returnWaypoints.Count}");
            Debug.Log($"  Queue-based routing enabled: Multiple fans can move concurrently");
            Debug.Log($"  HasAvailableHomePoint: {HasAvailableHomePoint}");
        }

        public bool TryRegisterFan(FanController fan, out Vector3 homePoint)
        {
            homePoint = Vector3.zero;

            if (fan == null) return false;
            if (_fanHomeSlots.ContainsKey(fan))
            {
                int homeIndex = _fanHomeSlots[fan];
                if (homeIndex < 0 || homeIndex >= _homePoints.Count || _homePoints[homeIndex] == null)
                    return false;

                homePoint = _homePoints[homeIndex].position;
                return true;
            }

            for (int i = 0; i < _homePoints.Count; i++)
            {
                if (_homePoints[i] == null) continue;
                if (_fanHomeSlots.ContainsValue(i)) continue;

                _fanHomeSlots[fan] = i;
                homePoint = _homePoints[i].position;
                return true;
            }

            return false;
        }

        public bool TryBeginRoute(
            FanController fan,
            out Vector3[] toStoragePath,
            out Vector3[] toTownPath,
            out Vector3[] returnPath)
        {
            toStoragePath = System.Array.Empty<Vector3>();
            toTownPath = System.Array.Empty<Vector3>();
            returnPath = System.Array.Empty<Vector3>();

            if (fan == null || _storageLoadPoint == null || _townUnloadPoint == null)
                return false;

            if (!_fanHomeSlots.ContainsKey(fan))
                return false;

            // Multiple fans allowed — just provide the paths
            toStoragePath = BuildPath(_toStorageWaypoints, _storageLoadPoint.position);
            toTownPath = BuildPath(_toTownWaypoints, _townUnloadPoint.position);
            returnPath = BuildPath(_returnWaypoints);
            
            // Add to storage queue when beginning route
            if (!_storageQueue.Contains(fan))
                _storageQueue.Enqueue(fan);
            
            return true;
        }

        public void EndRoute(FanController fan)
        {
            // Remove from storage queue if still in it
            if (_storageQueue.Count > 0 && _storageQueue.Peek() == fan)
            {
                _storageQueue.Dequeue();
                if (_fanLoadingAtStorage == fan)
                    _fanLoadingAtStorage = null;
            }
            
            // Remove from town queue if still in it
            if (_townQueue.Count > 0 && _townQueue.Peek() == fan)
            {
                _townQueue.Dequeue();
                if (_fanUnloadingAtTown == fan)
                    _fanUnloadingAtTown = null;
            }
        }

        public void ReleaseFan(FanController fan)
        {
            EndRoute(fan);
            _fanHomeSlots.Remove(fan);
        }

        /// <summary>
        /// Check if this fan is next in line to load at storage.
        /// Called by FanController to know when to start loading.
        /// </summary>
        public bool IsNextInStorageQueue(FanController fan)
        {
            return _storageQueue.Count > 0 && _storageQueue.Peek() == fan && _fanLoadingAtStorage == null;
        }

        /// <summary>
        /// Called by FanController when it reaches storage and is ready to load.
        /// Returns true if this fan can load now (not blocked by another fan).
        /// </summary>
        public bool TryBeginStorageLoad(FanController fan)
        {
            if (!IsNextInStorageQueue(fan))
                return false;

            _fanLoadingAtStorage = fan;
            return true;
        }

        /// <summary>
        /// Called by FanController when load completes.
        /// </summary>
        public void EndStorageLoad(FanController fan)
        {
            if (_fanLoadingAtStorage == fan)
            {
                _fanLoadingAtStorage = null;
                _storageQueue.Dequeue(); // Remove from queue
            }
        }

        /// <summary>
        /// Called by FanController after finishing storage load.
        /// Adds the fan to the town queue so it can unload when it arrives.
        /// </summary>
        public void EnqueueForTownUnload(FanController fan)
        {
            if (!_townQueue.Contains(fan))
            {
                _townQueue.Enqueue(fan);
                Debug.Log($"[FanRouteManager] Fan {fan.FanId} added to town queue. Queue size: {_townQueue.Count}");
            }
        }

        /// <summary>
        /// Check if this fan is next in line to unload at town.
        /// </summary>
        public bool IsNextInTownQueue(FanController fan)
        {
            return _townQueue.Count > 0 && _townQueue.Peek() == fan && _fanUnloadingAtTown == null;
        }

        /// <summary>
        /// Called by FanController when it reaches town and is ready to unload.
        /// </summary>
        public bool TryBeginTownUnload(FanController fan)
        {
            if (!IsNextInTownQueue(fan))
                return false;

            _fanUnloadingAtTown = fan;
            return true;
        }

        /// <summary>
        /// Called by FanController when unload completes.
        /// </summary>
        public void EndTownUnload(FanController fan)
        {
            if (_fanUnloadingAtTown == fan)
            {
                _fanUnloadingAtTown = null;
                if (_townQueue.Count > 0 && _townQueue.Peek() == fan)
                    _townQueue.Dequeue();
            }
        }

        public bool HasAvailableHomePoint => _fanHomeSlots.Count < _homePoints.Count;

        private static Vector3[] BuildPath(List<Transform> waypoints, params Vector3[] finalPoints)
        {
            var points = new List<Vector3>(waypoints.Count + finalPoints.Length);

            foreach (var waypoint in waypoints)
            {
                if (waypoint != null)
                    points.Add(waypoint.position);
            }

            for (int i = 0; i < finalPoints.Length; i++)
                points.Add(finalPoints[i]);

            return points.ToArray();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawPath(_homePoints, Color.white, "Home");
            DrawPath(_toStorageWaypoints, new Color(0.3f, 0.8f, 1f), "To Storage");
            DrawPath(_toTownWaypoints, new Color(1f, 0.7f, 0.2f), "To Town");
            DrawPath(_returnWaypoints, new Color(0.8f, 0.4f, 1f), "Return");

            if (_storageLoadPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_storageLoadPoint.position, 0.2f);
            }

            if (_townUnloadPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_townUnloadPoint.position, 0.2f);
            }
        }

        private static void DrawPath(List<Transform> waypoints, Color color, string label)
        {
            Gizmos.color = color;

            for (int i = 0; i < waypoints.Count; i++)
            {
                var waypoint = waypoints[i];
                if (waypoint == null) continue;

                Gizmos.DrawWireSphere(waypoint.position, 0.18f);
                UnityEditor.Handles.Label(waypoint.position + Vector3.up * 0.25f, $"{label} {i}");

                if (i + 1 < waypoints.Count && waypoints[i + 1] != null)
                    Gizmos.DrawLine(waypoint.position, waypoints[i + 1].position);
            }
        }
#endif
    }
}