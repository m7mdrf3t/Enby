using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// PS Controller Input System
/// Controls a UI cursor via the left joystick and triggers actions on Square (PS) / X (Xbox) button.
///
/// SETUP INSTRUCTIONS:
/// 1. Attach this script to a GameObject in your scene (e.g., "InputManager").
/// 2. Assign a RectTransform as the `cursorTransform` (your cursor UI Image).
/// 3. Assign the `cursorCanvas` (the Canvas the cursor lives in).
/// 4. Optionally assign `cursorIcon` to swap sprites on action.
/// 5. Install the Unity Input System package (Package Manager → Input System).
/// 6. Set Project Settings → Player → Active Input Handling → "Input System Package (New)".
/// </summary>
public class PSControllerInputSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────

    [Header("Cursor Settings")]
    [Tooltip("The RectTransform of your cursor UI element.")]
    public RectTransform cursorTransform;

    [Tooltip("The Canvas the cursor moves within.")]
    public Canvas cursorCanvas;

    [Tooltip("How fast the cursor moves across the screen (pixels/sec).")]
    [Range(100f, 2000f)]
    public float cursorSpeed = 600f;

    [Tooltip("Joystick dead zone — inputs below this are ignored.")]
    [Range(0f, 0.5f)]
    public float deadZone = 0.15f;

    [Header("Action Feedback")]
    [Tooltip("Optional: Image component on cursor to swap sprites.")]
    public Image cursorIcon;

    [Tooltip("Sprite to show when Square is pressed.")]
    public Sprite actionSprite;

    [Tooltip("Default cursor sprite.")]
    public Sprite defaultSprite;

    [Tooltip("How long the action visual feedback lasts (seconds).")]
    [Range(0.05f, 0.5f)]
    public float actionFeedbackDuration = 0.15f;

    // ─────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────

    private Gamepad _gamepad;
    private Vector2 _joystickInput;
    private Vector2 _cursorPosition;
    private RectTransform _canvasRect;
    private float _feedbackTimer;
    private bool _isActionActive;

    // Input System action references (bound at runtime)
    private InputAction _moveAction;
    private InputAction _squareAction;

    // ─────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        ValidateReferences();
        SetupInputActions();
        CenterCursor();
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _squareAction?.Enable();

        // Subscribe to Square button events
        _squareAction.performed += OnSquarePressed;
        _squareAction.canceled  += OnSquareReleased;

        // Listen for gamepad connect/disconnect
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
        DetectGamepad();
        ReadJoystickInput();
        MoveCursor();
        HandleFeedbackTimer();
    }

    // ─────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────

    private void ValidateReferences()
    {
        if (cursorTransform == null)
            Debug.LogError("[PSController] cursorTransform is not assigned!", this);

        if (cursorCanvas == null)
        {
            cursorCanvas = FindObjectOfType<Canvas>();
            if (cursorCanvas == null)
                Debug.LogError("[PSController] No Canvas found in scene!", this);
        }

        _canvasRect = cursorCanvas.GetComponent<RectTransform>();
    }

    private void SetupInputActions()
    {
        // Left Joystick → cursor movement
        _moveAction = new InputAction(
            name: "MoveCursor",
            type: InputActionType.Value,
            binding: "<Gamepad>/leftStick"
        );

        // Square button (PS) = buttonWest in Unity's Input System
        _squareAction = new InputAction(
            name: "SquareButton",
            type: InputActionType.Button,
            binding: "<Gamepad>/buttonWest"   // Square on PS, X on Xbox
        );
    }

    private void CenterCursor()
    {
        if (cursorTransform != null)
        {
            _cursorPosition = Vector2.zero; // Center of the canvas
            cursorTransform.anchoredPosition = _cursorPosition;
        }
    }

    // ─────────────────────────────────────────────
    // Input Reading
    // ─────────────────────────────────────────────

    private void DetectGamepad()
    {
        _gamepad = Gamepad.current;

        if (_gamepad == null)
        {
            // No gamepad — optionally show a warning once
            // Debug.LogWarning("[PSController] No gamepad detected.");
        }
    }

    private void ReadJoystickInput()
    {
        if (_moveAction == null) return;

        Vector2 raw = _moveAction.ReadValue<Vector2>();

        // Apply circular dead zone
        _joystickInput = raw.magnitude > deadZone
            ? raw.normalized * ((raw.magnitude - deadZone) / (1f - deadZone))
            : Vector2.zero;
    }

    // ─────────────────────────────────────────────
    // Cursor Movement
    // ─────────────────────────────────────────────

    private void MoveCursor()
    {
        if (_joystickInput == Vector2.zero || _canvasRect == null) return;

        // Scale speed by canvas scale for resolution independence
        float scaleFactor = cursorCanvas.scaleFactor > 0 ? cursorCanvas.scaleFactor : 1f;
        Vector2 delta = _joystickInput * (cursorSpeed / scaleFactor) * Time.deltaTime;

        _cursorPosition += delta;

        // Clamp within canvas bounds
        Vector2 half = _canvasRect.sizeDelta * 0.5f;
        _cursorPosition.x = Mathf.Clamp(_cursorPosition.x, -half.x, half.x);
        _cursorPosition.y = Mathf.Clamp(_cursorPosition.y, -half.y, half.y);

        cursorTransform.anchoredPosition = _cursorPosition;
    }

    // ─────────────────────────────────────────────
    // Square Button Actions
    // ─────────────────────────────────────────────

    private void OnSquarePressed(InputAction.CallbackContext context)
    {
        Debug.Log($"[PSController] Square pressed! Cursor at: {_cursorPosition}");

        // ── YOUR ACTION LOGIC HERE ─────────────────
        PerformCursorAction(_cursorPosition);
        // ──────────────────────────────────────────

        // Visual feedback
        if (cursorIcon != null && actionSprite != null)
        {
            cursorIcon.sprite = actionSprite;
            _isActionActive = true;
            _feedbackTimer = actionFeedbackDuration;
        }

        // Haptic rumble on PS controller
        TriggerHapticFeedback(lowFreq: 0.3f, highFreq: 0.6f, duration: 0.1f);
    }

    private void OnSquareReleased(InputAction.CallbackContext context)
    {
        Debug.Log("[PSController] Square released.");
        RestoreCursorSprite();
    }

    /// <summary>
    /// Override or extend this method to define what happens when Square is pressed.
    /// `position` is the cursor's anchored position within the canvas.
    /// </summary>
    private void PerformCursorAction(Vector2 position)
    {
        // Example: Raycast or hit-test at cursor world position
        // Example: Select a UI element under the cursor
        // Example: Spawn an object at the cursor position

        Debug.Log($"[PSController] Action performed at canvas position: {position}");

        // ── REPLACE WITH YOUR GAME LOGIC ──────────
        // e.g., CheckHitAtPosition(position);
        // ──────────────────────────────────────────
    }

    // ─────────────────────────────────────────────
    // Feedback Helpers
    // ─────────────────────────────────────────────

    private void HandleFeedbackTimer()
    {
        if (!_isActionActive) return;

        _feedbackTimer -= Time.deltaTime;
        if (_feedbackTimer <= 0f)
        {
            RestoreCursorSprite();
            _isActionActive = false;
        }
    }

    private void RestoreCursorSprite()
    {
        if (cursorIcon != null && defaultSprite != null)
            cursorIcon.sprite = defaultSprite;
    }

    private void TriggerHapticFeedback(float lowFreq, float highFreq, float duration)
    {
        if (_gamepad == null) return;

        _gamepad.SetMotorSpeeds(lowFreq, highFreq);
        // Stop rumble after duration using a coroutine
        StartCoroutine(StopHapticsAfter(duration));
    }

    private System.Collections.IEnumerator StopHapticsAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _gamepad?.ResetHaptics();
    }

    // ─────────────────────────────────────────────
    // Device Events
    // ─────────────────────────────────────────────

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Added:
                Debug.Log($"[PSController] Controller connected: {device.displayName}");
                break;
            case InputDeviceChange.Removed:
                Debug.LogWarning($"[PSController] Controller disconnected: {device.displayName}");
                break;
        }
    }

    // ─────────────────────────────────────────────
    // Public API (call from other scripts)
    // ─────────────────────────────────────────────

    /// <summary>Returns the cursor's current canvas position.</summary>
    public Vector2 GetCursorPosition() => _cursorPosition;

    /// <summary>Returns true if a gamepad is currently connected.</summary>
    public bool IsGamepadConnected() => _gamepad != null;

    /// <summary>Teleports the cursor to a specific canvas position.</summary>
    public void SetCursorPosition(Vector2 position)
    {
        _cursorPosition = position;
        if (cursorTransform != null)
            cursorTransform.anchoredPosition = _cursorPosition;
    }
}