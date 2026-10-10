using System.Collections.Generic;

namespace IDLab.Core.Chemistry
{
    public static class SubstanceIds
    {
        public const string Water = "water";
        public const string Ethanol = "ethanol";
        public const string HydrochloricAcid = "hcl";
        public const string SulfuricAcid = "h2so4";
        public const string NitricAcid = "hno3";
        public const string AceticAcid = "acetic_acid";
        public const string SodiumHydroxide = "naoh";
        public const string PotassiumHydroxide = "koh";
        public const string Ammonia = "nh3";
        public const string SodiumChloride = "nacl";
        public const string PotassiumChloride = "kcl";
        public const string SodiumNitrate = "nano3";
        public const string PotassiumNitrate = "kno3";
        public const string SodiumSulfate = "na2so4";
        public const string PotassiumSulfate = "k2so4";
        public const string SodiumAcetate = "sodium_acetate";
        public const string PotassiumAcetate = "potassium_acetate";
        public const string AmmoniumChloride = "nh4cl";
        public const string AmmoniumNitrate = "nh4no3";
        public const string AmmoniumSulfate = "nh4_2so4";
        public const string MagnesiumChloride = "mgcl2";
        public const string ZincChloride = "zncl2";
        public const string IronIIChloride = "fecl2";
        public const string CalciumChloride = "cacl2";
        public const string MagnesiumSulfate = "mgso4";
        public const string ZincSulfate = "znso4";
        public const string IronIISulfate = "feso4";
        public const string CopperNitrate = "cu_no3_2";
        public const string CopperSulfate = "cuso4";
        public const string IronIIIChloride = "fecl3";
        public const string PotassiumPermanganate = "kmno4";
        public const string SilverNitrate = "agno3";
        public const string SilverChloride = "agcl";
        public const string CopperHydroxide = "cu_oh_2";
        public const string IronIIIHydroxide = "fe_oh_3";
        public const string Magnesium = "mg";
        public const string Zinc = "zn";
        public const string Iron = "fe";
        public const string Copper = "cu";
        public const string Silver = "ag";
        public const string Sodium = "sodium";
        public const string SodiumBicarbonate = "nahco3";
        public const string CalciumCarbonate = "caco3";
        public const string Sucrose = "sucrose";
        public const string HydrogenPeroxide = "h2o2";
        public const string PotassiumIodide = "ki";
        public const string Detergent = "detergent";
        public const string DryYeast = "dry_yeast";
        public const string FoodDyeRed = "food_dye_red";
        public const string SodiumHypochlorite = "naocl";
        public const string UniversalIndicator = "universal_indicator";
        public const string Phenolphthalein = "phenolphthalein";
    }

    /// <summary>Micro-view particle species ids (the visual vocabulary of the atomic inset). ASCII; front-ends prettify.</summary>
    public static class SpeciesIds
    {
        public const string Water = "H2O";
        public const string Hydronium = "H3O+";
        public const string Hydroxide = "OH-";
        public const string SodiumIon = "Na+";
        public const string PotassiumIon = "K+";
        public const string ChlorideIon = "Cl-";
        public const string NitrateIon = "NO3-";
        public const string SulfateIon = "SO4^2-";
        public const string AmmoniumIon = "NH4+";
        public const string Ammonia = "NH3";
        public const string MagnesiumIon = "Mg^2+";
        public const string ZincIon = "Zn^2+";
        public const string IronIIIon = "Fe^2+";
        public const string IronIIIIon = "Fe^3+";
        public const string CopperIon = "Cu^2+";
        public const string SilverIon = "Ag+";
        public const string CalciumIon = "Ca^2+";
        public const string BicarbonateIon = "HCO3-";
        public const string PermanganateIon = "MnO4-";
        public const string IodideIon = "I-";
        public const string AcetateIon = "CH3COO-";
        public const string AceticAcid = "CH3COOH";
        public const string HypochloriteIon = "OCl-";
        public const string Ethanol = "C2H5OH";
        public const string Sucrose = "C12H22O11";
        public const string HydrogenPeroxide = "H2O2";
        public const string SodiumAtom = "Na";
        public const string Indicator = "Ind";
    }

    public sealed class SubstanceDatabase
    {
        private readonly Dictionary<string, Substance> _byId = new Dictionary<string, Substance>();

        public IEnumerable<Substance> All => _byId.Values;

        private readonly Dictionary<(string, string), Substance> _ionic = new Dictionary<(string, string), Substance>();
        private readonly Dictionary<string, Substance> _elements = new Dictionary<string, Substance>();

        public int Count => _byId.Count;

        public void Register(Substance substance)
        {
            _byId[substance.Id] = substance;
            _byFormula = null;
            Index(substance);
        }

        /// <summary>Re-indexes a substance after its ionic/element metadata changed.</summary>
        public void Index(Substance substance)
        {
            if (substance.IsIonic && !_ionic.ContainsKey((substance.CationKey, substance.AnionKey)))
            {
                _ionic[(substance.CationKey, substance.AnionKey)] = substance;
            }

            if (substance.ElementSymbol != null && !_elements.ContainsKey(substance.ElementSymbol))
            {
                _elements[substance.ElementSymbol] = substance;
            }
        }

        /// <summary>The compound made of a cation and an anion (e.g. ("Ag", "Cl") → AgCl), or null.</summary>
        public Substance FindIonic(string cationKey, string anionKey) =>
            _ionic.TryGetValue((cationKey, anionKey), out var s) ? s : null;

        private Dictionary<string, Substance> _byFormula;

        /// <summary>A visible substance with exactly this (pretty) formula, e.g. "Fe₃O₄", or null.</summary>
        public Substance FindByFormula(string formula)
        {
            if (_byFormula == null || _byFormula.Count == 0)
            {
                _byFormula = new Dictionary<string, Substance>();
                foreach (var s in _byId.Values)
                {
                    if (s.Hidden || string.IsNullOrEmpty(s.Formula)) continue;
                    if (!_byFormula.ContainsKey(s.Formula)) _byFormula[s.Formula] = s;
                }
            }

            return _byFormula.TryGetValue(formula, out var found) ? found : null;
        }

        /// <summary>The pure element substance for a symbol (e.g. "Cu"), or null.</summary>
        public Substance FindElement(string symbol) => _elements.TryGetValue(symbol, out var s) ? s : null;

        public Substance Get(string id)
        {
            if (_byId.TryGetValue(id, out var s))
            {
                return s;
            }

            throw new KeyNotFoundException($"Unknown substance '{id}'.");
        }

        public bool TryGet(string id, out Substance substance) => _byId.TryGetValue(id, out substance);

        internal static double Mass(Substance s, double moles) => System.Math.Max(0, moles) * s.MolarMass / 1000.0; // kg

        // ------------------------------------------------------------------ default data

        private static ColorRGBA C(uint hex, float a) => ColorRGBA.FromHex(hex, a);

        private void Add(Substance s, params string[] ions)
        {
            foreach (var ion in ions)
            {
                int star = ion.IndexOf('*');
                s.Ions.Add(star < 0 ? new IonPart(ion) : new IonPart(ion.Substring(0, star), int.Parse(ion.Substring(star + 1))));
            }

            Register(s);
        }

        private void Salt(string id, string name, string formula, double molarMass, double density, ColorRGBA color, double tint,
            Hazard hazards, params string[] ions) =>
            Add(new Substance
            {
                Id = id, Name = name, Formula = formula, Kind = SubstanceKind.Salt, Phase = SubstancePhase.Solid, Soluble = true,
                Form = SolidForm.Crystals, MolarMass = molarMass, Density = density, SpecificHeat = 900, Color = color,
                TintStrength = tint, Hazards = hazards,
            }, ions);

        private void Precipitate(string id, string name, string formula, double molarMass, double density, ColorRGBA color, SolidForm form = SolidForm.Precipitate) =>
            Add(new Substance
            {
                Id = id, Name = name, Formula = formula, Kind = SubstanceKind.Salt, Phase = SubstancePhase.Solid, Soluble = false,
                Form = form, MolarMass = molarMass, Density = density, SpecificHeat = 700, Color = color,
            });

        private void Metal(string id, string name, string formula, double molarMass, double density, ColorRGBA color, SolidForm form, Hazard hazards = Hazard.None) =>
            Add(new Substance
            {
                Id = id, Name = name, Formula = formula, Kind = SubstanceKind.Metal, Phase = SubstancePhase.Solid, Soluble = false,
                Form = form, MolarMass = molarMass, Density = density, SpecificHeat = 450, Color = color, Hazards = hazards,
                MolecularSpeciesId = formula,
            });

        private static SubstanceDatabase _shared;
        private static readonly object SharedLock = new object();

        /// <summary>
        /// The default catalogue, built once and shared by every <see cref="Simulation.LabWorld"/> (it holds tens of
        /// thousands of substances, which are read-only once registered).
        /// </summary>
        public static SubstanceDatabase Shared
        {
            get
            {
                lock (SharedLock)
                {
                    return _shared ?? (_shared = CreateDefault());
                }
            }
        }

        public static SubstanceDatabase CreateDefault()
        {
            var db = new SubstanceDatabase();
            var white = C(0xF2F2F2, 1f);

            // --- solvents
            db.Add(new Substance
            {
                Id = SubstanceIds.Water, Name = "Water", Formula = "H₂O", Kind = SubstanceKind.Solvent, Phase = SubstancePhase.Liquid,
                MolarMass = 18.015, Density = 1.0, SpecificHeat = 4184, BoilingPointC = 100, LatentHeatVaporization = 2.257e6,
                MolecularSpeciesId = SpeciesIds.Water, Color = new ColorRGBA(0.70f, 0.85f, 1.0f, 0.25f),
            });
            db.Add(new Substance
            {
                Id = SubstanceIds.Ethanol, Name = "Ethanol", Formula = "C₂H₅OH", Kind = SubstanceKind.Solvent, Phase = SubstancePhase.Liquid,
                MolarMass = 46.07, Density = 0.789, SpecificHeat = 2440, BoilingPointC = 78.4, LatentHeatVaporization = 8.46e5,
                MolecularSpeciesId = SpeciesIds.Ethanol, Color = new ColorRGBA(0.92f, 0.96f, 1.0f, 0.18f),
                Hazards = Hazard.Flammable | Hazard.Irritant,
            });

            // --- acids
            db.Add(new Substance
            {
                Id = SubstanceIds.HydrochloricAcid, Name = "Hydrochloric acid", Formula = "HCl", Kind = SubstanceKind.StrongAcid,
                MolarMass = 36.46, Density = 1.19, SpecificHeat = 2500, Protons = 1, Color = new ColorRGBA(0.95f, 0.97f, 0.85f, 0.25f),
                Hazards = Hazard.Corrosive | Hazard.Irritant,
            }, SpeciesIds.Hydronium, SpeciesIds.ChlorideIon);
            db.Add(new Substance
            {
                Id = SubstanceIds.SulfuricAcid, Name = "Sulfuric acid", Formula = "H₂SO₄", Kind = SubstanceKind.StrongAcid,
                MolarMass = 98.08, Density = 1.83, SpecificHeat = 1400, Protons = 2, Color = new ColorRGBA(0.97f, 0.97f, 0.9f, 0.25f),
                Hazards = Hazard.Corrosive,
            }, SpeciesIds.Hydronium + "*2", SpeciesIds.SulfateIon);
            db.Add(new Substance
            {
                Id = SubstanceIds.NitricAcid, Name = "Nitric acid", Formula = "HNO₃", Kind = SubstanceKind.StrongAcid,
                MolarMass = 63.01, Density = 1.51, SpecificHeat = 1700, Protons = 1, Color = new ColorRGBA(0.98f, 0.97f, 0.85f, 0.25f),
                Hazards = Hazard.Corrosive | Hazard.Oxidizer,
            }, SpeciesIds.Hydronium, SpeciesIds.NitrateIon);
            db.Add(new Substance
            {
                Id = SubstanceIds.AceticAcid, Name = "Acetic acid", Formula = "CH₃COOH", Kind = SubstanceKind.WeakAcid,
                MolarMass = 60.05, Density = 1.05, SpecificHeat = 2050, Protons = 1, Ka = 1.8e-5, MolecularSpeciesId = SpeciesIds.AceticAcid,
                Color = new ColorRGBA(1.0f, 0.98f, 0.9f, 0.25f), Hazards = Hazard.Irritant,
            }, SpeciesIds.Hydronium, SpeciesIds.AcetateIon);

            // --- bases
            db.Add(new Substance
            {
                Id = SubstanceIds.SodiumHydroxide, Name = "Sodium hydroxide", Formula = "NaOH", Kind = SubstanceKind.StrongBase,
                MolarMass = 40.0, Density = 2.13, SpecificHeat = 1490, Hydroxides = 1, Color = new ColorRGBA(0.92f, 0.95f, 1.0f, 0.25f),
                Hazards = Hazard.Corrosive,
            }, SpeciesIds.SodiumIon, SpeciesIds.Hydroxide);
            db.Add(new Substance
            {
                Id = SubstanceIds.PotassiumHydroxide, Name = "Potassium hydroxide", Formula = "KOH", Kind = SubstanceKind.StrongBase,
                MolarMass = 56.11, Density = 2.04, SpecificHeat = 1180, Hydroxides = 1, Color = new ColorRGBA(0.92f, 0.95f, 1.0f, 0.25f),
                Hazards = Hazard.Corrosive,
            }, SpeciesIds.PotassiumIon, SpeciesIds.Hydroxide);
            db.Add(new Substance
            {
                Id = SubstanceIds.Ammonia, Name = "Ammonia solution", Formula = "NH₃", Kind = SubstanceKind.WeakBase,
                MolarMass = 17.03, Density = 0.9, SpecificHeat = 4700, Hydroxides = 1, Kb = 1.8e-5, MolecularSpeciesId = SpeciesIds.Ammonia,
                Color = new ColorRGBA(0.95f, 0.97f, 1.0f, 0.22f), Hazards = Hazard.Corrosive | Hazard.Irritant | Hazard.Environment,
            }, SpeciesIds.AmmoniumIon, SpeciesIds.Hydroxide);

            // --- salts (dissolve in water)
            db.Salt(SubstanceIds.SodiumChloride, "Sodium chloride", "NaCl", 58.44, 2.16, white, 0, Hazard.None, SpeciesIds.SodiumIon, SpeciesIds.ChlorideIon);
            db.Salt(SubstanceIds.PotassiumChloride, "Potassium chloride", "KCl", 74.55, 1.98, white, 0, Hazard.None, SpeciesIds.PotassiumIon, SpeciesIds.ChlorideIon);
            db.Salt(SubstanceIds.SodiumNitrate, "Sodium nitrate", "NaNO₃", 84.99, 2.26, white, 0, Hazard.Oxidizer, SpeciesIds.SodiumIon, SpeciesIds.NitrateIon);
            db.Salt(SubstanceIds.PotassiumNitrate, "Potassium nitrate", "KNO₃", 101.1, 2.11, white, 0, Hazard.Oxidizer, SpeciesIds.PotassiumIon, SpeciesIds.NitrateIon);
            db.Salt(SubstanceIds.SodiumSulfate, "Sodium sulfate", "Na₂SO₄", 142.04, 2.66, white, 0, Hazard.None, SpeciesIds.SodiumIon + "*2", SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.PotassiumSulfate, "Potassium sulfate", "K₂SO₄", 174.26, 2.66, white, 0, Hazard.None, SpeciesIds.PotassiumIon + "*2", SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.SodiumAcetate, "Sodium acetate", "CH₃COONa", 82.03, 1.53, white, 0, Hazard.None, SpeciesIds.SodiumIon, SpeciesIds.AcetateIon);
            db.Salt(SubstanceIds.PotassiumAcetate, "Potassium acetate", "CH₃COOK", 98.14, 1.57, white, 0, Hazard.None, SpeciesIds.PotassiumIon, SpeciesIds.AcetateIon);
            db.Salt(SubstanceIds.AmmoniumChloride, "Ammonium chloride", "NH₄Cl", 53.49, 1.53, white, 0, Hazard.Irritant, SpeciesIds.AmmoniumIon, SpeciesIds.ChlorideIon);
            db.Salt(SubstanceIds.AmmoniumNitrate, "Ammonium nitrate", "NH₄NO₃", 80.04, 1.72, white, 0, Hazard.Oxidizer, SpeciesIds.AmmoniumIon, SpeciesIds.NitrateIon);
            db.Salt(SubstanceIds.AmmoniumSulfate, "Ammonium sulfate", "(NH₄)₂SO₄", 132.14, 1.77, white, 0, Hazard.None, SpeciesIds.AmmoniumIon + "*2", SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.MagnesiumChloride, "Magnesium chloride", "MgCl₂", 95.21, 2.32, white, 0, Hazard.None, SpeciesIds.MagnesiumIon, SpeciesIds.ChlorideIon + "*2");
            db.Salt(SubstanceIds.ZincChloride, "Zinc chloride", "ZnCl₂", 136.3, 2.91, white, 0, Hazard.Corrosive | Hazard.Environment, SpeciesIds.ZincIon, SpeciesIds.ChlorideIon + "*2");
            db.Salt(SubstanceIds.IronIIChloride, "Iron(II) chloride", "FeCl₂", 126.75, 3.16, C(0x9ED39A, 0.6f), 1.5, Hazard.Irritant, SpeciesIds.IronIIIon, SpeciesIds.ChlorideIon + "*2");
            db.Salt(SubstanceIds.CalciumChloride, "Calcium chloride", "CaCl₂", 110.98, 2.15, white, 0, Hazard.Irritant, SpeciesIds.CalciumIon, SpeciesIds.ChlorideIon + "*2");
            db.Salt(SubstanceIds.MagnesiumSulfate, "Magnesium sulfate", "MgSO₄", 120.37, 2.66, white, 0, Hazard.None, SpeciesIds.MagnesiumIon, SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.ZincSulfate, "Zinc sulfate", "ZnSO₄", 161.47, 3.54, white, 0, Hazard.Irritant | Hazard.Environment, SpeciesIds.ZincIon, SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.IronIISulfate, "Iron(II) sulfate", "FeSO₄", 151.91, 2.84, C(0xA8D8A0, 0.6f), 1.2, Hazard.Irritant, SpeciesIds.IronIIIon, SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.CopperNitrate, "Copper(II) nitrate", "Cu(NO₃)₂", 187.56, 3.05, C(0x2E7DE0, 0.75f), 2.5, Hazard.Oxidizer | Hazard.Environment, SpeciesIds.CopperIon, SpeciesIds.NitrateIon + "*2");
            db.Salt(SubstanceIds.CopperSulfate, "Copper(II) sulfate", "CuSO₄", 159.61, 3.6, C(0x1F6FE5, 0.8f), 4.0, Hazard.Irritant | Hazard.Environment, SpeciesIds.CopperIon, SpeciesIds.SulfateIon);
            db.Salt(SubstanceIds.PotassiumPermanganate, "Potassium permanganate", "KMnO₄", 158.03, 2.7, C(0x7A0F8C, 0.9f), 400, Hazard.Oxidizer | Hazard.Environment | Hazard.Irritant, SpeciesIds.PotassiumIon, SpeciesIds.PermanganateIon);
            db.Salt(SubstanceIds.PotassiumIodide, "Potassium iodide", "KI", 166.0, 3.12, white, 0, Hazard.None, SpeciesIds.PotassiumIon, SpeciesIds.IodideIon);
            db.Salt(SubstanceIds.SodiumBicarbonate, "Sodium bicarbonate", "NaHCO₃", 84.01, 2.2, white, 0, Hazard.None, SpeciesIds.SodiumIon, SpeciesIds.BicarbonateIon);
            db.Get(SubstanceIds.SodiumBicarbonate).Form = SolidForm.Powder;
            db.Get(SubstanceIds.SodiumAcetate).ConjugateId = SubstanceIds.AceticAcid;
            db.Get(SubstanceIds.PotassiumAcetate).ConjugateId = SubstanceIds.AceticAcid;
            db.Get(SubstanceIds.AmmoniumChloride).ConjugateId = SubstanceIds.Ammonia;
            db.Get(SubstanceIds.AmmoniumNitrate).ConjugateId = SubstanceIds.Ammonia;
            db.Get(SubstanceIds.AmmoniumSulfate).ConjugateId = SubstanceIds.Ammonia;
            db.Get(SubstanceIds.AmmoniumSulfate).ConjugateCount = 2;

            // Solutions supplied ready-made (aqueous only).
            db.Add(new Substance
            {
                Id = SubstanceIds.IronIIIChloride, Name = "Iron(III) chloride", Formula = "FeCl₃", Kind = SubstanceKind.Salt,
                MolarMass = 162.2, Density = 2.9, SpecificHeat = 900, Color = C(0xE8901A, 0.8f), TintStrength = 3.0,
                Hazards = Hazard.Corrosive | Hazard.Irritant,
            }, SpeciesIds.IronIIIIon, SpeciesIds.ChlorideIon + "*3");
            db.Add(new Substance
            {
                Id = SubstanceIds.SilverNitrate, Name = "Silver nitrate", Formula = "AgNO₃", Kind = SubstanceKind.Salt,
                MolarMass = 169.87, Density = 4.35, SpecificHeat = 600, Color = new ColorRGBA(0.95f, 0.95f, 0.95f, 0.2f),
                Hazards = Hazard.Corrosive | Hazard.Oxidizer | Hazard.Environment,
            }, SpeciesIds.SilverIon, SpeciesIds.NitrateIon);
            db.Add(new Substance
            {
                Id = SubstanceIds.HydrogenPeroxide, Name = "Hydrogen peroxide", Formula = "H₂O₂", Kind = SubstanceKind.Oxidizer,
                MolarMass = 34.01, Density = 1.45, SpecificHeat = 2620, MolecularSpeciesId = SpeciesIds.HydrogenPeroxide,
                Color = new ColorRGBA(0.95f, 0.97f, 1.0f, 0.2f), Hazards = Hazard.Oxidizer | Hazard.Irritant,
            });
            // --- household materials for the elephant-toothpaste demo
            db.Add(new Substance
            {
                Id = SubstanceIds.Detergent, Name = "Dish detergent (sodium laureth sulfate)", NameTr = "Bulaşık deterjanı", Formula = "C₁₆H₃₃NaO₆S",
                Kind = SubstanceKind.Solvent, Phase = SubstancePhase.Liquid, Soluble = true, MolarMass = 376.5, Density = 1.03, SpecificHeat = 3500,
                BoilingPointC = 105, LatentHeatVaporization = 2.2e6, MolecularSpeciesId = "C16H33NaO6S", Category = "organic",
                Color = new ColorRGBA(0.42f, 0.82f, 0.32f, 0.55f), Hazards = Hazard.Irritant,
            });
            db.Add(new Substance
            {
                Id = SubstanceIds.DryYeast, Name = "Dry yeast (contains the enzyme catalase)", NameTr = "Kuru maya", Formula = "maya (katalaz)",
                Kind = SubstanceKind.Molecular, Phase = SubstancePhase.Solid, Soluble = false, Form = SolidForm.Powder,
                MolarMass = 100, Density = 0.8, SpecificHeat = 1500, Category = "biochem", Color = C(0xC9A36B, 1f),
            });
            db.Add(new Substance
            {
                Id = SubstanceIds.FoodDyeRed, Name = "Red food colouring (Allura red AC)", NameTr = "Gıda boyası (kırmızı)", Formula = "C₁₈H₁₄N₂Na₂O₈S₂",
                Kind = SubstanceKind.Molecular, Phase = SubstancePhase.Aqueous, MolarMass = 496.4, Density = 1.6, SpecificHeat = 1500,
                MolecularSpeciesId = "C18H14N2Na2O8S2", Category = "organic", Color = C(0xD81B2C, 0.9f), TintStrength = 4000,
            });
            foreach (var (id, aliases) in new[]
            {
                (SubstanceIds.Detergent, new[] { "Deterjan", "Sıvı sabun", "Bulaşık sabunu", "SLES" }),
                (SubstanceIds.DryYeast, new[] { "Maya", "Ekmek mayası", "Katalaz", "Yeast" }),
                (SubstanceIds.FoodDyeRed, new[] { "Gıda boyası", "Boya", "Allura kırmızısı", "E129" }),
            })
            {
                db.Get(id).Aliases.AddRange(aliases);
            }

            db.Add(new Substance
            {
                Id = SubstanceIds.SodiumHypochlorite, Name = "Sodium hypochlorite (bleach)", Formula = "NaOCl", Kind = SubstanceKind.Oxidizer,
                MolarMass = 74.44, Density = 1.11, SpecificHeat = 3000, Color = new ColorRGBA(0.92f, 0.98f, 0.75f, 0.3f),
                Hazards = Hazard.Corrosive | Hazard.Environment,
            }, SpeciesIds.SodiumIon, SpeciesIds.HypochloriteIon);

            // --- insoluble solids: precipitates, carbonates, metals
            db.Precipitate(SubstanceIds.SilverChloride, "Silver chloride", "AgCl", 143.32, 5.56, C(0xF4F4F0, 1f));
            db.Precipitate(SubstanceIds.CopperHydroxide, "Copper(II) hydroxide", "Cu(OH)₂", 97.56, 3.37, C(0x68B4F0, 1f));
            db.Precipitate(SubstanceIds.IronIIIHydroxide, "Iron(III) hydroxide", "Fe(OH)₃", 106.87, 3.4, C(0x9A4A18, 1f));
            db.Precipitate(SubstanceIds.CalciumCarbonate, "Calcium carbonate (marble chips)", "CaCO₃", 100.09, 2.71, C(0xEDEAE2, 1f), SolidForm.Chunks);
            db.Metal(SubstanceIds.Magnesium, "Magnesium", "Mg", 24.31, 1.74, C(0xC9CCD2, 1f), SolidForm.Ribbon, Hazard.Flammable);
            db.Metal(SubstanceIds.Zinc, "Zinc", "Zn", 65.38, 7.14, C(0xAEB4BE, 1f), SolidForm.Granules);
            db.Metal(SubstanceIds.Iron, "Iron", "Fe", 55.85, 7.87, C(0x55585E, 1f), SolidForm.Powder);
            db.Metal(SubstanceIds.Copper, "Copper", "Cu", 63.55, 8.96, C(0xC8703A, 1f), SolidForm.Turnings);
            db.Metal(SubstanceIds.Silver, "Silver", "Ag", 107.87, 10.49, C(0xD8DADF, 1f), SolidForm.Crystals);
            db.Add(new Substance
            {
                Id = SubstanceIds.Sodium, Name = "Sodium metal", Formula = "Na", Kind = SubstanceKind.ReactiveMetal, Phase = SubstancePhase.Solid,
                Form = SolidForm.Chunks, MolarMass = 22.99, Density = 0.97, SpecificHeat = 1230, MolecularSpeciesId = SpeciesIds.SodiumAtom,
                Color = C(0xBFBFB8, 1f), Hazards = Hazard.Flammable | Hazard.WaterReactive | Hazard.Corrosive,
            });
            db.Add(new Substance
            {
                Id = SubstanceIds.Sucrose, Name = "Sucrose (sugar)", Formula = "C₁₂H₂₂O₁₁", Kind = SubstanceKind.Molecular, Phase = SubstancePhase.Solid,
                Soluble = true, Form = SolidForm.Crystals, MolarMass = 342.3, Density = 1.59, SpecificHeat = 1250,
                MolecularSpeciesId = SpeciesIds.Sucrose, Color = C(0xFAFAFA, 1f),
            });

            // --- indicators
            db.Add(new Substance
            {
                Id = SubstanceIds.UniversalIndicator, Name = "Universal indicator", Formula = "Ind", Kind = SubstanceKind.Indicator,
                MolarMass = 300, Density = 1.0, SpecificHeat = 2000, MolecularSpeciesId = SpeciesIds.Indicator,
                Color = new ColorRGBA(0.2f, 0.7f, 0.2f, 0.8f), Hazards = Hazard.Flammable,
            });
            db.Add(new Substance
            {
                Id = SubstanceIds.Phenolphthalein, Name = "Phenolphthalein", Formula = "C₂₀H₁₄O₄", Kind = SubstanceKind.Indicator,
                MolarMass = 318.3, Density = 1.28, SpecificHeat = 2000, MolecularSpeciesId = SpeciesIds.Indicator,
                Color = new ColorRGBA(0.97f, 0.97f, 0.97f, 0.15f), Hazards = Hazard.Irritant,
            });

            ChemicalLibrary.Register(db);
            return db;
        }
    }
}
