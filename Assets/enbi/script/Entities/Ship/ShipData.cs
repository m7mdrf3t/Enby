namespace PetroCitySimulator.Entities.Ship
{
    [System.Serializable]
    public class ShipData
    {
        public int ShipId;
        public float CargoAmount;
        public ShipState State;
        public int AssignedSocketIndex = -1;
    }
}
