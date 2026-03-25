// ============================================================
//  GameEvents.cs
//  Defines every event struct that flows through the EventBus.
//  Rule: ONE file, pure data, zero logic, zero MonoBehaviour.
// ============================================================

namespace PetroCitySimulator.Events
{
    // ----------------------------------------------------------
    //  SHIP EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by ShipSpawnManager when a new ship enters the
    /// spawn zone and is ready for the player to interact with.
    /// </summary>
    public struct OnShipSpawned
    {
        /// <summary>Unique instance ID for this ship.</summary>
        public int ShipId;

        /// <summary>How much gas cargo this ship is carrying (units).</summary>
        public float CargoAmount;
    }

    /// <summary>
    /// Raised by ShipController when the player taps a waiting
    /// ship and it begins moving toward the shore socket.
    /// </summary>
    public struct OnShipDockingStarted
    {
        public int ShipId;

        /// <summary>Index of the socket this ship is heading to.</summary>
        public int SocketIndex;
    }

    /// <summary>
    /// Raised by SocketController once the ship has physically
    /// arrived at the socket and the 20-second timer begins.
    /// </summary>
    public struct OnShipDocked
    {
        public int ShipId;
        public int SocketIndex;
        public float CargoAmount;

        /// <summary>How many seconds the ship will stay docked.</summary>
        public float DockDuration;
    }

    /// <summary>
    /// Raised by SocketController when the docking timer expires
    /// and cargo has been fully transferred to storage.
    /// </summary>
    public struct OnCargoDelivered
    {
        public int ShipId;
        public int SocketIndex;

        /// <summary>Actual gas units transferred this visit.</summary>
        public float AmountDelivered;
    }

    /// <summary>
    /// Raised by ShipController when the ship begins its
    /// departure animation after cargo delivery.
    /// </summary>
    public struct OnShipDeparting
    {
        public int ShipId;
        public int SocketIndex;
    }

    /// <summary>
    /// Raised by ShipSpawnManager just before the ship GameObject
    /// is returned to the object pool.
    /// </summary>
    public struct OnShipDespawned
    {
        public int ShipId;
    }


    // ----------------------------------------------------------
    //  SOCKET EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by SocketController when a socket transitions from
    /// Occupied back to Free. UI should re-enable tap affordances.
    /// </summary>
    public struct OnSocketFreed
    {
        public int SocketIndex;
    }

    /// <summary>
    /// Raised by SocketController each second during docking to
    /// update the countdown timer shown on the HUD.
    /// </summary>
    public struct OnSocketTimerTick
    {
        public int SocketIndex;

        /// <summary>Seconds remaining until cargo is delivered.</summary>
        public float SecondsRemaining;
    }


    // ----------------------------------------------------------
    //  STORAGE EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by StorageManager whenever the gas level changes —
    /// either from a cargo delivery or from city consumption.
    /// Subscribers should treat this as the single source of truth
    /// for the current storage level.
    /// </summary>
    public struct OnStorageChanged
    {
        /// <summary>Current gas level in storage (units).</summary>
        public float CurrentAmount;

        /// <summary>Maximum capacity of the storage tank (units).</summary>
        public float MaxCapacity;

        /// <summary>Normalised fill level in [0, 1].</summary>
        public float FillRatio;

        /// <summary>True if the tank just became empty this tick.</summary>
        public bool JustBecameEmpty;

        /// <summary>True if the tank just became full this tick.</summary>
        public bool JustBecameFull;
    }

    /// <summary>
    /// Raised by StorageManager when storage drops to exactly zero.
    /// CityManager listens to this to trigger the blackout state.
    /// </summary>
    public struct OnStorageEmpty { }

    /// <summary>
    /// Raised by StorageManager when storage goes from zero to any
    /// positive value. CityManager listens to restore city lights.
    /// </summary>
    public struct OnStorageRestored
    {
        public float RestoredAmount;
    }


    // ----------------------------------------------------------
    //  CITY EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by CityManager when the city enters blackout state
    /// (storage hit zero). Lights off, ambient changes.
    /// </summary>
    public struct OnCityBlackout { }

    /// <summary>
    /// Raised by CityManager when the city exits blackout state.
    /// </summary>
    public struct OnCityLightsRestored { }

    /// <summary>
    /// Raised by CityManager on a regular interval so the UI can
    /// display the current consumption rate.
    /// </summary>
    public struct OnCityConsumptionTick
    {
        /// <summary>Gas units consumed since the last tick.</summary>
        public float AmountConsumed;

        /// <summary>Units consumed per second at the current rate.</summary>
        public float ConsumptionRate;
    }


    // ----------------------------------------------------------
    //  FAN EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by FanController when the player taps a fan and
    /// a gas transfer to the city pipeline begins.
    /// </summary>
    public struct OnFanActivated
    {
        public int FanId;

        /// <summary>Gas units that will be transferred by this fan activation.</summary>
        public float TransferAmount;
    }

    /// <summary>
    /// Raised by FanController when a fan finishes its transfer
    /// animation and returns to idle.
    /// </summary>
    public struct OnFanCompleted
    {
        public int FanId;
        public float AmountTransferred;
    }


    // ----------------------------------------------------------
    //  GAME STATE EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by GameManager when transitioning between high-level
    /// game states (e.g. Playing → Paused, Paused → Playing).
    /// </summary>
    public struct OnGameStateChanged
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    /// <summary>
    /// Matches the states tracked inside GameManager.
    /// Defined here so event subscribers don't need to import GameManager.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }

    // ----------------------------------------------------------
    //  INPUT EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by ShipController when the player taps a ship.
    /// ShoreManager listens and validates whether docking can begin.
    /// Carrying the controller reference avoids a secondary lookup.
    /// </summary>
    public struct OnShipTapped
    {
        public int ShipId;

        /// <summary>Direct reference so ShoreManager can call BeginDocking().</summary>
        public Entities.Ship.ShipController ShipController;
    }

    /// <summary>
    /// Raised by FanController the moment a fan is activated.
    /// StorageManager listens and immediately drains the requested amount.
    /// Separate from OnFanActivated so storage deduction is synchronous
    /// while the visual transfer animation plays asynchronously.
    /// </summary>
    public struct OnFanTransferRequested
    {
        public int FanId;
        public float TransferAmount;
    }
}