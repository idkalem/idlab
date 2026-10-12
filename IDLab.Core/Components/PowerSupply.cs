using System;
using System.Collections.Generic;
using IDLab.Core.Chemistry;
using IDLab.Core.Data;
using IDLab.Core.Events;
using IDLab.Core.Model;

namespace IDLab.Core.Components
{
    /// <summary>
    /// Bench DC power supply (0–12 V) wired with crocodile clips to two rods standing in the same solution:
    /// electrolysis and electroplating. The current follows from the voltage, the back-EMF of the two half-reactions and
    /// the resistance of the solution; Faraday's law turns the charge into moles. Cathode: the most easily reduced
    /// metal ion plates out (else H₂). Anode: a metal rod dissolves; graphite releases Cl₂ (chlorides) or O₂.
    /// </summary>
    public sealed class PowerSupply : LabComponent, IDataSource
    {
        public const double Faraday = 96485.0;
        public const double MaxVoltage = 12.0;

        public double MaxCurrentA { get; }

        /// <summary>Voltage set with the knob (V).</summary>
        public double Voltage { get; set; }

        /// <summary>Rod clipped to the black (−) lead: the cathode, which gets plated.</summary>
        public EquipmentInstance Negative { get; private set; }

        /// <summary>Rod clipped to the red (+) lead: the anode.</summary>
        public EquipmentInstance Positive { get; private set; }

        public double CurrentA { get; private set; }

        /// <summary>Charge passed since the leads were connected (C).</summary>
        public double ChargeC { get; private set; }

        /// <summary>What happens at each electrode, for the information panel ("Cu²⁺ + 2e⁻ → Cu").</summary>
        public string CathodeReaction { get; private set; }
        public string AnodeReaction { get; private set; }

        /// <summary>True when the two rods do not stand in the same solution (open circuit).</summary>
        public bool OpenCircuit { get; private set; } = true;

        private readonly HashSet<string> _announced = new HashSet<string>();
        private bool _userDisconnected;
        private double _wireTimer;

        public PowerSupply(double maxCurrentA) => MaxCurrentA = maxCurrentA;

        public override int StepOrder => OrderContainers + 20;

        public void Connect(EquipmentInstance negative, EquipmentInstance positive)
        {
            Negative = negative;
            Positive = positive;
            ChargeC = 0;
            _userDisconnected = false;
            _announced.Clear();
        }

        public void Disconnect()
        {
            ClearPolarity();
            Negative = Positive = null;
            CurrentA = 0;
            _userDisconnected = true;
        }

        /// <summary>Swaps the leads (the other rod gets plated).</summary>
        public void Swap()
        {
            ClearPolarity();
            Connect(Positive, Negative);
        }

        private void ClearPolarity()
        {
            var a = Negative?.Get<Rod>();
            var b = Positive?.Get<Rod>();
            if (a != null) a.Polarity = 0;
            if (b != null) b.Polarity = 0;
        }

        /// <summary>
        /// Clips the leads onto two rods standing in one solution (the nearest such pair). The rod made of the metal
        /// that is in solution (or graphite) becomes the anode, so the other one is plated.
        /// </summary>
        public bool AutoConnect()
        {
            var byContainer = new Dictionary<LiquidContainer, List<Rod>>();
            foreach (var inst in World.Instances)
            {
                var rod = inst.Get<Rod>();
                if (rod == null || !rod.Conducts || rod.ImmersedIn == null) continue;
                if (!byContainer.TryGetValue(rod.ImmersedIn, out var list)) byContainer[rod.ImmersedIn] = list = new List<Rod>();
                list.Add(rod);
            }

            List<Rod> best = null;
            float bestDist = float.MaxValue;
            foreach (var kv in byContainer)
            {
                if (kv.Value.Count < 2) continue;
                float d = System.Numerics.Vector3.Distance(kv.Key.Owner.Pose.Position, Owner.Pose.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = kv.Value;
                }
            }

            if (best == null) return false;
            // Moles of each metal's ions in the solution: the rod of the most abundant one feeds the bath (anode).
            var metals = new Dictionary<string, double>();
            foreach (var kv in best[0].ImmersedIn.Contents.Components)
            {
                var s = best[0].ImmersedIn.Contents.Database.Get(kv.Key);
                var dep = s.IsIonic ? ActivitySeries.ForCation(s.CationKey) : null;
                if (dep != null) metals[dep.Symbol] = (metals.TryGetValue(dep.Symbol, out double n) ? n : 0) + kv.Value * s.CationCount;
            }

            Rod anode = null;
            double most = 1e-8;
            foreach (var r in best)
            {
                if (r.Material != null && metals.TryGetValue(r.Material, out double n) && n > most)
                {
                    most = n;
                    anode = r;
                }
            }

            anode = anode ?? best.Find(r => r.IsGraphite) ?? best[1];
            Rod cathode = best.Find(r => r != anode);
            Connect(cathode.Owner, anode.Owner);
            return true;
        }

        public override void Step(double dt)
        {
            // Turning the knob up clips the leads onto the nearest pair of rods standing in one solution.
            if (Voltage > 0.01 && (Negative == null || Positive == null) && !_userDisconnected)
            {
                _wireTimer -= dt;
                if (_wireTimer <= 0)
                {
                    _wireTimer = 0.5;
                    AutoConnect();
                }
            }

            var neg = Negative?.Get<Rod>();
            var pos = Positive?.Get<Rod>();
            if (neg != null && World.Find(Negative.Id) == null) neg = null;
            if (pos != null && World.Find(Positive.Id) == null) pos = null;
            if (neg == null || pos == null)
            {
                if (neg != null) neg.Polarity = 0;
                if (pos != null) pos.Polarity = 0;
                Negative = neg?.Owner;
                Positive = pos?.Owner;
                CurrentA = 0;
                OpenCircuit = true;
                CathodeReaction = AnodeReaction = null;
                return;
            }

            neg.Polarity = -1;
            pos.Polarity = +1;
            var c = neg.ImmersedIn;
            OpenCircuit = c == null || pos.ImmersedIn != c;
            if (OpenCircuit || Voltage <= 0.01)
            {
                CurrentA = 0;
                neg.GasRateMolPerS *= Math.Exp(-dt * 3);
                pos.GasRateMolPerS *= Math.Exp(-dt * 3);
                if (OpenCircuit) CathodeReaction = AnodeReaction = null;
                return;
            }

            var m = c.Contents;
            var db = m.Database;
            double liters = Math.Max(1e-6, m.LiquidVolumeLiters);
            double pH = c.PH;
            if (double.IsNaN(pH)) pH = 7;

            // ---- cathode: the cation reduced most easily (H₂ needs ~0.5 V extra on most metals).
            Substance plateSalt = null;
            ActivitySeries.Entry plate = null;
            Substance acid = null;
            double ionicStrength = 0;
            foreach (var kv in m.Components)
            {
                if (kv.Value <= 1e-10) continue;
                var s = db.Get(kv.Key);
                if (!s.IsIonic || !s.IsDissolved(m.HasWater)) continue;
                double molar = kv.Value / liters;
                ionicStrength += molar * (s.CationCount + s.AnionCount);
                if (s.CationKey == "H")
                {
                    if (acid == null || kv.Value > m.Moles(acid.Id)) acid = s;
                    continue;
                }

                var dep = ActivitySeries.ForCation(s.CationKey);
                if (dep == null || dep.Potential < -1.0) continue; // Al³⁺, Mg²⁺, Na⁺ ... stay in solution
                if (plate == null || dep.Potential > plate.Potential) { plate = dep; plateSalt = s; }
            }

            double eH2 = -0.059 * pH - 0.45;
            bool depositMetal = plate != null && plate.Potential > eH2 && m.Moles(plateSalt.Id) > 1e-9;
            double eCathode = depositMetal ? plate.Potential : eH2;

            // ---- anode: a metal rod dissolves; graphite (or gold/platinum) gives Cl₂ from chlorides, else O₂.
            string anodeMetal = pos.SurfaceSymbol;
            var anodeEntry = ActivitySeries.Get(anodeMetal);
            bool inertAnode = pos.IsGraphite || anodeEntry == null || anodeEntry.CationKey == null;
            Substance chloride = null;
            if (inertAnode)
            {
                foreach (var kv in m.Components)
                {
                    var s = db.Get(kv.Key);
                    if (s.AnionKey == "Cl" && s.IsDissolved(m.HasWater) && kv.Value / liters > 0.05 && (chloride == null || kv.Value > m.Moles(chloride.Id))) chloride = s;
                }
            }

            double eAnode = !inertAnode ? anodeEntry.Potential : chloride != null ? 1.36 + 0.25 : 1.23 - 0.059 * pH + 0.45;
            double backEmf = Math.Max(0, eAnode - eCathode) + 0.15;

            // ---- solution resistance: ~ 1 / (conductivity × area / gap).
            double area = Math.Max(1e-5, Math.Min(neg.WettedAreaM2, pos.WettedAreaM2));
            double gap = Math.Max(0.015, System.Numerics.Vector3.Distance(neg.Owner.Pose.Position, pos.Owner.Pose.Position));
            double kappa = 5e-4 + 6.0 * ionicStrength; // S/m
            double resistance = gap / (kappa * area) + 0.4;
            CurrentA = Math.Min(MaxCurrentA, Math.Max(0, (Voltage - backEmf) / resistance));
            if (CurrentA < 1e-6)
            {
                CurrentA = 0;
                neg.GasRateMolPerS *= Math.Exp(-dt * 3);
                pos.GasRateMolPerS *= Math.Exp(-dt * 3);
                return;
            }

            double electrons = CurrentA * dt / Faraday;
            ChargeC += CurrentA * dt;
            c.AddHeat(CurrentA * CurrentA * resistance * dt);

            // Ion pools that recombine into compounds afterwards.
            var cations = new Dictionary<string, double>();
            var anions = new Dictionary<string, double>();
            double hPlus = 0, ohMinus = 0;

            // Cathode half-reaction.
            if (depositMetal)
            {
                int z = Math.Max(1, plateSalt.CationCharge);
                double metal = Math.Min(electrons / z, m.Moles(plateSalt.Id) * plateSalt.CationCount);
                double saltUsed = metal / plateSalt.CationCount;
                m.Remove(plateSalt.Id, saltUsed);
                Add(anions, plateSalt.AnionKey, saltUsed * plateSalt.AnionCount);
                neg.AddCoating(plate.Symbol, metal);
                neg.GasRateMolPerS *= Math.Exp(-dt * 3);
                CathodeReaction = $"{plate.Symbol}{Charge(z)} + {z}e⁻ → {plate.Symbol}";
                Announce(c, LabEventType.Reaction, "electro.plate:" + plate.Symbol, $"Electrolysis: {plate.Symbol} plates onto the cathode.");
            }
            else
            {
                if (acid != null && pH < 4)
                {
                    double used = Math.Min(electrons / Math.Max(1, acid.CationCount), m.Moles(acid.Id));
                    m.Remove(acid.Id, used);
                    Add(anions, acid.AnionKey, used * acid.AnionCount);
                    ohMinus += electrons - used * acid.CationCount;
                    CathodeReaction = "2H⁺ + 2e⁻ → H₂↑";
                }
                else
                {
                    ohMinus += electrons;
                    CathodeReaction = "2H₂O + 2e⁻ → H₂↑ + 2OH⁻";
                }

                neg.GasFormula = "H₂";
                neg.GasRateMolPerS = electrons / 2 / dt;
                Announce(c, LabEventType.Reaction, "electro.h2", "Electrolysis: hydrogen bubbles at the cathode.");
            }

            // Anode half-reaction.
            if (!inertAnode)
            {
                var ion = AnyCompound(db, anodeEntry.CationKey);
                int z = Math.Max(1, ion?.CationCharge ?? 2);
                pos.Dissolve(anodeMetal, electrons / z);
                Add(cations, anodeEntry.CationKey, electrons / z);
                pos.GasRateMolPerS *= Math.Exp(-dt * 3);
                AnodeReaction = $"{anodeMetal} → {anodeMetal}{Charge(z)} + {z}e⁻";
                Announce(c, LabEventType.Reaction, "electro.anode:" + anodeMetal, $"Electrolysis: the {anodeMetal} anode dissolves.");
            }
            else if (chloride != null)
            {
                double used = Math.Min(electrons / chloride.AnionCount, m.Moles(chloride.Id));
                m.Remove(chloride.Id, used);
                Add(cations, chloride.CationKey, used * chloride.CationCount);
                pos.GasFormula = "Cl₂";
                pos.GasRateMolPerS = used * chloride.AnionCount / 2 / dt;
                AnodeReaction = "2Cl⁻ → Cl₂↑ + 2e⁻";
                Announce(c, LabEventType.ToxicGas, "electro.cl2", "Electrolysis: toxic chlorine gas forms at the anode.");
            }
            else
            {
                hPlus += electrons;
                pos.GasFormula = "O₂";
                pos.GasRateMolPerS = electrons / 4 / dt;
                AnodeReaction = "2H₂O → O₂↑ + 4H⁺ + 4e⁻";
                Announce(c, LabEventType.Reaction, "electro.o2", "Electrolysis: oxygen bubbles at the anode.");
            }

            Recombine(m, cations, anions, hPlus, ohMinus);
        }

        private static string Charge(int z) => z == 1 ? "⁺" : z == 2 ? "²⁺" : z == 3 ? "³⁺" : z + "+";

        private static void Add(Dictionary<string, double> pool, string key, double moles)
        {
            if (key == null || moles <= 0) return;
            pool[key] = (pool.TryGetValue(key, out double n) ? n : 0) + moles;
        }

        private static Substance AnyCompound(SubstanceDatabase db, string cationKey) =>
            db.FindIonic(cationKey, "SO4") ?? db.FindIonic(cationKey, "Cl") ?? db.FindIonic(cationKey, "NO3") ?? db.FindIonic(cationKey, "OH");

        /// <summary>Puts the ions made at the two electrodes back together as compounds (CuSO₄, H₂SO₄, NaOH, Fe(OH)₂ ...).</summary>
        private static void Recombine(Mixture m, Dictionary<string, double> cations, Dictionary<string, double> anions, double hPlus, double ohMinus)
        {
            var db = m.Database;
            double water = Math.Min(hPlus, ohMinus);
            hPlus -= water;
            ohMinus -= water;
            if (ohMinus > 0) Add(anions, "OH", ohMinus);
            if (hPlus > 0) Add(cations, "H", hPlus);

            foreach (var cat in new List<string>(cations.Keys))
            {
                foreach (var an in new List<string>(anions.Keys))
                {
                    double nc = cations[cat], na = anions[an];
                    if (nc <= 1e-15 || na <= 1e-15) continue;
                    var s = db.FindIonic(cat, an);
                    if (s == null) continue;
                    double units = Math.Min(nc / s.CationCount, na / s.AnionCount);
                    m.Add(s.Id, units);
                    cations[cat] = nc - units * s.CationCount;
                    anions[an] = na - units * s.AnionCount;
                }
            }
        }

        private void Announce(LiquidContainer c, LabEventType type, string code, string message)
        {
            if (_announced.Add(code)) c.Publish(type, 0.4, message, c.MouthPosition, code);
        }

        public void RegisterChannels(DataLogger logger)
        {
            var ch = logger.AddChannel($"{Owner.Id}.Current", $"Current — {Owner.Definition.DisplayName} #{Owner.Id}", "A", Owner.Id, () => CurrentA);
            ch.SuggestedMin = 0;
            ch.SuggestedMax = 1;
        }
    }
}
