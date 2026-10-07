using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using IDLab.Core.Components;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Draws the lab room and every item from core state (glass, liquids, flame, streams, readouts).</summary>
    internal static class Scene
    {
        private const float Wall = 0.0025f;
        private static readonly Color Glass = new Color(205, 228, 255, 34);
        private static readonly Color Steel = Gfx.Rgba(0x8C939C);
        private static readonly Color Brass = Gfx.Rgba(0xC79E52);
        private static readonly Color DarkPlastic = Gfx.Rgba(0x1C1F25);
        private static float _flicker;

        public static void Draw(Lab lab, float dt)
        {
            _flicker += dt * 14f;
            var cam = lab.Camera;
            Gfx.Ambient = new Vector3(0.30f, 0.33f, 0.40f) + lab.Effects.Flash * 0.6f;
            Gfx.BeginFrame(cam.Position);

            DrawRoom();
            foreach (var item in lab.Items)
            {
                DrawShadow(lab, item);
            }

            foreach (var item in lab.Items)
            {
                DrawOpaque(item);
            }

            DrawFlames(lab);
            DrawMarkers(lab);

            // Transparent pass: far → near, liquid before its glass.
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            foreach (var item in lab.Items.Where(i => i.Container != null || i.Probe?.Quantity == ProbeQuantity.PH)
                                         .OrderByDescending(i => Vector3.DistanceSquared(i.Position, cam.Position)))
            {
                DrawTransparent(item);
            }

            DrawStreams(lab);
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
            lab.Effects.Draw();
        }

        // ------------------------------------------------------------------ room

        private static void DrawRoom()
        {
            // Bench top (black epoxy), cabinet, floor, back wall, shelf, light strips.
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.8f, 0.04f, 0.9f), Gfx.Translate(0, -0.02f, 0)), Gfx.Rgba(0x151A22));
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.74f, 0.86f, 0.82f), Gfx.Translate(0, -0.47f, 0.02f)), Gfx.Rgba(0x1B2029));
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(12f, 0.02f, 12f), Gfx.Translate(0, -0.91f, 0)), Gfx.Rgba(0x0E1118));
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(6f, 3f, 0.05f), Gfx.Translate(0, 0.6f, 0.8f)), Gfx.Rgba(0x161B25));
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.8f, 0.02f, 0.18f), Gfx.Translate(0, 0.45f, 0.69f)), Gfx.Rgba(0x1B2029));
            var glow = new Vector3(0.15f, 0.75f, 1f);
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.8f, 0.008f, 0.01f), Gfx.Translate(0, 0.25f, 0.77f)), Gfx.Rgba(0x0A2A3A), glow);
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.8f, 0.008f, 0.01f), Gfx.Translate(0, 0.75f, 0.77f)), Gfx.Rgba(0x0A2A3A), glow);
            Gfx.Draw(Gfx.Cube, Gfx.Mul(Gfx.Scale(1.8f, 0.004f, 0.004f), Gfx.Translate(0, -0.002f, -0.452f)), Gfx.Rgba(0x0A2A3A), glow * 0.7f);

            // Decorative stock bottles on the shelf.
            for (int i = 0; i < 9; i++)
            {
                float x = -0.75f + i * 0.18f;
                var col = Theme.Series[i % Theme.Series.Length];
                Gfx.Draw(Gfx.Vessel(0.03f, 0.12f, 0.003f, 0.012f, 0.08f), Gfx.Translate(x, 0.46f, 0.68f), new Color((int)col.R, col.G, col.B, 110));
            }

            // 5 cm grid on the bench.
            var line = new Color(40, 52, 70, 255);
            for (float x = -0.9f; x <= 0.9001f; x += 0.05f)
            {
                DrawLine3D(new Vector3(x, 0.0006f, -0.45f), new Vector3(x, 0.0006f, 0.45f), line);
            }

            for (float z = -0.45f; z <= 0.4501f; z += 0.05f)
            {
                DrawLine3D(new Vector3(-0.9f, 0.0006f, z), new Vector3(0.9f, 0.0006f, z), line);
            }
        }

        private static void DrawShadow(Lab lab, Item item)
        {
            if (item.Host != null) return;
            var box = lab.WorldBox(item);
            float r = Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) * 0.5f;
            float height = Math.Max(0, box.Min.Y);
            float a = Math.Clamp(0.35f - height * 2f, 0.06f, 0.35f);
            var c = new Vector3((box.Min.X + box.Max.X) / 2, 0.0012f, (box.Min.Z + box.Max.Z) / 2);
            Gfx.Draw(Gfx.Disc, Gfx.Mul(Gfx.Scale(r, 1, r), Gfx.Translate(c)), new Color(0, 0, 0, (int)(a * 255)));
        }

        // ------------------------------------------------------------------ equipment

        private static void DrawOpaque(Item item)
        {
            var pose = Gfx.Pose(item.Position, item.Rotation);
            switch (item.Def.VisualKey)
            {
                case "burner": DrawBurner(item, pose); break;
                case "thermometer": DrawProbe(item, pose, false); break;
                case "ph_probe": DrawProbe(item, pose, true); break;
                case "bottle":
                {
                    var c = item.Container;
                    float h = (float)c.InnerHeightM, r = (float)c.RadiusM + Wall;
                    var accent = Gfx.FromCore(item.Def.AccentColor);
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(r + 0.0008f, h * 0.32f, r + 0.0008f), Gfx.Translate(0, h * 0.22f, 0)), pose), Gfx.Lerp(accent, Color.White, 0.25f));
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0175f, 0.01f, 0.0175f), Gfx.Translate(0, h + 0.004f, 0)), pose), accent);
                    break;
                }

                case "jar":
                {
                    var c = item.Container;
                    float h = (float)c.InnerHeightM, r = (float)c.RadiusM + Wall;
                    Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(r + 0.0008f, 0.012f, r + 0.0008f), Gfx.Translate(0, h * 0.35f, 0)), pose), Gfx.FromCore(item.Def.AccentColor));
                    break;
                }

                case "beaker":
                {
                    var c = item.Container;
                    float r = (float)c.RadiusM + Wall;
                    for (int ml = 50; ml <= 250; ml += 50)
                    {
                        float y = ml / 1000f / (MathF.PI * (float)(c.RadiusM * c.RadiusM));
                        float w = ml % 100 == 0 ? 0.014f : 0.008f;
                        Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(w, 0.0012f, 0.0008f), Gfx.Translate(0, y, -r - 0.0004f)), pose), Gfx.Rgba(0xE8F0FF));
                    }

                    break;
                }

                default:
                    if (item.Container == null && item.Heat == null && item.Probe == null)
                    {
                        var s = item.Size;
                        Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(s.X, s.Y, s.Z), Gfx.Translate(0, s.Y / 2, 0)), pose), Gfx.Rgba(0x777F8A));
                    }

                    break;
            }
        }

        private static void DrawBurner(Item item, Matrix4x4 pose)
        {
            const float barrelTop = 0.13f, gauze = 0.205f;
            float valve = (float)item.Heat.GasValve;
            var baseMesh = Gfx.CachedLathe("burnerBase", new[] { new Vector2(0, 0), new Vector2(0.042f, 0), new Vector2(0.042f, 0.006f), new Vector2(0.03f, 0.014f), new Vector2(0, 0.014f) });
            Gfx.Draw(baseMesh, pose, DarkPlastic);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0085f, barrelTop - 0.014f, 0.0085f), Gfx.Translate(0, 0.014f, 0)), pose), Brass);
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.011f, 0.014f, 0.011f), Gfx.Translate(0, 0.03f, 0)), pose), Brass);
            // Gas hose to the back.
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Mul(Gfx.Scale(0.005f, 0.12f, 0.005f), Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2))), Gfx.Translate(0, 0.008f, 0.02f)), pose), Gfx.Rgba(0xE07020));

            // Gas valve knob facing the student (−Z), pointer shows the valve opening.
            var knobRot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2) * Quaternion.Identity;
            var knobLocal = Gfx.Mul(Gfx.Mul(Gfx.Scale(0.011f, 0.01f, 0.011f), Gfx.Rotate(knobRot)), Gfx.Translate(0, 0.009f, -0.042f));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(knobLocal, pose), Gfx.Rgba(0xD92626));
            var spin = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -valve * MathF.PI / 2);
            var pointerLocal = Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0016f, 0.009f, 0.0016f), Gfx.Rotate(spin)), Gfx.Translate(0, 0.009f, -0.0525f));
            Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Translate(0, 0.5f, 0), pointerLocal), pose), Color.White, new Vector3(0.6f));

            // Tripod: ring + three legs, wire gauze with ceramic centre (glows when hot).
            var ring = Gfx.CachedLathe("tripodRing", new[] { new Vector2(0.06f, 0), new Vector2(0.066f, 0), new Vector2(0.066f, 0.004f), new Vector2(0.06f, 0.004f), new Vector2(0.06f, 0) }, 40);
            Gfx.Draw(ring, Gfx.Part(Gfx.Translate(0, gauze - 0.004f, 0), pose), Steel);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 90f) * MathF.PI / 180f; // no leg directly in front of the burner
                var top = new Vector3(MathF.Cos(a) * 0.063f, gauze - 0.002f, MathF.Sin(a) * 0.063f);
                var foot = new Vector3(MathF.Cos(a) * 0.085f, 0, MathF.Sin(a) * 0.085f);
                var dir = Vector3.Normalize(top - foot);
                var rot = FromTo(Vector3.UnitY, dir);
                var local = Gfx.Mul(Gfx.Mul(Gfx.Scale(0.0025f, Vector3.Distance(top, foot), 0.0025f), Gfx.Rotate(rot)), Gfx.Translate(foot));
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(local, pose), Steel);
            }

            Gfx.Draw(Gfx.Cube, Gfx.Part(Gfx.Mul(Gfx.Scale(0.14f, 0.002f, 0.14f), Gfx.Translate(0, gauze + 0.001f, 0)), pose), Gfx.Rgba(0x5A5E66));
            float glow = item.Heat.IsLit ? valve * valve : 0f;
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.035f, 0.0026f, 0.035f), Gfx.Translate(0, gauze + 0.0002f, 0)), pose),
                Gfx.Rgba(0xD8D8D0), new Vector3(1f, 0.25f, 0.05f) * glow * 0.9f);
        }

        private static void DrawProbe(Item item, Matrix4x4 pose, bool ph)
        {
            const float length = 0.2f;
            var accent = Gfx.FromCore(item.Def.AccentColor);
            if (!ph)
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(0.0022f, length * 0.68f, 0.0022f), pose), Gfx.Rgba(0xC0C5CC));
            }

            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.008f, length * 0.32f, 0.008f), Gfx.Translate(0, length * 0.68f, 0)), pose), Gfx.Rgba(0x1F2228));
            Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(0.0085f, 0.008f, 0.0085f), Gfx.Translate(0, length * 0.7f, 0)), pose), accent);
        }

        private static void DrawTransparent(Item item)
        {
            var pose = Gfx.Pose(item.Position, item.Rotation);
            if (item.Probe != null)
            {
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Scale(0.005f, 0.2f * 0.68f, 0.005f), pose), new Color(215, 235, 255, 90));
                return;
            }

            var c = item.Container;
            float r = (float)c.RadiusM, h = (float)c.InnerHeightM;
            if (!c.Contents.IsEmpty && c.LiquidHeightM > 0.0004)
            {
                var col = Gfx.FromCore(c.LiquidColor);
                col.A = (byte)Math.Max((int)col.A, 120);
                float lh = (float)Math.Min(c.LiquidHeightM, h - Wall);
                Gfx.Draw(Gfx.Cylinder, Gfx.Part(Gfx.Mul(Gfx.Scale(r - 0.0006f, lh, r - 0.0006f), Gfx.Translate(0, Wall, 0)), pose), col);
            }

            Mesh vessel = item.Def.VisualKey == "bottle"
                ? Gfx.Vessel(r + Wall, h + 0.012f, Wall, 0.016f, h * 0.72f)
                : Gfx.Vessel(r + Wall, h, Wall);
            Gfx.Draw(vessel, pose, Glass);
        }

        private static void DrawFlames(Lab lab)
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            BeginBlendMode(BlendMode.Additive);
            foreach (var item in lab.Items)
            {
                if (item.Heat == null || !item.Heat.IsLit) continue;
                float valve = (float)item.Heat.GasValve;
                float noise = 0.88f + 0.24f * Noise(_flicker + item.Inst.Id * 3.1f);
                var basePos = item.Inst.Pose.TransformPoint(new Vector3(0, 0.13f, 0));
                float outer = (0.025f + 0.05f * valve) * noise;
                DrawCylinderEx(basePos, basePos + new Vector3(0, outer, 0), 0.008f + valve * 0.004f, 0f, 20, new Color(60, 110, 255, 120));
                DrawCylinderEx(basePos, basePos + new Vector3(0, outer * 0.45f, 0), 0.0055f, 0f, 20, new Color(140, 220, 255, 200));
                DrawCylinderEx(basePos + new Vector3(0, outer * 0.7f, 0), basePos + new Vector3(0, outer * 1.15f, 0), 0.004f, 0f, 12, new Color(255, 150, 60, (int)(60 * valve)));
            }

            EndBlendMode();
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
        }

        private static void DrawStreams(Lab lab)
        {
            foreach (var item in lab.Items)
            {
                var c = item.Container;
                if (c == null || !c.IsPouring) continue;
                var lip = c.PourLipPosition;
                var target = c.PourTarget?.Get<LiquidContainer>();
                float endY = target != null ? (float)(target.Owner.Pose.Position.Y + target.LiquidHeightM) + 0.002f : 0.001f;
                float width = Math.Clamp(MathF.Sqrt((float)c.PourRateLitersPerSecond) * 0.03f, 0.002f, 0.012f);
                var col = Gfx.FromCore(c.LiquidColor);
                col.A = (byte)Math.Max(150, (int)col.A);
                var outward = lip - item.Position;
                outward.Y = 0;
                outward = outward.LengthSquared() > 1e-6f ? Vector3.Normalize(outward) * 0.01f : Vector3.Zero;
                var mid = lip + outward + new Vector3(0, -0.01f, 0);
                DrawCylinderEx(lip, mid, width, width * 0.9f, 10, col);
                DrawCylinderEx(mid, new Vector3(mid.X, endY, mid.Z), width * 0.9f, width * 0.6f, 10, col);
            }
        }

        private static void DrawMarkers(Lab lab)
        {
            if (lab.SnapPreview.HasValue)
            {
                var c = lab.SnapPreview.Value;
                var p = c.Target.Pose.TransformPoint(c.TargetSnap.LocalPosition) + new Vector3(0, 0.002f, 0);
                for (int i = 0; i < 4; i++)
                {
                    DrawCircle3D(p, 0.016f + i * 0.0025f, Vector3.UnitX, 90f, new Color(60, 255, 150, 255));
                }
            }

            if (lab.Selected != null)
            {
                var box = lab.WorldBox(lab.Selected);
                float r = Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) * 0.62f;
                var p = new Vector3((box.Min.X + box.Max.X) / 2, box.Min.Y + 0.002f, (box.Min.Z + box.Max.Z) / 2);
                for (int i = 0; i < 3; i++)
                {
                    DrawCircle3D(p, r + i * 0.002f, Vector3.UnitX, 90f, new Color(53, 196, 255, 255));
                }
            }
        }

        /// <summary>2D labels over the 3D view: live probe readouts and reagent names.</summary>
        public static void DrawLabels(Lab lab)
        {
            foreach (var item in lab.Items)
            {
                if (item.Probe != null)
                {
                    var top = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, 0.215f, 0)), lab.Camera);
                    string text = item.Probe.Quantity == ProbeQuantity.Temperature
                        ? (double.IsNaN(item.Probe.Reading) ? "--" : string.Format(CultureInfo.CurrentCulture, "{0:0.0} °C", item.Probe.Reading))
                        : (double.IsNaN(item.Probe.Reading) ? "pH --" : string.Format(CultureInfo.CurrentCulture, "pH {0:0.00}", item.Probe.Reading));
                    Pill(text, top, Gfx.FromCore(item.Def.AccentColor), 20);
                }
                else if (item.Def.VisualKey == "bottle" || item.Def.VisualKey == "jar")
                {
                    var top = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, (float)item.Size.Y + 0.02f, 0)), lab.Camera);
                    Pill(Tr.Name(item.Def), top, Gfx.FromCore(item.Def.AccentColor), 14);
                }
            }
        }

        private static void Pill(string text, Vector2 at, Color accent, float size)
        {
            var m = Ui.Measure(text, size, true);
            float pad = 8 * Ui.S;
            var r = new Rectangle(at.X - m.X / 2 - pad, at.Y - m.Y - pad * 1.6f, m.X + pad * 2, m.Y + pad);
            DrawRectangleRounded(r, 0.5f, 8, Gfx.WithAlpha(Theme.Raised, 0.88f));
            DrawRectangleRoundedLinesEx(r, 0.5f, 8, 1.5f * Ui.S, accent);
            Ui.Text(text, r.X + pad, r.Y + pad / 2, size, Theme.Text, true);
        }

        private static float Noise(float t) => 0.5f + 0.25f * MathF.Sin(t * 1.7f) + 0.15f * MathF.Sin(t * 3.9f + 1.3f) + 0.1f * MathF.Sin(t * 7.3f);

        private static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            var axis = Vector3.Cross(from, to);
            float d = Vector3.Dot(from, to);
            if (axis.LengthSquared() < 1e-8f) return d > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(d, -1f, 1f)));
        }
    }
}
