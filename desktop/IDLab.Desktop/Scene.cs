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
                EmitFoam(lab, item, c, dt);
                if (c.TemperatureC > 70 && c.Contents.HasLiquid)
                {
                    lab.Effects.Steam(c.MouthPosition, (float)c.MouthRadiusM * 0.8f, c.IsBoiling ? 30f : 5f, dt);
                }

                if (c.OnFire)
                {
                    var fuel = c.BurningSubstance;
                    if (fuel != null && fuel.Phase == IDLab.Core.Chemistry.SubstancePhase.Solid)
                    {
                        // A burning solid: the flame sits on the pile at the bottom and takes the element's colour.
                        var (tint, dazzling, smoke) = FlameStyle(fuel.ElementSymbol);
                        var pile = c.BasePosition + new Vector3(0, (float)Math.Max(0.004, c.SolidHeightM), 0);
                        lab.Effects.Fire(pile, (float)Math.Max(0.008, c.RadiusM * 0.6), dt, tint, dazzling, smoke);
                    }
                    else
                    {
                        lab.Effects.Fire(c.MouthPosition, (float)Math.Max(0.012, c.MouthRadiusM), dt);
                    }
                }

                // The lip follows the drawn vessel every frame (the simulation's copy only updates at 50 Hz,
                // which made the stream judder against the bottle neck).
                var lip = item.CanTilt ? item.Position + lab.LipOffset(item) : c.PourLipPosition;
                if (c.IsPouring)
                {
                    var col = Gfx.FromCore(c.LiquidColor);
                    float landY = LandingHeight(c.PourTarget?.Get<LiquidContainer>());
                    var outward = PourOutward(item, lip);
                    if (c.IsPouringSolid)
                    {
                        var solid = c.Contents.Solids().OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
                        var sc = solid != null ? Gfx.FromCore(solid.Color) : Color.Gray;
                        float piece = solid != null ? VesselState.PieceStyle(solid.Form).size : 0;
                        bool powder = piece <= 0.0026f;
                        float size = powder ? 0.0021f : Math.Max(0.0025f, piece * 0.7f);
                        // Powders run out as a dense trickle of grains; granules and chunks as fewer, larger pieces.
                        float perSecond = powder ? (float)Math.Clamp(c.PourRateLitersPerSecond * 1000 * 35, 40, 110) : 12f;
                        _chunkAccumulator += dt * perSecond;
                        while (_chunkAccumulator >= 1)
                        {
                            _chunkAccumulator -= 1;
                            var grain = Gfx.Lerp(sc, Rand() > 0 ? Color.White : Color.Black, Math.Abs(Rand()) * 0.18f);
                            lab.Effects.Chunk(lip + new Vector3(Rand() * 0.004f, 0, Rand() * 0.004f),
                                outward * 0.3f + new Vector3(Rand() * 0.03f, 0, Rand() * 0.03f), grain, size * (0.8f + Math.Abs(Rand()) * 0.5f), landY);
                        }
                    }
                    else if (c.PourRateLitersPerSecond < 0.0012)
                    {
                        _dripAccumulator += dt * (float)Math.Clamp(c.PourRateLitersPerSecond * 20000, 3, 25);
                        while (_dripAccumulator >= 1)
                        {
                            _dripAccumulator -= 1;
                            lab.Effects.Drop(lip, outward * 0.15f, col, landY);
                        }
                    }
                    else if (_splashTimer <= 0 && Streams.HasLanded(item.Inst.Id))
                    {
                        var end = StreamEnd(lip, outward * StreamSpeed(c), landY);
                        lab.Effects.Splash(end, col, 4);
                    }
                }

                {
                    bool flowing = c.IsPouring && !c.IsPouringSolid && c.PourRateLitersPerSecond >= 0.0012;
                    float landY = flowing ? LandingHeight(c.PourTarget?.Get<LiquidContainer>()) : 0;
                    var v0 = flowing ? PourOutward(item, lip) * StreamSpeed(c) : Vector3.Zero;
                    Streams.Feed(item.Inst.Id, flowing, flowing ? lip : Vector3.Zero, v0, landY, c.PourRateLitersPerSecond,
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
                float id = item.Inst.Id * 3.1f;
                var basePos = item.Inst.Pose.TransformPoint(new Vector3(0, 0.134f, 0));
                // A roaring blue Bunsen flame: pale inner cone, blue-violet mantle that sways and licks upwards,
                // and a faint orange tip when the gas is turned up (incomplete combustion at the top).
                float height = (0.03f + 0.055f * valve) * (0.92f + 0.16f * Noise(_time * 13f + id));
                float r0 = 0.0074f + valve * 0.0016f;
                var sway = new Vector3(0.002f * MathF.Sin(_time * 9f + id), 0, 0.002f * MathF.Cos(_time * 7f + id)) * valve;
                for (int layer = 0; layer < 4; layer++)
                {
                    float k = 1f - layer * 0.12f;
                    float hl = height * k * (0.95f + 0.1f * Noise(_time * 17f + layer * 1.7f + id));
                    DrawCylinderEx(basePos, basePos + new Vector3(0, hl, 0) + sway * (1 + layer), r0 * (1.05f - layer * 0.08f), 0f, 18,
                        new Color(50 + layer * 10, 90 + layer * 15, 255, 46 + layer * 10));
                }

                for (int t = 0; t < 3; t++)
                {
                    float a = _time * (2.3f + t) + t * 2.1f + id;
                    var off = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * r0 * 0.45f;
                    float th = height * (0.55f + 0.35f * Noise(_time * 21f + t * 4.3f + id));
                    DrawCylinderEx(basePos + off * 0.5f, basePos + off + new Vector3(0, th, 0) + sway * 2, r0 * 0.42f, 0f, 10, new Color(90, 120, 255, 55));
                }

                float inner = height * (0.36f + 0.04f * Noise(_time * 25f + id));
                DrawCylinderEx(basePos, basePos + new Vector3(0, inner, 0), r0 * 0.72f, 0f, 18, new Color(120, 230, 255, 170));
                DrawCylinderEx(basePos, basePos + new Vector3(0, inner * 0.7f, 0), r0 * 0.45f, 0f, 14, new Color(210, 250, 255, 150));
                if (valve > 0.55f)
                {
                    DrawCylinderEx(basePos + new Vector3(0, height * 0.72f, 0) + sway, basePos + new Vector3(0, height * 1.12f, 0) + sway * 3, r0 * 0.5f, 0f, 10,
                        new Color(255, 150, 60, (int)(90 * (valve - 0.5f))));
                }
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

        private static float _foamAccumulator;

        /// <summary>
        /// Elephant toothpaste: oxygen rushing out of a soapy mixture is trapped as foam that is pushed out of the mouth
        /// as a thick, rising column (faster gas → taller column), coloured by the liquid (add food colouring!).
        /// </summary>
        private static void EmitFoam(Lab lab, Item item, LiquidContainer c, float dt)
        {
            if (c.GasFormula != "O₂" || c.GasRateMolPerS < 4e-4 || c.Contents.Moles(SubstanceIds.Detergent) < 1e-5) return;
            float strength = (float)Math.Clamp(c.GasRateMolPerS / 0.012, 0.0, 1.6);
            _foamAccumulator += dt * (45f + 170f * strength);
            var tint = Gfx.FromCore(c.LiquidColor);
            var foam = Gfx.Lerp(new Color(250, 250, 246, 255), new Color(tint.R, tint.G, tint.B, (byte)255), Math.Clamp((tint.A / 255f - 0.3f) * 1.3f, 0f, 0.7f));
            var mouth = c.MouthPosition;
            float r = (float)c.MouthRadiusM;
            while (_foamAccumulator >= 1)
            {
                _foamAccumulator -= 1;
                float a = (float)(Rng.NextDouble() * Math.PI * 2);
                float off = (float)Rng.NextDouble() * r * 0.55f;
                var start = mouth + new Vector3(MathF.Cos(a) * off, 0.004f, MathF.Sin(a) * off);
                float side = 0.015f + (float)Rng.NextDouble() * (0.04f + 0.09f * strength);
                var v = new Vector3(MathF.Cos(a) * side, 0.22f + (0.5f + (float)Rng.NextDouble() * 0.2f) * strength, MathF.Sin(a) * side);
                float size = (float)(r * (0.26f + Rng.NextDouble() * 0.22f));
                var shade = Gfx.Lerp(foam, Color.White, (float)Rng.NextDouble() * 0.2f);
                lab.Effects.FoamBlob(start, v, shade, Math.Max(0.006f, size));
            }
        }

        /// <summary>Flame colour of a burning element (flame-test colours), whether it is blinding, whether it smokes white.</summary>
        private static (Color tint, bool dazzling, bool smoke) FlameStyle(string element)
        {
            switch (element)
            {
                case "Mg": return (new Color(255, 255, 255, 255), true, true);
                case "Al": case "Ti": case "Zr": return (new Color(255, 250, 235, 255), true, true);
                case "P": return (new Color(255, 236, 170, 255), true, true);
                case "S": return (new Color(80, 110, 255, 255), false, false);
                case "Na": return (new Color(255, 190, 40, 255), false, true);
                case "K": case "Rb": case "Cs": return (new Color(200, 120, 255, 255), false, true);
                case "Li": case "Sr": return (new Color(255, 40, 60, 255), false, true);
                case "Ca": return (new Color(255, 110, 40, 255), false, true);
                case "Ba": return (new Color(150, 255, 90, 255), false, true);
                case "Cu": return (new Color(60, 255, 170, 255), false, false);
                case "Zn": return (new Color(200, 230, 255, 255), true, true);
                case "Fe": return (new Color(255, 170, 60, 255), false, false);
                case null: return (new Color(255, 170, 50, 255), false, false); // organic solids: sooty yellow flame
                default: return (new Color(255, 200, 120, 255), false, true);
            }
        }

        private static float Noise(float t) => 0.5f + 0.25f * MathF.Sin(t * 1.7f) + 0.15f * MathF.Sin(t * 3.9f + 1.3f) + 0.1f * MathF.Sin(t * 7.3f);
        private static float Rand() => (float)(Rng.NextDouble() * 2 - 1);
    }
}
