// ============================================================
//  SocketStateMachine.cs
//  Pure C# finite state machine for a single docking socket.
//  No MonoBehaviour — fully unit-testable.
//
//  States
//  ──────
//  Free       → socket is empty, ready to accept a ship
//  Incoming   → a ship has been assigned and is sailing over
//  Occupied   → ship is docked, 20-second countdown running
//  Cooldown   → brief lock-out after ship departs (optional tunable)
//
//  Pattern : State Machine (enum + guard table)
// ============================================================

using System;
using UnityEngine;

namespace PetroCitySimulator.Entities.Socket
{
    public enum SocketState
    {
        Free,
        Incoming,
        Occupied,
        Cooldown
    }

    public class SocketStateMachine
    {
        // ---------------------------------------------------
        //  Current state
        // ---------------------------------------------------

        public SocketState Current { get; private set; } = SocketState.Free;

        // ---------------------------------------------------
        //  Callback
        // ---------------------------------------------------

        /// <summary>Fired after every successful transition.</summary>
        public event Action<SocketState, SocketState> OnStateChanged;

        // ---------------------------------------------------
        //  Transition API
        // ---------------------------------------------------

        public bool TransitionTo(SocketState next)
        {
            if (!IsValidTransition(Current, next))
            {
                Debug.LogWarning(
                    $"[SocketStateMachine] Illegal transition: {Current} → {next}");
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

        /// <summary>True when a new ship can be assigned to this socket.</summary>
        public bool IsAvailable => Current == SocketState.Free;

        public bool IsIncoming => Current == SocketState.Incoming;
        public bool IsOccupied => Current == SocketState.Occupied;
        public bool IsOnCooldown => Current == SocketState.Cooldown;

        // ---------------------------------------------------
        //  Reset
        // ---------------------------------------------------

        public void Reset() => Current = SocketState.Free;

        // ---------------------------------------------------
        //  Guard table
        // ---------------------------------------------------

        private static bool IsValidTransition(SocketState from, SocketState to) =>
            (from, to) switch
            {
                (SocketState.Free, SocketState.Incoming) => true,
                (SocketState.Incoming, SocketState.Occupied) => true,
                (SocketState.Occupied, SocketState.Cooldown) => true,
                (SocketState.Occupied, SocketState.Free) => true,   // skip cooldown if disabled
                (SocketState.Cooldown, SocketState.Free) => true,

                // Ship aborted mid-approach — release socket
                (SocketState.Incoming, SocketState.Free) => true,

                _ => false
            };
    }
}