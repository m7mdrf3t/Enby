// ============================================================
//  Timer.cs
//  A plain C# reusable countdown timer.
//  NOT a MonoBehaviour — must be ticked manually from an owner.
//
//  Usage:
//    var t = new Timer(20f, OnComplete, OnTick);
//    t.Start();
//    // In owner's Update():
//    t.Tick(Time.deltaTime);
// ============================================================

using System;
using UnityEngine;

namespace PetroCitySimulator.Utils
{
    public class Timer
    {
        // ---------------------------------------------------
        //  Configuration
        // ---------------------------------------------------

        /// <summary>Total duration this timer counts down from.</summary>
        public float Duration { get; private set; }

        // ---------------------------------------------------
        //  State
        // ---------------------------------------------------

        private float _remaining;
        private bool _isRunning;
        private bool _isPaused;

        // ---------------------------------------------------
        //  Callbacks
        // ---------------------------------------------------

        /// <summary>Fired once when the timer reaches zero.</summary>
        public event Action OnCompleted;

        /// <summary>
        /// Fired every Tick() call while running.
        /// Argument is the seconds remaining (never negative).
        /// </summary>
        public event Action<float> OnTicked;

        // ---------------------------------------------------
        //  Public read-only
        // ---------------------------------------------------

        public float Remaining => _remaining;
        public bool IsRunning => _isRunning && !_isPaused;
        public bool IsPaused => _isPaused;
        public bool IsCompleted => !_isRunning && _remaining <= 0f;

        /// <summary>Normalised elapsed progress in [0, 1].</summary>
        public float Progress =>
            Duration > 0f ? 1f - Mathf.Clamp01(_remaining / Duration) : 1f;

        // ---------------------------------------------------
        //  Constructor
        // ---------------------------------------------------

        /// <param name="duration">Countdown length in seconds.</param>
        /// <param name="onCompleted">Optional callback on completion.</param>
        /// <param name="onTicked">Optional callback each tick with remaining time.</param>
        public Timer(float duration, Action onCompleted = null, Action<float> onTicked = null)
        {
            Duration = duration;
            _remaining = duration;
            _isRunning = false;
            _isPaused = false;

            if (onCompleted != null) OnCompleted += onCompleted;
            if (onTicked != null) OnTicked += onTicked;
        }

        // ---------------------------------------------------
        //  Control
        // ---------------------------------------------------

        /// <summary>Start (or restart) the countdown from Duration.</summary>
        public void Start()
        {
            _remaining = Duration;
            _isRunning = true;
            _isPaused = false;
        }

        /// <summary>Start with a new duration, overriding the original.</summary>
        public void Start(float newDuration)
        {
            Duration = newDuration;
            Start();
        }

        /// <summary>Pause ticking without resetting the remaining time.</summary>
        public void Pause()
        {
            if (_isRunning) _isPaused = true;
        }

        /// <summary>Resume a paused timer.</summary>
        public void Resume()
        {
            if (_isRunning && _isPaused) _isPaused = false;
        }

        /// <summary>Stop and reset to Duration. OnCompleted will NOT fire.</summary>
        public void Reset()
        {
            _isRunning = false;
            _isPaused = false;
            _remaining = Duration;
        }

        /// <summary>Stop immediately without resetting. OnCompleted will NOT fire.</summary>
        public void Stop()
        {
            _isRunning = false;
            _isPaused = false;
        }

        // ---------------------------------------------------
        //  Tick — call from owner's Update()
        // ---------------------------------------------------

        /// <summary>
        /// Advance the timer by deltaTime.
        /// Fires OnTicked each call, and OnCompleted exactly once when it hits zero.
        /// Safe to call when stopped or paused — just does nothing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning || _isPaused) return;

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _isRunning = false;

                // Tick callback fires with 0 remaining before completion
                OnTicked?.Invoke(0f);
                OnCompleted?.Invoke();
            }
            else
            {
                OnTicked?.Invoke(_remaining);
            }
        }
    }
}