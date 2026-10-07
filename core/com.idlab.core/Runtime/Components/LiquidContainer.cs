using System;
using System.Numerics;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Core.Components
{
    /// <summary>
    /// An open cylindrical vessel (beaker, bottle, jar) holding a <see cref="Mixture"/>.
    /// Responsibilities each tick: run reactions on changed contents → lumped thermal model with
    /// convective loss and boiling → tilt-driven pouring into whatever is under the lip → overflow.
    /// Local origin is the centre of the base; +Y is the mouth.
    /// </summary>
    public sealed class LiquidContainer : LabComponent
    {
        private const double MaxGlassTemperatureC = 500.0;
        private const double SpillEventCooldownS = 2.0;

        private readonly ReagentPreset _initialContents;
        private double _pendingHeatJ;
        private int _lastReactedVersion = -1;
        private int _lastPhVersion = -1;
        private double _ph = double.NaN;
        private double _lastSpillEventTime = double.NegativeInfinity;

        public double InnerDiameterM { get; }
        public double InnerHeightM { get; }
        public double GlassMassKg { get; }

        public Mixture Contents { get; private set; }

        /// <summary>Bulk temperature of glass + contents (°C).</summary>
        public double TemperatureC { get; private set; }

        public bool IsBoiling { get; private set; }

        // Pour state, read by the front-end to draw the liquid stream.
        public bool IsPouring { get; private set; }
        public double PourRateLitersPerSecond { get; private set; }
        public Vector3 PourLipPosition { get; private set; }
        public EquipmentInstance PourTarget { get; private set; }

        public LiquidContainer(double innerDiameterM, double innerHeightM, double glassMassKg, ReagentPreset initialContents)
        {
            InnerDiameterM = innerDiameterM;
            InnerHeightM = innerHeightM;
            GlassMassKg = glassMassKg;
            _initialContents = initialContents;
        }

        public override int StepOrder => OrderContainers;

        public double RadiusM => InnerDiameterM * 0.5;
        public double CapacityLiters => Math.PI * RadiusM * RadiusM * InnerHeightM * 1000.0;
        public double VolumeLiters => Contents.VolumeLiters;
        public double VolumeMilliliters => VolumeLiters * 1000.0;
        public double FillFraction => Math.Min(1.0, VolumeLiters / CapacityLiters);
        public double LiquidHeightM => FillFraction * InnerHeightM;
        public double GlassHeatCapacity => GlassMassKg * PhysicalConstants.GlassSpecificHeat;

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

        /// <summary>World position of the centre of the mouth.</summary>
        public Vector3 MouthPosition => Owner.Pose.TransformPoint(new Vector3(0, (float)InnerHeightM, 0));

        protected override void OnAttached()
        {
            TemperatureC = World.AmbientTemperatureC;
            Contents = new Mixture(World.Substances, TemperatureC);
            _initialContents?.FillInto(Contents);
        }

        /// <summary>Queues heat (J) to be applied on this container's next step (from burners, reactions...).</summary>
        public void AddHeat(double joules) => _pendingHeatJ += joules;

        /// <summary>Adds liquid poured from elsewhere; temperature equilibrates with glass + existing contents.</summary>
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

        public override void Step(double dt)
        {
            React();
            StepThermal(dt);
            StepPouring(dt);
            StepOverflow();
        }

        private void React()
        {
            if (Contents.Version == _lastReactedVersion)
            {
                return;
            }

            var outcome = World.Reactions.Process(Contents);
            _lastReactedVersion = Contents.Version;
            if (outcome.HeatReleasedJ != 0)
            {
                AddHeat(outcome.HeatReleasedJ);
            }

            foreach (var effect in outcome.Effects)
            {
                Publish(effect.Type, effect.Severity, effect.Message, MouthPosition);
            }
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
                Publish(LabEventType.BoilingStarted, 0.2, $"{Owner.Definition.DisplayName} is boiling.", MouthPosition);
            }

            IsBoiling = boiled;
            SetTemperature(Math.Min(t, MaxGlassTemperatureC));
        }

        private void StepPouring(double dt)
        {
            IsPouring = false;
            PourRateLitersPerSecond = 0;
            PourTarget = null;
            if (Contents.IsEmpty)
            {
                return;
            }

            var pose = Owner.Pose;
            double rate = PourModel.FlowRateLitersPerSecond(FillFraction, InnerHeightM, InnerDiameterM, pose.TiltDegrees);
            if (rate <= 0)
            {
                return;
            }

            PourLipPosition = ComputeLipPosition(pose);
            PourTarget = FindPourTarget(PourLipPosition);
            IsPouring = true;
            PourRateLitersPerSecond = rate;

            var portion = Contents.TakeVolume(rate * dt);
            var targetContainer = PourTarget?.Get<LiquidContainer>();
            if (targetContainer != null)
            {
                targetContainer.ReceiveLiquid(portion);
            }
            else if (World.Time - _lastSpillEventTime > SpillEventCooldownS)
            {
                _lastSpillEventTime = World.Time;
                Publish(LabEventType.Spill, 0.3, "Liquid spilled onto the bench.", PourLipPosition);
            }
        }

        private void StepOverflow()
        {
            double excess = VolumeLiters - CapacityLiters;
            if (excess <= 1e-6)
            {
                return;
            }

            Contents.TakeVolume(excess);
            if (World.Time - _lastSpillEventTime > SpillEventCooldownS)
            {
                _lastSpillEventTime = World.Time;
                Publish(LabEventType.Spill, 0.4, $"{Owner.Definition.DisplayName} overflowed.", MouthPosition);
            }
        }

        /// <summary>The lowest point of the rim — where the stream leaves the vessel.</summary>
        private Vector3 ComputeLipPosition(Pose3 pose)
        {
            Vector3 up = Vector3.Normalize(pose.Up);
            Vector3 down = -Vector3.UnitY;
            Vector3 lowSide = down - Vector3.Dot(down, up) * up;
            lowSide = lowSide.LengthSquared() > 1e-8 ? Vector3.Normalize(lowSide) : Vector3.UnitX;
            return pose.TransformPoint(new Vector3(0, (float)InnerHeightM, 0)) + lowSide * (float)RadiusM;
        }

        /// <summary>The first open container the falling stream would hit, or null (spill).</summary>
        private EquipmentInstance FindPourTarget(Vector3 lip)
        {
            EquipmentInstance best = null;
            float bestRimY = float.NegativeInfinity;
            foreach (var other in World.Instances)
            {
                if (other == Owner)
                {
                    continue;
                }

                var container = other.Get<LiquidContainer>();
                if (container == null)
                {
                    continue;
                }

                Vector3 rim = container.MouthPosition;
                float dx = rim.X - lip.X;
                float dz = rim.Z - lip.Z;
                float catchRadius = (float)container.RadiusM * 1.1f;
                if (rim.Y < lip.Y && dx * dx + dz * dz <= catchRadius * catchRadius && rim.Y > bestRimY)
                {
                    best = other;
                    bestRimY = rim.Y;
                }
            }

            return best;
        }

        private void SetTemperature(double t)
        {
            TemperatureC = t;
            Contents.TemperatureC = t;
        }

        private void Publish(LabEventType type, double severity, string message, Vector3 position) =>
            World.Events.Publish(new LabEvent(type, severity, message, Owner.Id, position, World.Time));
    }
}
