using System.Collections.Generic;
using System.Numerics;

namespace IDLab.Core.Model
{
    /// <summary>
    /// Static, data-only description of a kind of equipment (what the inventory lists).
    /// The front-end decides how it looks via <see cref="VisualKey"/>; the core decides how it
    /// behaves via <see cref="Components"/>.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public EquipmentCategory Category { get; set; }
        public Discipline Disciplines { get; set; }

        /// <summary>Which view/prefab builder the front-end should use ("beaker", "burner", ...).</summary>
        public string VisualKey { get; set; }

        /// <summary>Approximate bounding box (metres) used for drag offsets and inventory thumbnails.</summary>
        public Vector3 BoundsSize { get; set; }

        /// <summary>Optional tint used by the front-end for labels/caps (e.g. reagent bottle cap colour).</summary>
        public ColorRGBA AccentColor { get; set; } = new ColorRGBA(0.6f, 0.7f, 0.8f);

        public List<SnapPointDefinition> SnapPoints { get; } = new List<SnapPointDefinition>();
        public List<ComponentSpec> Components { get; } = new List<ComponentSpec>();

        public SnapPointDefinition FindSnap(string id)
        {
            foreach (var snap in SnapPoints)
            {
                if (snap.Id == id)
                {
                    return snap;
                }
            }

            return null;
        }

        public override string ToString() => Id;
    }
}
