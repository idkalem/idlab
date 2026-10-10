using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Core.Components
{
    /// <summary>
    /// An open vessel (beaker, flask, bottle, jar, test tube, burette...) holding a <see cref="Mixture"/>,
    /// modelled for physics as a cylinder of the same capacity. Each tick: reactions (instant + kinetic) →
    /// lumped thermal model with boiling → fire → tilt-driven pouring (liquid, or solid pieces when there
    /// is no liquid) into whatever is under the lip → overflow.
    /// The vessel's base centre sits at <see cref="LocalOffset"/> in the equipment's frame; +Y is the mouth.
    /// </summary>
    public sealed class LiquidContainer : LabComponent
    {
        private const double MaxGlassTemperatureC = 500.0;
        private const double SpillEventCooldownS = 2.0;
        private const double FireBurnRateKgPerS = 0.00005;     // 0.05 g/s of fuel
        private const double CombustionEnthalpyJPerKg = 2.97e7; // ethanol
        private const double SolidPourRateLps = 0.004;          // 4 mL/s through a 4 cm mouth

        private readonly ReagentPreset _initialContents;
        private readonly HashSet<string> _activeRules = new HashSet<string>();
        private double _pendingHeatJ;
        private int _lastReactedVersion = -1;
        private int _lastReactBucket = int.MinValue;
        private bool _reacting;
        private int _lastPhVersion = -1;
        private double _ph = double.NaN;
        private double _lastSpillEventTime = double.NegativeInfinity;
        private double _fireCooldown;
        private bool _flameBelow, _flameBelowNext;
        private double _hotSurfaceC, _hotSurfaceNextC;
        private double _popCooldown, _tarnishNoticeTime = double.NegativeInfinity;

        public double InnerDiameterM { get; }
        public double InnerHeightM { get; }
        public double GlassMassKg { get; }

        /// <summary>Inner diameter of the opening liquid pours through and into (a bottle neck is narrower than its body).</summary>
        public double MouthDiameterM { get; }

        /// <summary>Base centre of the vessel in the equipment's local frame (e.g. a burette held up by its stand).</summary>
        public Vector3 LocalOffset { get; }

        public Mixture Contents { get; private set; }

        /// <summary>Bulk temperature of glass + contents (°C).</summary>
        public double TemperatureC { get; private set; }

        public bool IsBoiling { get; private set; }
        public bool OnFire { get; private set; }

        /// <summary>What is burning while <see cref="OnFire"/> (a liquid's vapour, or a solid such as Mg or S).</summary>
        public Substance BurningSubstance { get; private set; }

        /// <summary>Gas leaving the mixture (mol/s, smoothed) and what it is — drives bubbles in the view.</summary>
        public double GasRateMolPerS { get; private set; }
        public string GasFormula { get; private set; }

        // Pour state, read by the front-end to draw the stream.
        public bool IsPouring { get; private set; }
        public bool IsPouringSolid { get; private set; }
        public double PourRateLitersPerSecond { get; private set; }
        public Vector3 PourLipPosition { get; private set; }
        public EquipmentInstance PourTarget { get; private set; }

        public LiquidContainer(double innerDiameterM, double innerHeightM, double glassMassKg, ReagentPreset initialContents,
            double mouthDiameterM = 0, Vector3 localOffset = default)
        {
            MouthDiameterM = mouthDiameterM > 0 ? Math.Min(mouthDiameterM, innerDiameterM) : innerDiameterM;
            InnerDiameterM = innerDiameterM;
            InnerHeightM = innerHeightM;
            GlassMassKg = glassMassKg;
            LocalOffset = localOffset;
            _initialContents = initialContents;
        }

        public override int StepOrder => OrderContainers;

        public double RadiusM => InnerDiameterM * 0.5;
        public double MouthRadiusM => MouthDiameterM * 0.5;
        public double CapacityLiters => Math.PI * RadiusM * RadiusM * InnerHeightM * 1000.0;
        public double VolumeLiters => Contents.VolumeLiters;
        public double VolumeMilliliters => VolumeLiters * 1000.0;
        public double LiquidVolumeMilliliters => Contents.LiquidVolumeLiters * 1000.0;
        public double SolidVolumeMilliliters => Contents.SolidVolumeLiters * 1000.0;
        public double FillFraction => Math.Min(1.0, VolumeLiters / CapacityLiters);
        public double LiquidHeightM => FillFraction * InnerHeightM;
        public double SolidHeightM => Math.Min(InnerHeightM, Contents.SolidVolumeLiters / CapacityLiters * InnerHeightM);
        public double GlassHeatCapacity => GlassMassKg * PhysicalConstants.GlassSpecificHeat;

        /// <summary>Glass + contents, in grams (what a balance shows).</summary>
        public double TotalMassGrams => (GlassMassKg + Contents.MassKg) * 1000.0;

        public double PH
        {
            get
            {
                if (_lastPhVersion != Contents.Version)
                {
                    _ph = AcidBase.ComputePH(Contents);
                    _lastPhVersion = Contents.Version;
                }

                return _ph;
            }
        }

        public ColorRGBA LiquidColor => Contents.DisplayColor(PH);

        /// <summary>World position of the centre of the base.</summary>
        public Vector3 BasePosition => Owner.Pose.TransformPoint(LocalOffset);

        /// <summary>World position of the centre of the mouth.</summary>
        public Vector3 MouthPosition => Owner.Pose.TransformPoint(LocalOffset + new Vector3(0, (float)InnerHeightM, 0));

        protected override void OnAttached()
        {
            TemperatureC = World.AmbientTemperatureC;
            Contents = new Mixture(World.Substances, TemperatureC);
            _initialContents?.FillInto(Contents);
        }

        /// <summary>Queues heat (J) to be applied on this container's next step (from burners, reactions...).</summary>
        public void AddHeat(double joules) => _pendingHeatJ += joules;

        /// <summary>Adds poured material; temperature equilibrates with glass + existing contents.</summary>
        public void ReceiveLiquid(Mixture incoming)
        {
            if (incoming == null || incoming.IsEmpty)
            {
                return;
            }

            double cHere = GlassHeatCapacity + Contents.HeatCapacity;
            double cIn = incoming.HeatCapacity;
            double t = (TemperatureC * cHere + incoming.TemperatureC * cIn) / Math.Max(1e-9, cHere + cIn);
            Contents.MergeFrom(incoming);
            SetTemperature(t);
        }

        /// <summary>Directly adds a substance (used by spatulas, droppers, tests).</summary>
        public void AddSubstance(string substanceId, double moles) => Contents.Add(substanceId, moles);

        /// <summary>Lights flammable vapour above the mouth (an open flame next to warm flammable liquid).</summary>
        public void Ignite()
        {
            var fuel = FlammableFuel();
            if (OnFire || _fireCooldown > 0 || fuel == null)
            {
                return;
            }

            OnFire = true;
            BurningSubstance = fuel;
            Publish(LabEventType.Fire, 0.7, $"{fuel.Name} vapour caught fire! Keep flammable liquids away from open flames.", MouthPosition, "fire.vapour:" + fuel.Id);
        }

        /// <summary>
        /// Called by a heat source every tick it heats this container: an open flame lights vapours and burns
        /// solids; a hot plate only ignites what its glowing surface (<paramref name="surfaceC"/>) can light.
        /// </summary>
        public void ExposeToHeat(bool openFlame, double surfaceC)
        {
            _flameBelowNext |= openFlame;
            _hotSurfaceNextC = Math.Max(_hotSurfaceNextC, surfaceC);
        }

        /// <summary>Puts the fire out (cover the mouth). It can re-ignite after a few seconds if still heated by a flame.</summary>
        public void Extinguish()
        {
            OnFire = false;
            BurningSubstance = null;
            _fireCooldown = 4.0;
        }

        public override void Step(double dt)
        {
            _fireCooldown = Math.Max(0, _fireCooldown - dt);
            _popCooldown = Math.Max(0, _popCooldown - dt);
            _flameBelow = _flameBelowNext;
            _hotSurfaceC = _hotSurfaceNextC;
            _flameBelowNext = false;
            _hotSurfaceNextC = 0;
            React(dt);
            StepIgnition(dt);
            StepFire(dt);
            StepThermal(dt);
            StepPouring(dt);
            StepOverflow();
        }

        private void React(double dt)
        {
            // Re-run the rules when the contents change, while a slow reaction runs, or when the temperature has moved
            // by more than 10 °C (some reactions only start when heated, e.g. Fe + S).
            int bucket = (int)Math.Floor(TemperatureC / 10.0);
            bool warmer = bucket != _lastReactBucket;
            _lastReactBucket = bucket;
            if (Contents.Version == _lastReactedVersion && !_reacting && !warmer)
            {
                GasRateMolPerS *= Math.Exp(-dt * 3);
                _activeRules.Clear();
                return;
            }

            var outcome = World.Reactions.Process(Contents, dt);
            _lastReactedVersion = Contents.Version;
            _reacting = outcome.Ongoing;
            if (outcome.HeatReleasedJ != 0)
            {
                AddHeat(outcome.HeatReleasedJ);
            }

            double gas = 0;
            string formula = GasFormula;
            double biggest = 0;
            foreach (var kv in outcome.GasReleased)
            {
                gas += kv.Value;
                if (kv.Value > biggest)
                {
                    biggest = kv.Value;
                    formula = kv.Key;
                }
            }

            double rate = gas / Math.Max(dt, 1e-4);
            GasRateMolPerS = Math.Max(rate, GasRateMolPerS * Math.Exp(-dt * 3));
            if (gas > 0) GasFormula = formula;

            // Announce a reaction once when it starts (not every tick while it keeps going).
            var nowActive = new HashSet<string>(outcome.AppliedRules);
            foreach (var effect in outcome.Effects)
            {
                if (effect.RuleId == null || !_activeRules.Contains(effect.RuleId))
                {
                    if (effect.Type == LabEventType.Explosion && effect.Severity >= 0.5) BlowOut(effect.Severity);
                    Publish(effect.Type, effect.Severity, effect.Message, MouthPosition, effect.Code);
                    if (effect.RuleId != null) _activeRules.Add(effect.RuleId);
                }
            }

            // An effect stays "announced" while its rule (the part of the id before ':') keeps running.
            _activeRules.RemoveWhere(r => !nowActive.Contains(r.IndexOf(':') < 0 ? r : r.Substring(0, r.IndexOf(':'))));
        }

        private Substance FlammableFuel()
        {
            foreach (var kv in Contents.Components)
            {
                var s = Contents.Database.Get(kv.Key);
                if (s.IsFlammable && s.Phase == SubstancePhase.Liquid && kv.Value > 1e-3)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>Heat meets flammable or explosive contents: vapour fires, vapour explosions, burning solids, detonations.</summary>
        private void StepIgnition(double dt)
        {
            if (!_flameBelow && _hotSurfaceC <= 0) return;
            var db = Contents.Database;
            Substance burnSolid = null, tarnish = null;
            foreach (var kv in Contents.Components)
            {
                if (kv.Value < 1e-5) continue;
                var s = db.Get(kv.Key);
                double boom = Combustion.ExplosionC(s);
                if (!double.IsNaN(boom) && TemperatureC >= boom)
                {
                    Detonate(s, kv.Value);
                    return;
                }

                if (s.Phase == SubstancePhase.Liquid && s.IsFlammable && kv.Value > 1e-3 && !OnFire && _fireCooldown <= 0)
                {
                    bool vapour = TemperatureC >= Math.Max(25, Combustion.FlashPointC(s));
                    bool lit = _flameBelow ? vapour
                        : _hotSurfaceC >= Combustion.AutoIgnitionC(s) && TemperatureC >= (double.IsNaN(s.BoilingPointC) ? 150 : s.BoilingPointC - 3);
                    if (!lit) continue;
                    if (!double.IsNaN(s.BoilingPointC) && s.BoilingPointC < 45 && TemperatureC >= s.BoilingPointC - 12)
                    {
                        // A very volatile liquid near its boiling point fills the air with vapour: it goes off at once.
                        double burst = Math.Min(Contents.Moles(s.Id), Math.Max(0.002, Contents.Moles(s.Id) * 0.6));
                        Contents.Remove(s.Id, burst);
                        AddHeat(burst * s.MolarMass / 1000.0 * CombustionEnthalpyJPerKg * 0.05);
                        Publish(LabEventType.Explosion, Math.Min(1.0, 0.4 + burst * 8), $"{s.Name} vapour exploded!", MouthPosition, "boom.vapour:" + s.Id);
                        _fireCooldown = 0;
                    }

                    OnFire = true;
                    BurningSubstance = s;
                    Publish(LabEventType.Fire, 0.7, $"{s.Name} vapour caught fire!", MouthPosition,
                        (_flameBelow ? "fire.vapour:" : "fire.hotplate:") + s.Id);
                    return;
                }

                if (s.Phase == SubstancePhase.Solid && _flameBelow)
                {
                    double ignite = Combustion.SolidIgnitionC(s);
                    if (!double.IsNaN(ignite) && TemperatureC >= ignite && burnSolid == null) burnSolid = s;
                    double t = Combustion.TarnishC(s);
                    if (!double.IsNaN(t) && TemperatureC >= t) tarnish = s;
                }
            }

            if (burnSolid != null && !OnFire && _fireCooldown <= 0 && !Contents.HasLiquid)
            {
                OnFire = true;
                BurningSubstance = burnSolid;
                Publish(LabEventType.Fire, 0.6, $"{burnSolid.Name} is burning in air.", MouthPosition, "fire.solid:" + burnSolid.Id);
            }

            if (tarnish != null && !Contents.HasLiquid)
            {
                // Copper, lead... turn into their oxide slowly, without a flame (copper goes black: CuO).
                var (oxide, per) = Combustion.OxideOf(db, tarnish);
                if (oxide != null)
                {
                    double n = Math.Min(Contents.Moles(tarnish.Id), Contents.Moles(tarnish.Id) * 0.04 * dt + 2e-5 * dt);
                    Contents.Remove(tarnish.Id, n);
                    Contents.Add(oxide.Id, n * per);
                    if (World.Time - _tarnishNoticeTime > 30)
                    {
                        _tarnishNoticeTime = World.Time;
                        Publish(LabEventType.Reaction, 0.3, $"{tarnish.Name} oxidises to {oxide.Formula}.", MouthPosition, "tarnish:" + tarnish.Id + ":" + oxide.Id);
                    }
                }
            }

            // Hydrogen bubbling out of a mixture right above a flame: it pops.
            if (_flameBelow && _popCooldown <= 0 && GasFormula == "H₂" && GasRateMolPerS > 1e-4)
            {
                _popCooldown = 3;
                Publish(LabEventType.Explosion, Math.Min(0.5, 0.15 + GasRateMolPerS * 200), "Hydrogen pops in the flame!", MouthPosition, "boom.h2");
            }
        }

        /// <summary>A blast in the vessel throws part of the liquid out over the bench.</summary>
        private void BlowOut(double severity)
        {
            double fraction = Math.Min(0.6, 0.15 + 0.45 * severity);
            if (Contents.HasLiquid) Contents.TakeLiquid(Contents.LiquidVolumeLiters * fraction);
        }

        private void Detonate(Substance s, double moles)
        {
            double kg = moles * s.MolarMass / 1000.0;
            Contents.Remove(s.Id, moles);
            // The blast throws most of what was in the vessel out of it.
            var leftovers = new List<KeyValuePair<string, double>>(Contents.Components);
            foreach (var kv in leftovers) Contents.Remove(kv.Key, kv.Value * 0.8);
            OnFire = false;
            BurningSubstance = null;
            _fireCooldown = 2;
            AddHeat(kg * 4e6 * 0.02);
            Publish(LabEventType.Explosion, Math.Min(1.0, 0.5 + kg * 100), $"{s.Name} exploded on heating!", MouthPosition, "boom.explosive:" + s.Id);
        }

        private void StepFire(double dt)
        {
            if (!OnFire)
            {
                return;
            }

            var fuel = BurningSubstance;
            if (fuel == null || Contents.Moles(fuel.Id) < 1e-6 || (fuel.Phase == SubstancePhase.Solid && Contents.HasLiquid))
            {
                fuel = FlammableFuel();
                if (fuel == null)
                {
                    OnFire = false;
                    BurningSubstance = null;
                    return;
                }

                BurningSubstance = fuel;
            }

            if (fuel.Phase == SubstancePhase.Solid)
            {
                // A burning solid turns into its oxide (Mg → MgO, white smoke; S → SO₂ gas; organic solids → CO₂ + H₂O).
                double have = Contents.Moles(fuel.Id);
                double n = Math.Min(have, Math.Max(have * 0.12, 0.0004) * dt);
                Contents.Remove(fuel.Id, n);
                var (oxide, per) = Combustion.OxideOf(Contents.Database, fuel);
                if (oxide != null && oxide.Phase != SubstancePhase.Aqueous && !oxide.IsGas)
                {
                    Contents.Add(oxide.Id, n * per);
                }
                else
                {
                    GasFormula = oxide != null ? oxide.Formula : "CO₂";
                    GasRateMolPerS = Math.Max(GasRateMolPerS, n / Math.Max(dt, 1e-4));
                }

                AddHeat(n * 3e5 * 0.1);
                return;
            }

            double kg = Math.Min(FireBurnRateKgPerS * dt, Contents.Moles(fuel.Id) * fuel.MolarMass / 1000.0);
            Contents.Remove(fuel.Id, kg * 1000.0 / fuel.MolarMass);
            AddHeat(kg * CombustionEnthalpyJPerKg * 0.15); // most of the heat goes up with the flame
        }

        private void StepThermal(double dt)
        {
            double heatCapacity = GlassHeatCapacity + Contents.HeatCapacity;
            double outerArea = Math.PI * InnerDiameterM * InnerHeightM + Math.PI * RadiusM * RadiusM;
            double lossW = PhysicalConstants.ConvectiveCoefficient * outerArea * (TemperatureC - World.AmbientTemperatureC);
            double t = TemperatureC + (_pendingHeatJ - lossW * dt) / heatCapacity;
            _pendingHeatJ = 0;

            bool boiled = false;
            var volatileSubstance = Contents.PrimaryVolatile;
            if (volatileSubstance != null && t > volatileSubstance.BoilingPointC)
            {
                // Clamp at the boiling point; the excess energy becomes latent heat of vaporisation.
                double excessJ = (t - volatileSubstance.BoilingPointC) * heatCapacity;
                double availableKg = Contents.Moles(volatileSubstance.Id) * volatileSubstance.MolarMass / 1000.0;
                double evaporatedKg = Math.Min(excessJ / volatileSubstance.LatentHeatVaporization, availableKg);
                Contents.Remove(volatileSubstance.Id, evaporatedKg * 1000.0 / volatileSubstance.MolarMass);
                double leftoverJ = excessJ - evaporatedKg * volatileSubstance.LatentHeatVaporization;
                double remainingCapacity = GlassHeatCapacity + Contents.HeatCapacity;
                t = volatileSubstance.BoilingPointC + leftoverJ / remainingCapacity;
                boiled = evaporatedKg > 0;
            }

            if (boiled && !IsBoiling)
            {
                Publish(LabEventType.BoilingStarted, 0.2, $"{Owner.Definition.DisplayName} is boiling.", MouthPosition, "boiling");
            }

            IsBoiling = boiled;
            SetTemperature(Math.Min(t, MaxGlassTemperatureC));
        }

        private void StepPouring(double dt)
        {
            IsPouring = false;
            IsPouringSolid = false;
            PourRateLitersPerSecond = 0;
            PourTarget = null;
            if (Contents.IsEmpty)
            {
                return;
            }

            var pose = Owner.Pose;
            double tilt = pose.TiltDegrees;
            double rate;
            bool solid = !Contents.HasLiquid;
            if (!solid)
            {
                rate = PourModel.FlowRateLitersPerSecond(FillFraction, InnerHeightM, InnerDiameterM, tilt, MouthDiameterM);
            }
            else
            {
                // Grains slide out once the pile's slope passes the angle of repose (≈ 35° beyond where liquid would pour).
                double threshold = Math.Max(45.0, PourModel.ThresholdTiltDegrees(FillFraction, InnerHeightM, InnerDiameterM) - 10.0);
                double mouthScale = Math.Min(2.0, MouthDiameterM / 0.04);
                rate = tilt > threshold ? SolidPourRateLps * mouthScale * mouthScale * Math.Min(1.0, (tilt - threshold) / 25.0 + 0.2) : 0;
            }

            if (rate <= 0)
            {
                return;
            }

            PourLipPosition = ComputeLipPosition(pose);
            PourTarget = PourTargets.Find(World, Owner, PourLipPosition);
            IsPouring = true;
            IsPouringSolid = solid;
            PourRateLitersPerSecond = rate;

            var portion = solid ? Contents.TakeSolids(rate * dt) : Contents.TakeLiquid(rate * dt);
            var targetContainer = PourTarget?.Get<LiquidContainer>();
            if (targetContainer != null)
            {
                targetContainer.ReceiveLiquid(portion);
            }
            else if (World.Time - _lastSpillEventTime > SpillEventCooldownS)
            {
                _lastSpillEventTime = World.Time;
                Publish(LabEventType.Spill, 0.3, "Liquid spilled onto the bench.", PourLipPosition, solid ? "spill.solid" : "spill.liquid");
            }
        }

        private void StepOverflow()
        {
            double excess = VolumeLiters - CapacityLiters;
            if (excess <= 1e-6)
            {
                return;
            }

            Contents.TakeLiquid(excess);
            if (World.Time - _lastSpillEventTime > SpillEventCooldownS)
            {
                _lastSpillEventTime = World.Time;
                Publish(LabEventType.Spill, 0.4, $"{Owner.Definition.DisplayName} overflowed.", MouthPosition, "spill.overflow");
            }
        }

        /// <summary>The lowest point of the rim — where the stream leaves the vessel.</summary>
        private Vector3 ComputeLipPosition(Pose3 pose)
        {
            Vector3 up = Vector3.Normalize(pose.Up);
            Vector3 down = -Vector3.UnitY;
            Vector3 lowSide = down - Vector3.Dot(down, up) * up;
            lowSide = lowSide.LengthSquared() > 1e-8 ? Vector3.Normalize(lowSide) : Vector3.UnitX;
            return pose.TransformPoint(LocalOffset + new Vector3(0, (float)InnerHeightM, 0)) + lowSide * (float)MouthRadiusM;
        }

        private void SetTemperature(double t)
        {
            TemperatureC = t;
            Contents.TemperatureC = t;
        }

        internal void Publish(LabEventType type, double severity, string message, Vector3 position, string code = null) =>
            World.Events.Publish(new LabEvent(type, severity, message, Owner.Id, position, World.Time, code));
    }

    /// <summary>Finds which open container a falling stream (or drip) lands in.</summary>
    public static class PourTargets
    {
        public static EquipmentInstance Find(LabWorld world, EquipmentInstance source, Vector3 from)
        {
            EquipmentInstance best = null;
            float bestRimY = float.NegativeInfinity;
            foreach (var other in world.Instances)
            {
                if (other == source)
                {
                    continue;
                }

                var container = other.Get<LiquidContainer>();
                if (container == null)
                {
                    continue;
                }

                Vector3 rim = container.MouthPosition;
                float dx = rim.X - from.X;
                float dz = rim.Z - from.Z;
                float catchRadius = Math.Max(0.004f, (float)container.MouthRadiusM * 1.15f);
                if (rim.Y < from.Y && dx * dx + dz * dz <= catchRadius * catchRadius && rim.Y > bestRimY)
                {
                    best = other;
                    bestRimY = rim.Y;
                }
            }

            return best;
        }
    }
}
