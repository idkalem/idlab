using System;
using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    /// <summary>
    /// The contents of a container: amount (mol) of each substance plus a single bulk temperature.
    /// Volumes are ideal (sum of pure-component volumes) — good enough for high-school level labs.
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

        public double VolumeLiters
        {
            get
            {
                double ml = 0;
                foreach (var kv in _moles)
                {
                    var s = Database.Get(kv.Key);
                    ml += kv.Value * s.MolarMass / s.Density;
                }

                return ml / 1000.0;
            }
        }

        public double MassKg
        {
            get
            {
                double kg = 0;
                foreach (var kv in _moles)
                {
                    kg += SubstanceDatabase.Mass(Database.Get(kv.Key), kv.Value);
                }

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

        /// <summary>Concentration in mol/L (0 if empty).</summary>
        public double Molarity(string substanceId)
        {
            double v = VolumeLiters;
            return v > Epsilon ? Moles(substanceId) / v : 0.0;
        }

        /// <summary>The volatile substance with the lowest boiling point (what boils off first), or null.</summary>
        public Substance PrimaryVolatile
        {
            get
            {
                Substance best = null;
                foreach (var kv in _moles)
                {
                    var s = Database.Get(kv.Key);
                    if (s.IsVolatile && (best == null || s.BoilingPointC < best.BoilingPointC))
                    {
                        best = s;
                    }
                }

                return best;
            }
        }

        /// <summary>Removes <paramref name="liters"/> (proportionally from every component) into a new mixture.</summary>
        public Mixture TakeVolume(double liters)
        {
            var taken = new Mixture(Database, TemperatureC);
            double total = VolumeLiters;
            if (total <= Epsilon || liters <= 0)
            {
                return taken;
            }

            double fraction = Math.Min(1.0, liters / total);
            foreach (var kv in new List<KeyValuePair<string, double>>(_moles))
            {
                double amount = kv.Value * fraction;
                taken._moles[kv.Key] = amount;
                double left = kv.Value - amount;
                if (left <= Epsilon)
                {
                    _moles.Remove(kv.Key);
                }
                else
                {
                    _moles[kv.Key] = left;
                }
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

        /// <summary>Volume-weighted blend of component colours (before indicators are applied).</summary>
        public ColorRGBA BlendedColor()
        {
            double total = 0;
            double r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in _moles)
            {
                var s = Database.Get(kv.Key);
                double ml = kv.Value * s.MolarMass / s.Density;
                total += ml;
                r += s.Color.R * ml;
                g += s.Color.G * ml;
                b += s.Color.B * ml;
                a += s.Color.A * ml;
            }

            if (total <= Epsilon)
            {
                return new ColorRGBA(0, 0, 0, 0);
            }

            return new ColorRGBA((float)(r / total), (float)(g / total), (float)(b / total), (float)(a / total));
        }

        /// <summary>Colour as seen by the student: indicator colour if one is present, otherwise the blend.</summary>
        public ColorRGBA DisplayColor(double pH)
        {
            if (Contains(SubstanceIds.UniversalIndicator) && !double.IsNaN(pH))
            {
                return IndicatorColors.UniversalIndicator(pH).WithAlpha(0.75f);
            }

            return BlendedColor();
        }
    }
}
