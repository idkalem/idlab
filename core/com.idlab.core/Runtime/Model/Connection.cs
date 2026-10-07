namespace IDLab.Core.Model
{
    /// <summary>
    /// A snap between two instances: <see cref="Child"/> (the thing that was dropped) rests on/in
    /// <see cref="Host"/>. e.g. beaker.base → burner.heat_surface, thermometer.tip → beaker.probe_port.
    /// </summary>
    public sealed class Connection
    {
        public int Id { get; }
        public EquipmentInstance Child { get; }
        public string ChildSnapId { get; }
        public EquipmentInstance Host { get; }
        public string HostSnapId { get; }

        internal Connection(int id, EquipmentInstance child, string childSnapId, EquipmentInstance host, string hostSnapId)
        {
            Id = id;
            Child = child;
            ChildSnapId = childSnapId;
            Host = host;
            HostSnapId = hostSnapId;
        }

        public bool Involves(EquipmentInstance instance) => Child == instance || Host == instance;

        public bool Uses(EquipmentInstance instance, string snapId) =>
            (Child == instance && ChildSnapId == snapId) || (Host == instance && HostSnapId == snapId);

        public EquipmentInstance Other(EquipmentInstance instance) => Child == instance ? Host : Child;

        public override string ToString() => $"{Child}.{ChildSnapId} → {Host}.{HostSnapId}";
    }
}
