using System;
using System.Linq;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Components;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Per-frame visuals of the lab tab: room, equipment, liquids, pour streams, flames and particles.</summary>
    internal static class Scene
    {
        private static float _time;
        private static readonly Random Rng = new Random(3);
        private static float _dripAccumulator, _chunkAccumulator, _splashTimer;

        /// <summary>Emits time-based effects that follow the simulation (bubbles, drops, falling solids, steam, fire).</summary>
        public static void Update(Lab lab, float dt)
        {
            _time += dt;
            _splashTimer -= dt;
            Streams.Update(dt);
            foreach (var item in lab.Items)
            {
                var c = item.Container;
                if (c == null) continue;
                Bubbles.Update(item, dt);
                if (c.TemperatureC > 70 && c.Contents.HasLiquid)
                {
                    lab.Effects.Steam(c.MouthPosition, (float)c.MouthRadiusM * 0.8f, c.IsBoiling ? 30f : 5f, dt);
                }

                if (c.OnFire) lab.Effects.Fire(c.MouthPosition, (float)Math.Max(0.012, c.MouthRadiusM), dt);

                if (c.IsPouring)
                {
                    var col = Gfx.FromCore(c.LiquidColor);
                    float landY = LandingHeight(c.PourTarget?.Get<LiquidContainer>());
                    var outward = PourOutward(item, c.PourLipPosition);
                    if (c.IsPouringSolid)
                    {
                        var solid = c.Contents.Solids().OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
                        var sc = solid != null ? Gfx.FromCore(solid.Color) : Color.Gray;
                        float size = solid != null ? Math.Max(0.002f, VesselState.PieceStyle(solid.Form).size * 0.7f) : 0.003f;
                        _chunkAccumulator += dt * 10f;
                        while (_chunkAccumulator >= 1)
                        {
                            _chunkAccumulator -= 1;
                            lab.Effects.Chunk(c.PourLipPosition, outward * 0.3f + new Vector3(Rand() * 0.03f, 0, Rand() * 0.03f), sc, size, landY);
                        }
                    }
                    else if (c.PourRateLitersPerSecond < 0.0012)
                    {
                        _dripAccumulator += dt * (float)Math.Clamp(c.PourRateLitersPerSecond * 20000, 3, 25);
                        while (_dripAccumulator >= 1)
                        {
                            _dripAccumulator -= 1;
                            lab.Effects.Drop(c.PourLipPosition, outward * 0.15f, col, landY);
                        }
                    }
                    else if (_splashTimer <= 0 && Streams.HasLanded(item.Inst.Id))
                    {
                        var end = StreamEnd(c.PourLipPosition, outward * StreamSpeed(c), landY);
                        lab.Effects.Splash(end, col, 4);
                    }
                }

                {
                    bool flowing = c.IsPouring && !c.IsPouringSolid && c.PourRateLitersPerSecond >= 0.0012;
                    float landY = flowing ? LandingHeight(c.PourTarget?.Get<LiquidContainer>()) : 0;
                    var v0 = flowing ? PourOutward(item, c.PourLipPosition) * StreamSpeed(c) : Vector3.Zero;
                    Streams.Feed(item.Inst.Id, flowing, flowing ? c.PourLipPosition : Vector3.Zero, v0, landY, c.PourRateLitersPerSecond,
                        Gfx.FromCore(c.LiquidColor), dt);
                }

                var tap = item.Tap;
                if (tap != null && tap.IsFlowing)
                {
                    var col = Gfx.FromCore(c.LiquidColor);
                    float landY = LandingHeight(tap.Target?.Get<LiquidContainer>());
                    if (tap.FlowLitersPerSecond < 0.0009)
                    {
                        _dripAccumulator += dt * (float)Math.Clamp(tap.FlowLitersPerSecond * 20000, 2, 20);
                        while (_dripAccumulator >= 1)
                        {
                            _dripAccumulator -= 1;
                            lab.Effects.Drop(tap.OutletPosition, Vector3.Zero, col, landY);
                        }
                    }
                    else if (_splashTimer <= 0 && Streams.HasLanded(-item.Inst.Id))
                    {
                        var o = tap.OutletPosition;
                        lab.Effects.Splash(new Vector3(o.X, landY, o.Z), col, 3);
                    }
                }

                if (tap != null)
                {
                    bool flowing = tap.IsFlowing && tap.FlowLitersPerSecond >= 0.0009;
                    float landY = flowing ? LandingHeight(tap.Target?.Get<LiquidContainer>()) : 0;
                    Streams.Feed(-item.Inst.Id, flowing, tap.OutletPosition, new Vector3(0, -0.12f, 0), landY, tap.FlowLitersPerSecond,
                        Gfx.FromCore(c.LiquidColor), dt);
                }
            }

            if (_splashTimer <= 0) _splashTimer = 0.08f;
        }

        private static float LandingHeight(LiquidContainer target)
        {
            if (target == null) return 0.001f;
            var shape = VesselShapeFor(target);
            float level = shape != null ? shape.HeightForVolume(target.Contents.VolumeLiters) : (float)target.LiquidHeightM;
            return target.BasePosition.Y + level;
        }

        private static VesselShape VesselShapeFor(LiquidContainer c)
        {
            var item = Program.LabInstance?.Find(c.Owner);
            return item != null ? VesselShape.For(item) : null;
        }

        private static Vector3 PourOutward(Item item, Vector3 lip)
        {
            var outward = lip - (item.Container.BasePosition + Vector3.Transform(new Vector3(0, (float)item.Container.InnerHeightM, 0), item.Rotation));
            outward.Y = 0;
            return outward.LengthSquared() > 1e-8f ? Vector3.Normalize(outward) : Vector3.Zero;
        }

        /// <summary>Horizontal outflow speed at the lip (m/s): a stronger flow shoots further out.</summary>
        private static float StreamSpeed(LiquidContainer c)
        {
            float speed = Math.Clamp(0.16f + (float)c.PourRateLitersPerSecond * 25f, 0.16f, 0.55f);
            var target = c.PourTarget?.Get<LiquidContainer>();
            if (target == null) return speed;
            // Keep the landing point inside the receiving vessel's mouth.
            float drop = Math.Max(0.005f, c.PourLipPosition.Y - LandingHeight(target));
            float fall = MathF.Sqrt(2 * drop / 9.81f);
            return Math.Min(speed, Math.Max(0.02f, 0.45f * (float)target.MouthRadiusM / fall));
        }

        private static Vector3 StreamEnd(Vector3 lip, Vector3 velocity, float landY)
        {
            float drop = Math.Max(0, lip.Y - landY);
            float t = MathF.Sqrt(2 * drop / 9.81f);
            return new Vector3(lip.X + velocity.X * t, landY + 0.001f, lip.Z + velocity.Z * t);
        }

        // ------------------------------------------------------------------ draw

        public static void Draw(Lab lab, float dt)
        {
            var cam = lab.Camera;
            Gfx.Ambient = new Vector3(0.46f, 0.48f, 0.52f) + lab.Effects.Flash * 0.6f;
            Gfx.FogColor = new Vector3(0.78f, 0.8f, 0.83f);
            Gfx.FogRange = new Vector2(4f, 8f);
            Gfx.BeginFrame(cam.Position);

            Room.DrawOpaque();
            foreach (var item in lab.Items) DrawShadow(lab, item);
            foreach (var item in lab.Items) Props.DrawOpaque(item, _time);
            lab.Effects.DrawSolids();
            DrawFlames(lab);
            DrawMarkers(lab);

            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            Room.DrawTransparent();
            foreach (var item in lab.Items.OrderByDescending(i => Vector3.DistanceSquared(i.Position, cam.Position)))
            {
                Props.DrawTransparent(item);
            }

            Streams.Draw();
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
            lab.Effects.Draw();
        }

        private static void DrawShadow(Lab lab, Item item)
        {
            if (item.Host != null) return;
            var box = lab.WorldBox(item);
            float r = Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) * 0.5f;
            float height = Math.Max(0, box.Min.Y);
            float a = Math.Clamp(0.32f - height * 2f, 0.05f, 0.32f);
            var c = new Vector3((box.Min.X + box.Max.X) / 2, 0.0012f, (box.Min.Z + box.Max.Z) / 2);
            Gfx.Draw(Gfx.Disc, Gfx.Mul(Gfx.Scale(r, 1, r), Gfx.Translate(c)), new Color(0, 0, 0, (int)(a * 255)));
        }

        private static void DrawFlames(Lab lab)
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthMask();
            BeginBlendMode(BlendMode.Additive);
            foreach (var item in lab.Items)
            {
                if (item.Def.VisualKey != "burner" || !item.Heat.IsLit) continue;
                float valve = (float)item.Heat.GasValve;
                float noise = 0.88f + 0.24f * Noise(_time * 14f + item.Inst.Id * 3.1f);
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

        private static void DrawMarkers(Lab lab)
        {
            if (lab.SnapPreview.HasValue)
            {
                var c = lab.SnapPreview.Value;
                var p = c.Target.Pose.TransformPoint(c.TargetSnap.LocalPosition) + new Vector3(0, 0.002f, 0);
                for (int i = 0; i < 4; i++) DrawCircle3D(p, 0.016f + i * 0.0025f, Vector3.UnitX, 90f, new Color(60, 255, 150, 255));
            }

            if (lab.Selected != null)
            {
                var box = lab.WorldBox(lab.Selected);
                float r = Math.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z) * 0.62f;
                var p = new Vector3((box.Min.X + box.Max.X) / 2, box.Min.Y + 0.002f, (box.Min.Z + box.Max.Z) / 2);
                for (int i = 0; i < 3; i++) DrawCircle3D(p, r + i * 0.002f, Vector3.UnitX, 90f, new Color(53, 196, 255, 255));
            }
        }

        public static void DrawLabels(Lab lab) => Props.DrawLabels(lab);

        private static float Noise(float t) => 0.5f + 0.25f * MathF.Sin(t * 1.7f) + 0.15f * MathF.Sin(t * 3.9f + 1.3f) + 0.1f * MathF.Sin(t * 7.3f);
        private static float Rand() => (float)(Rng.NextDouble() * 2 - 1);
    }
}
