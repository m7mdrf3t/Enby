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

        [Header("Route Points")]
        [Tooltip("Where fans pick up products (at the factory output).")]
        [SerializeField] private Transform _factoryLoadPoint;
        [Tooltip("Intermediate points the fan travels through (in order) between factory and storage.")]
        [SerializeField] private List<Transform> _routeWaypoints = new List<Transform>();
        [Tooltip("Where fans deliver products (at the product storage).")]
        [SerializeField] private Transform _productStoragePoint;

        // Storage queue: fans waiting to load, or actively loading
        private readonly Queue<FanController> _storageQueue = new Queue<FanController>();
        private FanController _fanLoadingAtStorage;

        // Delivery queue: fans waiting to unload at product storage
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
            Debug.Log($"  Pickup Point: {(_factoryLoadPoint != null ? _factoryLoadPoint.name : "NOT ASSIGNED")}");
            Debug.Log($"  Dropoff Point: {(_productStoragePoint != null ? _productStoragePoint.name : "NOT ASSIGNED")}");
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
            out Vector3[] toPickupPath,
            out Vector3[] toDropoffPath,
            out Vector3[] returnPath)
        {
            toPickupPath = System.Array.Empty<Vector3>();
            toDropoffPath = System.Array.Empty<Vector3>();
            returnPath = System.Array.Empty<Vector3>();

            if (fan == null || _factoryLoadPoint == null || _productStoragePoint == null)
                return false;

            if (!_fanHomeSlots.ContainsKey(fan))
                return false;

            // Build forward path: factory → waypoints → product storage
            var forward = new System.Collections.Generic.List<Vector3>();
            forward.Add(_factoryLoadPoint.position);
            foreach (var wp in _routeWaypoints)
                if (wp != null) forward.Add(wp.position);
            forward.Add(_productStoragePoint.position);

            toPickupPath  = new[] { _factoryLoadPoint.position };
            toDropoffPath = forward.ToArray();
            returnPath    = System.Array.Empty<Vector3>();

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
                Debug.Log($"[FanRouteManager] Fan {fan.FanId} added to delivery queue. Queue size: {_townQueue.Count}");
            }
        }

        public bool IsNextInTownQueue(FanController fan)
        {
            return _townQueue.Count > 0 && _townQueue.Peek() == fan && _fanUnloadingAtTown == null;
        }

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Home points
            Gizmos.color = Color.white;
            foreach (var hp in _homePoints)
            {
                if (hp != null)
                    Gizmos.DrawWireSphere(hp.position, 0.18f);
            }

            if (_factoryLoadPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_factoryLoadPoint.position, 0.2f);
                UnityEditor.Handles.Label(_factoryLoadPoint.position + Vector3.up * 0.3f, "Factory Load");
            }

            // In-between waypoints
            Gizmos.color = new Color(0.6f, 1f, 0.6f);
            Vector3 prev = _factoryLoadPoint != null ? _factoryLoadPoint.position : Vector3.zero;
            foreach (var wp in _routeWaypoints)
            {
                if (wp == null) continue;
                Gizmos.DrawWireSphere(wp.position, 0.15f);
                Gizmos.DrawLine(prev, wp.position);
                prev = wp.position;
            }

            if (_productStoragePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_productStoragePoint.position, 0.2f);
                UnityEditor.Handles.Label(_productStoragePoint.position + Vector3.up * 0.3f, "Product Storage");
                if (_routeWaypoints.Count > 0)
                    Gizmos.DrawLine(prev, _productStoragePoint.position);
            }
        }
#endif
    }
}