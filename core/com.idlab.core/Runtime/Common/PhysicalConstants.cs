namespace IDLab.Core
{
    public static class PhysicalConstants
    {
        public const double KelvinOffset = 273.15;
        public const double Gravity = 9.81;                  // m/s²
        public const double WaterIonProduct = 1.0e-14;       // Kw at 25 °C
        public const double GlassSpecificHeat = 840.0;       // J/(kg·K), borosilicate
        public const double ConvectiveCoefficient = 10.0;    // W/(m²·K), still air around glassware
        public const double NeutralizationEnthalpy = -57_100.0; // J/mol water formed (strong acid + strong base)
    }
}
