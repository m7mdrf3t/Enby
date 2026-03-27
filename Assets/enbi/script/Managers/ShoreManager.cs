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
using PetroCitySimulator.Data;
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

        [Header("Config")]
        [SerializeField] private EconomyConfigSO _economyConfig;

        [Header("References")]
        [Tooltip("ShipSpawnManager for querying idle export ships.")]
        [SerializeField] private ShipSpawnManager _shipSpawnManager;

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

        /// <summary>Returns true if at least one compatible socket is available for this cargo type.</summary>
        public bool HasAvailableSocketFor(ShipCargoType cargoType) =>
            GetFirstAvailableSocketFor(cargoType) != null;

        /// <summary>Returns the world position of the first available socket, or Vector3.zero.</summary>
        public Vector3 GetAvailableSocketPosition()
        {
            var socket = GetFirstAvailableSocket();
            return socket != null ? socket.WorldPosition : Vector3.zero;
        }

        public Vector3 GetAvailableSocketPosition(ShipCargoType cargoType)
        {
            var socket = GetFirstAvailableSocketFor(cargoType);
            return socket != null ? socket.WorldPosition : Vector3.zero;
        }

        // ---------------------------------------------------
        //  Core event handlers
        // ---------------------------------------------------

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;
            TryAutoDockExportShips();
        }

        private void HandleShipTapped(OnShipTapped e)
        {
            if (!Core.GameManager.Instance.IsPlaying) return;

            // Export ships dock automatically — ignore player taps
            if (e.ShipController.CargoType == ShipCargoType.ExportProducts)
            {
                Debug.Log($"[ShoreManager] Ship {e.ShipId} is export — tap ignored (auto-docks).");
                return;
            }

            // Guard: is this ship already assigned?
            if (_shipToSocket.ContainsKey(e.ShipId))
            {
                Debug.Log($"[ShoreManager] Ship {e.ShipId} already has a socket assignment — tap ignored.");
                return;
            }

            // Money gate for import ships
            float cost = _economyConfig != null ? _economyConfig.DockingCostPerShip : 0f;
            if (cost > 0f && MoneyManager.Instance != null && !MoneyManager.Instance.CanAfford(cost))
            {
                Debug.Log($"[ShoreManager] Not enough money to dock ship {e.ShipId} (need {cost:F1}, have {MoneyManager.Instance.CurrentMoney:F1}).");
                return;
            }

            // Find a compatible free socket
            var socket = GetFirstAvailableSocketFor(e.ShipController.CargoType);
            if (socket == null)
            {
                Debug.Log($"[ShoreManager] No compatible free socket for ship {e.ShipId} ({e.ShipController.CargoType}) — tap ignored.");
                return;
            }

            // Deduct money
            if (cost > 0f && MoneyManager.Instance != null)
                MoneyManager.Instance.SpendMoney(cost, "ShipDocking");

            // Lock the socket immediately so no other tap can steal it
            socket.AssignIncomingShip(e.ShipController);
            _shipToSocket[e.ShipId] = socket.SocketIndex;

            // Tell the ship to start moving
            e.ShipController.BeginDocking(socket.SocketIndex, socket.WorldPosition);

            Debug.Log($"[ShoreManager] Ship {e.ShipId} → Socket {socket.SocketIndex} (cost: {cost:F1}).");
        }

        /// <summary>
        /// Every frame, try to auto-assign idle export ships to free sockets.
        /// </summary>
        private void TryAutoDockExportShips()
        {
            if (_shipSpawnManager == null) return;

            // Only dock export ships when there are products to collect.
            if (ProductStorageManager.Instance == null || ProductStorageManager.Instance.CurrentAmount <= 0f)
                return;

            var idleExports = _shipSpawnManager.GetIdleExportShips();
            if (idleExports == null || idleExports.Count == 0) return;

            for (int i = idleExports.Count - 1; i >= 0; i--)
            {
                var ship = idleExports[i];
                if (ship == null || _shipToSocket.ContainsKey(ship.ShipId)) continue;

                var socket = GetFirstAvailableSocketFor(ShipCargoType.ExportProducts);
                if (socket == null) break; // no free export sockets left

                socket.AssignIncomingShip(ship);
                _shipToSocket[ship.ShipId] = socket.SocketIndex;
                ship.BeginDocking(socket.SocketIndex, socket.WorldPosition);

                Debug.Log($"[ShoreManager] Auto-docking export ship {ship.ShipId} → Socket {socket.SocketIndex}.");
            }
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

        private SocketController GetFirstAvailableSocketFor(ShipCargoType cargoType)
        {
            foreach (var socket in _sockets)
            {
                if (socket != null && socket.IsAvailable && socket.CanAcceptCargoType(cargoType))
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