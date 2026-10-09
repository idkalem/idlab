using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using IDLab.Core;
using IDLab.Core.Components;
using IDLab.Core.Events;
using IDLab.Core.Interaction;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Front-end state of one piece of equipment on the bench.</summary>
    internal sealed class Item
    {
        public EquipmentInstance Inst;
        public Item Host;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.Identity;
        public float Tilt;           // degrees; > 0 keeps the item "in hand" for pouring
        public float VelocityY;
        public bool Dragging;
        public Vector3? Home;        // bench spot to return to after an assisted pour
        public Item PourInto;        // container this item is being held over while tilted
        public float VisualTilt;     // the tilt actually shown; eases toward Tilt so pouring is animated
        public double AutoPourMl = -1;   // > 0: pour this much automatically, then put the container back
        public double AutoPourStart;     // contents (mL) when the automatic pour began

        public EquipmentDefinition Def => Inst.Definition;
        public LiquidContainer Container => Inst.Get<LiquidContainer>();
        public HeatSource Heat => Inst.Get<HeatSource>();
        public Probe Probe => Inst.Get<Probe>();
        public Stopcock Tap => Inst.Get<Stopcock>();
        public bool CanTilt => Container != null && Tap == null;
        public bool IsHeld => Dragging || (Tilt > 0.5f && Host == null);
        public Vector3 Size => Def.BoundsSize == Vector3.Zero ? new Vector3(0.05f) : Def.BoundsSize;
        public Vector3 Position { get => Inst.Pose.Position; set => Inst.Pose = new Pose3(value, Inst.Pose.Rotation); }
        public Quaternion Rotation { get => Inst.Pose.Rotation; set => Inst.Pose = new Pose3(Inst.Pose.Position, value); }

        /// <summary>World position of this item's turnable control (gas valve, hot-plate dial, burette tap), if any.</summary>
        public Vector3? KnobPosition
        {
            get
            {
                switch (Def.VisualKey)
                {
                    case "burner": return Inst.Pose.TransformPoint(new Vector3(0, 0.009f, -0.05f));
                    case "hotplate": return Inst.Pose.TransformPoint(new Vector3(-0.04f, 0.03f, -0.1f));
                    case "burette": return Inst.Pose.TransformPoint(new Vector3(0.02f, 0.245f, 0));
                    default: return null;
                }
            }
        }

        public double KnobValue
        {
            get => Tap?.Valve ?? Heat?.GasValve ?? 0;
            set
            {
                if (Tap != null) Tap.Valve = value;
                else if (Heat != null) Heat.GasValve = value;
            }
        }
    }

    /// <summary>
    /// The lab session: owns the core <see cref="LabWorld"/>, the bench items, simple kinematics
    /// (items rest on the highest thing below them, children ride on their hosts), drag / snap / tilt
    /// interaction and the orbit camera. No physics engine is needed for the board edition.
    /// </summary>
    internal sealed class Lab
    {
        public const float BenchHalfX = 0.85f, BenchHalfZ = 0.42f;
        private const float Lift = 0.025f;
        private const float SnapRadius = 0.065f;
        private const float SnapMaxDrop = 0.4f;

        private enum Mode { Idle, Pending, Orbit, Knob, Drag }

        private readonly Dictionary<int, Item> _items = new Dictionary<int, Item>();
        private Mode _mode;
        private Vector2 _pressPos;
        private Item _pending;
        private bool _pendingKnob;
        private Item _dragged;
        private Vector3 _grabOffset;
        private float _workHeight; // height of the last thing the pointer was over while dragging
        private Vector3 _dragStart;
        private float _shake;

        public LabWorld World { get; } = new LabWorld();
        public SnapSystem Snaps { get; }
        public ExperimentCatalog Experiments { get; } = ExperimentCatalog.CreateDefault();
        public ExperimentDefinition Experiment { get; private set; }
        public Effects Effects { get; } = new Effects();
        public Item Selected { get; set; }

        /// <summary>The item under the pointer (for name labels).</summary>
        public Item Hovered { get; private set; }
        public IEnumerable<Item> Items => _items.Values;
        public SnapCandidate? SnapPreview { get; private set; }

        // Orbit camera
        public float Yaw, Pitch = 24f, Distance = 1.3f;
        public Vector3 Target = new Vector3(0, 0.12f, 0.03f);
        public Camera3D Camera;
        public Rectangle Viewport; // free screen area between panels (camera centres the bench in it)

        public event Action<LabEvent, string> Notified;

        public Lab()
        {
            Snaps = new SnapSystem(World);
            World.InstanceAdded += i => _items[i.Id] = new Item { Inst = i };
            World.InstanceRemoved += i =>
            {
                _items.Remove(i.Id);
                Bubbles.Forget(i);
                if (Selected?.Inst == i) Selected = null;
                if (Hovered?.Inst == i) Hovered = null;
                if (_dragged?.Inst == i) { _dragged = null; _mode = Mode.Idle; }
            };
            World.Disconnected += c =>
            {
                var child = Find(c.Child);
                if (child != null && child.Host?.Inst == c.Host)
                {
                    child.Host = null;
                }
            };
            World.Events.Published += OnEvent;
            LoadExperiment(ExperimentCatalog.SandboxId);
        }

        public Item Find(EquipmentInstance i) => i != null && _items.TryGetValue(i.Id, out var it) ? it : null;

        public void LoadExperiment(string id)
        {
            Selected = null;
            World.Reset();
            Effects.Clear();
            Experiment = Experiments.Get(id);
            var spawned = new List<Item>();
            foreach (var (eid, x, z) in Experiment.InitialLayout)
            {
                spawned.Add(Spawn(eid, new Vector3(x, 0, z)));
            }

            // Items laid out on a snap spot (a flask under the burette) start attached to it.
            foreach (var item in spawned)
            {
                if (item.Host == null && Snaps.TryFindDropSnap(item.Inst, new Pose3(item.Position + new Vector3(0, 0.03f, 0), Quaternion.Identity), 0.03f, 0.4f, out var c))
                {
                    Snap(item, c);
                }
            }

            Titrations.Clear();
        }

        // ------------------------------------------------------------------ titration curve (pH vs added titrant)

        internal sealed class TitrationCurve
        {
            public readonly List<(double ml, double ph)> Points = new List<(double, double)>();
            public double AcidMl, TitrantMolarity;
            public EquipmentInstance Flask;
        }

        /// <summary>Per burette (instance id): pH of the flask below it against the volume delivered.</summary>
        public Dictionary<int, TitrationCurve> Titrations { get; } = new Dictionary<int, TitrationCurve>();

        private void RecordTitrations()
        {
            foreach (var item in _items.Values)
            {
                var tap = item.Tap;
                if (tap == null) continue;
                if (!Titrations.TryGetValue(item.Inst.Id, out var curve))
                {
                    curve = new TitrationCurve();
                    Titrations[item.Inst.Id] = curve;
                }

                if (tap.Target != null) curve.Flask = tap.Target;
                var flask = curve.Flask?.Get<LiquidContainer>();
                if (flask == null || !_items.ContainsKey(curve.Flask.Id) || double.IsNaN(flask.PH)) continue;
                double ml = tap.DeliveredMilliliters;
                if (curve.Points.Count > 0 && ml < curve.Points[curve.Points.Count - 1].ml - 0.01)
                {
                    curve.Points.Clear(); // counter was reset / burette refilled: start a new curve
                }

                if (curve.Points.Count == 0)
                {
                    curve.AcidMl = Math.Max(0, flask.LiquidVolumeMilliliters - ml);
                    var b = item.Container;
                    double naoh = b.Contents.Moles(IDLab.Core.Chemistry.SubstanceIds.SodiumHydroxide);
                    curve.TitrantMolarity = b.LiquidVolumeMilliliters > 0.5 ? naoh / (b.LiquidVolumeMilliliters / 1000.0) : 0.1;
                    curve.Points.Add((ml, flask.PH));
                }
                else if (ml - curve.Points[curve.Points.Count - 1].ml >= 0.04)
                {
                    curve.Points.Add((ml, flask.PH));
                }
            }
        }

        public IEnumerable<EquipmentDefinition> Inventory() =>
            Experiment.Equipment.Count == 0 ? World.Catalog.All : Experiment.Equipment.Select(World.Catalog.Get);

        public Item Spawn(string definitionId, Vector3 position)
        {
            var inst = World.Spawn(definitionId, new Pose3(position, Quaternion.Identity));
            return _items[inst.Id];
        }

        public void Remove(Item item)
        {
            if (item != null)
            {
                World.Remove(item.Inst);
            }
        }

        public void Snap(Item item, SnapCandidate c)
        {
            var host = Find(c.Target);
            Snaps.Apply(item.Inst, c);
            item.Tilt = 0;
            item.VelocityY = 0;
            item.Host = host;
            var inv = Quaternion.Inverse(host.Rotation);
            item.LocalPosition = Vector3.Transform(item.Position - host.Position, inv);
            item.LocalRotation = inv * item.Rotation;
        }

        public void Shake(float amount) => _shake = Math.Max(_shake, amount);

        // ------------------------------------------------------------------ per frame

        public void Update(float dt, bool pointerFree, bool overInventory)
        {
            UpdateCamera(dt);
            Hovered = pointerFree && _mode == Mode.Idle ? ItemUnderPointer() : null;
            HandlePointer(pointerFree, overInventory);
            HandleKeys(dt);

            foreach (var item in _items.Values.Where(i => i.Host == null).ToList())
            {
                DriveAutoPour(item, dt);
                UpdateKinematics(item, dt);
            }

            World.Advance(dt);
            RecordTitrations();
            Effects.Update(dt, this);
        }

        private static float Approach(float value, float target, float maxPerSecond, float dt)
        {
            float eased = value + (target - value) * (1f - MathF.Exp(-dt * 7f));
            float step = Math.Clamp(eased - value, -maxPerSecond * dt, maxPerSecond * dt);
            return value + step;
        }

        private static Vector3 MoveTowards(Vector3 from, Vector3 to, float dt)
        {
            var d = to - from;
            float dist = d.Length();
            if (dist < 1e-5f) return to;
            float speed = Math.Clamp(dist * 7f, 0.05f, 0.55f); // m/s: fast across the bench, gentle at the end
            return dist <= speed * dt ? to : from + d / dist * speed * dt;
        }

        private void UpdateKinematics(Item item, float dt)
        {
            if (!item.Dragging && item.Host == null)
            {
                if (item.Tilt > 0.5f || item.Home != null)
                {
                    float targetTilt = item.Tilt;
                    if (item.Tilt > 0.5f)
                    {
                        if (item.Home == null)
                        {
                            // Assisted pour: tipping a container near another one lifts it over that container.
                            item.Home = item.Position;
                            item.PourInto ??= NearestPourTarget(item, item.Position, 0.35f);
                        }

                        if (item.PourInto != null && _items.ContainsKey(item.PourInto.Inst.Id))
                        {
                            // Lift straight up first, then carry it over the target, and only tip it fully once it is there.
                            var desired = PourPosition(item, item.PourInto);
                            var p = item.Position;
                            float horizontal = new Vector2(desired.X - p.X, desired.Z - p.Z).Length();
                            // Travel a little above the pouring height so nothing passes through the target's glass.
                            float travelY = desired.Y + 0.05f;
                            var waypoint = horizontal <= 0.015f ? desired
                                : p.Y < travelY - 0.004f ? new Vector3(p.X, travelY, p.Z)
                                : new Vector3(desired.X, travelY, desired.Z);
                            item.Position = MoveTowards(p, waypoint, dt);
                            if (Vector3.Distance(item.Position, desired) > 0.012f) targetTilt = Math.Min(targetTilt, 12f);
                        }
                        else
                        {
                            var box = WorldBox(item);
                            float surface = SurfaceBelow(item, box);
                            if (box.Min.Y < surface + 0.002f)
                            {
                                item.Position += new Vector3(0, surface + 0.002f - box.Min.Y, 0);
                            }
                        }
                    }

                    item.VisualTilt = Approach(item.VisualTilt, targetTilt, 110f, dt);
                    item.Rotation = Orientation(item.VisualTilt);

                    if (item.Tilt <= 0.5f && item.Home != null && item.VisualTilt < 4f)
                    {
                        // Upright again: carry it back over its bench spot, then set it down.
                        item.Tilt = 0;
                        var home = item.Home.Value;
                        var p = item.Position;
                        float horizontal = new Vector2(home.X - p.X, home.Z - p.Z).Length();
                        var waypoint = horizontal > 0.004f ? new Vector3(home.X, Math.Max(p.Y, home.Y + 0.03f), home.Z) : home;
                        item.Position = MoveTowards(p, waypoint, dt);
                        if (Vector3.Distance(item.Position, home) < 0.0015f)
                        {
                            item.Position = home;
                            item.Home = null;
                            item.PourInto = null;
                            item.VisualTilt = 0;
                            item.Rotation = Quaternion.Identity;
                        }
                    }
                }
                else
                {
                    item.Tilt = 0;
                    item.PourInto = null;
                    item.VisualTilt = Approach(item.VisualTilt, 0, 110f, dt);
                    item.Rotation = item.VisualTilt > 0.2f ? Orientation(item.VisualTilt) : Quaternion.Identity;
                    float surface = SurfaceBelow(item, WorldBox(item));
                    var p = item.Position;
                    if (p.Y > surface + 1e-4f)
                    {
                        item.VelocityY -= 9.81f * dt;
                        p.Y = Math.Max(surface, p.Y + item.VelocityY * dt);
                        if (p.Y <= surface) item.VelocityY = 0;
                    }
                    else
                    {
                        p.Y = surface;
                        item.VelocityY = 0;
                    }

                    item.Position = p;
                }
            }

            foreach (var childInst in World.GetChildren(item.Inst).ToList())
            {
                var child = Find(childInst);
                if (child == null) continue;
                child.Position = item.Position + Vector3.Transform(child.LocalPosition, item.Rotation);
                child.Rotation = item.Rotation * child.LocalRotation;
                UpdateKinematics(child, dt);
            }
        }

        /// <summary>While set, the next tap on a container pours <see cref="PourPickAmount"/> from this item into it.</summary>
        public Item PourPickSource { get; set; }
        public double PourPickAmount { get; set; }

        /// <summary>Raised when a pour starts from a drop or a tap (source, target, mL).</summary>
        public event Action<Item, Item, double> AutoPourStarted;

        /// <summary>A container whose mouth is right below a dragged item that holds something pourable.</summary>
        private Item DropPourTarget(Item item)
        {
            var c = item.Container;
            if (c == null || !item.CanTilt || c.Contents.IsEmpty) return null;
            var box = WorldBox(item);
            var center = new Vector2((box.Min.X + box.Max.X) / 2, (box.Min.Z + box.Max.Z) / 2);
            Item best = null;
            float bestD = float.MaxValue;
            foreach (var other in _items.Values)
            {
                if (other == item || other.Container == null || IsDescendantOf(other, item) || IsDescendantOf(item, other)) continue;
                if (other.Tap != null) continue;
                var mouth = other.Container.MouthPosition;
                float d = Vector2.Distance(center, new Vector2(mouth.X, mouth.Z));
                if (d < (float)other.Container.MouthRadiusM + 0.045f && box.Min.Y > mouth.Y - 0.02f && d < bestD)
                {
                    bestD = d;
                    best = other;
                }
            }

            return best;
        }

        /// <summary>Starts an animated pour of <paramref name="milliliters"/> (∞ = everything) into the nearest container.</summary>
        public bool StartAutoPour(Item item, double milliliters)
        {
            var c = item.Container;
            if (c == null || !item.CanTilt || item.Host != null) return false;
            item.PourInto ??= NearestPourTarget(item, item.Home ?? item.Position, 0.35f);
            item.AutoPourMl = milliliters;
            item.AutoPourStart = c.LiquidVolumeMilliliters + c.SolidVolumeMilliliters;
            item.Tilt = Math.Max(item.Tilt, 25f);
            return true;
        }

        public void StopAutoPour(Item item)
        {
            if (item.AutoPourMl < 0) return;
            item.AutoPourMl = -1;
            item.Tilt = 0;
        }

        /// <summary>
        /// Automatic pour: tips the container a little further whenever nothing flows (the level drops as it empties)
        /// and eases back when the stream gets too strong, until the requested amount has left; then it is set back.
        /// </summary>
        private void DriveAutoPour(Item item, float dt)
        {
            if (item.AutoPourMl < 0) return;
            var c = item.Container;
            if (c == null || item.Dragging || item.Host != null)
            {
                item.AutoPourMl = -1;
                return;
            }

            double now = c.LiquidVolumeMilliliters + c.SolidVolumeMilliliters;
            double poured = item.AutoPourStart - now;
            double left = item.AutoPourMl - poured;
            if (left <= 0.05 || now < 0.15 || item.Tilt >= 134f && !c.IsPouring && item.VisualTilt > 130f)
            {
                StopAutoPour(item);
                return;
            }

            bool overTarget = item.PourInto == null || Vector3.Distance(item.Position, PourPosition(item, item.PourInto)) < 0.012f;
            if (!overTarget) return;
            double rate = c.PourRateLitersPerSecond * 1000;
            double wanted = c.Contents.HasLiquid ? Math.Clamp(left * 0.7, 1.5, c.MouthRadiusM > 0.03 ? 25 : 14) : Math.Clamp(left * 0.8, 0.4, 3);
            if (!c.IsPouring) item.Tilt = Math.Min(135f, item.Tilt + 32f * dt);
            else if (rate < wanted * 0.7) item.Tilt = Math.Min(135f, item.Tilt + 10f * dt);
            else if (rate > wanted * 1.5) item.Tilt = Math.Max(5f, item.Tilt - 14f * dt);
        }

        /// <summary>Lip (lowest rim point) relative to the item's origin for its current orientation.</summary>
        public Vector3 LipOffset(Item item)
        {
            var c = item.Container;
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, item.Rotation));
            var low = -Vector3.UnitY - Vector3.Dot(-Vector3.UnitY, up) * up;
            low = low.LengthSquared() > 1e-8f ? Vector3.Normalize(low) : Vector3.UnitX;
            return Vector3.Transform(c.LocalOffset + new Vector3(0, (float)c.InnerHeightM, 0), item.Rotation) + low * (float)c.MouthRadiusM;
        }

        /// <summary>Where to hold <paramref name="item"/> so its lip is just above the mouth of <paramref name="target"/>.</summary>
        public Vector3 PourPosition(Item item, Item target)
        {
            var mouth = target.Container.MouthPosition;
            return mouth + new Vector3(0, 0.035f, 0) - LipOffset(item);
        }

        /// <summary>Nearest other open container (horizontal distance from <paramref name="from"/> to its mouth).</summary>
        public Item NearestPourTarget(Item item, Vector3 from, float maxDistance)
        {
            Item best = null;
            float bestD = maxDistance;
            foreach (var other in _items.Values)
            {
                if (other == item || other.Container == null || IsDescendantOf(other, item) || IsDescendantOf(item, other)) continue;
                var m = other.Container.MouthPosition;
                float d = new Vector2(m.X - from.X, m.Z - from.Z).Length();
                if (other.Def.Category != EquipmentCategory.Glassware) d *= 2f; // prefer beakers over reagent bottles
                if (d < bestD)
                {
                    bestD = d;
                    best = other;
                }
            }

            return best;
        }

        /// <summary>Upright, then tipped about the camera's horizontal forward axis (pours toward screen-right).</summary>
        public Quaternion Orientation(float tiltDegrees)
        {
            var f = FlatForward;
            return Quaternion.CreateFromAxisAngle(f, tiltDegrees * MathF.PI / 180f);
        }

        public Vector3 FlatForward
        {
            get
            {
                var f = Camera.Target - Camera.Position;
                f.Y = 0;
                return f.LengthSquared() > 1e-6f ? Vector3.Normalize(f) : Vector3.UnitZ;
            }
        }

        /// <summary>Axis-aligned world box of the item's (rotated) bounds.</summary>
        public BoundingBox WorldBox(Item item)
        {
            var s = item.Size;
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -s.X / 2 : s.X / 2, (i & 2) == 0 ? 0 : s.Y, (i & 4) == 0 ? -s.Z / 2 : s.Z / 2);
                var w = item.Inst.Pose.TransformPoint(local);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }

            return new BoundingBox(min, max);
        }

        /// <summary>Collision boxes: most items are one box, a burette stand is a base plate, a rod and the tube.</summary>
        public IEnumerable<BoundingBox> Boxes(Item item)
        {
            if (item.Def.VisualKey != "burette")
            {
                yield return WorldBox(item);
                yield break;
            }

            BoundingBox Local(Vector3 centre, Vector3 size)
            {
                var a = item.Inst.Pose.TransformPoint(centre - size / 2);
                var b = item.Inst.Pose.TransformPoint(centre + size / 2);
                return new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
            }

            yield return Local(new Vector3(0, 0.006f, 0.02f), new Vector3(0.12f, 0.012f, 0.15f));
            yield return Local(new Vector3(0, 0.33f, 0.07f), new Vector3(0.016f, 0.66f, 0.016f));
            yield return Local(new Vector3(0, 0.39f, 0), new Vector3(0.022f, 0.36f, 0.022f));
            yield return Local(new Vector3(0, 0.5f, 0.035f), new Vector3(0.02f, 0.02f, 0.08f));
        }

        /// <summary>Top of the highest other item under the box's footprint (the bench is y = 0).</summary>
        private float SurfaceBelow(Item item, BoundingBox box)
        {
            float best = 0;
            foreach (var other in _items.Values)
            {
                if (other == item || IsDescendantOf(other, item))
                {
                    continue;
                }

                foreach (var b in Boxes(other))
                {
                    bool overlap = box.Min.X < b.Max.X && box.Max.X > b.Min.X && box.Min.Z < b.Max.Z && box.Max.Z > b.Min.Z;
                    if (overlap && b.Max.Y <= box.Min.Y + 0.03f && b.Max.Y > best)
                    {
                        best = b.Max.Y;
                    }
                }
            }

            return best;
        }

        private static bool IsDescendantOf(Item candidate, Item ancestor)
        {
            for (var h = candidate.Host; h != null; h = h.Host)
            {
                if (h == ancestor) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ camera

        private void UpdateCamera(float dt)
        {
            float wheel = GetMouseWheelMove();
            if (Math.Abs(wheel) > 0.01f && CheckCollisionPointRec(Ui.Mouse, Viewport))
            {
                Distance = Math.Clamp(Distance * (1f - wheel * 0.1f), 0.3f, 3.5f);
            }

            if (IsMouseButtonDown(MouseButton.Right))
            {
                Orbit(GetMouseDelta());
            }

            Pitch = Math.Clamp(Pitch, 2f, 80f);
            float yaw = Yaw * MathF.PI / 180f, pitch = Pitch * MathF.PI / 180f;
            var forward = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
            var position = Target - forward * Distance;

            // Shift the view so the bench centre lands in the middle of the free area between panels.
            float screenW = GetScreenWidth(), screenH = GetScreenHeight();
            float pixelsPerMeter = screenH / (2f * Distance * MathF.Tan(20f * MathF.PI / 180f));
            float dx = (Viewport.X + Viewport.Width / 2f - screenW / 2f) / pixelsPerMeter;
            float dy = (Viewport.Y + Viewport.Height / 2f - screenH / 2f) / pixelsPerMeter;
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Cross(right, forward);
            var shift = -right * dx + up * dy;

            if (_shake > 0.0001f)
            {
                shift += new Vector3(Rand() * _shake, Rand() * _shake, Rand() * _shake);
                _shake = Math.Max(0, _shake - dt * 0.05f);
            }

            Camera = new Camera3D(position + shift, Target + shift, Vector3.UnitY, 40f, CameraProjection.Perspective);
        }

        public void Orbit(Vector2 delta)
        {
            Yaw -= delta.X * 0.25f;
            Pitch += delta.Y * 0.2f;
        }

        public void ResetView()
        {
            Yaw = 0;
            Pitch = 24;
            Distance = 1.3f;
        }

        private static readonly Random Rng = new Random();
        private static float Rand() => (float)(Rng.NextDouble() * 2 - 1);

        // ------------------------------------------------------------------ pointer

        private void HandlePointer(bool pointerFree, bool overInventory)
        {
            switch (_mode)
            {
                case Mode.Idle:
                    if (Ui.Pressed && pointerFree)
                    {
                        Pick();
                    }

                    break;

                case Mode.Pending:
                    if (!Ui.Down)
                    {
                        _mode = Mode.Idle;
                        if (PourPickSource != null)
                        {
                            // "Pour into…": the tap chooses the receiving container.
                            var source = PourPickSource;
                            PourPickSource = null;
                            if (_pending != null && _pending != source && _pending.Container != null && !IsDescendantOf(_pending, source))
                            {
                                source.PourInto = _pending;
                                source.Home = null;
                                StartAutoPour(source, PourPickAmount);
                                AutoPourStarted?.Invoke(source, _pending, PourPickAmount);
                                Selected = source;
                                break;
                            }
                        }

                        Selected = _pending;
                    }
                    else if (Vector2.Distance(Ui.Mouse, _pressPos) > 10 * Ui.S)
                    {
                        if (_pending == null) _mode = Mode.Orbit;
                        else if (_pendingKnob) { Selected = _pending; _mode = Mode.Knob; }
                        else BeginDrag(_pending, GrabOffset(_pending));
                    }

                    break;

                case Mode.Orbit:
                    Orbit(GetMouseDelta());
                    if (!Ui.Down) _mode = Mode.Idle;
                    break;

                case Mode.Knob:
                    var d = GetMouseDelta();
                    if (_pending != null)
                    {
                        _pending.KnobValue = Math.Clamp(_pending.KnobValue + (d.X - d.Y) / (300f * Ui.S), 0, 1);
                    }

                    if (!Ui.Down) _mode = Mode.Idle;
                    break;

                case Mode.Drag:
                    if (_dragged == null) { _mode = Mode.Idle; break; }
                    UpdateDrag();
                    if (!Ui.Down) EndDrag(overInventory);
                    break;
            }
        }

        private void Pick()
        {
            _pressPos = Ui.Mouse;
            _pending = null;
            _pendingKnob = false;
            _mode = Mode.Pending;
            var ray = GetScreenToWorldRay(Ui.Mouse, Camera);
            float best = float.MaxValue;
            foreach (var item in _items.Values)
            {
                if (item.KnobPosition is Vector3 k)
                {
                    var knobBox = new BoundingBox(k - new Vector3(0.022f), k + new Vector3(0.022f));
                    var kh = GetRayCollisionBox(ray, knobBox);
                    // The knob sits inside the burner's bounds: give it priority so it can be turned.
                    if (kh.Hit && kh.Distance - 0.15f < best)
                    {
                        best = kh.Distance - 0.15f;
                        _pending = item;
                        _pendingKnob = true;
                    }
                }

                foreach (var box in Boxes(item))
                {
                    var hit = GetRayCollisionBox(ray, box);
                    // Thin probes and tubes get priority over the vessel or rack they stand in.
                    float distance = hit.Distance - (item.Probe != null || item.Host != null ? 0.05f : 0f);
                    if (hit.Hit && distance < best)
                    {
                        best = distance;
                        _pending = item;
                        _pendingKnob = false;
                    }
                }
            }
        }

        private Item ItemUnderPointer()
        {
            var ray = GetScreenToWorldRay(Ui.Mouse, Camera);
            Item found = null;
            float best = float.MaxValue;
            foreach (var item in _items.Values)
            {
                foreach (var box in Boxes(item))
                {
                    var hit = GetRayCollisionBox(ray, box);
                    float d = hit.Distance - (item.Probe != null || item.Host != null ? 0.05f : 0f);
                    if (hit.Hit && d < best)
                    {
                        best = d;
                        found = item;
                    }
                }
            }

            return found;
        }

        /// <summary>Starts dragging a brand-new item from the inventory.</summary>
        public void BeginSpawnDrag(string definitionId)
        {
            var p = PointerOnPlane(0.05f) ?? new Vector3(0, 0.05f, 0);
            p.X = Math.Clamp(p.X, -BenchHalfX, BenchHalfX);
            p.Z = Math.Clamp(p.Z, -BenchHalfZ, BenchHalfZ);
            var item = Spawn(definitionId, p);
            BeginDrag(item, Vector3.Zero);
        }

        private void BeginDrag(Item item, Vector3 grabOffset)
        {
            item.Dragging = true;
            if (item.Host != null)
            {
                World.DetachFromHost(item.Inst);
                item.Host = null;
            }

            _dragged = item;
            _grabOffset = grabOffset;
            _workHeight = item.Position.Y;
            _dragStart = item.Home ?? item.Position;
            Selected = item;
            _mode = Mode.Drag;
        }

        private Vector3 GrabOffset(Item item)
        {
            var p = PointerOnPlane(item.Position.Y);
            if (p == null) return Vector3.Zero;
            var o = item.Position - p.Value;
            o.Y = 0;
            return o.Length() > 0.1f ? Vector3.Normalize(o) * 0.1f : o;
        }

        private void UpdateDrag()
        {
            var item = _dragged;
            item.Rotation = Orientation(item.Tilt);

            // Aim at what the pointer is over (the top of a burner, a beaker...) so items climb onto things;
            // over empty space the item follows the finger at its current height (e.g. a bottle held up to pour).
            var ray = GetScreenToWorldRay(Ui.Mouse, Camera);
            Vector3? aim = null;
            float nearest = float.MaxValue;
            foreach (var other in _items.Values)
            {
                if (other == item || IsDescendantOf(other, item)) continue;
                foreach (var b in Boxes(other))
                {
                    var hit = GetRayCollisionBox(ray, b);
                    if (hit.Hit && hit.Distance < nearest)
                    {
                        nearest = hit.Distance;
                        aim = hit.Point;
                    }
                }
            }

            // A tipped container is held up at the working height (e.g. beside a beaker on the burner);
            // an upright item simply follows the pointer down onto the bench.
            bool held = item.Tilt > 0.5f;
            if (aim.HasValue) _workHeight = aim.Value.Y;
            aim ??= PointerOnPlane(held ? _workHeight : item.Position.Y);
            _grabOffset *= MathF.Exp(-GetFrameTime() * 6f); // ease the initial grab offset away
            var target = aim.HasValue ? aim.Value + _grabOffset : item.Position;
            target.X = Math.Clamp(target.X, -BenchHalfX, BenchHalfX);
            target.Z = Math.Clamp(target.Z, -BenchHalfZ, BenchHalfZ);

            // Hover just above whatever lies under the footprint.
            var current = item.Position;
            item.Position = new Vector3(target.X, current.Y, target.Z);
            var box = WorldBox(item);
            float bottomOffset = item.Position.Y - box.Min.Y;
            float hoverY = SurfaceBelowForHover(item, box) + bottomOffset + Lift;
            if (held) hoverY = Math.Max(hoverY, _workHeight + bottomOffset + 0.01f);
            float y = current.Y + (hoverY - current.Y) * (1f - MathF.Exp(-GetFrameTime() * 18f));
            item.Position = new Vector3(target.X, y, target.Z);
            item.VelocityY = 0;
            item.Home = null;
            item.PourInto = null;

            // While tipped, lock the lip onto a nearby container's mouth so pouring is easy on a touch screen.
            if (held)
            {
                var lip = item.Position + LipOffset(item);
                var pourTarget = NearestPourTarget(item, lip, 0.12f);
                if (pourTarget != null)
                {
                    item.Position = PourPosition(item, pourTarget);
                    item.PourInto = pourTarget;
                }
            }

            SnapPreview = null;
            if (item.Tilt < 0.5f && Snaps.TryFindDropSnap(item.Inst, item.Inst.Pose, SnapRadius, SnapMaxDrop, out var c))
            {
                SnapPreview = c;
            }
        }

        private float SurfaceBelowForHover(Item item, BoundingBox box)
        {
            float best = 0;
            foreach (var other in _items.Values)
            {
                if (other == item || IsDescendantOf(other, item)) continue;
                foreach (var b in Boxes(other))
                {
                    // Only things the item could sit on (not a burette tube hanging above it).
                    if (b.Min.Y > box.Min.Y + 0.02f) continue;
                    if (box.Min.X < b.Max.X && box.Max.X > b.Min.X && box.Min.Z < b.Max.Z && box.Max.Z > b.Min.Z)
                    {
                        best = Math.Max(best, b.Max.Y);
                    }
                }
            }

            return best;
        }

        private void EndDrag(bool overInventory)
        {
            var item = _dragged;
            _dragged = null;
            _mode = Mode.Idle;
            SnapPreview = null;
            item.Dragging = false;

            if (overInventory)
            {
                Remove(item);
                return;
            }

            if (item.Tilt < 0.5f && Snaps.TryFindDropSnap(item.Inst, item.Inst.Pose, SnapRadius, SnapMaxDrop, out var c))
            {
                Snap(item, c);
            }
            else if (item.Tilt < 0.5f && DropPourTarget(item) is Item into)
            {
                // Dropped on top of another container: pour into it (25 mL, or some of a solid), then go back.
                var cont = item.Container;
                double amount = cont.Contents.HasLiquid ? 25 : Math.Max(0.3, (cont.LiquidVolumeMilliliters + cont.SolidVolumeMilliliters) * 0.3);
                item.Home = new Vector3(_dragStart.X, 0, _dragStart.Z);
                item.PourInto = into;
                StartAutoPour(item, amount);
                AutoPourStarted?.Invoke(item, into, amount);
            }
            else if (item.Tilt >= 0.5f)
            {
                // Released while pouring: it stays in the (virtual) hand; at zero tilt it goes back to the bench.
                item.Home = new Vector3(_dragStart.X, 0, _dragStart.Z);
            }
        }

        /// <summary>False while another tab owns the keyboard (e.g. typing in the chemical search).</summary>
        public bool KeysEnabled { get; set; } = true;

        private void HandleKeys(float dt)
        {
            if (!KeysEnabled) return;
            var target = _dragged ?? Selected;
            float axis = (IsKeyDown(KeyboardKey.E) ? 1 : 0) - (IsKeyDown(KeyboardKey.Q) ? 1 : 0);
            if (target != null && target.CanTilt && target.Host == null && axis != 0)
            {
                target.Tilt = Math.Clamp(target.Tilt + axis * 80f * dt, 0f, 130f);
            }

            if ((IsKeyPressed(KeyboardKey.Delete) || IsKeyPressed(KeyboardKey.Backspace)) && Selected != null && _mode == Mode.Idle)
            {
                Remove(Selected);
            }
        }

        public Vector3? PointerOnPlane(float y)
        {
            var ray = GetScreenToWorldRay(Ui.Mouse, Camera);
            if (Math.Abs(ray.Direction.Y) < 1e-4f) return null;
            float t = (y - ray.Position.Y) / ray.Direction.Y;
            return t > 0 ? ray.Position + ray.Direction * t : (Vector3?)null;
        }

        public bool IsBusy => _mode == Mode.Drag || _mode == Mode.Knob || _mode == Mode.Orbit;

        // ------------------------------------------------------------------ events

        private void OnEvent(LabEvent e)
        {
            var owner = World.Find(e.InstanceId);
            string name = owner != null ? Tr.Name(owner.Definition) : "";
            Effects.OnEvent(e, this);
            if (e.Type == LabEventType.Explosion)
            {
                Shake(0.006f + 0.012f * (float)e.Severity);
            }

            Notified?.Invoke(e, Tr.Event(e, name));
        }
    }
}
