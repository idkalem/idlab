using System;
using IDLab.Core.Data;

namespace IDLab.Core.Components
{
    public enum ProbeQuantity
    {
        Temperature,
        PH,
    }

    /// <summary>
    /// A sensor that reads the container its tip is snapped into, with a first-order response
    /// (time constant τ) like a real thermometer bulb. Publishes a live data channel.
    /// </summary>
    public sealed class Probe : LabComponent, IDataSource
    {
        private const double MinImmersionLiters = 0.002;

        public ProbeQuantity Quantity { get; }
        public double TimeConstantS { get; }
        public string TipSnapId { get; }

        public double Reading { get; private set; } = double.NaN;
        public LiquidContainer ImmersedIn { get; private set; }

        public Probe(ProbeQuantity quantity, double timeConstantS, string tipSnapId)
        {
            Quantity = quantity;
            TimeConstantS = timeConstantS;
            TipSnapId = tipSnapId;
        }

        public override int StepOrder => OrderSensors;

        public string QuantityLabel => Quantity == ProbeQuantity.Temperature ? "Temperature" : "pH";
        public string Unit => Quantity == ProbeQuantity.Temperature ? "°C" : "pH";

        public string FormattedReading =>
            double.IsNaN(Reading) ? "--" : (Quantity == ProbeQuantity.Temperature ? $"{Reading:0.0} °C" : $"pH {Reading:0.00}");

        protected override void OnAttached()
        {
            Reading = Quantity == ProbeQuantity.Temperature ? World.AmbientTemperatureC : double.NaN;
        }

        public override void Step(double dt)
        {
            var host = World.GetAttached(Owner, TipSnapId)?.Get<LiquidContainer>();
            ImmersedIn = host != null && host.VolumeLiters >= MinImmersionLiters ? host : null;

            double target;
            if (Quantity == ProbeQuantity.Temperature)
            {
                target = ImmersedIn != null ? ImmersedIn.TemperatureC : World.AmbientTemperatureC;
            }
            else
            {
                target = ImmersedIn != null ? ImmersedIn.PH : double.NaN;
            }

            if (double.IsNaN(target) || double.IsNaN(Reading) || TimeConstantS <= 0)
            {
                Reading = target;
                return;
            }

            Reading += (target - Reading) * (1.0 - Math.Exp(-dt / TimeConstantS));
        }

        public void RegisterChannels(DataLogger logger)
        {
            var channel = logger.AddChannel(
                $"{Owner.Id}.{Quantity}",
                $"{QuantityLabel} — {Owner.Definition.DisplayName} #{Owner.Id}",
                Unit,
                Owner.Id,
                () => Reading);
            channel.SuggestedMin = 0;
            channel.SuggestedMax = Quantity == ProbeQuantity.Temperature ? 110 : 14;
        }
    }
}
