using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Draws each kind of equipment: opaque parts, solids inside, then liquid + glass in the transparent pass.</summary>
    internal static class Props
    {
        private static readonly Color GlassColor = new Color(205, 228, 255, 36);
        private static readonly Color AmberGlass = new Color(150, 85, 25, 120);
        private static readonly Color Steel = Gfx.Rgba(0x8C939C);
        private static readonly Color Brass = Gfx.Rgba(0xC79E52);
        private static readonly Color DarkPlastic = Gfx.Rgba(0x1C1F25);
        private static readonly Color Mark = Gfx.Rgba(0xF2F5FA);
        private static readonly Color Wood = Gfx.Rgba(0xB0814F);
        private static readonly Color CapWhite = new Color(236, 236, 236, 255);
        private static Mesh _gauzeQuad, _ribbedCap;
        private static bool _materials;

        /// <summary>How shiny each material is (environment reflection strength in the lit shader).</summary>
        private static void RegisterMaterials()
        {
            if (_materials) return;
            _materials = true;
            Gfx.SetMaterialEnv(GlassColor, 0.85f);
            Gfx.SetMaterialEnv(AmberGlass, 0.7f);
            Gfx.SetMaterialEnv(Steel, 0.62f);
            Gfx.SetMaterialEnv(Brass, 0.5f);
            Gfx.SetMaterialEnv(DarkPlastic, 0.12f);
            Gfx.SetMaterialEnv(CapWhite, 0.18f);
            _gauzeQuad = Gfx.Quad(0.14f, 0.14f, 1, 1);
            _ribbedCap = Gfx.Ribbed(40, 0.07f);
        }

        public static Matrix4x4 PoseOf(Item item) => Gfx.Pose(item.Position, item.Rotation);

        /// <summary>World transform of the vessel's base (container LocalOffset applied).</summary>
        public static Matrix4x4 VesselPose(Item item)
        {
            var offset = item.Container?.LocalOffset ?? Vector3.Zero;
            return Gfx.Mul(Gfx.Translate(offset), PoseOf(item));
        }

        // ------------------------------------------------------------------ opaque

        public static void DrawOpaque(Item item, float time)
        {
            RegisterMaterials();
            var pose = PoseOf(item);
            switch (item.Def.VisualKey)
            {
                case "burner": DrawBurner(item, pose); break;
                case "hotplate": DrawHotPlate(item, pose); break;
                case "balance": DrawBalance(item, pose); break;
                case "burette": DrawBuretteStand(item, pose); break;
                case "rack": DrawRack(pose); break;
                case "thermometer": DrawProbe(item, pose, false); break;
                case "ph_probe": DrawProbe(item, pose, true); break;
                case "bottle":
                case "amber":
                {
                    Gfx.DrawTextured(LabelBand(0.0368f, 0.05f), Gfx.Part(Gfx.Translate(0, 0.03f, 0), pose), Textures.LabelFor(item), Color.White);
                    // The opened, knurled screw cap lies on the bench beside the bottle (upside down).
                    if (!item.IsHeld && item.Tilt < 1 && item.VisualTilt < 1)
                    {
                        var capColor = Gfx.FromCore(item.Def.AccentColor);
                        var capPose = Gfx.Part(Gfx.Translate(0.052f, 0, -0.02f), pose);
                        Gfx.Draw(_ribbedCap, Gfx.Mul(Gfx.Scale(0.0175f, 0.02f, 0.0175f), capPose), capColor);
                        Gfx.Draw(Gfx.Disc, Gfx.Mul(Gfx.Scale(0.0168f, 1, 0.0168f), capPose), Gfx.Lerp(capColor, Color.Black, 0.25f));
                        Gfx.Draw(Gfx.Disc, Gfx.Mul(Gfx.Scale(0.0150f, 1, 0.0150f), Gfx.Mul(Gfx.Translate(0, 0.0195f, 0), capPose)), Gfx.Lerp(capColor, Color.Black, 0.45f));
                    }

                    // Neck lip and screw threads.
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0168f, 0.006f, 0.0168f), Gfx.Translate(0, 0.137f, 0)), pose), CapWhite);
                    var thread = Gfx.CachedLathe("thread", new[] { new Vector2(0.0158f, 0), new Vector2(0.0174f, 0.0012f), new Vector2(0.0158f, 0.0024f) }, 40);
                    Gfx.Draw(thread, Gfx.Part(Gfx.Translate(0, 0.1445f, 0), pose), CapWhite);
                    Gfx.Draw(thread, Gfx.Part(Gfx.Translate(0, 0.1485f, 0), pose), CapWhite);
                    break;
                }

                case "dropper":
                {
                    Gfx.DrawTextured(LabelBand(0.0172f, 0.024f), Gfx.Part(Gfx.Translate(0, 0.012f, 0), pose), Textures.LabelFor(item), Color.White);
                    var bulb = Gfx.CachedLathe("bulb", new[]
                    {
                        new Vector2(0, 0), new Vector2(0.0095f, 0), new Vector2(0.0095f, 0.008f), new Vector2(0.007f, 0.012f), new Vector2(0.0085f, 0.02f),
                        new Vector2(0.0075f, 0.03f), new Vector2(0.004f, 0.035f), new Vector2(0, 0.036f),
                    }, 20);
                    Gfx.Draw(bulb, Gfx.Part(Gfx.Translate(0, 0.058f, 0), pose), Gfx.Lerp(Gfx.FromCore(item.Def.AccentColor), DarkPlastic, 0.4f));
                    break;
                }

                case "jar":
                    Gfx.DrawTextured(LabelBand(0.0282f, 0.03f), Gfx.Part(Gfx.Translate(0, 0.012f, 0), pose), Textures.LabelFor(item), Color.White);
                    break;
                case "cylinder":
                {
                    var foot = Gfx.CachedLathe("hexfoot", new[] { new Vector2(0, 0), new Vector2(0.034f, 0), new Vector2(0.034f, 0.012f), new Vector2(0, 0.012f) }, 6);
                    Gfx.Draw(foot, pose, new Color(225, 235, 245, 255));
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0175f, 0.008f, 0.0175f), Gfx.Translate(0, 0.17f, 0)), pose), new Color(60, 140, 230, 255));
                    Graduations(item, pose, 10, 100, 0.0153f, 0.012f);
                    break;
                }

                case "beaker":
                    Graduations(item, pose, item.Container.CapacityLiters > 0.5 ? 100 : item.Container.CapacityLiters > 0.2 ? 50 : 25,
                        item.Container.CapacityLiters > 0.5 ? 600 : item.Container.CapacityLiters > 0.2 ? 250 : 100, (float)item.Container.RadiusM + 0.0026f, 0);
                    break;
                case "erlenmeyer":
                    Graduations(item, pose, 50, 200, 0, 0);
                    break;
                case "round_flask":
                {
                    var ring = Gfx.CachedLathe("cork", new[] { new Vector2(0.022f, 0), new Vector2(0.04f, 0), new Vector2(0.04f, 0.012f), new Vector2(0.022f, 0.012f), new Vector2(0.022f, 0) }, 32);
                    Gfx.Draw(ring, Gfx.Part(Gfx.Translate(0, -0.002f, 0), pose), new Color(176, 132, 84, 255));
                    break;
                }
            }

            if (item.Container != null)
            {
                DrawSolids(item);
            }
        }

        private static Mesh LabelBand(float radius, float height) =>
            Gfx.CachedLathe($"band{radius}{height}", new[] { new Vector2(radius, 0), new Vector2(radius, height) }, 48);

        private static void Graduations(Item item, Matrix4x4 pose, int stepMl, int maxMl, float radius, float yOffset)
        {
            var shape = VesselShape.For(item);
            for (int ml = stepMl; ml <= maxMl; ml += stepMl)
            {
                float y = shape.HeightForVolume(ml / 1000.0);
                if (y >= shape.RimY - 0.002f) break;
                float r = (radius > 0 ? radius : shape.RadiusAt(y) + shape.Wall) + 0.0004f;
                bool major = ml % (stepMl * 2) == 0;
                Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(major ? 0.012f : 0.007f, 0.0011f, 0.0006f), Gfx.Translate(0, yOffset + y, -r)), pose), Mark, new Vector3(0.2f));
            }
        }

        /// <summary>Undissolved solids: a pile (powders, precipitates, crystals) and/or individual pieces (ribbon, granules, chips...).</summary>
        private static void DrawSolids(Item item)
        {
            var c = item.Container;
            if (!c.Contents.HasSolids) return;
            var shape = VesselShape.For(item);
            var state = VesselState.For(item);
            var vesselPose = VesselPose(item);
            var upLocal = Vector3.Transform(Vector3.UnitY, Quaternion.Inverse(item.Rotation));

            double pileLiters = 0;
            var pieceSolids = new List<KeyValuePair<Substance, double>>();
            Color pileColor = default;
            double pileMost = 0;
            foreach (var kv in c.Contents.Solids())
            {
                double ml = kv.Value * kv.Key.MolarMass / kv.Key.Density;
                var (perMl, _) = VesselState.PieceStyle(kv.Key.Form);
                if (perMl <= 0 || kv.Key.Form == SolidForm.Crystals)
                {
                    pileLiters += ml / 1000.0 * 1.4; // loose powder packs with air gaps
                    if (ml > pileMost) { pileMost = ml; pileColor = Gfx.FromCore(kv.Key.Color); }
                }

                if (perMl > 0) pieceSolids.Add(new KeyValuePair<Substance, double>(kv.Key, ml));
            }

            float pileLevel = shape.Floor;
            if (pileLiters > 1e-7)
            {
                pileLevel = shape.HeightForVolume(pileLiters);
                state.Pile.Update(shape, pileLevel, upLocal);
                pileColor.A = 255;
                Gfx.Draw(state.Pile.Mesh, vesselPose, pileColor);
            }

            // Pieces: seeded positions inside the pile region; they slide toward the low side when tipped.
            var lowLocal = Vector3.Transform(-Vector3.UnitY, Quaternion.Inverse(item.Rotation));
            lowLocal.Y = 0;
            float tiltShift = Math.Clamp(lowLocal.Length() * 1.4f, 0, 1);
            if (lowLocal.LengthSquared() > 1e-6f) lowLocal = Vector3.Normalize(lowLocal);
            int seedIndex = 0;
            foreach (var kv in pieceSolids)
            {
                var (perMl, size) = VesselState.PieceStyle(kv.Key.Form);
                int count = Math.Clamp((int)Math.Round(kv.Value * perMl), 1, 40);
                var color = Gfx.FromCore(kv.Key.Color);
                color.A = 255;
                float pieceTop = Math.Max(shape.Floor + size, shape.HeightForVolume(kv.Value / 1000.0 * 2.5));
                for (int i = 0; i < count && seedIndex < state.Pieces.Length; i++, seedIndex++)
                {
                    var seed = state.Pieces[seedIndex];
                    float y = shape.Floor + size * 0.5f + seed.Y * (pieceTop - shape.Floor - size * 0.5f);
                    float rMax = Math.Max(0.002f, shape.RadiusAt(y) - size * 0.6f);
                    var p = new Vector3(MathF.Cos(seed.Z) * seed.X * rMax, y, MathF.Sin(seed.Z) * seed.X * rMax);
                    p += lowLocal * tiltShift * rMax * 0.8f;
                    var flat = new Vector2(p.X, p.Z);
                    if (flat.Length() > rMax) flat = Vector2.Normalize(flat) * rMax;
                    p = new Vector3(flat.X, p.Y, flat.Y);
                    var spin = Quaternion.CreateFromYawPitchRoll(seed.W * 6.28f, seed.Z, seed.X * 2f);
                    var tint = Gfx.Lerp(color, Color.Black, seed.W * 0.25f);
                    Matrix4x4 local;
                    Mesh mesh = Gfx.Cube;
                    switch (kv.Key.Form)
                    {
                        case SolidForm.Ribbon:
                            local = Gfx.Mul(Gfx.Mul(Gfx.Scale(size, 0.0007f, 0.004f), Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, seed.Z))), Gfx.Translate(p));
                            break;
                        case SolidForm.Granules:
                            mesh = Gfx.Sphere;
                            local = Gfx.Mul(Gfx.Scale(size * (0.8f + seed.W * 0.4f)), Gfx.Translate(p));
                            break;
                        case SolidForm.Turnings:
                            local = Gfx.Mul(Gfx.Mul(Gfx.Scale(size, 0.0009f, 0.0015f), Gfx.Rotate(spin)), Gfx.Translate(p));
                            break;
                        default:
                            local = Gfx.Mul(Gfx.Mul(Gfx.Scale(size * (0.7f + seed.W * 0.6f)), Gfx.Rotate(spin)), Gfx.Translate(p));
                            break;
                    }

                    Gfx.Draw(mesh, Gfx.Mul(local, vesselPose), tint);
                }
            }
        }

        private static void DrawBurner(Item item, Matrix4x4 pose)
        {
            const float barrelTop = 0.13f, gauze = 0.205f;
            float valve = (float)item.Heat.GasValve;
            var baseMesh = Gfx.CachedLathe("burnerBase", new[] { new Vector2(0, 0), new Vector2(0.042f, 0), new Vector2(0.042f, 0.006f), new Vector2(0.03f, 0.014f), new Vector2(0, 0.014f) });
            Gfx.Draw(baseMesh, pose, DarkPlastic);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0085f, barrelTop - 0.014f, 0.0085f), Gfx.Translate(0, 0.014f, 0)), pose), Brass);
            // Air-regulating collar with its two air holes.
            Gfx.Draw(_ribbedCap, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0112f, 0.016f, 0.0112f), Gfx.Translate(0, 0.028f, 0)), pose), Brass);
            foreach (float side in new[] { -1f, 1f })
            {
                var hole = Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0032f, 0.0016f, 0.0032f), Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2))), Gfx.Translate(side * 0.0105f, 0.036f, 0));
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(hole, pose), new Color(20, 18, 16, 255));
            }

            // Barrel rim and gas inlet nozzle, with the orange rubber hose curving away over the bench.
            var rim = Gfx.CachedLathe("burnerRim", new[] { new Vector2(0.0072f, 0), new Vector2(0.0092f, 0), new Vector2(0.0092f, 0.004f), new Vector2(0.0072f, 0.004f) }, 32);
            Gfx.Draw(rim, Gfx.Part(Gfx.Translate(0, barrelTop - 0.004f, 0), pose), Brass);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0032f, 0.03f, 0.0032f), Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2))), Gfx.Translate(0, 0.009f, 0.012f)), pose), Brass);
            var hose = Gfx.Rgba(0xE07020);
            Vector3 prev = new Vector3(0, 0.009f, 0.04f);
            for (int i = 1; i <= 8; i++)
            {
                float k = i / 8f;
                var p = new Vector3(0.05f * k * k, 0.009f - 0.006f * k, 0.04f + 0.11f * k);
                Rod(pose, prev, p, 0.0052f, hose);
                prev = p;
            }
            Knob(pose, new Vector3(0, 0.009f, -0.042f), valve, Gfx.Rgba(0xD92626));
            var ring = Gfx.CachedLathe("tripodRing", new[] { new Vector2(0.06f, 0), new Vector2(0.066f, 0), new Vector2(0.066f, 0.004f), new Vector2(0.06f, 0.004f), new Vector2(0.06f, 0) }, 40);
            Gfx.Draw(ring, Gfx.Part(Gfx.Translate(0, gauze - 0.004f, 0), pose), Steel);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 90f) * MathF.PI / 180f;
                var top = new Vector3(MathF.Cos(a) * 0.063f, gauze - 0.002f, MathF.Sin(a) * 0.063f);
                var foot = new Vector3(MathF.Cos(a) * 0.085f, 0, MathF.Sin(a) * 0.085f);
                Rod(pose, foot, top, 0.0025f, Steel);
            }

            // Wire gauze with its ceramic centre.
            Gfx.DrawTextured(_gauzeQuad, Gfx.Part(Gfx.Translate(0, gauze + 0.0012f, 0), pose), Textures.Gauze, Color.White, default, 0.3f);
            float glow = item.Heat.IsLit ? valve * valve : 0f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.035f, 0.0026f, 0.035f), Gfx.Translate(0, gauze + 0.0002f, 0)), pose),
                Gfx.Rgba(0xD8D8D0), new Vector3(1f, 0.25f, 0.05f) * glow * 0.9f);
        }

        private static void DrawHotPlate(Item item, Matrix4x4 pose)
        {
            float power = (float)item.Heat.GasValve;
            float heat = item.Heat.IsLit ? power : 0f;
            var enamel = new Color(238, 240, 245, 255);
            var panel = Gfx.Rgba(0x2A2D35);
            Gfx.SetMaterialEnv(enamel, 0.22f);
            foreach (var (fx, fz) in new[] { (-0.07f, -0.08f), (0.07f, -0.08f), (-0.07f, 0.08f), (0.07f, 0.08f) })
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.009f, 0.004f, 0.009f), Gfx.Translate(fx, 0, fz)), pose), DarkPlastic);
            }

            // Moulded enamel housing with a soft top edge and a darker control fascia across the front.
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.17f, 0.054f, 0.19f), 0.01f), Gfx.Part(Gfx.Translate(0, 0.031f, 0), pose), enamel);
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.162f, 0.03f, 0.012f), 0.005f), Gfx.Part(Gfx.Translate(0, 0.026f, -0.091f), pose), panel);

            // Round glass-ceramic top in a brushed steel bezel; the heating coil glows through when it is on.
            var bezel = Gfx.CachedLathe("hotplateBezel2", new[]
            {
                new Vector2(0.064f, 0), new Vector2(0.071f, 0), new Vector2(0.0725f, 0.004f), new Vector2(0.071f, 0.0085f), new Vector2(0.065f, 0.0085f),
            }, 72);
            Gfx.Draw(bezel, Gfx.Part(Gfx.Translate(0, 0.058f, 0.008f), pose), Steel);
            Gfx.Env = 0.35f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.065f, 0.0085f, 0.065f), Gfx.Translate(0, 0.0585f, 0.008f)), pose),
                new Color(26, 27, 33, 255), new Vector3(0.6f, 0.12f, 0.03f) * heat * heat);
            Gfx.Env = -1f;
            for (int k = 1; k <= 5; k++)
            {
                float r = 0.0105f * k + 0.003f;
                var coil = Gfx.CachedLathe("hotplateCoil2_" + k, new[] { new Vector2(r - 0.0016f, 0), new Vector2(r + 0.0016f, 0), new Vector2(r + 0.0016f, 0.0005f), new Vector2(r - 0.0016f, 0.0005f) }, 64);
                float flicker = 0.93f + 0.07f * MathF.Sin((float)GetTime() * 3f + k);
                Gfx.Draw(coil, Gfx.Part(Gfx.Translate(0, 0.0672f, 0.008f), pose), heat > 0.01f ? new Color(120, 40, 20, 255) : new Color(44, 46, 52, 255),
                    new Vector3(1.6f, 0.45f, 0.08f) * heat * flicker);
            }

            // Knurled heat and stirrer knobs, the power lamp and the temperature display.
            Knob(pose, new Vector3(-0.05f, 0.026f, -0.098f), power, new Color(240, 120, 40, 255));
            Knob(pose, new Vector3(-0.015f, 0.026f, -0.098f), 0f, new Color(70, 130, 230, 255));
            Gfx.Draw(Gfx.Sphere, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0032f), Gfx.Translate(0.068f, 0.026f, -0.0975f)), pose),
                power > 0.01f ? new Color(255, 60, 40, 255) : new Color(80, 30, 30, 255), power > 0.01f ? new Vector3(1.2f, 0.2f, 0.1f) : default);
            float surface = item.Heat.IsLit ? 20 + 300 * power : 20;
            Display(item, pose, new Vector3(0.03f, 0.026f, -0.0975f), 0.05f, 0.018f,
                item.Heat.IsLit ? string.Format(CultureInfo.InvariantCulture, "{0:0}°C", surface) : "OFF", item.Heat.IsLit);
        }

        private static void DrawBalance(Item item, Matrix4x4 pose)
        {
            var body = new Color(232, 235, 240, 255);
            Gfx.SetMaterialEnv(body, 0.2f);
            foreach (var (fx, fz) in new[] { (-0.07f, -0.085f), (0.07f, -0.085f), (-0.07f, 0.085f), (0.07f, 0.085f) })
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.007f, 0.005f, 0.007f), Gfx.Translate(fx, 0, fz)), pose), DarkPlastic);
            }

            // Body, a sloping control strip at the front with the display and two keys, the stainless weighing pan.
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.17f, 0.046f, 0.2f), 0.012f), Gfx.Part(Gfx.Translate(0, 0.028f, 0), pose), body);
            var slope = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f); // the front strip leans back towards the pan
            var stripCentre = new Vector3(0, 0.034f, -0.104f);
            var strip = Gfx.Mul(Gfx.Rotate(slope), Gfx.Translate(stripCentre));
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.16f, 0.034f, 0.014f), 0.006f), Gfx.Part(strip, pose), Gfx.Rgba(0x30343C));
            // Keys and display sit on the strip's sloping front face.
            Vector3 OnStrip(float x, float out_) => stripCentre + Vector3.Transform(new Vector3(x, 0, -0.007f - out_), slope);
            foreach (var (kx, col) in new[] { (0.052f, Gfx.Rgba(0x3E8E5A)), (0.07f, Gfx.Rgba(0x4A505C)) })
            {
                var key = Gfx.Mul(Gfx.Rotate(slope), Gfx.Translate(OnStrip(kx, 0.0012f)));
                Gfx.Draw(Gfx.RoundedBox(new Vector3(0.013f, 0.009f, 0.004f), 0.0018f), Gfx.Part(key, pose), col);
            }

            var bal = item.Inst.Get<Balance>();
            Display(item, pose, OnStrip(-0.012f, 0.0006f), 0.08f, 0.021f,
                string.Format(CultureInfo.InvariantCulture, "{0:0.00} g", bal?.ReadingGrams ?? 0), true, slope);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.006f, 0.01f, 0.006f), Gfx.Translate(0, 0.05f, 0.01f)), pose), Steel);
            var pan = Gfx.CachedLathe("balancePan2", new[]
            {
                new Vector2(0, 0), new Vector2(0.064f, 0), new Vector2(0.069f, 0.002f), new Vector2(0.07f, 0.006f), new Vector2(0.066f, 0.006f),
                new Vector2(0.064f, 0.004f), new Vector2(0, 0.004f),
            }, 72);
            Gfx.Draw(pan, Gfx.Part(Gfx.Translate(0, 0.058f, 0.01f), pose), Steel);
        }

        private static void DrawBuretteStand(Item item, Matrix4x4 pose)
        {
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.12f, 0.012f, 0.15f), 0.005f), Gfx.Part(Gfx.Translate(0, 0.006f, 0.02f), pose), new Color(40, 44, 52, 255));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.006f, 0.66f, 0.006f), Gfx.Translate(0, 0, 0.07f)), pose), Steel);
            foreach (float y in new[] { 0.5f, 0.33f })
            {
                Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(0.012f, 0.012f, 0.07f), Gfx.Translate(0, y, 0.035f)), pose), new Color(90, 96, 106, 255));
                Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(0.022f, 0.016f, 0.008f), Gfx.Translate(0, y, 0.008f)), pose), new Color(200, 60, 40, 255));
            }

            // Stopcock: PTFE key whose handle turns with the valve.
            var stop = item.Inst.Get<Stopcock>();
            float valve = stop != null ? (float)stop.Valve : 0f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0045f, 0.03f, 0.0045f), Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2))), Gfx.Translate(0.015f, 0.245f, 0)), pose), new Color(240, 240, 240, 255));
            var handleRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, valve * MathF.PI / 2);
            Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.006f, 0.026f, 0.006f), Gfx.Rotate(handleRot)), Gfx.Translate(0.019f, 0.245f, 0)), pose), new Color(40, 110, 220, 255));
            // Graduations every 1 mL (major every 5 mL) on the front of the tube.
            var c = item.Container;
            for (int ml = 0; ml <= 25; ml++)
            {
                float y = 0.27f + 0.29f - ml / 25f * 0.28f;
                float w = ml % 5 == 0 ? 0.008f : 0.004f;
                Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(w, 0.0007f, 0.0005f), Gfx.Translate(0, y, -(float)c.RadiusM - 0.0022f)), pose), Mark, new Vector3(0.3f));
            }
        }

        private static void DrawRack(Matrix4x4 pose)
        {
            // White moulded rack: a base, two end posts, and a top shelf with four dark holes.
            var plastic = new Color(240, 242, 246, 255);
            Gfx.SetMaterialEnv(plastic, 0.18f);
            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.18f, 0.008f, 0.05f), 0.0035f), Gfx.Part(Gfx.Translate(0, 0.004f, 0), pose), plastic);
            foreach (float x in new[] { -0.086f, 0.086f })
            {
                Gfx.Draw(Gfx.RoundedBox(new Vector3(0.008f, 0.07f, 0.05f), 0.0035f), Gfx.Part(Gfx.Translate(x, 0.039f, 0), pose), plastic);
            }

            Gfx.Draw(Gfx.RoundedBox(new Vector3(0.18f, 0.008f, 0.05f), 0.0035f), Gfx.Part(Gfx.Translate(0, 0.066f, 0), pose), plastic);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.06f + i * 0.04f;
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0095f, 0.0012f, 0.0095f), Gfx.Translate(x, 0.0697f, 0)), pose), Gfx.Rgba(0x2A2D35));
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.008f, 0.0012f, 0.008f), Gfx.Translate(x, 0.0082f, 0)), pose), Gfx.Rgba(0xC9CDD6));
            }
        }

        private static void DrawProbe(Item item, Matrix4x4 pose, bool ph)
        {
            const float length = 0.2f;
            var accent = Gfx.FromCore(item.Def.AccentColor);
            if (!ph) Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(0.0022f, length * 0.68f, 0.0022f), pose), Gfx.Rgba(0xC0C5CC));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.008f, length * 0.32f, 0.008f), Gfx.Translate(0, length * 0.68f, 0)), pose), Gfx.Rgba(0x1F2228));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0085f, 0.008f, 0.0085f), Gfx.Translate(0, length * 0.7f, 0)), pose), accent);
        }

        private static void Knob(Matrix4x4 pose, Vector3 at, float value, Color color)
        {
            // A knurled knob facing the user, with a white pointer showing its setting.
            var knobRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            Gfx.Draw(_ribbedCap, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0115f, 0.009f, 0.0115f), Gfx.Rotate(knobRot)), Gfx.Translate(at)), pose), color);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0098f, 0.0105f, 0.0098f), Gfx.Rotate(knobRot)), Gfx.Translate(at)), pose), Gfx.Lerp(color, Color.White, 0.12f));
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -value * MathF.PI * 0.75f + MathF.PI * 0.375f);
            var pointer = Gfx.Mul(Gfx.Mul(Gfx.Mul(Gfx.Translate(0, 0.5f, 0), Gfx.Scale(0.0014f, 0.0075f, 0.0012f)), Gfx.Rotate(spin)), Gfx.Translate(at + new Vector3(0, 0, -0.0108f)));
            Gfx.Draw(Gfx.Cube, Gfx.Part(pointer, pose), Color.White, new Vector3(0.6f));
        }

        // ------------------------------------------------------------------ digital displays

        private sealed class LcdScreen
        {
            public RenderTexture2D Target;
            public string Text = "";
            public bool On;
        }

        private static readonly Dictionary<int, LcdScreen> Displays = new Dictionary<int, LcdScreen>();
        private static readonly List<(LcdScreen lcd, string text, bool on)> Pending = new List<(LcdScreen, string, bool)>();
        private static Mesh _screenQuad;
        private static bool _screenQuadMade;

        /// <summary>
        /// A small seven-segment-style display on the device itself (the digits are drawn into a texture once they
        /// change; see <see cref="UpdateDisplays"/>), instead of a label floating in front of it.
        /// </summary>
        private static void Display(Item item, Matrix4x4 pose, Vector3 at, float width, float height, string text, bool on, Quaternion? tilt = null)
        {
            if (!Displays.TryGetValue(item.Inst.Id, out var lcd))
            {
                lcd = new LcdScreen { Target = LoadRenderTexture(256, 72) };
                SetTextureFilter(lcd.Target.Texture, TextureFilter.Bilinear);
                SetTextureWrap(lcd.Target.Texture, TextureWrap.Repeat);
                Displays[item.Inst.Id] = lcd;
                Pending.Add((lcd, text, on));
            }
            else if (lcd.Text != text || lcd.On != on)
            {
                Pending.Add((lcd, text, on));
            }

            if (!_screenQuadMade)
            {
                _screenQuadMade = true;
                _screenQuad = Gfx.Quad(1, 1, -1, 1, flipV: true); // u runs backwards: the texture reads correctly from the front
            }

            // The quad lies in XZ (normal +Y): stand it up to face the front (−Z), then follow the panel's slope.
            var face = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            if (tilt.HasValue) face = Quaternion.Concatenate(face, tilt.Value);
            var local = Gfx.Mul(Gfx.Mul(Gfx.Scale(width, 1, height), Gfx.Rotate(face)), Gfx.Translate(at));
            Gfx.Env = 0.25f;
            Gfx.DrawTextured(_screenQuad, Gfx.Part(local, pose), lcd.Target.Texture, Color.White, on ? new Vector3(0.3f) : new Vector3(0.05f), 0.8f);
            Gfx.Env = -1f;
        }

        /// <summary>Redraws the displays whose reading changed (call outside any 3D / texture mode).</summary>
        public static void UpdateDisplays()
        {
            foreach (var (lcd, text, on) in Pending)
            {
                lcd.Text = text;
                lcd.On = on;
                BeginTextureMode(lcd.Target);
                ClearBackground(on ? new Color(10, 30, 20, 255) : new Color(10, 13, 12, 255));
                var font = Ui.BoldFont;
                var m = MeasureTextEx(font, text, 52, 0);
                DrawTextEx(font, text, new Vector2(256 - m.X - 16, (72 - m.Y) / 2 + 2), 52, 0, on ? new Color(130, 255, 160, 255) : new Color(60, 80, 64, 255));
                EndTextureMode();
            }

            Pending.Clear();
        }

        private static void Rod(Matrix4x4 pose, Vector3 from, Vector3 to, float radius, Color color)
        {
            var dir = Vector3.Normalize(to - from);
            var axis = Vector3.Cross(Vector3.UnitY, dir);
            var rot = axis.LengthSquared() < 1e-8f ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(dir.Y, -1f, 1f)));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(radius, Vector3.Distance(from, to), radius), Gfx.Rotate(rot)), Gfx.Translate(from)), pose), color);
        }

        // ------------------------------------------------------------------ transparent

        public static void DrawTransparent(Item item)
        {
            var pose = PoseOf(item);
            if (item.Probe != null)
            {
                if (item.Probe.Quantity == ProbeQuantity.PH)
                {
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(0.005f, 0.2f * 0.68f, 0.005f), pose), new Color(215, 235, 255, 90));
                }

                return;
            }

            var c = item.Container;
            if (c == null) return;
            var shape = VesselShape.For(item);
            var state = VesselState.For(item);
            var vesselPose = VesselPose(item);

            if (c.Contents.HasLiquid)
            {
                var upLocal = Vector3.Transform(Vector3.UnitY, Quaternion.Inverse(item.Rotation));
                float level = shape.HeightForVolume(c.Contents.VolumeLiters);
                state.Liquid.Update(shape, level, upLocal);
                var target = Gfx.FromCore(c.LiquidColor);
                // Suspended precipitate makes the liquid cloudy in its colour.
                foreach (var kv in c.Contents.Solids())
                {
                    if (kv.Key.Form == SolidForm.Precipitate)
                    {
                        float k = (float)Math.Min(0.6, kv.Value * 40);
                        target = Gfx.Lerp(target, Gfx.FromCore(kv.Key.Color), k);
                        target.A = (byte)Math.Max(target.A, (int)(160 + 80 * k));
                    }
                }

                target.A = (byte)Math.Max((int)target.A, 110);
                state.LiquidColor = state.HasColor ? Gfx.Lerp(state.LiquidColor, target, 0.08f) : target;
                state.HasColor = true;
                Gfx.Env = 0.3f;
                Gfx.Draw(state.Liquid.Mesh, vesselPose, state.LiquidColor);
                Gfx.Env = -1f;
                Bubbles.Draw(item, shape, level);
            }

            var glass = item.Def.VisualKey == "amber" ? AmberGlass : GlassColor;
            Gfx.Draw(shape.Glass, vesselPose, glass);
            if (item.Def.VisualKey == "burette")
            {
                // conical tip below the tap
                var tip = Gfx.CachedLathe("buretteTip", new[] { new Vector2(0.0012f, 0), new Vector2(0.0045f, 0.035f), new Vector2(0.006f, 0.055f) }, 16);
                Gfx.Draw(tip, Gfx.Part(Gfx.Translate(0, 0.215f, 0), pose), GlassColor);
            }
            else if (item.Def.VisualKey == "dropper")
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0022f, 0.05f, 0.0022f), Gfx.Translate(0, 0.012f, 0)), pose), GlassColor);
            }
        }

        // ------------------------------------------------------------------ 2D overlays

        public static void DrawLabels(Lab lab)
        {
            var ci = CultureInfo.CurrentCulture;
            foreach (var item in lab.Items)
            {
                if (item.Probe != null)
                {
                    var top = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, 0.215f, 0)), lab.Camera);
                    string text = item.Probe.Quantity == ProbeQuantity.Temperature
                        ? (double.IsNaN(item.Probe.Reading) ? "--" : string.Format(ci, "{0:0.0} °C", item.Probe.Reading))
                        : (double.IsNaN(item.Probe.Reading) ? "pH --" : string.Format(ci, "pH {0:0.00}", item.Probe.Reading));
                    Pill(text, top, Gfx.FromCore(item.Def.AccentColor), 20);
                }
                else if (item.Def.VisualKey == "burette")
                {
                    var stop = item.Inst.Get<Stopcock>();
                    var at = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, 0.6f, 0)), lab.Camera);
                    Pill(string.Format(ci, "{0:0.00} mL verildi", stop.DeliveredMilliliters), at, Gfx.FromCore(item.Def.AccentColor), 15);
                }
                else if (item == lab.Selected || item == lab.Hovered)
                {
                    if (item.Container != null && item.Def.Category != IDLab.Core.Model.EquipmentCategory.Glassware)
                    {
                        var top = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, item.Size.Y + 0.02f, 0)), lab.Camera);
                        Pill(Tr.Name(item.Def), top, Gfx.FromCore(item.Def.AccentColor), 14);
                    }
                }
            }
        }

        public static void Pill(string text, Vector2 at, Color accent, float size)
        {
            var m = Ui.Measure(text, size, true);
            float pad = 8 * Ui.S;
            var r = new Rectangle(at.X - m.X / 2 - pad, at.Y - m.Y - pad * 1.6f, m.X + pad * 2, m.Y + pad);
            DrawRectangleRounded(r, 0.5f, 8, Gfx.WithAlpha(Theme.Raised, 0.9f));
            DrawRectangleRoundedLinesEx(r, 0.5f, 8, 1.5f * Ui.S, accent);
            Ui.Text(text, r.X + pad, r.Y + pad / 2, size, Theme.Text, true);
        }

    }
}
