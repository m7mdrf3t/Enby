// ============================================================
//  SocketController.cs
//  Manages a single docking slot on the shore.
//  Owns the dock countdown Timer, triggers cargo delivery,
//  and publishes socket-lifecycle events.
//
//  One SocketController per physical socket in the scene.
//  ShoreManager holds references to all of them.
//
//  Pattern : Controller (drives FSM, owns Timer, publishes events)
// ============================================================

using UnityEngine;
using PetroCitySimulator.Events;
using PetroCitySimulator.Utils;
using PetroCitySimulator.Entities.Ship;

namespace PetroCitySimulator.Entities.Socket
{
    public class SocketController : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Identity")]
        [Tooltip("Unique index for this socket. Set manually; must match ShoreManager's list order.")]
        [SerializeField] private int _socketIndex;

        [Header("Dock Settings")]
        [Tooltip("Seconds ship stays docked. Overridden at runtime by ShipConfigSO.DockDuration.")]
        [SerializeField] private float _defaultDockDuration = 20f;

        [Tooltip("Enable a brief cooldown between ships departing and the socket accepting a new one.")]
        [SerializeField] private bool _enableCooldown = false;

        [Tooltip("Seconds of cooldown after departure (only used if EnableCooldown is true).")]
        [SerializeField, Min(0f)] private float _cooldownDuration = 3f;

        [Header("Visual Feedback")]
        [Tooltip("Renderer that changes colour to reflect socket state (optional).")]
        [SerializeField] private Renderer _statusRenderer;

        [SerializeField] private Color _colorFree = Color.green;
        [SerializeField] private Color _colorIncoming = Color.yellow;
        [SerializeField] private Color _colorOccupied = Color.red;
        [SerializeField] private Color _colorCooldown = Color.grey;

        // ---------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------

        private SocketStateMachine _fsm;
        private Timer _dockTimer;
        private Timer _cooldownTimer;
        private ShipController _dockedShip;
        private float _assignedCargo;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public int SocketIndex => _socketIndex;
        public SocketState CurrentState => _fsm?.Current ?? SocketState.Free;
        public bool IsAvailable => _fsm != null && _fsm.IsAvailable;
        public Vector3 WorldPosition => transform.position;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void Awake()
        {
            _fsm = new SocketStateMachine();
            _fsm.OnStateChanged += HandleFsmStateChanged;

            // Dock timer — duration set when ship arrives
            _dockTimer = new Timer(_defaultDockDuration);
            _dockTimer.OnCompleted += HandleDockTimerComplete;
            _dockTimer.OnTicked += HandleDockTimerTick;

            // Cooldown timer
            _cooldownTimer = new Timer(_cooldownDuration);
            _cooldownTimer.OnCompleted += HandleCooldownComplete;
        }

        private void OnEnable()
        {
            EventBus<OnShipDocked>.Subscribe(HandleShipDocked);
        }

        private void OnDisable()
        {
            EventBus<OnShipDocked>.Unsubscribe(HandleShipDocked);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;

            _dockTimer.Tick(Time.deltaTime);
            _cooldownTimer.Tick(Time.deltaTime);
        }

        // ---------------------------------------------------
        //  Public API — called by ShoreManager
        // ---------------------------------------------------

        /// <summary>
        /// Reserve this socket for an incoming ship.
        /// Called immediately when the player's tap is validated.
        /// </summary>
        public void AssignIncomingShip(ShipController ship)
        {
            if (!_fsm.IsAvailable)
            {
                Debug.LogWarning($"[SocketController {_socketIndex}] AssignIncomingShip called on unavailable socket.");
                return;
            }

            _dockedShip = ship;
            _fsm.TransitionTo(SocketState.Incoming);
        }

        /// <summary>
        /// Release the socket immediately without going through departure.
        /// Used for editor resets or game-over cleanup.
        /// </summary>
        public void ForceReset()
        {
            _dockTimer.Stop();
            _cooldownTimer.Stop();
            _dockedShip = null;
            _assignedCargo = 0f;
            _fsm.Reset();
            UpdateStatusColor();
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        /// <summary>
        /// Fires when any ship finishes moving and snaps to a socket.
        /// We only act if the ship is assigned to our index.
        /// </summary>
        private void HandleShipDocked(OnShipDocked e)
        {
            if (e.SocketIndex != _socketIndex) return;

            _assignedCargo = e.CargoAmount;

            _fsm.TransitionTo(SocketState.Occupied);

            // Start the dock countdown using the ship's configured duration
            _dockTimer.Start(e.DockDuration);

            Debug.Log($"[SocketController {_socketIndex}] Ship {e.ShipId} docked. " +
                      $"Cargo: {e.CargoAmount:F1}. Timer: {e.DockDuration:F0}s.");
        }

        // ---------------------------------------------------
        //  Timer callbacks
        // ---------------------------------------------------

        private void HandleDockTimerTick(float remaining)
        {
            EventBus<OnSocketTimerTick>.Raise(new OnSocketTimerTick
            {
                SocketIndex = _socketIndex,
                SecondsRemaining = remaining
            });
        }

        private void HandleDockTimerComplete()
        {
            if (_dockedShip == null)
            {
                Debug.LogWarning($"[SocketController {_socketIndex}] Timer complete but no docked ship reference.");
                FreeSocket();
                return;
            }

            // 1. Publish cargo delivered — StorageManager will pick this up
            EventBus<OnCargoDelivered>.Raise(new OnCargoDelivered
            {
                ShipId = _dockedShip.ShipId,
                SocketIndex = _socketIndex,
                AmountDelivered = _assignedCargo
            });

            Debug.Log($"[SocketController {_socketIndex}] Cargo delivered: {_assignedCargo:F1} units.");

            // 2. Tell the ship to depart
            _dockedShip.BeginDeparture();
            _dockedShip = null;

            // 3. Transition socket state
            if (_enableCooldown)
            {
                _fsm.TransitionTo(SocketState.Cooldown);
                _cooldownTimer.Start();
            }
            else
            {
                FreeSocket();
            }
        }

        private void HandleCooldownComplete()
        {
            FreeSocket();
        }

        // ---------------------------------------------------
        //  FSM state change handler
        // ---------------------------------------------------

        private void HandleFsmStateChanged(SocketState previous, SocketState next)
        {
            UpdateStatusColor();
            Debug.Log($"[SocketController {_socketIndex}] {previous} → {next}");
        }

        // ---------------------------------------------------
        //  Freeing the socket
        // ---------------------------------------------------

        private void FreeSocket()
        {
            _assignedCargo = 0f;
            _fsm.TransitionTo(SocketState.Free);

            EventBus<OnSocketFreed>.Raise(new OnSocketFreed
            {
                SocketIndex = _socketIndex
            });

            Debug.Log($"[SocketController {_socketIndex}] Socket is now FREE.");
        }

        // ---------------------------------------------------
        //  Visual helpers
        // ---------------------------------------------------

        private void UpdateStatusColor()
        {
            if (_statusRenderer == null) return;

            _statusRenderer.material.color = _fsm.Current switch
            {
                SocketState.Free => _colorFree,
                SocketState.Incoming => _colorIncoming,
                SocketState.Occupied => _colorOccupied,
                SocketState.Cooldown => _colorCooldown,
                _ => Color.white
            };
        }

        // ---------------------------------------------------
        //  Gizmos — draw socket position in Scene view
        // ---------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_fsm == null)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
                return;
            }

            Gizmos.color = IsAvailable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"Socket {_socketIndex}\n{_fsm.Current}");
        }
#endif
    }
}