// ============================================================
//  ShipStateMachine.cs
//  Pure C# finite state machine for a single ship entity.
//  No MonoBehaviour, no Unity dependencies — fully unit-testable.
//
//  States
//  ──────
//  Spawning   → ship has just been created, moving to idle zone
//  Idle       → floating in wait zone, player can tap it
//  Docking    → player tapped, ship moving toward socket
//  Docked     → at socket, 20-second countdown running
//  Departing  → countdown done, moving off-screen
//  Despawned  → terminal state, ready to return to pool
//
//  Pattern : State Machine (enum + transition guard table)
// ============================================================

using System;
using UnityEngine;

namespace PetroCitySimulator.Entities.Ship
{
    public enum ShipState
    {
        Spawning,
        Idle,
        Docking,
        Docked,
        Departing,
        Despawned
    }

    public class ShipStateMachine
    {
        // ---------------------------------------------------
        //  Current state
        // ---------------------------------------------------

        public ShipState Current { get; private set; } = ShipState.Spawning;

        // ---------------------------------------------------
        //  State-change callback
        // ---------------------------------------------------

        /// <summary>
        /// Fired after every successful transition.
        /// Arguments: (previousState, newState)
        /// </summary>
        public event Action<ShipState, ShipState> OnStateChanged;

        // ---------------------------------------------------
        //  Transition API
        // ---------------------------------------------------

        /// <summary>
        /// Attempt to move to <paramref name="next"/>.
        /// Logs a warning and returns false if the transition is illegal.
        /// </summary>
        public bool TransitionTo(ShipState next)
        {
            if (!IsValidTransition(Current, next))
            {
                Debug.LogWarning(
                    $"[ShipStateMachine] Illegal transition: {Current} → {next}");
                return false;
            }

            var previous = Current;
            Current = next;
            OnStateChanged?.Invoke(previous, next);
            return true;
        }

        // ---------------------------------------------------
        //  Convenience state-check helpers
        // ---------------------------------------------------

        public bool IsIdle => Current == ShipState.Idle;
        public bool IsDocking => Current == ShipState.Docking;
        public bool IsDocked => Current == ShipState.Docked;
        public bool IsDeparting => Current == ShipState.Departing;
        public bool IsDespawned => Current == ShipState.Despawned;

        /// <summary>True while the ship can be tapped by the player.</summary>
        public bool IsTappable => Current == ShipState.Idle;

        // ---------------------------------------------------
        //  Reset (called when the object is taken from the pool)
        // ---------------------------------------------------

        public void Reset()
        {
            Current = ShipState.Spawning;
        }

        // ---------------------------------------------------
        //  Transition guard table
        // ---------------------------------------------------

        private static bool IsValidTransition(ShipState from, ShipState to)
        {
            return (from, to) switch
            {
                // Normal forward flow
                (ShipState.Spawning, ShipState.Idle) => true,
                (ShipState.Idle, ShipState.Docking) => true,
                (ShipState.Docking, ShipState.Docked) => true,
                (ShipState.Docked, ShipState.Departing) => true,
                (ShipState.Departing, ShipState.Despawned) => true,

                // Edge cases
                // Ship can be forced to depart from Docking
                // (e.g. socket freed mid-approach by a manager reset)
                (ShipState.Docking, ShipState.Departing) => true,

                // Ship can be despawned from Idle
                // (e.g. game over while ships are waiting)
                (ShipState.Idle, ShipState.Despawned) => true,

                // Everything else is illegal
                _ => false
            };
        }
    }
}