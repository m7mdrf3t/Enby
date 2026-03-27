using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Entities.Ship;
using PetroCitySimulator.Events;
using PetroCitySimulator.Managers;
using UnityEngine;


public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    [Header("Raycast Settings")]
    [SerializeField, Min(1f)] private float _raycastDistance = 100f;

    [Tooltip("Layers considered tappable by this input raycast.")]
    [SerializeField] private LayerMask _raycastLayers = ~0;

    [Tooltip("Whether trigger colliders should be considered hittable.")]
    [SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Collide;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        // Ensure raycast reaches at least as far as the camera can see.
        if (_camera != null && _raycastDistance < _camera.farClipPlane)
        {
            _raycastDistance = _camera.farClipPlane;
            Debug.Log($"[InputManager] Increased raycast distance to {_raycastDistance} (camera far clip: {_camera.farClipPlane})");
        }
    }

    private void Update()
    {
        if (_camera == null) 
        {
            Debug.LogWarning("[InputManager] Camera is null!");
            return;
        }

        if (PetroCitySimulator.Core.GameManager.Instance == null)
        {
            Debug.LogWarning("[InputManager] GameManager is null!");
            return;
        }

        if (!PetroCitySimulator.Core.GameManager.Instance.IsPlaying)
        {
            return; // Silent return when not playing
        }

        if (!Input.GetMouseButtonDown(0)) return;

        Debug.Log($"[InputManager] Mouse clicked at screen position: {Input.mousePosition}");

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        Debug.Log($"[InputManager] Raycast from: {ray.origin}, direction: {ray.direction}");

        if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _raycastLayers, _queryTriggerInteraction))
        {
            Debug.Log($"[InputManager] Raycast hit: {hit.collider.gameObject.name} at distance {hit.distance}");

            // Ship tapped?
            var ship = hit.collider.GetComponentInParent<ShipController>();
            if (ship != null)
            {
                Debug.Log($"[InputManager] Ship detected - ID: {ship.ShipId}, State: {ship.CurrentState}, Type: {ship.CargoType}");

                // Export ships auto-dock — ignore player taps
                if (ship.CargoType == ShipCargoType.ExportProducts)
                {
                    Debug.Log($"[InputManager] ✗ Export ship — auto-docks, tap ignored");
                    return;
                }

                if (ship.CurrentState == ShipState.Idle)
                {
                    Debug.Log($"[InputManager] ✓ Ship is Idle - raising OnShipTapped event");
                    EventBus<OnShipTapped>.Raise(new OnShipTapped
                    {
                        ShipId = ship.ShipId,
                        ShipController = ship
                    });
                    return;
                }
                else
                {
                    Debug.Log($"[InputManager] ✗ Ship state is {ship.CurrentState}, not Idle - ignoring tap");
                    return;
                }
            }

            // Fan tapped?
            var fan = hit.collider.GetComponentInParent<FanController>();
            if (fan != null)
            {
                Debug.Log($"[InputManager] ✓ Fan detected - ID: {fan.FanId}, calling TryActivate()");
                fan.TryActivate();
                return;
            }

            Debug.LogWarning($"[InputManager] Raycast hit {hit.collider.gameObject.name}, but no Ship/Fan handler found");
        }
        else
        {
            Debug.Log($"[InputManager] Raycast did not hit anything at screen position {Input.mousePosition}. Distance: {_raycastDistance}, LayerMask: {_raycastLayers.value}");
        }
    }
}