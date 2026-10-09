using System.Collections.Generic;

namespace IDLab.Core.Model
{
    /// <summary>A guided lab: which equipment the inventory offers, what to plot, what to observe.</summary>
    public sealed class ExperimentDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public Discipline Discipline { get; set; }
        public ExperimentStatus Status { get; set; }
        public string Summary { get; set; }

        /// <summary>Equipment offered in the inventory panel (empty = everything in the catalog).</summary>
        public List<string> Equipment { get; } = new List<string>();

        /// <summary>Equipment pre-placed on the bench when the experiment loads (id + bench x/z in metres).</summary>
        public List<(string id, float x, float z)> InitialLayout { get; } = new List<(string id, float x, float z)>();

        public List<string> Objectives { get; } = new List<string>();
    }

    public sealed class ExperimentCatalog
    {
        public const string SandboxId = "sandbox";

        private readonly List<ExperimentDefinition> _all = new List<ExperimentDefinition>();

        public IReadOnlyList<ExperimentDefinition> All => _all;

        public ExperimentDefinition Get(string id)
        {
            foreach (var e in _all)
            {
                if (e.Id == id)
                {
                    return e;
                }
            }

            throw new KeyNotFoundException($"Unknown experiment '{id}'.");
        }

        public void Register(ExperimentDefinition experiment) => _all.Add(experiment);

        public static ExperimentCatalog CreateDefault()
        {
            var c = new ExperimentCatalog();

            var sandbox = new ExperimentDefinition
            {
                Id = SandboxId, Title = "Free Lab (Sandbox)", Discipline = Discipline.All, Status = ExperimentStatus.Playable,
                Summary = "Every available instrument. Build your own experiment.",
            };
            sandbox.InitialLayout.Add((EquipmentIds.BunsenBurner, 0.08f, 0.05f));
            sandbox.InitialLayout.Add((EquipmentIds.Beaker250Water, -0.14f, 0.06f));
            sandbox.InitialLayout.Add((EquipmentIds.Thermometer, -0.26f, -0.04f));
            c.Register(sandbox);

            var heating = new ExperimentDefinition
            {
                Id = "chem.heating-curve", Title = "Heating Curve of Water", Discipline = Discipline.Chemistry,
                Status = ExperimentStatus.Playable,
                Summary = "Heat water with a Bunsen burner and record temperature vs. time. Observe the boiling plateau.",
            };
            heating.Equipment.AddRange(new[] { EquipmentIds.BunsenBurner, EquipmentIds.Beaker250Water, EquipmentIds.Thermometer, EquipmentIds.WaterBottle });
            heating.InitialLayout.Add((EquipmentIds.BunsenBurner, 0.08f, 0.05f));
            heating.InitialLayout.Add((EquipmentIds.Beaker250Water, -0.14f, 0.06f));
            heating.InitialLayout.Add((EquipmentIds.Thermometer, -0.26f, -0.04f));
            heating.Objectives.Add("Place the beaker on the gauze above the burner.");
            heating.Objectives.Add("Insert the thermometer and open the gas valve.");
            heating.Objectives.Add("Explain why the temperature stops rising at 100 °C.");
            c.Register(heating);

            var acidBase = new ExperimentDefinition
            {
                Id = "chem.acid-base", Title = "Acids, Bases & pH", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Mix HCl, NaOH and acetic acid; watch indicator colour, pH and the heat of neutralisation.",
            };
            acidBase.Equipment.AddRange(new[]
            {
                EquipmentIds.Beaker250, EquipmentIds.HclBottle, EquipmentIds.NaohBottle, EquipmentIds.VinegarBottle,
                EquipmentIds.IndicatorBottle, EquipmentIds.PhProbe, EquipmentIds.Thermometer, EquipmentIds.WaterBottle,
            });
            acidBase.InitialLayout.Add((EquipmentIds.Beaker250, 0.0f, 0.1f));
            acidBase.InitialLayout.Add((EquipmentIds.HclBottle, -0.18f, -0.05f));
            acidBase.InitialLayout.Add((EquipmentIds.NaohBottle, 0.15f, -0.05f));
            acidBase.InitialLayout.Add((EquipmentIds.IndicatorBottle, 0.0f, -0.12f));
            acidBase.InitialLayout.Add((EquipmentIds.PhProbe, 0.24f, 0.08f));
            c.Register(acidBase);

            var titration = new ExperimentDefinition
            {
                Id = "chem.titration", Title = "Acid–Base Titration", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Find the concentration of HCl by adding 0.1 M NaOH from a burette until phenolphthalein turns pink.",
            };
            titration.Equipment.AddRange(new[]
            {
                EquipmentIds.Burette, EquipmentIds.TitrationFlask, EquipmentIds.Erlenmeyer, EquipmentIds.Beaker100, EquipmentIds.HclBottle, EquipmentIds.PhenolphthaleinBottle,
                EquipmentIds.IndicatorBottle, EquipmentIds.PhProbe, EquipmentIds.GraduatedCylinder, EquipmentIds.WaterBottle,
            });
            titration.InitialLayout.Add((EquipmentIds.Burette, 0.0f, 0.06f));
            titration.InitialLayout.Add((EquipmentIds.TitrationFlask, 0.0f, 0.06f)); // stands under the burette tip
            titration.InitialLayout.Add((EquipmentIds.PhenolphthaleinBottle, 0.16f, -0.04f));
            titration.InitialLayout.Add((EquipmentIds.HclBottle, -0.3f, 0.05f));
            titration.InitialLayout.Add((EquipmentIds.PhProbe, 0.24f, 0.06f));
            c.Register(titration);

            var gases = new ExperimentDefinition
            {
                Id = "chem.gases", Title = "Gas-Producing Reactions", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Metals + acid (H₂), carbonates + acid (CO₂), hydrogen peroxide + KI (O₂). Compare how fast each one bubbles.",
            };
            gases.Equipment.AddRange(new[]
            {
                EquipmentIds.Beaker250, EquipmentIds.TestTube, EquipmentIds.TubeRack, EquipmentIds.Erlenmeyer, EquipmentIds.HclBottle,
                EquipmentIds.VinegarBottle, EquipmentIds.H2o2Bottle, EquipmentIds.KiSolution, EquipmentIds.MgJar, EquipmentIds.ZnJar,
                EquipmentIds.FeJar, EquipmentIds.CuJar, EquipmentIds.Nahco3Jar, EquipmentIds.Caco3Jar, EquipmentIds.Thermometer,
            });
            gases.InitialLayout.Add((EquipmentIds.TubeRack, 0.0f, 0.12f));
            gases.InitialLayout.Add((EquipmentIds.HclBottle, -0.22f, 0.02f));
            gases.InitialLayout.Add((EquipmentIds.MgJar, 0.12f, -0.06f));
            gases.InitialLayout.Add((EquipmentIds.ZnJar, 0.2f, -0.06f));
            gases.InitialLayout.Add((EquipmentIds.CuJar, 0.28f, -0.06f));
            gases.InitialLayout.Add((EquipmentIds.Beaker250, -0.06f, -0.05f));
            c.Register(gases);

            var precip = new ExperimentDefinition
            {
                Id = "chem.precipitation", Title = "Precipitation & Displacement", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Mix ionic solutions to form coloured precipitates; drop metals into CuSO₄ and AgNO₃ to see the activity series.",
            };
            precip.Equipment.AddRange(new[]
            {
                EquipmentIds.Beaker100, EquipmentIds.TestTube, EquipmentIds.TubeRack, EquipmentIds.Agno3Solution, EquipmentIds.NaclSolution,
                EquipmentIds.Cuso4Solution, EquipmentIds.Fecl3Solution, EquipmentIds.NaohBottle, EquipmentIds.FeJar, EquipmentIds.ZnJar,
                EquipmentIds.CuJar, EquipmentIds.Kmno4Jar, EquipmentIds.WaterBottle,
            });
            precip.InitialLayout.Add((EquipmentIds.TubeRack, 0.0f, 0.12f));
            precip.InitialLayout.Add((EquipmentIds.Cuso4Solution, -0.2f, 0.0f));
            precip.InitialLayout.Add((EquipmentIds.NaohBottle, 0.18f, 0.0f));
            precip.InitialLayout.Add((EquipmentIds.Beaker100, -0.04f, -0.06f));
            c.Register(precip);

            var mass = new ExperimentDefinition
            {
                Id = "chem.mass", Title = "Conservation of Mass", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Weigh marble chips reacting with acid in an open flask: the mass drops as CO₂ escapes.",
            };
            mass.Equipment.AddRange(new[]
            {
                EquipmentIds.Balance, EquipmentIds.Erlenmeyer, EquipmentIds.Beaker250, EquipmentIds.HclBottle, EquipmentIds.Caco3Jar,
                EquipmentIds.NaclSolution, EquipmentIds.Agno3Solution,
            });
            mass.InitialLayout.Add((EquipmentIds.Balance, 0.0f, 0.06f));
            mass.InitialLayout.Add((EquipmentIds.Erlenmeyer, -0.2f, 0.04f));
            mass.InitialLayout.Add((EquipmentIds.HclBottle, -0.3f, -0.04f));
            mass.InitialLayout.Add((EquipmentIds.Caco3Jar, 0.18f, -0.06f));
            c.Register(mass);

            var safety = new ExperimentDefinition
            {
                Id = "chem.safety", Title = "Lab Safety: Improper Mixing", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Supervised demonstration of why bleach + acid, bleach + ammonia, sodium + water and alcohol near flames are dangerous.",
            };
            safety.Equipment.AddRange(new[]
            {
                EquipmentIds.Beaker250Water, EquipmentIds.Beaker250, EquipmentIds.BleachBottle, EquipmentIds.HclBottle, EquipmentIds.Nh3Bottle,
                EquipmentIds.SodiumJar, EquipmentIds.EthanolBottle, EquipmentIds.BunsenBurner, EquipmentIds.HotPlate,
            });
            c.Register(safety);

            // Roadmap modules — defined so menus, progress tracking and content pipelines can be built now.
            c.Register(Planned("phys.dc-circuits", "DC Circuits & Short Circuits", Discipline.Physics, "Batteries, bulbs, resistors, fuses; V–I graphs; sparks and blown fuses on shorts."));
            c.Register(Planned("phys.ac-circuits", "AC Circuits", Discipline.Physics, "Signal generator, oscilloscope trace, RC/RL phase shift."));
            c.Register(Planned("phys.optics", "Optics Bench", Discipline.Physics, "Laser, lenses, prisms, refraction and focal length."));
            c.Register(Planned("phys.oscillations", "Springs & Pendulums", Discipline.Physics, "Hooke's law, period vs. length, energy graphs."));
            c.Register(Planned("bio.microscope", "Compound Microscope", Discipline.Biology, "Focus knobs, x4 / x10 / x40 objectives, prepared slides."));
            c.Register(Planned("bio.photosynthesis", "Photosynthesis Gas Production", Discipline.Biology, "Elodea under a lamp: O₂ bubble rate vs. light intensity."));
            return c;
        }

        private static ExperimentDefinition Planned(string id, string title, Discipline discipline, string summary) =>
            new ExperimentDefinition { Id = id, Title = title, Discipline = discipline, Status = ExperimentStatus.Planned, Summary = summary };
    }
}
