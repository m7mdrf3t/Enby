// ============================================================
//  FanController.cs
//  MonoBehaviour that owns a single fan GameObject.
//  Responsibilities:
//   • Receive player tap (OnMouseDown)
//   • Validate activation against storage level
//   • Drive FanStateMachine transitions
//   • Run transfer and cooldown timers
//   • Rotate blade transform during active/cooldown states
//   • Publish OnFanActivated and OnFanCompleted events
//   • React to OnStorageChanged to auto-disable/enable
//
//  Pattern : Controller (drives FSM, owns Timers, publishes events)
// ============================================================

using UnityEngine;
using PetroCitySimulator.Data;
using PetroCitySimulator.Events;
using PetroCitySimulator.Managers;
using PetroCitySimulator.Utils;
using System.Collections.Generic;

namespace PetroCitySimulator.Entities.Fan
{
    [RequireComponent(typeof(BoxCollider))]
    public class FanController : MonoBehaviour
    {
        // ---------------------------------------------------
        //  Inspector
        // ---------------------------------------------------

        [Header("Identity")]
        [SerializeField] private int _fanId;

        [Header("Config")]
        [SerializeField] private FanConfigSO _config;

        [Header("References")]
        [Tooltip("The blade transform that will be rotated.")]
        [SerializeField] private Transform _bladeTransform;

        [Tooltip("Optional particle system that plays while pumping.")]
        [SerializeField] private ParticleSystem _pumpParticles;

        [Header("Visual Feedback")]
        [Tooltip("Renderer used to tint the fan to reflect its state (optional).")]
        [SerializeField] private Renderer _statusRenderer;

        [SerializeField] private Color _colorIdle = Color.white;
        [SerializeField] private Color _colorPumping = new Color(0.4f, 0.9f, 0.4f);
        [SerializeField] private Color _colorCooldown = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color _colorDisabled = new Color(0.5f, 0.5f, 0.5f);

        // ---------------------------------------------------
        //  Runtime state
        // ---------------------------------------------------

        private FanStateMachine _fsm;
        private Timer _loadTimer;
        private Timer _unloadTimer;
        private Timer _cooldownTimer;
        private float _loadedAmount;
        private Vector3 _homePosition;
        private Vector3[] _toPickupPath = System.Array.Empty<Vector3>();
        private Vector3[] _toDropoffPath = System.Array.Empty<Vector3>();
        private Vector3[] _returnPath = System.Array.Empty<Vector3>();
        private readonly List<Vector3> _activePath = new List<Vector3>();
        private int _activePathIndex;
        private bool _routeReserved;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public int FanId => _fanId;
        public FanState CurrentState => _fsm.Current;
        public bool IsTappable => _fsm.IsTappable;

        /// <summary>Normalised cooldown progress [0=just started, 1=ready]. </summary>
        public float CooldownProgress => _cooldownTimer.Progress;

        /// <summary>Normalised load progress [0=just started, 1=done].</summary>
        public float TransferProgress => _loadTimer.Progress;

        // ---------------------------------------------------
        //  Unity lifecycle
        // ---------------------------------------------------

        private void Awake()
        {
            _fsm = new FanStateMachine();
            _fsm.OnStateChanged += HandleFsmStateChanged;
            _homePosition = transform.position;
            RebuildTimers();
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

            _loadTimer.Tick(Time.deltaTime);
            _unloadTimer.Tick(Time.deltaTime);
            _cooldownTimer.Tick(Time.deltaTime);

            UpdateMovement();
            UpdateBladeRotation();

            // Auto-dispatch: activate when idle and factory has products
            if (_fsm.IsTappable && FactoryManager.Instance != null && FactoryManager.Instance.HasProducts)
                TryActivate();
        }

        public void Initialise(int fanId, Vector3 homePosition, FanConfigSO config = null)
        {
            if (config != null) _config = config;

            _fanId = fanId;
            _homePosition = homePosition;
            _loadedAmount = 0f;
            _routeReserved = false;
            _toPickupPath = System.Array.Empty<Vector3>();
            _toDropoffPath = System.Array.Empty<Vector3>();
            _returnPath = System.Array.Empty<Vector3>();
            _activePath.Clear();
            _activePathIndex = 0;

            RebuildTimers();

            transform.position = _homePosition;
            _fsm.Reset();
            PlayParticles(false);
            UpdateStatusColor();
        }

        /// <summary>
        /// Attempt to activate the fan.
        /// Called from OnMouseDown or from a UI button / touch system.
        /// Returns true if activation succeeded.
        /// </summary>
        public bool TryActivate()
        {
            if (!_fsm.IsTappable)
                return false;

            if (FactoryManager.Instance == null || !FactoryManager.Instance.HasProducts)
            {
                Debug.Log($"[FanController {_fanId}] No products at factory.");
                return false;
            }

            if (FanRouteManager.Instance == null)
            {
                Debug.LogWarning($"[FanController {_fanId}] No FanRouteManager present in scene.");
                return false;
            }

            if (!FanRouteManager.Instance.TryBeginRoute(this, out _toPickupPath, out _toDropoffPath, out _returnPath))
            {
                Debug.Log($"[FanController {_fanId}] Route is busy or not configured.");
                return false;
            }

            ActivateRoute();
            return true;
        }

        // ---------------------------------------------------
        //  Activation
        // ---------------------------------------------------

        private void ActivateRoute()
        {
            _routeReserved = true;
            _loadedAmount = 0f;
            _fsm.TransitionTo(FanState.MovingToStorage);
            SetActivePath(_toPickupPath);

            EventBus<OnFanActivated>.Raise(new OnFanActivated
            {
                FanId = _fanId,
                TransferAmount = _config.TransferAmount
            });

            Debug.Log($"[FanController {_fanId}] Product delivery route started.");
        }

        // ---------------------------------------------------
        //  Timer callbacks
        // ---------------------------------------------------

        private void HandleLoadComplete()
        {
            if (FanRouteManager.Instance != null)
                FanRouteManager.Instance.EndStorageLoad(this);

            _loadedAmount = FactoryManager.Instance != null
                ? FactoryManager.Instance.TakeProducts(_config.TransferAmount)
                : 0f;

            PlayParticles(false);
            _fsm.TransitionTo(FanState.MovingToTown);
            SetActivePath(_toDropoffPath);

            if (FanRouteManager.Instance != null)
                FanRouteManager.Instance.EnqueueForTownUnload(this);

            Debug.Log($"[FanController {_fanId}] Loaded {_loadedAmount:F1} product units from factory.");
        }

        private void HandleUnloadComplete()
        {
            if (FanRouteManager.Instance != null)
                FanRouteManager.Instance.EndTownUnload(this);

            if (ProductStorageManager.Instance != null && _loadedAmount > 0f)
                ProductStorageManager.Instance.AddProducts(_loadedAmount);

            EventBus<OnFanCompleted>.Raise(new OnFanCompleted
            {
                FanId = _fanId,
                AmountTransferred = _loadedAmount
            });

            _fsm.TransitionTo(FanState.Returning);
            SetActiveReturnPath();
            Debug.Log($"[FanController {_fanId}] Delivered {_loadedAmount:F1} products to storage.");
        }

        private void HandleCooldownComplete()
        {
            if (_fsm.IsOnCooldown)
            {
                _fsm.TransitionTo(FanState.Idle);
                Debug.Log($"[FanController {_fanId}] Ready.");
            }
        }

        // ---------------------------------------------------
        //  Event handlers
        // ---------------------------------------------------

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (e.NewState == GameState.Paused)
            {
                _loadTimer.Pause();
                _unloadTimer.Pause();
                _cooldownTimer.Pause();
            }
            else if (e.NewState == GameState.Playing)
            {
                _loadTimer.Resume();
                _unloadTimer.Resume();
                _cooldownTimer.Resume();
            }
            else if (e.NewState == GameState.GameOver)
            {
                ResetState();
            }
        }

        private void HandleFsmStateChanged(FanState previous, FanState next)
        {
            UpdateStatusColor();
            Debug.Log($"[FanController {_fanId}] {previous} → {next}");
        }

        // ---------------------------------------------------
        //  Blade rotation
        // ---------------------------------------------------

        private void UpdateBladeRotation()
        {
            if (_bladeTransform == null) return;

            float speed = _fsm.Current switch
            {
                FanState.MovingToStorage => _config.ActiveRotationSpeed,
                FanState.Loading => _config.ActiveRotationSpeed,
                FanState.MovingToTown => _config.ActiveRotationSpeed,
                FanState.Unloading => _config.ActiveRotationSpeed,
                FanState.Cooldown => _config.CooldownRotationSpeed,
                _ => 0f
            };

            if (speed > 0f)
            {
                _bladeTransform.Rotate(Vector3.up, speed * Time.deltaTime);
            }
        }

        // ---------------------------------------------------
        //  Helpers
        // ---------------------------------------------------

        private void PlayParticles(bool play)
        {
            if (_pumpParticles == null) return;
            if (play) _pumpParticles.Play();
            else _pumpParticles.Stop();
        }

        private void UpdateMovement()
        {
            switch (_fsm.Current)
            {
                case FanState.MovingToStorage:
                    MoveAlongPath(_config.MoveToStorageSpeed, FanState.Loading, BeginLoading);
                    break;

                case FanState.Loading:
                    UpdateLoadingState();
                    break;

                case FanState.MovingToTown:
                    MoveAlongPath(_config.MoveToTownSpeed, FanState.Unloading, BeginUnloading);
                    break;

                case FanState.Unloading:
                    UpdateUnloadingState();
                    break;

                case FanState.Returning:
                    MoveAlongPath(_config.ReturnSpeed, FanState.Cooldown, BeginCooldown);
                    break;
            }
        }

        private void UpdateLoadingState()
        {
            // Check if timer is already running (we're loading)
            if (_loadTimer.IsRunning)
                return;

            // Timer not running — check if we can start loading
            if (FanRouteManager.Instance != null && FanRouteManager.Instance.TryBeginStorageLoad(this))
            {
                PlayParticles(true);
                _loadTimer.Start();
                Debug.Log($"[FanController {_fanId}] Starting load from storage...");
            }
            // else: still waiting, will try again next frame
        }

        private void UpdateUnloadingState()
        {
            if (_unloadTimer.IsRunning)
                return;

            bool canBeginUnload = FanRouteManager.Instance != null
                && FanRouteManager.Instance.TryBeginTownUnload(this);

            if (canBeginUnload)
            {
                PlayParticles(true);
                _unloadTimer.Start();
                Debug.Log($"[FanController {_fanId}] Starting unload at product storage...");
            }
        }

        private void MoveAlongPath(float speed, FanState nextState, System.Action onArrive)
        {
            if (_activePathIndex >= _activePath.Count)
            {
                _fsm.TransitionTo(nextState);
                onArrive?.Invoke();
                return;
            }

            Vector3 target = _activePath[_activePathIndex];
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target) > _config.SnapDistance)
                return;

            transform.position = target;
            _activePathIndex++;

            if (_activePathIndex >= _activePath.Count)
            {
                _fsm.TransitionTo(nextState);
                onArrive?.Invoke();
            }
        }

        private void BeginLoading()
        {
            // Transition to Loading state — actual loading starts in UpdateLoadingState
            Debug.Log($"[FanController {_fanId}] Arrived at storage, waiting for turn to load...");
        }

        private void BeginUnloading()
        {
            // Transition to Unloading state — actual unloading starts in UpdateUnloadingState
            Debug.Log($"[FanController {_fanId}] Arrived at town, waiting for turn to unload...");
        }

        private void BeginCooldown()
        {
            PlayParticles(false);
            ReleaseRoute();
            _cooldownTimer.Start();
        }

        private void ReleaseRoute()
        {
            if (!_routeReserved) return;

            FanRouteManager.Instance?.EndRoute(this);
            _routeReserved = false;
        }

        public void ReleaseHomeSlot()
        {
            ReleaseRoute();
            FanRouteManager.Instance?.ReleaseFan(this);
        }

        private void SetActivePath(IReadOnlyList<Vector3> points)
        {
            _activePath.Clear();
            _activePathIndex = 0;

            for (int i = 0; i < points.Count; i++)
                _activePath.Add(points[i]);
        }

        private void SetActiveReturnPath()
        {
            _activePath.Clear();
            _activePathIndex = 0;

            for (int i = 0; i < _returnPath.Length; i++)
                _activePath.Add(_returnPath[i]);

            _activePath.Add(_homePosition);
        }

        private void RebuildTimers()
        {
            _loadTimer = new Timer(_config.LoadDuration);
            _loadTimer.OnCompleted += HandleLoadComplete;

            _unloadTimer = new Timer(_config.UnloadDuration);
            _unloadTimer.OnCompleted += HandleUnloadComplete;

            _cooldownTimer = new Timer(_config.CooldownDuration);
            _cooldownTimer.OnCompleted += HandleCooldownComplete;
        }

        private void ResetState()
        {
            _loadTimer.Stop();
            _unloadTimer.Stop();
            _cooldownTimer.Stop();
            _fsm.Reset();
            _loadedAmount = 0f;
            _activePath.Clear();
            _activePathIndex = 0;
            transform.position = _homePosition;
            PlayParticles(false);
            ReleaseRoute();
            UpdateStatusColor();
        }

        private void UpdateStatusColor()
        {
            if (_statusRenderer == null) return;

            _statusRenderer.material.color = _fsm.Current switch
            {
                FanState.Idle => _colorIdle,
                FanState.MovingToStorage => _colorPumping,
                FanState.Loading => _colorPumping,
                FanState.MovingToTown => _colorPumping,
                FanState.Unloading => _colorPumping,
                FanState.Returning => _colorCooldown,
                FanState.Cooldown => _colorCooldown,
                FanState.Disabled => _colorDisabled,
                _ => Color.white
            };
        }

        // ---------------------------------------------------
        //  Gizmos
        // ---------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = (_fsm != null && _fsm.IsTappable) ? Color.green : Color.grey;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Fan {_fanId}\n{(_fsm != null ? _fsm.Current.ToString() : "?")}");
        }
#endif
    }
}