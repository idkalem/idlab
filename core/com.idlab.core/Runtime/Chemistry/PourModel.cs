using System;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// Geometry of pouring from an open cylindrical vessel. The free surface stays horizontal, so when the
    /// vessel tilts by θ the liquid rises by (D/2)·tanθ on the low side. Liquid spills over the lip once
    /// h + (D/2)·tanθ ≥ H, and the outflow follows a sharp-crested weir law Q = Cd·(2/3)·w·√(2g)·head^1.5.
    /// </summary>
    public static class PourModel
    {
        private const double DischargeCoefficient = 0.6;

        /// <summary>Tilt (degrees) at which the liquid first reaches the lip.</summary>
        public static double ThresholdTiltDegrees(double fillFraction, double innerHeightM, double innerDiameterM)
        {
            fillFraction = Math.Max(0, Math.Min(1, fillFraction));
            double rise = (1 - fillFraction) * innerHeightM;
            return Math.Atan2(2 * rise, innerDiameterM) * 180.0 / Math.PI;
        }

        /// <summary>Outflow in litres per second (0 when the liquid has not reached the lip).</summary>
        public static double FlowRateLitersPerSecond(double fillFraction, double innerHeightM, double innerDiameterM, double tiltDegrees)
        {
            if (fillFraction <= 0 || tiltDegrees <= 0)
            {
                return 0;
            }

            // Past ~85° the tan blows up; treat as fully inverted lip head.
            double theta = Math.Min(tiltDegrees, 85.0) * Math.PI / 180.0;
            double liquidHeight = fillFraction * innerHeightM;
            double lipLevel = liquidHeight + innerDiameterM * 0.5 * Math.Tan(theta);
            double head = (lipLevel - innerHeightM) * Math.Cos(theta);
            if (head <= 0)
            {
                return 0;
            }

            head = Math.Min(head, innerDiameterM);           // the lip can't be flooded deeper than the mouth
            double lipWidth = innerDiameterM * 0.5;
            double q = DischargeCoefficient * (2.0 / 3.0) * lipWidth * Math.Sqrt(2 * PhysicalConstants.Gravity) * Math.Pow(head, 1.5);
            return q * 1000.0; // m³/s → L/s
        }
    }
}
