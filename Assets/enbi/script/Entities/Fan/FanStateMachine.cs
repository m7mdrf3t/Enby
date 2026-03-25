// ============================================================
//  FanStateMachine.cs
//  Pure C# finite state machine for a single fan entity.
//  No MonoBehaviour — fully unit-testable.
//
//  States
//  ──────
//  Idle             → fan is waiting for a tap
//  MovingToStorage  → travelling to the shared storage load point
//  Loading          → loading gas at storage
//  MovingToTown     → travelling to the town unload point
//  Unloading        → unloading gas at town
//  Returning        → travelling back to the home point
//  Cooldown         → temporary lockout before becoming tappable again
//  Disabled         → fan cannot be interacted with (e.g. storage too low)
//
//  Pattern : State Machine (enum + guard table)
// ============================================================

using System;
using UnityEngine;

namespace PetroCitySimulator.Entities.Fan
{
    public enum FanState
    {
        Idle,
        MovingToStorage,
        Loading,
        MovingToTown,
        Unloading,
        Returning,
        Cooldown,
        Disabled
    }

    public class FanStateMachine
    {
        // ---------------------------------------------------
        //  Current state
        // ---------------------------------------------------

        public FanState Current { get; private set; } = FanState.Idle;

        // ---------------------------------------------------
        //  Callback
        // ---------------------------------------------------

        /// <summary>Fired after every successful transition.</summary>
        public event Action<FanState, FanState> OnStateChanged;

        // ---------------------------------------------------
        //  Transition API
        // ---------------------------------------------------

        public bool TransitionTo(FanState next)
        {
            if (!IsValidTransition(Current, next))
            {
                Debug.LogWarning(
                    $"[FanStateMachine] Illegal transition: {Current} → {next}");
                return false;
            }

            var previous = Current;
            Current = next;
            OnStateChanged?.Invoke(previous, next);
            return true;
        }

        // ---------------------------------------------------
        //  Convenience helpers
        // ---------------------------------------------------

        /// <summary>True when the player can tap this fan.</summary>
        public bool IsTappable => Current == FanState.Idle;

        public bool IsMovingToStorage => Current == FanState.MovingToStorage;
        public bool IsLoading => Current == FanState.Loading;
        public bool IsMovingToTown => Current == FanState.MovingToTown;
        public bool IsUnloading => Current == FanState.Unloading;
        public bool IsReturning => Current == FanState.Returning;
        public bool IsOnCooldown => Current == FanState.Cooldown;
        public bool IsDisabled => Current == FanState.Disabled;

        // ---------------------------------------------------
        //  Reset
        // ---------------------------------------------------

        public void Reset() => Current = FanState.Idle;

        // ---------------------------------------------------
        //  Guard table
        // ---------------------------------------------------

        private static bool IsValidTransition(FanState from, FanState to) =>
            (from, to) switch
            {
                (FanState.Idle, FanState.MovingToStorage) => true,
                (FanState.MovingToStorage, FanState.Loading) => true,
                (FanState.Loading, FanState.MovingToTown) => true,
                (FanState.MovingToTown, FanState.Unloading) => true,
                (FanState.Unloading, FanState.Returning) => true,
                (FanState.Returning, FanState.Cooldown) => true,
                (FanState.Cooldown, FanState.Idle) => true,

                (FanState.Idle, FanState.Disabled) => true,
                (FanState.Cooldown, FanState.Disabled) => true,
                (FanState.Disabled, FanState.Idle) => true,

                (FanState.MovingToStorage, FanState.Returning) => true,
                (FanState.Loading, FanState.Returning) => true,
                (FanState.MovingToTown, FanState.Returning) => true,
                (FanState.Returning, FanState.Disabled) => true,

                _ => false
            };
    }
}