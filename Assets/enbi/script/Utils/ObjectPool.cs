// ============================================================
//  ObjectPool.cs
//  Generic, non-MonoBehaviour object pool for Unity GameObjects.
//  Avoids Instantiate/Destroy GC spikes for frequently spawned
//  objects (ships, particles, UI popups).
//
//  Usage:
//    var pool = new ObjectPool<ShipController>(prefab, parent, 10);
//    ShipController ship = pool.Get();   // activate from pool
//    pool.Return(ship);                  // deactivate back to pool
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace PetroCitySimulator.Utils
{
    public class ObjectPool<T> where T : Component
    {
        // ---------------------------------------------------
        //  State
        // ---------------------------------------------------

        private readonly T _prefab;
        private readonly Transform _poolParent;
        private readonly Stack<T> _available;
        private readonly HashSet<T> _inUse;
        private readonly int _maxSize;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;
        public int TotalCount => _available.Count + _inUse.Count;

        // ---------------------------------------------------
        //  Constructor
        // ---------------------------------------------------

        /// <param name="prefab">
        ///   The prefab Component to instantiate.
        ///   Must be on a GameObject with the component attached at root.
        /// </param>
        /// <param name="poolParent">
        ///   Transform used as parent for all pooled instances.
        ///   Pass null to use the scene root.
        /// </param>
        /// <param name="initialSize">
        ///   How many instances to pre-warm at construction time.
        /// </param>
        /// <param name="maxSize">
        ///   Hard cap on pool size. Get() returns null if pool is
        ///   exhausted and maxSize is reached. Pass 0 for unlimited.
        /// </param>
        public ObjectPool(T prefab, Transform poolParent, int initialSize = 5, int maxSize = 0)
        {
            _prefab = prefab;
            _poolParent = poolParent;
            _maxSize = maxSize;
            _available = new Stack<T>(Mathf.Max(initialSize, 4));
            _inUse = new HashSet<T>();

            Prewarm(initialSize);
        }

        // ---------------------------------------------------
        //  Public API
        // ---------------------------------------------------

        /// <summary>
        /// Retrieve an instance from the pool, activating its GameObject.
        /// Returns null if the pool is at max capacity.
        /// </summary>
        public T Get()
        {
            T instance;

            if (_available.Count > 0)
            {
                instance = _available.Pop();
            }
            else
            {
                if (_maxSize > 0 && TotalCount >= _maxSize)
                {
                    Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Pool exhausted (max {_maxSize}). Returning null.");
                    return null;
                }

                instance = CreateInstance();
            }

            instance.gameObject.SetActive(true);
            _inUse.Add(instance);
            return instance;
        }

        /// <summary>
        /// Return an instance to the pool, deactivating its GameObject.
        /// Safe to call with null — does nothing.
        /// </summary>
        public void Return(T instance)
        {
            if (instance == null) return;

            if (!_inUse.Contains(instance))
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Attempted to return an object not owned by this pool.");
                return;
            }

            _inUse.Remove(instance);
            instance.gameObject.SetActive(false);

            if (_poolParent != null)
                instance.transform.SetParent(_poolParent);

            _available.Push(instance);
        }

        /// <summary>
        /// Return ALL in-use instances to the pool at once.
        /// Useful for scene reset / game restart.
        /// </summary>
        public void ReturnAll()
        {
            // Copy to a temporary list — can't modify HashSet while iterating
            var toReturn = new List<T>(_inUse);
            foreach (var instance in toReturn)
                Return(instance);
        }

        /// <summary>
        /// Destroy all pooled GameObjects and clear the pool.
        /// Call this when the pool owner is destroyed.
        /// </summary>
        public void Dispose()
        {
            ReturnAll();

            while (_available.Count > 0)
            {
                var instance = _available.Pop();
                if (instance != null)
                    Object.Destroy(instance.gameObject);
            }

            _inUse.Clear();
        }

        // ---------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _available.Push(instance);
            }
        }

        private T CreateInstance()
        {
            var go = Object.Instantiate(_prefab.gameObject, _poolParent);
            go.SetActive(false);

            var component = go.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"[ObjectPool<{typeof(T).Name}>] Prefab '{_prefab.name}' does not have a {typeof(T).Name} component.");
            }

            return component;
        }
    }
}