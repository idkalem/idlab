using System;
using System.Numerics;
using IDLab.Core.Data;
using IDLab.Core.Model;

namespace IDLab.Core.Components
{
    /// <summary>
    /// A tap at the bottom of a vessel (burette, separating funnel). Opening it drains liquid from the
    /// outlet tip into whatever container is below — drop by drop when barely open. Logs the volume
    /// delivered, which is what a titration is plotted against.
    /// </summary>
    public sealed class Stopcock : LabComponent, IDataSource
    {
        private double _valve;

        public double MaxFlowLitersPerSecond { get; }
        public Vector3 OutletLocal { get; }

        /// <summary>0 = closed, 1 = fully open. Flow ∝ valve² for fine control near closed.</summary>
        public double Valve
        {
            get => _valve;
            set => _valve = value < 0 ? 0 : (value > 1 ? 1 : value);
        }

        public bool IsFlowing { get; private set; }
        public double FlowLitersPerSecond { get; private set; }
        public double DeliveredMilliliters { get; private set; }
        public EquipmentInstance Target { get; private set; }
        public Vector3 OutletPosition => Owner.Pose.TransformPoint(OutletLocal);

        public Stopcock(double maxFlowLitersPerSecond, Vector3 outletLocal)
        {
            MaxFlowLitersPerSecond = maxFlowLitersPerSecond;
            OutletLocal = outletLocal;
        }

        public override int StepOrder => OrderContainers + 10;

        public void ResetCounter() => DeliveredMilliliters = 0;

        /// <summary>Volume still to be let out by <see cref="Dispense"/> (mL); the tap opens and closes by itself.</summary>
        public double PendingMilliliters { get; private set; }

        /// <summary>Lets out exactly <paramref name="milliliters"/> (one drop ≈ 0.05 mL) at a slow, drop-wise rate.</summary>
        public void Dispense(double milliliters) => PendingMilliliters += Math.Max(0, milliliters);

        public void CancelDispense() => PendingMilliliters = 0;

        public override void Step(double dt)
        {
            IsFlowing = false;
            FlowLitersPerSecond = 0;
            Target = null;
            var container = Owner.Get<LiquidContainer>();
            bool dispensing = PendingMilliliters > 1e-6;
            if (container == null || (_valve < 0.01 && !dispensing) || !container.Contents.HasLiquid)
            {
                if (container == null || !container.Contents.HasLiquid) PendingMilliliters = 0;
                return;
            }

            // A metered dose runs at about 0.4 mL/s (drop-wise); otherwise the tap position sets the flow.
            double rate = dispensing ? Math.Max(MaxFlowLitersPerSecond * _valve * _valve, 0.0004) : MaxFlowLitersPerSecond * _valve * _valve;
            double volume = Math.Min(rate * dt, container.Contents.LiquidVolumeLiters);
            if (dispensing && _valve < 0.01)
            {
                volume = Math.Min(volume, PendingMilliliters / 1000.0);
            }

            if (dispensing) PendingMilliliters = Math.Max(0, PendingMilliliters - volume * 1000.0);
            var portion = container.Contents.TakeLiquid(volume);
            Target = PourTargets.Find(World, Owner, OutletPosition);
            Target?.Get<LiquidContainer>()?.ReceiveLiquid(portion);
            IsFlowing = true;
            FlowLitersPerSecond = rate;
            DeliveredMilliliters += volume * 1000.0;
        }

        public void RegisterChannels(DataLogger logger)
        {
            var channel = logger.AddChannel($"{Owner.Id}.Delivered", $"Delivered volume — {Owner.Definition.DisplayName} #{Owner.Id}", "mL",
                Owner.Id, () => DeliveredMilliliters);
            channel.SuggestedMin = 0;
            channel.SuggestedMax = 25;
        }
    }

    /// <summary>Electronic balance: weighs whatever stands on its pan (glass + contents), with tare.</summary>
    public sealed class Balance : LabComponent, IDataSource
    {
        public string PanSnapId { get; }
        public double TareGrams { get; private set; }
        public double ReadingGrams { get; private set; }
        public bool HasLoad { get; private set; }

        public Balance(string panSnapId)
        {
            PanSnapId = panSnapId;
        }

        public override int StepOrder => OrderSensors;

        public void Tare() => TareGrams += ReadingGrams;

        public override void Step(double dt)
        {
            var load = World.GetAttached(Owner, PanSnapId);
            double grams = 0;
            if (load != null)
            {
                grams += LoadGrams(load);
                foreach (var child in World.GetChildren(load)) grams += LoadGrams(child);
            }

            HasLoad = load != null;
            ReadingGrams = Math.Round(grams - TareGrams, 2);
        }

        private static double LoadGrams(EquipmentInstance item)
        {
            var c = item.Get<LiquidContainer>();
            return c != null ? c.TotalMassGrams : (item.Has<Probe>() ? 25.0 : 50.0);
        }

        public void RegisterChannels(DataLogger logger)
        {
            var channel = logger.AddChannel($"{Owner.Id}.Mass", $"Mass — {Owner.Definition.DisplayName} #{Owner.Id}", "g", Owner.Id, () => ReadingGrams);
            channel.SuggestedMin = 0;
            channel.SuggestedMax = 200;
        }
    }
}
