using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using IDLab.Core.Components;
using Raylib_cs;

namespace IDLab.Desktop
{
    /// <summary>
    /// Metal rods, electrodes and the glass stirring rod (with their coatings: a copper film, silver crystals ...),
    /// the bench DC power supply and the red/black leads clipped onto the rods, and gas bubbles on electrodes.
    /// </summary>
    internal static class RodsView
    {
        private static bool _materials;

        /// <summary>Bare metal colours (a little more characteristic than the element table).</summary>
        public static Color MetalColor(string symbol)
        {
            switch (symbol)
            {
                case "Fe": return Gfx.Rgba(0x70757C);
                case "Cu": return Gfx.Rgba(0xC8703F);
                case "Ag": return Gfx.Rgba(0xE4E6EA);
                case "Zn": return Gfx.Rgba(0xA7B0BB);
                case "Al": return Gfx.Rgba(0xCDD2D8);
                case "Pb": return Gfx.Rgba(0x5E646B);
                case "Ni": return Gfx.Rgba(0xCBC7B8);
                case "Sn": return Gfx.Rgba(0xC9CED3);
                case "Mg": return Gfx.Rgba(0xD2D5DA);
                case "Au": return Gfx.Rgba(0xE2B53A);
                case "C": return Gfx.Rgba(0x2A2A2E);
                case "Co": return Gfx.Rgba(0x9AA3B0);
                case "Cr": return Gfx.Rgba(0xC4CAD2);
                case "Cd": return Gfx.Rgba(0xB9BFC6);
                default: return Gfx.Rgba(0xB0B4BA);
            }
        }

        /// <summary>Colour of a freshly deposited layer: displacement gives a dull, spongy film; plating a bright one.</summary>
        private static Color CoatingColor(string symbol, bool crystals)
        {
            switch (symbol)
            {
                case "Cu": return crystals ? Gfx.Rgba(0x8E4528) : Gfx.Rgba(0xC9683A);
                case "Ag": return crystals ? Gfx.Rgba(0xC9CBCF) : Gfx.Rgba(0xEDEFF2);
                case "Pb": return Gfx.Rgba(0x4E5358);
                case "Zn": return Gfx.Rgba(0xB7BFC9);
                case "Ni": return Gfx.Rgba(0xD3CFC2);
                case "Sn": return Gfx.Rgba(0xD7DBDF);
                default: return MetalColor(symbol);
            }
        }

        private static void RegisterMaterials()
        {
            if (_materials) return;
            _materials = true;
            foreach (var sym in new[] { "Fe", "Cu", "Ag", "Zn", "Al", "Pb", "Ni", "Sn", "Mg", "Au", "Co", "Cr", "Cd" })
            {
                Gfx.SetMaterialEnv(MetalColor(sym), sym == "Ag" || sym == "Ni" || sym == "Au" ? 0.6f : 0.45f);
            }
        }

        // ------------------------------------------------------------------ rods

        public static void DrawRod(Item item, Matrix4x4 pose, float time)
        {
            RegisterMaterials();
            var rod = item.Rod;
            if (rod == null) return;
            float length = (float)rod.LengthM, radius = (float)rod.DiameterM / 2;
            pose = StirPose(item, rod, pose, time);
            var bare = MetalColor(rod.Material);

            if (rod.Material == "Mg")
            {
                // A crinkled ribbon rather than a rod.
                for (int i = 0; i < 6; i++)
                {
                    float y0 = length * i / 6f;
                    var bend = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (i % 2 == 0 ? 1 : -1) * 0.12f);
                    Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Mul(Gfx.Translate(0, 0.5f, 0), Gfx.Scale(0.006f, length / 6f, 0.0005f)), Gfx.Rotate(bend)), Gfx.Translate(0, y0, 0)), pose), Coated(rod, bare, 0, y0));
                }

                DrawCrystals(item, rod, pose, radius);
                return;
            }

            float coated = (float)rod.CoatedLengthM;
            var surface = Coated(rod, bare, 0, 0);
            bool anode = rod.Polarity > 0 && rod.DissolvedMoles > 0;
            if (anode)
            {
                // An anode that has been eaten into looks dull and etched where it was wet.
                float etch = (float)Math.Min(0.55, rod.DissolvedMoles / Math.Max(1e-9, rod.WettedAreaM2 + 1e-5) / 4.0);
                surface = Gfx.Lerp(surface, Gfx.Lerp(bare, Color.Black, 0.35f), etch);
            }

            // Wet (coated / etched) part, then the dry part up to the top.
            float wet = Math.Max(coated, anode ? (float)rod.ImmersedLengthM : 0);
            if (wet > 0.0005f)
            {
                Gfx.Env = rod.IsGraphite ? 0.03f : 0.25f;
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(radius * 1.015f, wet, radius * 1.015f), pose), surface);
                Gfx.Env = -1;
            }

            if (rod.IsGraphite) Gfx.Env = 0.04f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(radius, length - wet, radius), Gfx.Translate(0, wet, 0)), pose), bare);
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Mul(Gfx.Scale(radius, radius * 0.5f, radius), Gfx.Translate(0, length, 0)), pose), bare);
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Scale(radius * 1.015f, radius * 0.4f, radius * 1.015f), pose), wet > 0.0005f ? surface : bare);
            Gfx.Env = -1;
            DrawCrystals(item, rod, pose, radius);
        }

        /// <summary>
        /// The coated part seen through the liquid: drawn again, half transparent, after the liquids, so a copper film stays
        /// visible through a deep blue CuSO₄ solution (as it is in real life).
        /// </summary>
        public static void DrawThroughLiquid(Lab lab, float time)
        {
            foreach (var item in lab.Items)
            {
                var rod = item.Rod;
                if (rod == null || rod.IsGlass || rod.ImmersedIn == null || rod.CoatedLengthM <= 0.001 || rod.Layers.Count == 0) continue;
                var surface = Coated(rod, MetalColor(rod.Material), 0, 0);
                float wet = (float)Math.Min(rod.CoatedLengthM, rod.ImmersedLengthM);
                float radius = (float)rod.DiameterM / 2 * 1.06f;
                var pose = StirPose(item, rod, Props.PoseOf(item), time);
                Gfx.Env = 0.15f;
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(radius, wet, radius), pose), Gfx.WithAlpha(surface, 0.55f));
                Gfx.Env = -1;
            }
        }

        /// <summary>Surface colour after the coating layers (each hides what is under it by its coverage).</summary>
        private static Color Coated(Rod rod, Color bare, float x, float y)
        {
            var color = bare;
            foreach (var layer in rod.Layers)
            {
                if (layer.Crystals) continue;
                color = Gfx.Lerp(color, CoatingColor(layer.Symbol, false), (float)rod.Coverage(layer));
            }

            // Loose crystals also darken the surface they grow on.
            foreach (var layer in rod.Layers)
            {
                if (!layer.Crystals) continue;
                color = Gfx.Lerp(color, CoatingColor(layer.Symbol, true), (float)rod.Coverage(layer) * 0.6f);
            }

            return color;
        }

        /// <summary>Silver / lead "trees": needles sprouting from the wet part of the rod, longer as more metal deposits.</summary>
        private static void DrawCrystals(Item item, Rod rod, Matrix4x4 pose, float radius)
        {
            double moles = 0;
            string symbol = null;
            foreach (var layer in rod.Layers)
            {
                if (!layer.Crystals) continue;
                moles += layer.Moles;
                symbol = layer.Symbol;
            }

            if (symbol == null || moles < 2e-7 || rod.CoatedLengthM <= 0) return;
            var color = CoatingColor(symbol, true);
            int count = (int)Math.Min(90, 12 + moles * 4e6);
            float growth = (float)Math.Min(0.018, 0.0015 + Math.Sqrt(moles) * 2.2);
            var rng = new Random(item.Inst.Id * 977);
            float span = (float)rod.CoatedLengthM;
            Gfx.Env = 0.35f;
            for (int i = 0; i < count; i++)
            {
                float y = (float)rng.NextDouble() * span;
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                float len = growth * (0.35f + 0.65f * (float)rng.NextDouble()) * (0.6f + 0.4f * (1 - y / Math.Max(1e-4f, span)));
                var outward = new Vector3(MathF.Cos(a), 0.25f * ((float)rng.NextDouble() - 0.3f), MathF.Sin(a));
                outward = Vector3.Normalize(outward);
                var from = new Vector3(outward.X * radius * 0.9f, y, outward.Z * radius * 0.9f);
                var to = from + outward * len;
                Props.Stick(pose, from, to, 0.0004f + 0.0003f * (float)rng.NextDouble(), color);
                // Side branches: what makes it look like a little tree.
                if (len > 0.004f)
                {
                    var side = Vector3.Normalize(Vector3.Cross(outward, Vector3.UnitY) + new Vector3(0, 0.6f, 0));
                    var mid = Vector3.Lerp(from, to, 0.55f);
                    Props.Stick(pose, mid, mid + Vector3.Normalize(outward + side) * len * 0.4f, 0.0003f, color);
                    Props.Stick(pose, mid, mid + Vector3.Normalize(outward - side) * len * 0.35f, 0.0003f, color);
                }
            }

            Gfx.Env = -1;
        }

        /// <summary>A glass rod that is stirring swings round the vessel.</summary>
        private static Matrix4x4 StirPose(Item item, Rod rod, Matrix4x4 pose, float time)
        {
            if (rod.StirTime <= 0 || rod.ImmersedIn == null) return pose;
            float a = time * 7f;
            var lean = Quaternion.CreateFromYawPitchRoll(0, MathF.Sin(a) * 0.12f, MathF.Cos(a) * 0.12f);
            return Gfx.Mul(Gfx.Rotate(lean), pose);
        }

        public static void DrawGlass(Item item, Matrix4x4 pose, float time)
        {
            var rod = item.Rod;
            if (rod == null) return;
            pose = StirPose(item, rod, pose, time);
            float length = (float)rod.LengthM, radius = (float)rod.DiameterM / 2;
            var glass = new Color(215, 235, 250, 120);
            Gfx.Env = 0.8f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(radius, length, radius), pose), glass);
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Mul(Gfx.Scale(radius), Gfx.Translate(0, length, 0)), pose), glass);
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Scale(radius), pose), glass);
            Gfx.Env = -1;
        }

        // ------------------------------------------------------------------ power supply

        // Seen from the front (−Z side) +X is on the viewer's left: displays and knob left, binding posts right.
        private static readonly Vector3 PlusTerminal = new Vector3(-0.045f, 0.022f, -0.068f);
        private static readonly Vector3 MinusTerminal = new Vector3(-0.068f, 0.022f, -0.068f);

        public static void DrawSupply(Item item, Matrix4x4 pose)
        {
            var ps = item.Supply;
            if (ps == null) return;
            var ci = CultureInfo.InvariantCulture;
            var body = Gfx.Rgba(0x34373E);
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.16f, 0.09f, 0.13f), 0.008f), Gfx.Part(Gfx.Translate(0, 0.045f, 0), pose), body);
            // Front panel (−Z), handle on top and little rubber feet.
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.15f, 0.08f, 0.004f), 0.002f), Gfx.Part(Gfx.Translate(0, 0.045f, -0.064f), pose), Gfx.Rgba(0x1D1F24));
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.11f, 0.008f, 0.016f), 0.003f), Gfx.Part(Gfx.Translate(0, 0.094f, 0.02f), pose), Gfx.Rgba(0x24262B));
            foreach (var (fx, fz) in new[] { (-0.065f, -0.05f), (0.065f, -0.05f), (-0.065f, 0.05f), (0.065f, 0.05f) })
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.008f, 0.003f, 0.008f), Gfx.Translate(fx, 0, fz)), pose), Gfx.Rgba(0x111111));
            }

            bool on = ps.Voltage > 0.01;
            // Two displays: volts and amps.
            Props.Display(item, pose, new Vector3(0.035f, 0.064f, -0.0665f), 0.058f, 0.017f, on ? string.Format(ci, "{0:0.0} V", ps.Voltage) : "OFF", on, null, 0);
            Props.Display(item, pose, new Vector3(-0.035f, 0.064f, -0.0665f), 0.058f, 0.017f, on ? string.Format(ci, "{0:0.00} A", ps.CurrentA) : "", on, null, 1);
            Props.Knob(pose, new Vector3(0.035f, 0.026f, -0.066f), (float)(ps.Voltage / IDLab.Core.Components.PowerSupply.MaxVoltage), Gfx.Rgba(0xD8DCE2));
            // Power lamp.
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Mul(Gfx.Scale(0.003f), Gfx.Translate(-0.005f, 0.026f, -0.067f)), pose), on ? new Color(90, 255, 120, 255) : new Color(30, 60, 36, 255), on ? new Vector3(0.9f) : default);
            // Binding posts: red (+) and black (−).
            Post(pose, PlusTerminal, Gfx.Rgba(0xD8282C));
            Post(pose, MinusTerminal, Gfx.Rgba(0x1A1A1A));
        }

        private static void Post(Matrix4x4 pose, Vector3 at, Color color)
        {
            var face = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0065f, 0.012f, 0.0065f), Gfx.Rotate(face)), Gfx.Translate(at)), pose), color);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0028f, 0.016f, 0.0028f), Gfx.Rotate(face)), Gfx.Translate(at)), pose), Gfx.Rgba(0xC9A24A));
        }

        /// <summary>Leads from the binding posts to crocodile clips on the rods (sagging like a real cable).</summary>
        public static void DrawCables(Lab lab)
        {
            foreach (var item in lab.Items)
            {
                var ps = item.Supply;
                if (ps == null) continue;
                Cable(lab, item, PlusTerminal, ps.Positive, Gfx.Rgba(0xD8282C));
                Cable(lab, item, MinusTerminal, ps.Negative, Gfx.Rgba(0x202226));
            }
        }

        private static void Cable(Lab lab, Item supply, Vector3 terminal, IDLab.Core.Model.EquipmentInstance rodInst, Color color)
        {
            if (rodInst == null) return;
            Item rodItem = null;
            foreach (var it in lab.Items)
            {
                if (it.Inst == rodInst) rodItem = it;
            }

            if (rodItem?.Rod == null) return;
            var a = supply.Inst.Pose.TransformPoint(terminal + new Vector3(0, 0, -0.012f));
            var rodPose = Props.PoseOf(rodItem);
            float top = (float)rodItem.Rod.LengthM - 0.012f;
            var b = rodItem.Inst.Pose.TransformPoint(new Vector3(0, top, 0));
            var side = rodItem.Inst.Pose.TransformDirection(new Vector3(0.006f, 0, 0));
            // Crocodile clip on the rod.
            var clipPose = Gfx.Mul(Gfx.Translate(0, top, 0), rodPose);
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.014f, 0.024f, 0.009f), 0.002f), Gfx.Mul(Gfx.Translate(0.006f, 0, 0), clipPose), color);
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Mul(Gfx.Scale(0.004f, 0.026f, 0.004f), Gfx.Translate(0.0035f, 0, 0)), clipPose), Gfx.Rgba(0xB9BDC4));
            b += side * 1.6f;

            // A soft cubic curve: up out of the clip, over the rim, then down and along the bench to the binding post.
            var front = supply.Inst.Pose.TransformDirection(new Vector3(0, 0, -1));
            var away = new Vector3(a.X - b.X, 0, a.Z - b.Z);
            away = away.LengthSquared() > 1e-8f ? Vector3.Normalize(away) : Vector3.UnitX;
            var p0 = b;
            var p1 = b + new Vector3(0, 0.07f, 0) + away * 0.04f;
            var p3 = a;
            var p2 = a + front * 0.07f + new Vector3(0, 0.01f, 0);
            float dist = Vector3.Distance(a, b);
            float sag = Math.Min(0.05f, dist * 0.15f);
            var prev = p0;
            const int segments = 28;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1 - t;
                var p = u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
                p.Y -= sag * 4 * t * (1 - t);
                p.Y = Math.Max(p.Y, 0.0025f);
                Props.Stick(Matrix4x4.Identity, prev, p, 0.0021f, color);
                prev = p;
            }
        }

        // ------------------------------------------------------------------ bubbles on electrodes

        private static readonly Random Rng = new Random(17);
        private static readonly Dictionary<int, float> Accumulators = new Dictionary<int, float>();

        public static void Update(Lab lab, float dt)
        {
            foreach (var item in lab.Items)
            {
                var rod = item.Rod;
                if (rod?.ImmersedIn == null) continue;
                float rate = (float)Math.Min(70, rod.GasRateMolPerS * 4e6 + rod.CoatingRateMolPerS * 2e6 * 0);
                if (rate <= 0.05f) continue;
                Item vessel = null;
                foreach (var it in lab.Items)
                {
                    if (it.Container == rod.ImmersedIn) vessel = it;
                }

                if (vessel == null) continue;
                Accumulators.TryGetValue(item.Inst.Id, out float acc);
                acc += rate * dt;
                var vesselPose = vessel.Inst.Pose;
                var invRot = Quaternion.Inverse(vesselPose.Rotation);
                var offset = vessel.Container.LocalOffset;
                while (acc >= 1)
                {
                    acc -= 1;
                    float y = (float)(Rng.NextDouble() * rod.ImmersedLengthM);
                    float a = (float)(Rng.NextDouble() * Math.PI * 2);
                    var world = item.Inst.Pose.TransformPoint(new Vector3(MathF.Cos(a) * (float)rod.DiameterM * 0.6f, y, MathF.Sin(a) * (float)rod.DiameterM * 0.6f));
                    var local = Vector3.Transform(world - vesselPose.Position, invRot) - offset;
                    Bubbles.Add(vessel, local, 0.0004f + (float)Rng.NextDouble() * 0.0008f);
                }

                Accumulators[item.Inst.Id] = acc;
            }
        }
    }
}
