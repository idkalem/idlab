namespace IDLab.Core.Components
{
    /// <summary>
    /// A Bunsen burner (or hot plate). Delivers valve × max power × efficiency to the container
    /// sitting on its heating snap (tripod + gauze).
    /// </summary>
    public sealed class HeatSource : LabComponent
    {
        private double _gasValve;

        public double MaxPowerW { get; }
        public double Efficiency { get; }
        public string HeatingSnapId { get; }

        /// <summary>0 = closed, 1 = fully open.</summary>
        public double GasValve
        {
            get => _gasValve;
            set => _gasValve = value < 0 ? 0 : (value > 1 ? 1 : value);
        }

        public bool IsLit => _gasValve > 0.01;

        /// <summary>Thermal output of the flame (W), regardless of whether anything is being heated.</summary>
        public double FlamePowerW => IsLit ? _gasValve * MaxPowerW : 0;

        /// <summary>Power actually delivered into a container last step (W).</summary>
        public double DeliveredPowerW { get; private set; }

        /// <summary>An open flame (Bunsen) ignites flammable vapour; a hot plate does not.</summary>
        public bool OpenFlame { get; }

        public HeatSource(double maxPowerW, double efficiency, string heatingSnapId, bool openFlame = true)
        {
            OpenFlame = openFlame;
            MaxPowerW = maxPowerW;
            Efficiency = efficiency;
            HeatingSnapId = heatingSnapId;
        }

        public override int StepOrder => OrderSources;

        public LiquidContainer HeatedContainer => World.GetAttached(Owner, HeatingSnapId)?.Get<LiquidContainer>();

        public override void Step(double dt)
        {
            var target = HeatedContainer;
            if (!IsLit || target == null)
            {
                DeliveredPowerW = 0;
                return;
            }

            DeliveredPowerW = FlamePowerW * Efficiency;
            target.AddHeat(DeliveredPowerW * dt);
            if (OpenFlame && target.TemperatureC > 35)
            {
                target.Ignite(); // flammable vapour meets the flame
            }
        }
    }
}
