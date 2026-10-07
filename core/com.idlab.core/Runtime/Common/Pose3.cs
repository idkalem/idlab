using System.Numerics;

namespace IDLab.Core
{
    /// <summary>World-space position + orientation of a piece of equipment (metres, Y up).</summary>
    public struct Pose3
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public Pose3(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public static Pose3 Identity => new Pose3(Vector3.Zero, Quaternion.Identity);

        public static Pose3 At(float x, float y, float z) => new Pose3(new Vector3(x, y, z), Quaternion.Identity);

        public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);

        public Vector3 TransformPoint(Vector3 local) => Position + Vector3.Transform(local, Rotation);

        public Vector3 TransformDirection(Vector3 local) => Vector3.Transform(local, Rotation);

        /// <summary>Angle in degrees between the object's local up axis and world up.</summary>
        public double TiltDegrees
        {
            get
            {
                double dot = Clamp(Vector3.Dot(Vector3.Normalize(Up), Vector3.UnitY), -1.0, 1.0);
                return System.Math.Acos(dot) * 180.0 / System.Math.PI;
            }
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }
}
