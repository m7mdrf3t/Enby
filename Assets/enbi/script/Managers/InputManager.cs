using PetroCitySimulator.Entities.Fan;
using PetroCitySimulator.Entities.Ship;
using PetroCitySimulator.Events;
using UnityEngine;


public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (_camera == null) return;
        if (PetroCitySimulator.Core.GameManager.Instance == null) return;
        if (!PetroCitySimulator.Core.GameManager.Instance.IsPlaying) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Ship tapped?
            var ship = hit.collider.GetComponentInParent<ShipController>();
            if (ship != null && ship.CurrentState == ShipState.Idle)
            {
                EventBus<OnShipTapped>.Raise(new OnShipTapped
                {
                    ShipId = ship.ShipId,
                    ShipController = ship
                });
                return;
            }

            // Fan tapped?
            var fan = hit.collider.GetComponentInParent<FanController>();
            if (fan != null)
            {
                fan.TryActivate();
            }
        }
    }
}