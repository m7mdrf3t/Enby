using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Entities.Ship;
using PetroCitySimulator.Events;
using PetroCitySimulator.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Unified Input Manager for PetroCitySimulator.
/// Supports both mouse click (editor/mobile) and PS controller (joystick cursor + Square button).
///
/// SETUP:
/// 1. Assign _camera, _raycastDistance, _raycastLayers as before.
/// 2. For controller support: assign _cursorTransform (UI RectTransform) and _cursorCanvas.
/// 3. Install Unity Input System package and set Active Input Handling to "Both" or "New".
/// </summary>
public class InputManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Existing Fields (unchanged)
    // ─────────────────────────────────────────────

    [SerializeField] private Camera _camera;

    [Header("Raycast Settings")]
    [SerializeField, Min(1f)] private float _raycastDistance = 100f;

    [Tooltip("Layers considered tappable by this input raycast.")]
    [SerializeField] private LayerMask _raycastLayers = ~0;

    [Tooltip("Whether trigger colliders should be considered hittable.")]
    [SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Collide;

    // ─────────────────────────────────────────────
    // PS Controller Fields
    // ─────────────────────────────────────────────

    [Header("PS Controller - Cursor")]
    [Tooltip("UI RectTransform used as the on-screen cursor when using a gamepad.")]
    [SerializeField] private RectTransform _cursorTransform;

    [Tooltip("The Canvas the cursor lives in.")]
    [SerializeField] private Canvas _cursorCanvas;

    [Tooltip("Cursor movement speed in pixels/sec.")]
    [Range(100f, 2000f)]
    [SerializeField] private float _cursorSpeed = 700f;

    [Tooltip("Joystick dead zone radius — inputs below this magnitude are ignored.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _deadZone = 0.15f;

    [Header("PS Controller - Feedback")]
    [Tooltip("Optional Image on the cursor to swap sprite on Square press.")]
    [SerializeField] private Image _cursorIcon;

    [SerializeField] private Sprite _defaultCursorSprite;
    [SerializeField] private Sprite _actionCursorSprite;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float _actionFeedbackDuration = 0.15f;

    // ─────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────

    private Vector2        _cursorPosition;
    private RectTransform  _canvasRect;
    private float          _feedbackTimer;
    private bool           _feedbackActive;

    private InputAction    _moveAction;
    private InputAction    _squareAction;
    private Gamepad        _gamepad;

    // Tracks which input mode is active so the cursor UI shows/hides correctly
    private bool _isGamepadMode;

    // ─────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        InitCamera();
        InitControllerInput();
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _squareAction?.Enable();

        _squareAction.performed += OnSquarePressed;
        _squareAction.canceled  += OnSquareReleased;

        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _squareAction?.Disable();

        _squareAction.performed -= OnSquarePressed;
        _squareAction.canceled  -= OnSquareReleased;

        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Update()
    {
        if (!IsGameReady()) return;

        // Auto-detect active input mode each frame
        _isGamepadMode = Gamepad.current != null;
        _gamepad = Gamepad.current;

        SetCursorVisible(_isGamepadMode);

        if (_isGamepadMode)
        {
            UpdateJoystickCursor();
            HandleFeedbackTimer();
        }
        else
        {
            HandleMouseInput();
        }
    }

    // ─────────────────────────────────────────────
    // Init Helpers
    // ─────────────────────────────────────────────

    private void InitCamera()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null && _raycastDistance < _camera.farClipPlane)
        {
            _raycastDistance = _camera.farClipPlane;
            Debug.Log($"[InputManager] Increased raycast distance to {_raycastDistance}");
        }
    }

    private void InitControllerInput()
    {
        if (_cursorCanvas != null)
        {
            _canvasRect = _cursorCanvas.GetComponent<RectTransform>();
            _cursorPosition = Vector2.zero;
        }

        // Left joystick → cursor movement
        _moveAction = new InputAction(
            name: "MoveCursor",
            type: InputActionType.Value,
            binding: "<Gamepad>/leftStick"
        );

        // Square (PS) = buttonWest in Unity Input System
        _squareAction = new InputAction(
            name: "SquareButton",
            type: InputActionType.Button,
            binding: "<Gamepad>/buttonWest"
        );
    }

    // ─────────────────────────────────────────────
    // Guard
    // ─────────────────────────────────────────────

    private bool IsGameReady()
    {
        if (_camera == null)
        {
            Debug.LogWarning("[InputManager] Camera is null!");
            return false;
        }

        if (PetroCitySimulator.Core.GameManager.Instance == null)
        {
            Debug.LogWarning("[InputManager] GameManager is null!");
            return false;
        }

        return PetroCitySimulator.Core.GameManager.Instance.IsPlaying;
    }

    // ─────────────────────────────────────────────
    // Mouse Input (existing behaviour — untouched)
    // ─────────────────────────────────────────────

    private void HandleMouseInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Debug.Log($"[InputManager] Mouse clicked at: {Input.mousePosition}");
        PerformRaycast(_camera.ScreenPointToRay(Input.mousePosition));
    }

    // ─────────────────────────────────────────────
    // Joystick Cursor
    // ─────────────────────────────────────────────

    private void UpdateJoystickCursor()
    {
        if (_canvasRect == null || _cursorTransform == null) return;

        Vector2 raw = _moveAction.ReadValue<Vector2>();

        // Circular dead zone
        Vector2 input = raw.magnitude > _deadZone
            ? raw.normalized * ((raw.magnitude - _deadZone) / (1f - _deadZone))
            : Vector2.zero;

        if (input != Vector2.zero)
        {
            float scale = _cursorCanvas.scaleFactor > 0 ? _cursorCanvas.scaleFactor : 1f;
            _cursorPosition += input * (_cursorSpeed / scale) * Time.deltaTime;

            // Clamp inside canvas
            Vector2 half = _canvasRect.sizeDelta * 0.5f;
            _cursorPosition.x = Mathf.Clamp(_cursorPosition.x, -half.x, half.x);
            _cursorPosition.y = Mathf.Clamp(_cursorPosition.y, -half.y, half.y);

            _cursorTransform.anchoredPosition = _cursorPosition;
        }
    }

    // ─────────────────────────────────────────────
    // Square Button → reuses existing raycast logic
    // ─────────────────────────────────────────────

    private void OnSquarePressed(InputAction.CallbackContext ctx)
    {
        if (!IsGameReady()) return;

        // Convert cursor canvas position → screen position → ray
        Vector2 screenPos = CursorCanvasToScreen(_cursorPosition);
        Debug.Log($"[InputManager] Square pressed — cursor screen pos: {screenPos}");

        PerformRaycast(_camera.ScreenPointToRay(screenPos));

        // Visual + haptic feedback
        ShowActionFeedback();
        TriggerHaptics(lowFreq: 0.3f, highFreq: 0.6f, duration: 0.1f);
    }

    private void OnSquareReleased(InputAction.CallbackContext ctx)
    {
        RestoreCursorSprite();
    }

    // ─────────────────────────────────────────────
    // Core Raycast (shared by mouse + controller)
    // ─────────────────────────────────────────────

    private void PerformRaycast(Ray ray)
    {
        Debug.Log($"[InputManager] Raycast from: {ray.origin}, dir: {ray.direction}");

        if (!Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _raycastLayers, _queryTriggerInteraction))
        {
            Debug.Log($"[InputManager] Raycast hit nothing. Distance: {_raycastDistance}, Mask: {_raycastLayers.value}");
            return;
        }

        Debug.Log($"[InputManager] Hit: {hit.collider.gameObject.name} at {hit.distance}");

        // ── Action target (factory/city world object)? ───
        var actionTarget = hit.collider.GetComponentInParent<ActionTapTarget>();
        if (actionTarget != null)
        {
            Debug.Log($"[InputManager] ✓ ActionTapTarget hit on {hit.collider.gameObject.name}, triggering action");
            actionTarget.TriggerAction();
            return;
        }

        // ── Ship? ──────────────────────────────────
        var ship = hit.collider.GetComponentInParent<ShipController>();
        if (ship != null)
        {
            Debug.Log($"[InputManager] Ship — ID: {ship.ShipId}, State: {ship.CurrentState}, Type: {ship.CargoType}");

            if (ship.CargoType == ShipCargoType.ExportProducts)
            {
                Debug.Log("[InputManager] ✗ Export ship — auto-docks, ignored");
                return;
            }

            if (ship.CurrentState == ShipState.Idle)
            {
                Debug.Log("[InputManager] ✓ Ship Idle — raising OnShipTapped");
                EventBus<OnShipTapped>.Raise(new OnShipTapped
                {
                    ShipId = ship.ShipId,
                    ShipController = ship
                });
            }
            else
            {
                Debug.Log($"[InputManager] ✗ Ship state {ship.CurrentState} — ignored");
            }
            return;
        }

        // ── Fan? ───────────────────────────────────
        var fan = hit.collider.GetComponentInParent<FanController>();
        if (fan != null)
        {
            Debug.Log($"[InputManager] ✓ Fan — ID: {fan.FanId}, calling TryActivate()");
            fan.TryActivate();
            return;
        }

        Debug.LogWarning($"[InputManager] Hit {hit.collider.gameObject.name} — no Ship/Fan handler");
    }

    // ─────────────────────────────────────────────
    // Coordinate Conversion
    // ─────────────────────────────────────────────

    /// <summary>
    /// Converts an anchored canvas position (center = 0,0) to screen pixels.
    /// Works for Screen Space - Overlay and Screen Space - Camera canvases.
    /// </summary>
    private Vector2 CursorCanvasToScreen(Vector2 canvasPos)
    {
        if (_canvasRect == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // Anchored pos is relative to canvas centre → offset to screen origin
        Vector2 canvasCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float scale = _cursorCanvas.scaleFactor > 0 ? _cursorCanvas.scaleFactor : 1f;
        return canvasCentre + canvasPos * scale;
    }

    // ─────────────────────────────────────────────
    // Cursor UI Helpers
    // ─────────────────────────────────────────────

    private void SetCursorVisible(bool visible)
    {
        if (_cursorTransform != null)
            _cursorTransform.gameObject.SetActive(visible);

        // Hide OS cursor when gamepad is active
        Cursor.visible = !visible;
    }

    private void ShowActionFeedback()
    {
        if (_cursorIcon != null && _actionCursorSprite != null)
        {
            _cursorIcon.sprite = _actionCursorSprite;
            _feedbackActive = true;
            _feedbackTimer = _actionFeedbackDuration;
        }
    }

    private void HandleFeedbackTimer()
    {
        if (!_feedbackActive) return;
        _feedbackTimer -= Time.deltaTime;
        if (_feedbackTimer <= 0f)
        {
            RestoreCursorSprite();
            _feedbackActive = false;
        }
    }

    private void RestoreCursorSprite()
    {
        if (_cursorIcon != null && _defaultCursorSprite != null)
            _cursorIcon.sprite = _defaultCursorSprite;
    }

    // ─────────────────────────────────────────────
    // Haptics
    // ─────────────────────────────────────────────

    private void TriggerHaptics(float lowFreq, float highFreq, float duration)
    {
        if (_gamepad == null) return;
        _gamepad.SetMotorSpeeds(lowFreq, highFreq);
        StartCoroutine(StopHapticsAfter(duration));
    }

    private System.Collections.IEnumerator StopHapticsAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _gamepad?.ResetHaptics();
    }

    // ─────────────────────────────────────────────
    // Device Connect / Disconnect
    // ─────────────────────────────────────────────

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Added:
                Debug.Log($"[InputManager] Controller connected: {device.displayName}");
                break;
            case InputDeviceChange.Removed:
                Debug.LogWarning($"[InputManager] Controller disconnected: {device.displayName}");
                SetCursorVisible(false);
                break;
        }
    }

    // ─────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────

    /// <summary>Current cursor canvas position (valid in gamepad mode).</summary>
    public Vector2 CursorPosition => _cursorPosition;

    /// <summary>True when a gamepad is driving input.</summary>
    public bool IsGamepadActive => _isGamepadMode;
}