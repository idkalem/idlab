using System.Numerics;
using IDLab.Core.Model;

namespace IDLab.Core.Interaction
{
    public static class SnapRules
    {
        /// <summary>Can a snap of kind <paramref name="moving"/> (on the dragged item) attach to <paramref name="target"/>?</summary>
        public static bool AreCompatible(SnapKind moving, SnapKind target)
        {
            switch (moving)
            {
                case SnapKind.ContainerBase: return target == SnapKind.HeatSurface;
                case SnapKind.ProbeTip: return target == SnapKind.ProbePort;
                case SnapKind.WireEnd: return target == SnapKind.Terminal;
                case SnapKind.RailMount: return target == SnapKind.RailMount;
                default: return false;
            }
        }
    }

    public struct SnapCandidate
    {
        public EquipmentInstance Target;
        public SnapPointDefinition MovingSnap;
        public SnapPointDefinition TargetSnap;

        /// <summary>Pose the moving item must take so both snap points coincide.</summary>
        public Pose3 SnappedPose;
        public float Distance;
    }

    /// <summary>
    /// Finds the best free, compatible snap point near a dragged item. Pure geometry so it is identical on
    /// every front-end and unit-testable; the 3D engine only previews and applies the result.
    /// </summary>
    public sealed class SnapSystem
    {
        private readonly LabWorld _world;

        public float DefaultRadius { get; set; } = 0.06f;

        public SnapSystem(LabWorld world)
        {
            _world = world;
        }

        public bool TryFindSnap(EquipmentInstance moving, Pose3 candidatePose, out SnapCandidate result) =>
            TryFindSnap(moving, candidatePose, DefaultRadius, out result);

        public bool TryFindSnap(EquipmentInstance moving, Pose3 candidatePose, float radius, out SnapCandidate result) =>
            Search(moving, candidatePose, radius, 0f, out result);

        /// <summary>
        /// "Drop onto" query used while dragging: items hover above whatever is under them, so a snap matches
        /// when it is within <paramref name="horizontalRadius"/> in the XZ plane and up to <paramref name="maxDrop"/>
        /// below the dragged snap point (e.g. a thermometer hovering above a beaker drops into its probe port).
        /// </summary>
        public bool TryFindDropSnap(EquipmentInstance moving, Pose3 candidatePose, float horizontalRadius, float maxDrop, out SnapCandidate result) =>
            Search(moving, candidatePose, horizontalRadius, maxDrop, out result);

        private bool Search(EquipmentInstance moving, Pose3 candidatePose, float radius, float maxDrop, out SnapCandidate result)
        {
            const float aboveTolerance = 0.02f; // a dragged snap may sit slightly below its target
            result = default;
            float best = radius;
            bool found = false;

            foreach (var movingSnap in moving.Definition.SnapPoints)
            {
                Vector3 movingWorld = candidatePose.TransformPoint(movingSnap.LocalPosition);
                foreach (var other in _world.Instances)
                {
                    if (other == moving || IsRelated(moving, other))
                    {
                        continue;
                    }

                    foreach (var targetSnap in other.Definition.SnapPoints)
                    {
                        if (!SnapRules.AreCompatible(movingSnap.Kind, targetSnap.Kind) || _world.IsSnapOccupied(other, targetSnap.Id))
                        {
                            continue;
                        }

                        Vector3 targetWorld = other.Pose.TransformPoint(targetSnap.LocalPosition);
                        float d;
                        if (maxDrop > 0f)
                        {
                            float drop = movingWorld.Y - targetWorld.Y;
                            if (drop < -aboveTolerance || drop > maxDrop)
                            {
                                continue;
                            }

                            float dx = movingWorld.X - targetWorld.X;
                            float dz = movingWorld.Z - targetWorld.Z;
                            d = (float)System.Math.Sqrt(dx * dx + dz * dz);
                        }
                        else
                        {
                            d = Vector3.Distance(movingWorld, targetWorld);
                        }

                        if (d < best)
                        {
                            best = d;
                            found = true;
                            // Stand upright, aligned with the host, then offset so the snap points coincide.
                            Quaternion rotation = other.Pose.Rotation;
                            Vector3 position = targetWorld - Vector3.Transform(movingSnap.LocalPosition, rotation);
                            result = new SnapCandidate
                            {
                                Target = other,
                                MovingSnap = movingSnap,
                                TargetSnap = targetSnap,
                                SnappedPose = new Pose3(position, rotation),
                                Distance = d,
                            };
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>Applies a snap: moves the item, records the connection in the world.</summary>
        public Connection Apply(EquipmentInstance moving, SnapCandidate candidate)
        {
            _world.DetachFromHost(moving);
            moving.Pose = candidate.SnappedPose;
            return _world.Connect(moving, candidate.MovingSnap.Id, candidate.Target, candidate.TargetSnap.Id);
        }

        /// <summary>Items already resting on/in the moving item travel with it and must not be snap targets.</summary>
        private bool IsRelated(EquipmentInstance moving, EquipmentInstance other)
        {
            for (var host = _world.GetHost(other); host != null; host = _world.GetHost(host))
            {
                if (host == moving)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
