using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// The contents of a container: amount (mol) of each substance plus a single bulk temperature.
    /// Two phases are tracked: the <b>liquid phase</b> (solvents + everything dissolved) and <b>undissolved
    /// solids</b> (metal pieces, marble chips, precipitates, or any solid with no water to dissolve in).
    /// Volumes are ideal (sum of pure-component volumes) — good enough for school labs.
    /// </summary>
    public sealed class Mixture
    {
        private const double Epsilon = 1e-12;
        private readonly Dictionary<string, double> _moles = new Dictionary<string, double>();

        public SubstanceDatabase Database { get; }
        public double TemperatureC { get; set; }

        /// <summary>Increments whenever composition changes; lets reactions/pH caches skip unchanged mixtures.</summary>
        public int Version { get; private set; }

        public Mixture(SubstanceDatabase database, double temperatureC = 20.0)
        {
            Database = database;
            TemperatureC = temperatureC;
        }

        public IEnumerable<KeyValuePair<string, double>> Components => _moles;

        public bool IsEmpty => _moles.Count == 0;

        public double Moles(string substanceId) => _moles.TryGetValue(substanceId, out var n) ? n : 0.0;

        public bool Contains(string substanceId, double minMoles = Epsilon) => Moles(substanceId) > minMoles;

        /// <summary>Water present (needed for dissolving and for aqueous chemistry).</summary>
        public bool HasWater => Contains(SubstanceIds.Water, 1e-6);

        public void Add(string substanceId, double moles)
        {
            Database.Get(substanceId); // validate
            double next = Moles(substanceId) + moles;
            if (next <= Epsilon)
            {
                _moles.Remove(substanceId);
            }
            else
            {
                _moles[substanceId] = next;
            }

            Version++;
        }

        public void Remove(string substanceId, double moles) => Add(substanceId, -moles);

        public void Clear()
        {
            _moles.Clear();
            Version++;
        }

        // ------------------------------------------------------------------ phases

        public bool IsInLiquid(Substance s) => s.IsLiquidPhase(HasWater);

        private static double MilliLiters(Substance s, double moles) => moles * s.MolarMass / s.Density;

        public double VolumeLiters
        {
            get
            {
                double ml = 0;
                foreach (var kv in _moles) ml += MilliLiters(Database.Get(kv.Key), kv.Value);
                return ml / 1000.0;
            }
        }

        public double LiquidVolumeLiters
        {
            get
            {
                bool water = HasWater;
                double ml = 0;
                foreach (var kv in _moles)
                {
                    var s = Database.Get(kv.Key);
                    if (s.IsLiquidPhase(water)) ml += MilliLiters(s, kv.Value);
                }

                return ml / 1000.0;
            }
        }

        public double SolidVolumeLiters => Math.Max(0, VolumeLiters - LiquidVolumeLiters);

        public bool HasLiquid => LiquidVolumeLiters > 1e-7;
        public bool HasSolids => SolidVolumeLiters > 1e-8;

        /// <summary>Undissolved solids with their amounts (for drawing pieces / sediment).</summary>
        public IEnumerable<KeyValuePair<Substance, double>> Solids()
        {
            bool water = HasWater;
            foreach (var kv in _moles)
            {
                var s = Database.Get(kv.Key);
                if (!s.IsLiquidPhase(water)) yield return new KeyValuePair<Substance, double>(s, kv.Value);
            }
        }

        public double MassKg
        {
            get
            {
                double kg = 0;
                foreach (var kv in _moles) kg += SubstanceDatabase.Mass(Database.Get(kv.Key), kv.Value);
                return kg;
            }
        }

        /// <summary>Total heat capacity Σ mᵢ·cᵢ in J/K.</summary>
        public double HeatCapacity
        {
            get
            {
                double c = 0;
                foreach (var kv in _moles)
                {
                    var s = Database.Get(kv.Key);
                    c += SubstanceDatabase.Mass(s, kv.Value) * s.SpecificHeat;
                }

                return c;
            }
        }

        /// <summary>Concentration in mol per litre of liquid (0 if no liquid).</summary>
        public double Molarity(string substanceId)
        {
            double v = LiquidVolumeLiters;
            return v > Epsilon ? Moles(substanceId) / v : 0.0;
        }

        /// <summary>The volatile liquid-phase substance with the lowest boiling point (what boils off first), or null.</summary>
        public Substance PrimaryVolatile
        {
            get
            {
                Substance best = null;
                foreach (var kv in _moles)
                {
                    var s = Database.Get(kv.Key);
                    if (s.IsVolatile && s.Phase == SubstancePhase.Liquid && (best == null || s.BoilingPointC < best.BoilingPointC))
                    {
                        best = s;
                    }
                }

                return best;
            }
        }

        // ------------------------------------------------------------------ transfers

        /// <summary>Removes <paramref name="liters"/> (proportionally from every component, all phases).</summary>
        public Mixture TakeVolume(double liters) => Take(liters, VolumeLiters, _ => true);

        /// <summary>Pours off liquid only — solids stay behind (decanting).</summary>
        public Mixture TakeLiquid(double liters)
        {
            bool water = HasWater;
            return Take(liters, LiquidVolumeLiters, s => s.IsLiquidPhase(water));
        }

        /// <summary>Tips out undissolved solid pieces only.</summary>
        public Mixture TakeSolids(double liters)
        {
            bool water = HasWater;
            return Take(liters, SolidVolumeLiters, s => !s.IsLiquidPhase(water));
        }

        private Mixture Take(double liters, double available, Func<Substance, bool> include)
        {
            var taken = new Mixture(Database, TemperatureC);
            if (available <= Epsilon || liters <= 0)
            {
                return taken;
            }

            double fraction = Math.Min(1.0, liters / available);
            foreach (var kv in new List<KeyValuePair<string, double>>(_moles))
            {
                if (!include(Database.Get(kv.Key)))
                {
                    continue;
                }

                double amount = kv.Value * fraction;
                taken._moles[kv.Key] = amount;
                double left = kv.Value - amount;
                if (left <= Epsilon) _moles.Remove(kv.Key);
                else _moles[kv.Key] = left;
            }

            taken.Version++;
            Version++;
            return taken;
        }

        /// <summary>Pours <paramref name="other"/> into this mixture; temperature mixes by heat capacity.</summary>
        public void MergeFrom(Mixture other)
        {
            if (other == null || other.IsEmpty)
            {
                return;
            }

            double c1 = HeatCapacity;
            double c2 = other.HeatCapacity;
            if (c1 + c2 > Epsilon)
            {
                TemperatureC = (TemperatureC * c1 + other.TemperatureC * c2) / (c1 + c2);
            }
            else if (IsEmpty)
            {
                TemperatureC = other.TemperatureC;
            }

            foreach (var kv in other._moles)
            {
                _moles[kv.Key] = Moles(kv.Key) + kv.Value;
            }

            Version++;
        }

        // ------------------------------------------------------------------ colour

        /// <summary>
        /// Colour of the liquid phase: the solvents' blend, then tinted by each coloured solute according to its
        /// concentration (Beer–Lambert-like: 1 − e^(−k·c)), so 0.5 M CuSO₄ is clearly blue but 0.01 M is pale.
        /// </summary>
        public ColorRGBA BlendedColor()
        {
            bool water = HasWater;
            double total = 0, r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in _moles)
            {
                var s = Database.Get(kv.Key);
                if (s.Phase != SubstancePhase.Liquid) continue;
                double ml = MilliLiters(s, kv.Value);
                total += ml;
                r += s.Color.R * ml;
                g += s.Color.G * ml;
                b += s.Color.B * ml;
                a += s.Color.A * ml;
            }

            var color = total > Epsilon
                ? new ColorRGBA((float)(r / total), (float)(g / total), (float)(b / total), (float)(a / total))
                : new ColorRGBA(0.85f, 0.92f, 1f, 0.25f);

            double v = LiquidVolumeLiters;
            if (v <= Epsilon) return new ColorRGBA(0, 0, 0, 0);
            foreach (var kv in _moles)
            {
                var s = Database.Get(kv.Key);
                if (s.TintStrength <= 0 || !s.IsDissolved(water)) continue;
                float t = (float)(1.0 - Math.Exp(-s.TintStrength * kv.Value / v));
                color = ColorRGBA.Lerp(color, s.Color, t);
            }

            return color;
        }

        /// <summary>Colour as seen by the student: indicators react to pH, otherwise the tinted blend.</summary>
        public ColorRGBA DisplayColor(double pH)
        {
            if (Contains(SubstanceIds.UniversalIndicator) && !double.IsNaN(pH))
            {
                return IndicatorColors.UniversalIndicator(pH).WithAlpha(0.75f);
            }

            var color = BlendedColor();
            if (Contains(SubstanceIds.Phenolphthalein) && !double.IsNaN(pH))
            {
                color = ColorRGBA.Lerp(color, IndicatorColors.PhenolphthaleinPink, IndicatorColors.PhenolphthaleinIntensity(pH));
            }

            return color;
        }
    }
}
