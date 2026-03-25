namespace PetroCitySimulator.Entities.Ship
{
    public enum ShipCargoType
    {
        ImportGas,
        ExportProducts
    }

    [System.Serializable]
    public class ShipData
    {
        public int ShipId;
        public float CargoAmount;
        public ShipCargoType CargoType = ShipCargoType.ImportGas;
        public ShipState State;
        public int AssignedSocketIndex = -1;
    }
}
