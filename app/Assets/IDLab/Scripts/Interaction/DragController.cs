using IDLab.App.Rendering;
using IDLab.App.Views;
using IDLab.Core.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IDLab.App.Interaction
{
    /// <summary>
    /// Pointer state machine for the 3D bench:
    ///   press on equipment → (move past threshold) drag it, hovering just above whatever is beneath it;
    ///   release            → snap onto a compatible snap point below (burner gauze, beaker probe port),
    ///                         or drop with physics, or delete if released over the inventory panel;
    ///   press on a knob    → turn it;   press on empty space → orbit;   tap → select.
    /// Q / E (or the inspector slider) tilt the held container to pour.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DragController : MonoBehaviour
    {
        private enum Mode
        {
            Idle,
            UiCapture,
            Pending,
            Orbit,
            DragKnob,
            DragEquipment,
        }

        private const float Lift = 0.025f;
        private const float SnapHorizontalRadius = 0.045f;
        private const float SnapMaxDrop = 0.4f;
        private const float TiltSpeed = 80f; // deg/s
        private const float BenchHalfX = 0.85f;
        private const float BenchHalfZ = 0.4f;

        private LabApp _app;
        private Mode _mode;
        private Vector2 _pressPosition;
        private EquipmentView _pendingView;
        private KnobInteractable _pendingKnob;
        private KnobInteractable _knob;
        private EquipmentView _dragged;
        private Vector3 _grabOffset;
        private GameObject _snapMarker;
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        public bool IsDraggingEquipment => _mode == Mode.DragEquipment;
        public KnobInteractable ActiveKnob => _mode == Mode.DragKnob ? _knob : null;

        public void Initialize(LabApp app)
        {
            _app = app;
            _snapMarker = new GameObject("SnapMarker");
            _snapMarker.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ring(0.018f, 0.024f, 0.0015f);
            var r = _snapMarker.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFactory.Additive(new Color(0.2f, 1f, 0.6f, 0.9f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _snapMarker.SetActive(false);
        }

        /// <summary>Called by an inventory item when the student drags it out of the panel.</summary>
        public void BeginSpawnDrag(string definitionId)
        {
            Vector3 p = PointerOnPlane(0.05f) ?? new Vector3(0, 0.05f, 0);
            p.x = Mathf.Clamp(p.x, -BenchHalfX, BenchHalfX);
            p.z = Mathf.Clamp(p.z, -BenchHalfZ, BenchHalfZ);
            var view = _app.Spawn(definitionId, p);
            BeginDrag(view, Vector3.zero);
        }

        private void Update()
        {
            if (_app == null)
            {
                return;
            }

            UpdateTilt();

            switch (_mode)
            {
                case Mode.Idle:
                    if (PointerInput.Pressed)
                    {
                        if (IsPointerOverUi())
                        {
                            _mode = Mode.UiCapture;
                        }
                        else
                        {
                            Pick();
                        }
                    }

                    break;

                case Mode.UiCapture:
                    if (PointerInput.Released || !PointerInput.Held)
                    {
                        _mode = Mode.Idle;
                    }

                    break;

                case Mode.Pending:
                    if (PointerInput.Released || !PointerInput.Held)
                    {
                        _app.Select(_pendingKnob != null ? _pendingKnob.GetComponentInParent<EquipmentView>() : _pendingView);
                        _mode = Mode.Idle;
                    }
                    else if ((PointerInput.Position - _pressPosition).magnitude > DragThresholdPixels)
                    {
                        if (_pendingKnob != null)
                        {
                            _knob = _pendingKnob;
                            _app.Select(_knob.GetComponentInParent<EquipmentView>());
                            _mode = Mode.DragKnob;
                        }
                        else if (_pendingView != null)
                        {
                            BeginDrag(_pendingView, GrabOffsetFor(_pendingView));
                        }
                        else
                        {
                            _mode = Mode.Orbit;
                        }
                    }

                    break;

                case Mode.Orbit:
                    _app.CameraRig.Orbit(PointerInput.Delta);
                    if (PointerInput.Released || !PointerInput.Held)
                    {
                        _mode = Mode.Idle;
                    }

                    break;

                case Mode.DragKnob:
                    _knob.ApplyDrag(PointerInput.Delta);
                    if (PointerInput.Released || !PointerInput.Held)
                    {
                        _mode = Mode.Idle;
                    }

                    break;

                case Mode.DragEquipment:
                    if (_dragged == null)
                    {
                        _mode = Mode.Idle;
                        break;
                    }

                    UpdateDrag();
                    if (PointerInput.Released || !PointerInput.Held)
                    {
                        EndDrag();
                    }

                    break;
            }

            if (PointerInput.DeletePressed && _app.Selected != null && _mode == Mode.Idle)
            {
                _app.Remove(_app.Selected);
            }

            UpdateHeldItems();
        }

        private float DragThresholdPixels => 8f * (Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f);

        private void Pick()
        {
            _pressPosition = PointerInput.Position;
            _pendingView = null;
            _pendingKnob = null;
            _mode = Mode.Pending;

            Ray ray = _app.MainCamera.ScreenPointToRay(PointerInput.Position);
            int count = Physics.RaycastNonAlloc(ray, _hits, 10f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance >= best)
                {
                    continue;
                }

                var view = _hits[i].collider.GetComponentInParent<EquipmentView>();
                if (view == null)
                {
                    continue;
                }

                best = _hits[i].distance;
                _pendingView = view;
                _pendingKnob = _hits[i].collider.GetComponent<KnobInteractable>();
            }
        }

        private void BeginDrag(EquipmentView view, Vector3 grabOffset)
        {
            view.IsDragging = true; // set first: the Disconnected handler must not drop it with physics
            if (view.HostView != null)
            {
                _app.World.DetachFromHost(view.Instance); // LabApp un-parents the view on the Disconnected event
            }

            view.SetKinematic(true);
            _dragged = view;
            _grabOffset = grabOffset;
            _mode = Mode.DragEquipment;
            _app.Select(view);
        }

        private Vector3 GrabOffsetFor(EquipmentView view)
        {
            Vector3 pivot = view.transform.position;
            Vector3? p = PointerOnPlane(pivot.y);
            if (p == null)
            {
                return Vector3.zero;
            }

            Vector3 offset = pivot - p.Value;
            offset.y = 0;
            return Vector3.ClampMagnitude(offset, 0.1f);
        }

        private void UpdateDrag()
        {
            Transform t = _dragged.transform;
            _dragged.ApplyOrientation(_app.CameraRig.FlatForward);

            Vector3? onPlane = PointerOnPlane(t.position.y);
            Vector3 target = t.position;
            if (onPlane.HasValue)
            {
                target = onPlane.Value + _grabOffset;
            }

            target.x = Mathf.Clamp(target.x, -BenchHalfX, BenchHalfX);
            target.z = Mathf.Clamp(target.z, -BenchHalfZ, BenchHalfZ);

            // Hover just above the highest thing under the item's footprint.
            Bounds b = _dragged.WorldBounds();
            float bottomOffset = t.position.y - b.min.y;
            Vector3 footprintCentre = new Vector3(target.x + (b.center.x - t.position.x), 0, target.z + (b.center.z - t.position.z));
            float surface = HighestSurfaceBelow(_dragged, footprintCentre, b.extents);
            float hoverY = surface + bottomOffset + Lift;
            target.y = Mathf.Lerp(t.position.y, hoverY, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            t.position = target;

            // Snap preview.
            _dragged.PushPoseToCore();
            if (_dragged.TiltDegrees < 0.5f &&
                _app.Snaps.TryFindDropSnap(_dragged.Instance, _dragged.Instance.Pose, SnapHorizontalRadius, SnapMaxDrop, out var candidate))
            {
                _snapMarker.SetActive(true);
                _snapMarker.transform.position = candidate.Target.Pose.TransformPoint(candidate.TargetSnap.LocalPosition).ToUnity() + Vector3.up * 0.001f;
            }
            else
            {
                _snapMarker.SetActive(false);
            }
        }

        private void EndDrag()
        {
            var view = _dragged;
            _dragged = null;
            _mode = Mode.Idle;
            _snapMarker.SetActive(false);
            view.IsDragging = false;

            if (_app.Hud != null && _app.Hud.IsOverInventory(PointerInput.Position))
            {
                _app.Remove(view); // dropped back into the inventory
                return;
            }

            view.PushPoseToCore();
            if (view.TiltDegrees < 0.5f &&
                _app.Snaps.TryFindDropSnap(view.Instance, view.Instance.Pose, SnapHorizontalRadius, SnapMaxDrop, out SnapCandidate candidate))
            {
                _app.Snap(view, candidate);
            }
            else if (!view.IsHeld)
            {
                view.SetKinematic(false); // fall onto the bench
            }
        }

        private void UpdateTilt()
        {
            var view = _dragged != null ? _dragged : _app.Selected;
            float axis = PointerInput.TiltAxis;
            if (view == null || !view.CanTilt || Mathf.Abs(axis) < 0.01f || view.HostView != null)
            {
                return;
            }

            if (!view.IsHeld && axis > 0)
            {
                view.SetKinematic(true); // lift it into the hand before tipping
            }

            view.TiltDegrees += axis * TiltSpeed * Time.deltaTime;
        }

        /// <summary>Items held in the air (tilted for pouring) keep their orientation; at zero tilt they drop.</summary>
        private void UpdateHeldItems()
        {
            foreach (var view in _app.Views)
            {
                if (view.IsDragging || view.HostView != null || !view.CanTilt)
                {
                    continue;
                }

                if (view.TiltDegrees > 0.5f)
                {
                    view.SetKinematic(true);
                    view.ApplyOrientation(_app.CameraRig.FlatForward);
                    float lowest = view.WorldBounds().min.y;
                    if (lowest < 0.002f)
                    {
                        view.transform.position += Vector3.up * (0.002f - lowest); // tip around the rim, not into the bench
                    }
                }
                else if (view.Body.isKinematic)
                {
                    view.TiltDegrees = 0f;
                    view.ApplyOrientation(_app.CameraRig.FlatForward);
                    view.SetKinematic(false);
                }
            }
        }

        private float HighestSurfaceBelow(EquipmentView view, Vector3 centreXZ, Vector3 extents)
        {
            var origin = new Vector3(centreXZ.x, 3f, centreXZ.z);
            var halfExtents = new Vector3(Mathf.Max(0.004f, extents.x * 0.9f), 0.001f, Mathf.Max(0.004f, extents.z * 0.9f));
            int count = Physics.BoxCastNonAlloc(origin, halfExtents, Vector3.down, _hits, Quaternion.identity, 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                var hitTransform = _hits[i].collider.transform;
                if (hitTransform == view.transform || hitTransform.IsChildOf(view.transform))
                {
                    continue;
                }

                if (_hits[i].distance > 0f)
                {
                    best = Mathf.Max(best, _hits[i].point.y);
                }
            }

            return best;
        }

        private Vector3? PointerOnPlane(float y)
        {
            Ray ray = _app.MainCamera.ScreenPointToRay(PointerInput.Position);
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
            {
                return null;
            }

            float t = (y - ray.origin.y) / ray.direction.y;
            return t > 0 ? ray.origin + ray.direction * t : (Vector3?)null;
        }

        private static bool IsPointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null)
            {
                return false;
            }

            return PointerInput.PointerId >= 0 ? es.IsPointerOverGameObject(PointerInput.PointerId) : es.IsPointerOverGameObject();
        }
    }
}
