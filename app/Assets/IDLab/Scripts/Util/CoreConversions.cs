using IDLab.Core;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace IDLab.App
{
    /// <summary>
    /// Bridges core (System.Numerics) and Unity math types. Both use Y-up and the same quaternion
    /// rotation operator, so components map 1:1.
    /// </summary>
    public static class CoreConversions
    {
        public static Vector3 ToUnity(this NVector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static NVector3 ToCore(this Vector3 v) => new NVector3(v.x, v.y, v.z);
        public static Quaternion ToUnity(this NQuaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
        public static NQuaternion ToCore(this Quaternion q) => new NQuaternion(q.x, q.y, q.z, q.w);
        public static Color ToUnity(this ColorRGBA c) => new Color(c.R, c.G, c.B, c.A);
        public static Pose3 ToCorePose(this Transform t) => new Pose3(t.position.ToCore(), t.rotation.ToCore());

        public static string ToHex(this Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
