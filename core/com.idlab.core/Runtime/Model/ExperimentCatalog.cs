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
            sandbox.InitialLayout.Add((EquipmentIds.BunsenBurner, 0.0f, 0.05f));
            sandbox.InitialLayout.Add((EquipmentIds.Beaker250Water, -0.25f, 0.1f));
            sandbox.InitialLayout.Add((EquipmentIds.Thermometer, -0.38f, 0.0f));
            c.Register(sandbox);

            var heating = new ExperimentDefinition
            {
                Id = "chem.heating-curve", Title = "Heating Curve of Water", Discipline = Discipline.Chemistry,
                Status = ExperimentStatus.Playable,
                Summary = "Heat water with a Bunsen burner and record temperature vs. time. Observe the boiling plateau.",
            };
            heating.Equipment.AddRange(new[] { EquipmentIds.BunsenBurner, EquipmentIds.Beaker250Water, EquipmentIds.Thermometer, EquipmentIds.WaterBottle });
            heating.InitialLayout.Add((EquipmentIds.BunsenBurner, 0.0f, 0.05f));
            heating.InitialLayout.Add((EquipmentIds.Beaker250Water, -0.25f, 0.1f));
            heating.InitialLayout.Add((EquipmentIds.Thermometer, -0.38f, 0.0f));
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
            acidBase.InitialLayout.Add((EquipmentIds.HclBottle, -0.2f, -0.05f));
            acidBase.InitialLayout.Add((EquipmentIds.NaohBottle, 0.2f, -0.05f));
            acidBase.InitialLayout.Add((EquipmentIds.IndicatorBottle, 0.0f, -0.12f));
            acidBase.InitialLayout.Add((EquipmentIds.PhProbe, 0.32f, 0.1f));
            c.Register(acidBase);

            var safety = new ExperimentDefinition
            {
                Id = "chem.safety", Title = "Lab Safety: Improper Mixing", Discipline = Discipline.Chemistry, Status = ExperimentStatus.Playable,
                Summary = "Supervised demonstration of why bleach + acid and sodium + water are dangerous.",
            };
            safety.Equipment.AddRange(new[] { EquipmentIds.Beaker250Water, EquipmentIds.BleachBottle, EquipmentIds.HclBottle, EquipmentIds.SodiumJar });
            c.Register(safety);

            // Roadmap modules — defined so menus, progress tracking and content pipelines can be built now.
            c.Register(Planned("chem.titration", "Acid–Base Titration", Discipline.Chemistry, "Burette + stopcock, titration curve plotted as pH vs. volume."));
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
