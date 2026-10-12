using System;
using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Events;

namespace IDLab.Core.Components
{
    /// <summary>
    /// A rod dipped into a container through a probe port: a metal rod (Fe, Cu, Ag, Zn ...), an inert graphite
    /// electrode or a glass stirring rod. A metal surface below a less active metal's ions is coated by displacement
    /// (Fe in CuSO₄ turns copper-red, Cu in AgNO₃ grows silver crystals); with a <see cref="PowerSupply"/> the rod is
    /// an electrode and is plated (cathode) or dissolves / releases gas (anode).
    /// </summary>
    public sealed class Rod : LabComponent
    {
        /// <summary>Coating thicker than this hides the metal underneath (it becomes the reacting surface).</summary>
        public const double CoveringThicknessM = 0.35e-6;

        private const double TipHeightM = 0.006;
        private const double DisplacementK = 4e-3; // mol / (s · m² · M)

        public sealed class Layer
        {
            public string Symbol;
            public double Moles;
            public bool Crystals; // grown as loose crystals (silver tree) rather than a smooth film
        }

        private readonly List<Layer> _layers = new List<Layer>();
        private readonly HashSet<string> _announced = new HashSet<string>();
        private double _quietTime;

        /// <summary>Element symbol of the rod material ("Fe", "Cu" ... "C" for graphite), or null for glass.</summary>
        public string Material { get; }
        public double LengthM { get; }
        public double DiameterM { get; }
        public string TipSnapId { get; }

        public bool IsGlass => Material == null;
        public bool IsGraphite => Material == "C";

        /// <summary>Takes part in electrolysis (metal or graphite).</summary>
        public bool Conducts => Material != null;

        public LiquidContainer ImmersedIn { get; private set; }
        public double ImmersedLengthM { get; private set; }

        /// <summary>Longest stretch that has been under the liquid while it was being coated (the coated part).</summary>
        public double CoatedLengthM { get; private set; }

        /// <summary>Moles of the rod's own metal that have dissolved away.</summary>
        public double DissolvedMoles { get; private set; }

        /// <summary>Coating layers, innermost first.</summary>
        public IReadOnlyList<Layer> Layers => _layers;

        /// <summary>Gas forming on this rod as an electrode (H₂, O₂, Cl₂), for bubbles.</summary>
        public string GasFormula { get; internal set; }
        public double GasRateMolPerS { get; internal set; }

        /// <summary>Electrode role set by the power supply: −1 cathode (−), +1 anode (+), 0 not connected.</summary>
        public int Polarity
        {
            get => _polarity;
            internal set
            {
                _polarity = value;
                _wired = value != 0;
            }
        }

        private int _polarity;
        private bool _wired; // refreshed by the power supply every tick; drops to 0 when the supply is gone

        /// <summary>Displacement (spontaneous coating) rate in mol of metal per second, for effects.</summary>
        public double CoatingRateMolPerS { get; private set; }

        /// <summary>Seconds of stirring left (glass rod).</summary>
        public double StirTime { get; private set; }

        public Rod(string material, double lengthM, double diameterM, string tipSnapId)
        {
            Material = material;
            LengthM = lengthM;
            DiameterM = diameterM;
            TipSnapId = tipSnapId;
        }

        public override int StepOrder => OrderContainers + 10;

        /// <summary>Wetted surface (m²): the immersed side plus the tip.</summary>
        public double WettedAreaM2 => ImmersedLengthM <= 0 ? 0 : Math.PI * DiameterM * ImmersedLengthM + Math.PI * DiameterM * DiameterM / 4;

        /// <summary>Metal that is in contact with the solution: the outer coating once it covers the rod, else the rod.</summary>
        public string SurfaceSymbol
        {
            get
            {
                for (int i = _layers.Count - 1; i >= 0; i--)
                {
                    if (!_layers[i].Crystals && ThicknessM(_layers[i]) >= CoveringThicknessM) return _layers[i].Symbol;
                }

                return Material;
            }
        }

        /// <summary>Total coating of an element (mol).</summary>
        public double CoatingMoles(string symbol)
        {
            double n = 0;
            foreach (var l in _layers)
            {
                if (l.Symbol == symbol) n += l.Moles;
            }

            return n;
        }

        /// <summary>Average thickness of a layer over the coated part of the rod (m).</summary>
        public double ThicknessM(Layer layer)
        {
            var s = World?.Substances.FindElement(layer.Symbol);
            if (s == null || CoatedLengthM <= 0) return 0;
            double area = Math.PI * DiameterM * CoatedLengthM + Math.PI * DiameterM * DiameterM / 4;
            double cm3 = layer.Moles * s.MolarMass / Math.Max(0.5, s.Density);
            return cm3 * 1e-6 / area;
        }

        /// <summary>0..1: how completely a layer hides what is under it.</summary>
        public double Coverage(Layer layer) => 1 - Math.Exp(-ThicknessM(layer) / (CoveringThicknessM * 0.6));

        public double CoatingMassGrams
        {
            get
            {
                double g = 0;
                foreach (var l in _layers)
                {
                    var s = World?.Substances.FindElement(l.Symbol);
                    if (s != null) g += l.Moles * s.MolarMass;
                }

                return g;
            }
        }

        public void Stir(double seconds = 3) => StirTime = Math.Max(StirTime, seconds);

        /// <summary>Removes all coating (polish the rod with sandpaper).</summary>
        public void Clean()
        {
            _layers.Clear();
            CoatedLengthM = 0;
            _announced.Clear();
        }

        public override void Step(double dt)
        {
            StirTime = Math.Max(0, StirTime - dt);
            if (_wired) _wired = false;
            else _polarity = 0;
            var host = World.GetAttached(Owner, TipSnapId)?.Get<LiquidContainer>();
            ImmersedIn = host != null && host.Contents.HasWater ? host : null;
            ImmersedLengthM = ImmersedIn == null ? 0 : Math.Max(0, Math.Min(LengthM * 0.85, ImmersedIn.LiquidHeightM - TipHeightM));
            if (ImmersedLengthM < 0.002) ImmersedIn = null;
            if (Polarity == 0)
            {
                GasRateMolPerS *= Math.Exp(-dt * 3);
                if (GasRateMolPerS < 1e-9) GasFormula = null;
            }

            CoatingRateMolPerS = 0;
            if (ImmersedIn != null && Conducts && !IsGraphite) Displace(dt);

            bool active = CoatingRateMolPerS > 1e-9 || (Polarity != 0 && GasRateMolPerS > 1e-9);
            _quietTime = active ? 0 : _quietTime + dt;
            if (_quietTime > 3) _announced.Clear();
        }

        // ------------------------------------------------------------------ displacement (no current)

        private void Displace(double dt)
        {
            var m = ImmersedIn.Contents;
            var db = m.Database;
            double liters = Math.Max(1e-6, m.LiquidVolumeLiters);
            double tempFactor = Math.Exp((ImmersedIn.TemperatureC - 20) / 25.0);
            bool chloride = false;
            foreach (var kv in m.Components)
            {
                if (kv.Value > 1e-6 && db.Get(kv.Key).AnionKey == "Cl") chloride = true;
            }

            foreach (var salt in DissolvedSalts(m))
            {
                var target = ActivitySeries.ForCation(salt.CationKey);
                if (target == null) continue;
                // The outer metal reacts; under a film of the deposited metal the rod keeps reacting slowly through its pores.
                string surface = SurfaceSymbol;
                var e = ActivitySeries.Get(surface);
                if (e == null || e.CationKey == null || target.Potential <= e.Potential || surface == target.Symbol)
                {
                    surface = Material;
                    e = ActivitySeries.Get(surface);
                    if (e == null || e.CationKey == null || target.Potential <= e.Potential || surface == target.Symbol) continue;
                }
                var product = db.FindIonic(e.CationKey, salt.AnionKey);
                var deposit = db.FindElement(target.Symbol);
                if (product == null || deposit == null) continue;

                double conc = m.Moles(salt.Id) * salt.CationCount / liters;
                double drive = Math.Min(1.5, Math.Max(0.25, (target.Potential - e.Potential) / 0.8));
                if (surface == "Al" && !chloride) drive *= 0.05; // protective oxide layer
                bool crystals = target.Symbol == "Ag" || target.Symbol == "Pb" || target.Symbol == "Sn";
                // A smooth film slows the reaction down once it covers the rod; loose crystals do not.
                double film = crystals ? 0 : CoatingMoles(target.Symbol);
                double slow = 1.0 / (1.0 + film / Math.Max(1e-9, 0.5 * WettedAreaM2));
                double nDep = DisplacementK * WettedAreaM2 * conc * drive * tempFactor * slow * dt;
                nDep = Math.Min(nDep, m.Moles(salt.Id) * salt.CationCount * 0.5);
                int zDep = Math.Max(1, salt.CationCharge), zSurf = Math.Max(1, product.CationCharge);
                double nSurf = nDep * zDep / zSurf;
                if (surface != Material)
                {
                    // Dissolving a coating: limited by what is there.
                    double have = CoatingMoles(surface);
                    if (nSurf > have)
                    {
                        nSurf = have;
                        nDep = nSurf * zSurf / zDep;
                    }
                }

                if (nDep <= 1e-12) continue;

                m.Remove(salt.Id, nDep / salt.CationCount);
                m.Add(product.Id, nSurf / product.CationCount);
                if (surface == Material) DissolvedMoles += nSurf;
                else RemoveCoating(surface, nSurf);
                AddCoating(target.Symbol, nDep, crystals);
                ImmersedIn.AddHeat(nDep * 80_000);
                CoatingRateMolPerS += nDep / dt;
                Announce(LabEventType.Reaction, $"rod.coat:{target.Symbol}:{surface}",
                    $"{deposit.Name} deposits on the {surface} rod: {surface} + {salt.Formula} → {product.Formula} + {target.Symbol}.");
            }
        }

        private static List<Substance> DissolvedSalts(Mixture m)
        {
            var list = new List<Substance>();
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = m.Database.Get(kv.Key);
                if (s.IsIonic && s.CationKey != "H" && s.IsDissolved(m.HasWater)) list.Add(s);
            }

            return list;
        }

        // ------------------------------------------------------------------ coating bookkeeping (also used by electrolysis)

        internal void AddCoating(string symbol, double moles, bool crystals = false)
        {
            CoatedLengthM = Math.Max(CoatedLengthM, ImmersedLengthM);
            var top = _layers.Count > 0 ? _layers[_layers.Count - 1] : null;
            if (top != null && top.Symbol == symbol && top.Crystals == crystals) top.Moles += moles;
            else _layers.Add(new Layer { Symbol = symbol, Moles = moles, Crystals = crystals });
        }

        /// <summary>Takes metal off the outside of the rod (anode, or a coating being displaced). Returns the moles removed from the coating.</summary>
        internal double RemoveCoating(string symbol, double moles)
        {
            double removed = 0;
            for (int i = _layers.Count - 1; i >= 0 && moles - removed > 1e-15; i--)
            {
                if (_layers[i].Symbol != symbol) continue;
                double take = Math.Min(_layers[i].Moles, moles - removed);
                _layers[i].Moles -= take;
                removed += take;
                if (_layers[i].Moles <= 1e-12) _layers.RemoveAt(i);
            }

            return removed;
        }

        internal void Dissolve(string symbol, double moles)
        {
            double fromCoating = RemoveCoating(symbol, moles);
            if (symbol == Material) DissolvedMoles += moles - fromCoating;
        }

        internal void Announce(LabEventType type, string code, string message)
        {
            if (ImmersedIn == null || !_announced.Add(code)) return;
            ImmersedIn.Publish(type, 0.4, message, Owner.Pose.Position, code);
        }
    }
}
