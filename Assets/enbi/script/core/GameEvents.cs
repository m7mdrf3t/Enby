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

        /// <summary>Whether this ship imports gas or exports products.</summary>
        public Entities.Ship.ShipCargoType CargoType;
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
        public Entities.Ship.ShipCargoType CargoType;

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
    /// Raised when an export ship loads products from product storage and departs.
    /// </summary>
    public struct OnProductsExported
    {
        public int ShipId;
        public int SocketIndex;
        public float AmountExported;
    }

    /// <summary>
    /// Raised by SocketController when an export ship is ready to load products.
    /// ProductStorageManager handles the request and raises OnProductsExported.
    /// </summary>
    public struct OnProductExportRequested
    {
        public int ShipId;
        public int SocketIndex;
        public float RequestedAmount;
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
    //  FACTORY EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised by FactoryManager whenever the gas buffer or product output changes.
    /// </summary>
    public struct OnFactoryStateChanged
    {
        public float GasBuffer;
        public float GasBufferCapacity;
        public float GasBufferFillRatio;
        public float ProductOutputBuffer;
    }

    /// <summary>
    /// Raised when a factory upgrade is unlocked and becomes available to purchase.
    /// UI can use this to enable/show the upgrade button.
    /// </summary>
    public struct OnFactoryUpgradeUnlocked
    {
        public int CurrentLevel;
        public int NextLevel;
        public float UpgradeCost;
    }

    /// <summary>
    /// Raised when the player successfully upgrades the factory.
    /// Level increased, stats changed, visual model updated.
    /// </summary>
    public struct OnFactoryUpgraded
    {
        public int NewLevel;
        public float GasPerProduct;
        public float ProductsPerCycle;
        public float ProductionDuration;
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

        /// <summary>Product units that will be transferred by this fan activation.</summary>
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
    /// Raised by GameManager while the main match timer is running.
    /// UI can use this for countdown labels and radial fills.
    /// </summary>
    public struct OnMainTimerTick
    {
        public float RemainingSeconds;
        public float DurationSeconds;
        public float NormalizedRemaining;
    }

    /// <summary>
    /// Raised once when the match ends so final-screen UI can display
    /// end-of-round totals even if managers reset on GameOver.
    /// </summary>
    public struct OnGameFinishedSummary
    {
        public float MoneyAmount;
        public float ProductAmount;
        public float GasAmount;
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



    // ----------------------------------------------------------
    //  PRODUCT / ECONOMY EVENTS
    // ----------------------------------------------------------

    /// <summary>
    /// Raised whenever the city’s internal gas buffer changes.
    /// CityLightUI subscribes to this for health bar / blackout visuals.
    /// </summary>
    public struct OnCityGasChanged
    {
        public float CurrentAmount;
        public float MaxCapacity;
        public float FillRatio;
    }

    /// <summary>
    /// Raised whenever product storage amount changes.
    /// </summary>
    public struct OnProductStorageChanged
    {
        public float CurrentAmount;
        public float MaxCapacity;
        public float FillRatio;
    }

    /// <summary>
    /// Raised when total money changes due to product exports.
    /// </summary>
    public struct OnMoneyChanged
    {
        public float CurrentMoney;
        public float Delta;
        public string Source;
    }
}