using System.Numerics;

namespace IDLab.Core.Model
{
    /// <summary>A named attachment point in an equipment's local space (metres).</summary>
    public sealed class SnapPointDefinition
    {
        public string Id { get; }
        public SnapKind Kind { get; }
        public Vector3 LocalPosition { get; }

        public SnapPointDefinition(string id, SnapKind kind, Vector3 localPosition)
        {
            Id = id;
            Kind = kind;
            LocalPosition = localPosition;
        }

        public SnapPointDefinition(string id, SnapKind kind, float x, float y, float z)
            : this(id, kind, new Vector3(x, y, z))
        {
        }

        public override string ToString() => $"{Id} ({Kind})";
    }
}
