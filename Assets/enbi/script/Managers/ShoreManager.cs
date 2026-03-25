// ============================================================
//  ShoreManager.cs
//  Owns all SocketControllers on the shore.
//  Listens for OnShipTapped → validates → assigns ship to socket.
//  Listens for OnShipDespawned to clean up any stale references.
//
//  This is the single point of truth for "can a ship dock right now?"
//  Neither ShipController nor SocketController make that decision.
//
//  Pattern : Manager (orchestration, validation, routing)
//            Observer (OnShipTapped, OnShipDespawned)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using PetroCitySimulator.Events;
using PetroCitySimulator.Entities.Ship;
using PetroCitySimulator.Entities.Socket;

namespace PetroCitySimulator.Managers
{
    public class ShoreManager : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Sockets")]
        [Tooltip("All SocketControllers in the scene, in order. Drag them in here.")]
        [SerializeField] private List<SocketController> _sockets = new List<SocketController>();

        [Header("Departure")]
        [Tooltip("World-space position ships sail toward after departing (off-screen).")]
        [SerializeField] private Transform _departurePoint;

        // ---------------------------------------------------
        //  Runtime tracking
        // ---------------------------------------------------

        /// <summary>Maps ShipId → the socket index it is heading toward or docked at.</summary>
        private readonly Dictionary<int, int> _shipToSocket = new Dictionary<int, int>();

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void OnEnable()
        {
            EventBus<OnShipTapped>.Subscribe(HandleShipTapped);
            EventBus<OnShipDespawned>.Subscribe(HandleShipDespawned);
            EventBus<OnSocketFreed>.Subscribe(HandleSocketFreed);
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnShipTapped>.Unsubscribe(HandleShipTapped);
            EventBus<OnShipDespawned>.Unsubscribe(HandleShipDespawned);
            EventBus<OnSocketFreed>.Unsubscribe(HandleSocketFreed);
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        // ---------------------------------------------------
        //  Public read-only helpers
        // ---------------------------------------------------

        /// <summary>Returns true if at least one socket is available.</summary>
        public bool HasAvailableSocket => GetFirstAvailableSocket() != null;

        /// <summary>Returns the world position of the first available socket, or Vector3.zero.</summary>
        public Vector3 GetAvailableSocketPosition()
        {
            var socket = GetFirstAvailableSocket();
            return socket != null ? socket.WorldPosition : Vector3.zero;
        }

        // ---------------------------------------------------
        //  Core event handlers
        // ---------------------------------------------------

        private void HandleShipTapped(OnShipTapped e)
        {
            if (!Core.GameManager.Instance.IsPlaying) return;

            // Guard: is this ship already assigned?
            if (_shipToSocket.ContainsKey(e.ShipId))
            {
                Debug.Log($"[ShoreManager] Ship {e.ShipId} already has a socket assignment — tap ignored.");
                return;
            }

            // Find a free socket
            var socket = GetFirstAvailableSocket();
            if (socket == null)
            {
                Debug.Log("[ShoreManager] All sockets busy — tap ignored.");
                // TODO: Play a "busy" feedback sound/animation here
                return;
            }

            // Lock the socket immediately so no other tap can steal it
            socket.AssignIncomingShip(e.ShipController);
            _shipToSocket[e.ShipId] = socket.SocketIndex;

            // Tell the ship to start moving
            e.ShipController.BeginDocking(socket.SocketIndex, socket.WorldPosition);

            Debug.Log($"[ShoreManager] Ship {e.ShipId} → Socket {socket.SocketIndex}.");
        }

        private void HandleShipDespawned(OnShipDespawned e)
        {
            if (_shipToSocket.ContainsKey(e.ShipId))
                _shipToSocket.Remove(e.ShipId);
        }

        private void HandleSocketFreed(OnSocketFreed e)
        {
            // Nothing to do at manager level for now —
            // SpawnManager listens to this if it wants to pace spawning
            // based on socket availability.
            Debug.Log($"[ShoreManager] Socket {e.SocketIndex} freed.");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (e.NewState == GameState.GameOver)
                ResetAll();
        }

        // ---------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------

        /// <summary>
        /// Returns the first socket in _sockets whose FSM is Free.
        /// Iterates in order — socket 0 is always preferred.
        /// Change to a priority queue if you want smarter routing.
        /// </summary>
        private SocketController GetFirstAvailableSocket()
        {
            foreach (var socket in _sockets)
            {
                if (socket != null && socket.IsAvailable)
                    return socket;
            }
            return null;
        }

        /// <summary>
        /// Force-reset all sockets. Used on game over or scene reload.
        /// </summary>
        private void ResetAll()
        {
            foreach (var socket in _sockets)
                socket?.ForceReset();

            _shipToSocket.Clear();
            Debug.Log("[ShoreManager] All sockets reset.");
        }

        // ---------------------------------------------------
        //  Gizmos — draw departure point in Scene view
        // ---------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_departurePoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_departurePoint.position, 0.5f);
            UnityEditor.Handles.Label(
                _departurePoint.position + Vector3.up * 0.7f,
                "Departure point");
        }
#endif
    }
}