using System.Collections.Generic;
using System.Text;
using IDLab.App.Rendering;
using IDLab.Core.Model;
using UnityEngine;

namespace IDLab.App.Views
{
    /// <summary>
    /// Unity-side representation of one <see cref="EquipmentInstance"/>. The view owns the GameObject,
    /// colliders and rigid body (Unity/PhysX does the macro rigid-body physics); the core owns the science.
    /// Data flow per frame: transform → core pose (PushPoseToCore) → core step → visuals (SyncVisuals).
    /// Local origin = centre of the base, matching the core's snap-point coordinates.
    /// </summary>
    public abstract class EquipmentView : MonoBehaviour
    {
        private readonly List<Collider> _colliders = new List<Collider>();
        private float _tilt;

        public EquipmentInstance Instance { get; private set; }
        public LabApp App { get; private set; }
        public Rigidbody Body { get; private set; }

        /// <summary>The view this one is snapped onto (and parented under), if any.</summary>
        public EquipmentView HostView { get; private set; }

        public bool IsDragging { get; set; }

        /// <summary>Heading around world Y (degrees).</summary>
        public float Yaw { get; set; }

        /// <summary>Pour tilt (degrees) about the camera-facing axis. Non-zero tilt keeps the item "in hand".</summary>
        public float TiltDegrees
        {
            get => _tilt;
            set => _tilt = Mathf.Clamp(value, 0f, MaxTilt);
        }

        public virtual float MaxTilt => 0f;
        public bool CanTilt => MaxTilt > 0f;

        /// <summary>Held in the air by the (virtual) hand: kinematic, not snapped.</summary>
        public bool IsHeld => IsDragging || (TiltDegrees > 0.5f && HostView == null);

        public IReadOnlyList<Collider> Colliders => _colliders;

        public string DisplayName => $"{Instance.Definition.DisplayName} #{Instance.Id}";

        public void Initialize(LabApp app, EquipmentInstance instance)
        {
            App = app;
            Instance = instance;
            Yaw = 0f;
            name = $"{instance.Definition.Id}#{instance.Id}";
            transform.SetPositionAndRotation(instance.Pose.Position.ToUnity(), instance.Pose.Rotation.ToUnity());
            BuildVisuals();
            GetComponentsInChildren(true, _colliders);
            _colliders.RemoveAll(c => c.GetComponentInParent<EquipmentView>() != this);

            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = Mathf.Max(0.05f, MassKg);
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // PhET-style usability: equipment stays upright unless the student deliberately tilts it.
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        protected abstract void BuildVisuals();

        protected virtual float MassKg => 0.2f;

        /// <summary>Update renderers/particles from core state. Called after the simulation step.</summary>
        public virtual void SyncVisuals(float deltaTime)
        {
        }

        /// <summary>Appends live readouts for the inspector panel.</summary>
        public virtual void Describe(StringBuilder sb)
        {
            sb.AppendLine(Instance.Definition.Description);
        }

        public void PushPoseToCore() => Instance.Pose = transform.ToCorePose();

        /// <summary>Applies yaw + tilt about a world axis (usually the camera's horizontal forward).</summary>
        public void ApplyOrientation(Vector3 tiltAxisWorld)
        {
            var rotation = Quaternion.AngleAxis(TiltDegrees, tiltAxisWorld) * Quaternion.Euler(0f, Yaw, 0f);
            transform.rotation = rotation;
        }

        public Bounds WorldBounds()
        {
            // Renderer bounds (not collider bounds) so the result is current right after a transform change.
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.enabled && r.GetComponent<TextMesh>() == null)
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        // ------------------------------------------------------------ physics state

        public void SetKinematic(bool kinematic)
        {
            Body.isKinematic = kinematic;
            Body.detectCollisions = true;
        }

        public void AttachTo(EquipmentView host)
        {
            DetachFromHost();
            HostView = host;
            transform.SetParent(host.transform, true);
            SetKinematic(true);
            SetCollisionsWith(host, false);
        }

        public void DetachFromHost()
        {
            if (HostView == null)
            {
                return;
            }

            SetCollisionsWith(HostView, true);
            HostView = null;
            transform.SetParent(App.EquipmentRoot, true);
        }

        private void SetCollisionsWith(EquipmentView other, bool enabled)
        {
            foreach (var a in _colliders)
            {
                foreach (var b in other.Colliders)
                {
                    if (a != null && b != null)
                    {
                        Physics.IgnoreCollision(a, b, !enabled);
                    }
                }
            }
        }

        // ------------------------------------------------------------ building helpers

        protected GameObject Part(string partName, Mesh mesh, Material material, Vector3 localPosition, Transform parent = null)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return go;
        }

        protected GameObject Box(string partName, Vector3 size, Material material, Vector3 localCentre, bool collider = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
            {
                Destroy(go.GetComponent<Collider>());
            }

            return go;
        }

        protected BoxCollider AddBoxCollider(Vector3 size, Vector3 centre)
        {
            var c = gameObject.AddComponent<BoxCollider>();
            c.size = size;
            c.center = centre;
            return c;
        }

        protected TextMesh FloatingLabel(string text, Vector3 localPosition, float characterSize, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            var tm = go.AddComponent<TextMesh>();
            tm.font = UI.UIFactory.Font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.AddComponent<Billboard>();
            return tm;
        }

        protected static Material Metal(Color c) => MaterialFactory.Opaque(c, 0.7f, 0.8f);
        protected static Material Plastic(Color c) => MaterialFactory.Opaque(c, 0.45f, 0f);
    }

    /// <summary>Keeps labels and readouts facing the camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
            }
        }
    }
}
