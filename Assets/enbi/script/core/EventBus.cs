// ============================================================
//  EventBus.cs
//  A lightweight, type-safe, static publish / subscribe bus.
//
//  Pattern  : Observer (decoupled via generic type key)
//  Lifetime : Static — survives scene loads. Call ClearAll()
//             in GameManager.OnDestroy if you need a clean slate.
//
//  Usage:
//    Subscribe   →  EventBus<OnShipDocked>.Subscribe(OnShipDockedHandler);
//    Unsubscribe →  EventBus<OnShipDocked>.Unsubscribe(OnShipDockedHandler);
//    Raise       →  EventBus<OnShipDocked>.Raise(new OnShipDocked { ... });
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetroCitySimulator.Events
{
    /// <summary>
    /// Static, generic event bus.
    /// Each unique event struct type T gets its own isolated
    /// subscriber list — no string keys, no boxing, no casting.
    /// </summary>
    public static class EventBus<T> where T : struct
    {
        // ---------------------------------------------------
        //  Private state
        // ---------------------------------------------------

        /// <summary>
        /// All active subscribers for event type T.
        /// Using a List rather than a delegate chain so we can
        /// safely iterate and remove during dispatch.
        /// </summary>
        private static readonly List<Action<T>> _subscribers
            = new List<Action<T>>();

        /// <summary>
        /// Guards against subscribe/unsubscribe calls that arrive
        /// while Raise() is currently iterating _subscribers.
        /// </summary>
        private static bool _isRaising = false;

        /// <summary>
        /// Deferred removals that arrived mid-raise.
        /// Applied after the current Raise() loop finishes.
        /// </summary>
        private static readonly List<Action<T>> _pendingRemove
            = new List<Action<T>>();

        // ---------------------------------------------------
        //  Public API
        // ---------------------------------------------------

        /// <summary>
        /// Register a callback to be invoked whenever event T is raised.
        /// Safe to call from Awake, OnEnable, or Start.
        /// </summary>
        public static void Subscribe(Action<T> callback)
        {
            if (callback == null)
            {
                Debug.LogWarning($"[EventBus<{typeof(T).Name}>] Subscribe called with null callback.");
                return;
            }

            if (!_subscribers.Contains(callback))
            {
                _subscribers.Add(callback);
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning($"[EventBus<{typeof(T).Name}>] Duplicate Subscribe ignored for {callback.Method.Name}.");
            }
#endif
        }

        /// <summary>
        /// Remove a previously registered callback.
        /// Safe to call from OnDisable or OnDestroy.
        /// If called during a Raise(), the removal is deferred
        /// until the current dispatch loop finishes.
        /// </summary>
        public static void Unsubscribe(Action<T> callback)
        {
            if (callback == null) return;

            if (_isRaising)
            {
                // Defer: mark for removal after the loop
                if (!_pendingRemove.Contains(callback))
                    _pendingRemove.Add(callback);
            }
            else
            {
                _subscribers.Remove(callback);
            }
        }

        /// <summary>
        /// Publish event T to all current subscribers.
        /// Subscribers are called in the order they were registered.
        /// Exceptions in one subscriber are caught and logged so they
        /// do not prevent other subscribers from receiving the event.
        /// </summary>
        public static void Raise(T eventData)
        {
            _isRaising = true;

            for (int i = 0; i < _subscribers.Count; i++)
            {
                // Skip subscribers that were deferred-removed mid-loop
                if (_pendingRemove.Count > 0 && _pendingRemove.Contains(_subscribers[i]))
                    continue;

                try
                {
                    _subscribers[i]?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[EventBus<{typeof(T).Name}>] Exception in subscriber " +
                        $"'{_subscribers[i]?.Method?.Name}': {ex}");
                }
            }

            _isRaising = false;

            // Apply any deferred removals
            if (_pendingRemove.Count > 0)
            {
                foreach (var toRemove in _pendingRemove)
                    _subscribers.Remove(toRemove);

                _pendingRemove.Clear();
            }
        }

        /// <summary>
        /// Remove ALL subscribers for this event type.
        /// Useful for scene transitions where all listeners are destroyed.
        /// </summary>
        public static void Clear()
        {
            _subscribers.Clear();
            _pendingRemove.Clear();
            _isRaising = false;
        }

        /// <summary>
        /// Returns how many subscribers are currently registered.
        /// Useful for debugging / unit tests.
        /// </summary>
        public static int SubscriberCount => _subscribers.Count;
    }


    // ============================================================
    //  EventBusUtil  —  convenience helper for clearing ALL buses
    // ============================================================

    /// <summary>
    /// Non-generic utility class.
    /// Tracks every bus type that has been used so GameManager can
    /// call EventBusUtil.ClearAll() on scene teardown without
    /// knowing the full list of event types at compile time.
    /// </summary>
    public static class EventBusUtil
    {
        // Registry of Clear() delegates, one per event type
        private static readonly List<Action> _clearActions = new List<Action>();

        /// <summary>
        /// Called automatically by EventBus<T> the first time it is
        /// accessed for a given T.  You never need to call this manually.
        /// </summary>
        internal static void Register(Action clearAction)
        {
            if (!_clearActions.Contains(clearAction))
                _clearActions.Add(clearAction);
        }

        /// <summary>
        /// Clears every registered EventBus<T>.
        /// Call this from GameManager.OnDestroy or on scene change.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var clear in _clearActions)
                clear?.Invoke();

            Debug.Log($"[EventBusUtil] Cleared {_clearActions.Count} event buses.");
        }
    }
}