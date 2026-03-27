// ============================================================
//  ShipController.cs
//  MonoBehaviour that owns a single ship GameObject.
//  Drives ShipStateMachine transitions, movement, bobbing,
//  player tap interaction, and EventBus publishing.
//
//  Lifecycle managed by ShipSpawnManager (object pool owner).
//  Call Initialise() after taking from pool, never in Awake.
//
//  Pattern : Controller (drives FSM + publishes events)
//            Component (thin Unity glue on top of pure logic)
// ============================================================

using UnityEngine;
using PetroCitySimulator.Data;
using PetroCitySimulator.Events;
using TMPro;

namespace PetroCitySimulator.Entities.Ship
{
    [RequireComponent(typeof(BoxCollider))]
    public class ShipController : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("References")]
        [Tooltip("Visual root to apply bob animation to (can be a child transform).")]
        [SerializeField] private Transform _visualRoot;

        [Tooltip("Optional renderer used to tint ship role (import/export).")]
        [SerializeField] private Renderer _roleRenderer;

        [Tooltip("Optional world-space label that shows IMPORT / EXPORT.")]
        [SerializeField] private TMP_Text _roleLabel;

        [Header("Role Colours")]
        [SerializeField] private Color _importColor = new Color(0.35f, 0.75f, 1f);
        [SerializeField] private Color _exportColor = new Color(1f, 0.65f, 0.25f);
        [SerializeField] private Color _importLabelColor = new Color(0.65f, 0.9f, 1f);
        [SerializeField] private Color _exportLabelColor = new Color(1f, 0.85f, 0.45f);

        [Header("Config — assign ShipConfigSO asset")]
        [SerializeField] private ShipConfigSO _config;

        [Header("Docking Rotation")]
        [Tooltip("How quickly the ship rotates to match socket heading while docking (deg/sec).")]
        [SerializeField, Min(0f)] private float _dockRotationSpeed = 220f;

        [Header("Departure Visibility")]
        [Tooltip("Minimum seconds the ship stays visible after departure begins.")]
        [SerializeField, Min(0f)] private float _minDepartureVisibleTime = 1.5f;

        [Tooltip("Minimum distance from departure start before ship can be despawned.")]
        [SerializeField, Min(0f)] private float _minDepartureVisibleDistance = 6f;

        // ---------------------------------------------------
        //  Runtime data (set by ShipSpawnManager on Get())
        // ---------------------------------------------------

        public int ShipId { get; private set; }
        public float CargoAmount { get; private set; }
        public ShipCargoType CargoType { get; private set; } = ShipCargoType.ImportGas;

        private int _assignedSocketIndex = -1;
        private Vector3 _socketWorldPosition;
        private Quaternion _socketWorldRotation;
        private Vector3 _departureTarget;
        private Vector3 _departureStartPosition;
        private float _departureElapsed;
        private Vector3 _idleAnchor;          // world position to bob around

        // ---------------------------------------------------
        //  State machine
        // ---------------------------------------------------

        private ShipStateMachine _fsm;
        public ShipState CurrentState => _fsm.Current;

        // ---------------------------------------------------
        //  Internal
        // ---------------------------------------------------

        private float _bobTimer;
        private float _originalVisualY;   // local Y of _visualRoot at rest
        private Camera _mainCamera;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void Awake()
        {
            _fsm = new ShipStateMachine();
            _fsm.OnStateChanged += HandleStateChanged;

            if (_visualRoot == null)
                _visualRoot = transform;

            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        private void Update()
        {
            if (!Core.GameManager.Instance.IsPlaying) return;

            switch (_fsm.Current)
            {
                case ShipState.Spawning: UpdateSpawning(); break;
                case ShipState.Idle: UpdateIdle(); break;
                case ShipState.Docking: UpdateDocking(); break;
                case ShipState.Departing: UpdateDeparting(); break;
                    // Docked and Despawned have no per-frame logic here
            }

                    UpdateRoleLabelFacing();
        }

        // ---------------------------------------------------
        //  Public initialisation (called by SpawnManager)
        // ---------------------------------------------------

        /// <summary>
        /// Fully reset and configure this ship for a new spawn.
        /// Must be called immediately after pool.Get().
        /// </summary>
        public void Initialise(
            int shipId,
            float cargoAmount,
            ShipCargoType cargoType,
            Vector3 spawnPosition,
            Vector3 idleAnchor,
            Vector3 departureTarget,
            ShipConfigSO config = null)
        {
            if (config != null) _config = config;

            ShipId = shipId;
            CargoAmount = cargoAmount;
            CargoType = cargoType;

            _idleAnchor = idleAnchor;
            _departureTarget = departureTarget;
            _bobTimer = Random.Range(0f, Mathf.PI * 2f); // desync bobs

            transform.position = spawnPosition;
            _originalVisualY = _visualRoot.localPosition.y;

            _assignedSocketIndex = -1;
            _socketWorldRotation = transform.rotation;
            _departureStartPosition = spawnPosition;
            _departureElapsed = 0f;

            UpdateRoleVisual();

            _fsm.Reset();   // → ShipState.Spawning

            EventBus<OnShipSpawned>.Raise(new OnShipSpawned
            {
                ShipId = ShipId,
                CargoAmount = CargoAmount,
                CargoType = CargoType
            });
        }

        // ---------------------------------------------------
        //  Public control (called by ShoreManager)
        // ---------------------------------------------------

        /// <summary>
        /// Called by ShoreManager when the player's tap is validated.
        /// Starts the ship moving toward the socket.
        /// </summary>
        public void BeginDocking(int socketIndex, Vector3 socketWorldPosition, Quaternion socketWorldRotation)
        {
            if (!_fsm.IsTappable)
            {
                Debug.LogWarning($"[ShipController {ShipId}] BeginDocking called on non-tappable ship.");
                return;
            }

            _assignedSocketIndex = socketIndex;
            _socketWorldPosition = socketWorldPosition;
            _socketWorldRotation = socketWorldRotation;

            _fsm.TransitionTo(ShipState.Docking);

            EventBus<OnShipDockingStarted>.Raise(new OnShipDockingStarted
            {
                ShipId = ShipId,
                SocketIndex = socketIndex
            });
        }

        /// <summary>
        /// Called by SocketController once the dock timer finishes
        /// and cargo has been handed off to StorageManager.
        /// </summary>
        public void BeginDeparture()
        {
            if (_fsm.Current != ShipState.Docked)
            {
                Debug.LogWarning($"[ShipController {ShipId}] BeginDeparture called but ship is not Docked.");
                return;
            }

            _fsm.TransitionTo(ShipState.Departing);
            _departureStartPosition = transform.position;
            _departureElapsed = 0f;

            EventBus<OnShipDeparting>.Raise(new OnShipDeparting
            {
                ShipId = ShipId,
                SocketIndex = _assignedSocketIndex
            });
        }

        // ---------------------------------------------------
        //  Per-state Update methods
        // ---------------------------------------------------

        private void UpdateSpawning()
        {
            // Move from spawn point toward the idle anchor
            MoveToward(_idleAnchor, _config.IdleSpeed);

            if (Vector3.Distance(transform.position, _idleAnchor) < _config.DockSnapDistance)
            {
                transform.position = _idleAnchor;
                _fsm.TransitionTo(ShipState.Idle);
            }
        }

        private void UpdateIdle()
        {
            // Bob gently in place
            _bobTimer += Time.deltaTime * _config.IdleBobFrequency * Mathf.PI * 2f;
            float bobOffset = Mathf.Sin(_bobTimer) * _config.IdleBobAmplitude;
            var lp = _visualRoot.localPosition;
            _visualRoot.localPosition = new Vector3(lp.x, _originalVisualY + bobOffset, lp.z);
        }

        private void UpdateDocking()
        {
            // Glide toward socket
            MoveToward(_socketWorldPosition, _config.DockingSpeed);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                _socketWorldRotation,
                _dockRotationSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _socketWorldPosition) < _config.DockSnapDistance)
            {
                // Snap precisely and notify socket
                transform.position = _socketWorldPosition;
                transform.rotation = _socketWorldRotation;
                ResetVisualBob();

                _fsm.TransitionTo(ShipState.Docked);

                EventBus<OnShipDocked>.Raise(new OnShipDocked
                {
                    ShipId = ShipId,
                    SocketIndex = _assignedSocketIndex,
                    CargoAmount = CargoAmount,
                    CargoType = CargoType,
                    DockDuration = _config.DockDuration
                });
            }
        }

        private void UpdateDeparting()
        {
            _departureElapsed += Time.deltaTime;
            MoveToward(_departureTarget, _config.DepartureSpeed);

            float distToTarget = Vector3.Distance(transform.position, _departureTarget);
            float movedSinceDeparture = Vector3.Distance(transform.position, _departureStartPosition);

            bool metVisibilityTime = _departureElapsed >= _minDepartureVisibleTime;
            bool metVisibilityDistance = movedSinceDeparture >= _minDepartureVisibleDistance;

            bool reachedTarget = distToTarget < _config.DockSnapDistance;
            bool exceededDespawnDistance = movedSinceDeparture > _config.DespawnDistance;

            if (metVisibilityTime && metVisibilityDistance && (reachedTarget || exceededDespawnDistance))
            {
                Despawn();
            }
        }

        // ---------------------------------------------------
        //  Despawn
        // ---------------------------------------------------

        private void Despawn()
        {
            _fsm.TransitionTo(ShipState.Despawned);

            EventBus<OnShipDespawned>.Raise(new OnShipDespawned
            {
                ShipId = ShipId
            });

            // ShipSpawnManager listens to OnShipDespawned and
            // calls pool.Return(this) — we do NOT deactivate here.
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        private void HandleStateChanged(ShipState previous, ShipState next)
        {
            Debug.Log($"[ShipController {ShipId}] {previous} → {next}");
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            // Nothing to do here for now — Update() guards on IsPlaying
        }

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        private void MoveToward(Vector3 target, float speed)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                speed * Time.deltaTime);
        }

        private void ResetVisualBob()
        {
            var lp = _visualRoot.localPosition;
            _visualRoot.localPosition = new Vector3(lp.x, _originalVisualY, lp.z);
        }

        private void UpdateRoleVisual()
        {
            if (_roleRenderer == null) return;

            _roleRenderer.material.color = CargoType == ShipCargoType.ExportProducts
                ? _exportColor
                : _importColor;

            if (_roleLabel != null)
            {
                bool isExport = CargoType == ShipCargoType.ExportProducts;
                _roleLabel.text = isExport ? "EXPORT" : "IMPORT";
                _roleLabel.color = isExport ? _exportLabelColor : _importLabelColor;
                _roleLabel.gameObject.SetActive(true);
            }
        }

        private void UpdateRoleLabelFacing()
        {
            if (_roleLabel == null) return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null) return;

            Transform labelTransform = _roleLabel.transform;
            Vector3 toCamera = labelTransform.position - _mainCamera.transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
                labelTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }
    }
}