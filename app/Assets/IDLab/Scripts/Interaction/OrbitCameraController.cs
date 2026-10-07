using UnityEngine;

namespace IDLab.App.Interaction
{
    /// <summary>
    /// Orbit / zoom / pan camera around the bench. Works with mouse (right-drag, wheel) and touch
    /// (two-finger drag, pinch). One-finger drag on empty space is routed here by the DragController.
    /// </summary>
    public sealed class OrbitCameraController : MonoBehaviour
    {
        public Vector3 Target = new Vector3(0f, 0.08f, 0.02f);
        public float Distance = 0.85f;
        public float Yaw = 0f;
        public float Pitch = 30f;
        public float MinDistance = 0.22f;
        public float MaxDistance = 2.2f;
        public float MinPitch = 6f;
        public float MaxPitch = 85f;
        public Vector2 TargetBoundsXZ = new Vector2(0.7f, 0.35f);

        private float _shake;
        private Camera _camera;

        public Camera Camera => _camera;

        /// <summary>Horizontal forward of the camera — the axis containers tilt around when pouring.</summary>
        public Vector3 FlatForward
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void Orbit(Vector2 screenDelta)
        {
            Yaw += screenDelta.x * 0.25f;
            Pitch = Mathf.Clamp(Pitch - screenDelta.y * 0.2f, MinPitch, MaxPitch);
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        private void LateUpdate()
        {
            if (PointerInput.OrbitHeld)
            {
                Orbit(PointerInput.OrbitDelta);
            }

            float zoom = PointerInput.Zoom;
            if (Mathf.Abs(zoom) > 1e-4f)
            {
                Distance = Mathf.Clamp(Distance * (1f - zoom), MinDistance, MaxDistance);
            }

            Target.x = Mathf.Clamp(Target.x, -TargetBoundsXZ.x, TargetBoundsXZ.x);
            Target.z = Mathf.Clamp(Target.z, -TargetBoundsXZ.y, TargetBoundsXZ.y);

            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 position = Target - rotation * Vector3.forward * Distance;
            if (_shake > 0.0001f)
            {
                position += Random.insideUnitSphere * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 0.08f);
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
