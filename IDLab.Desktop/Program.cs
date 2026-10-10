using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using IDLab.Core;
using IDLab.Core.Chemistry;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>
    /// ID Lab — smart-board / desktop edition. Starts full-screen; everything works with touch alone.
    /// Command line (for testing): --windowed [WxH]  --demo heating|acid|safety|titration|precip|metals|pour|periodic|info
    /// --frames N  --screenshot file.png  --trace  --camera yaw,pitch,dist
    /// </summary>
    internal static class Program
    {
        /// <summary>The running lab (for helpers that need to map core objects back to items).</summary>
        public static Lab LabInstance { get; private set; }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var tr = new CultureInfo("tr-TR");
                CultureInfo.DefaultThreadCurrentCulture = tr;
                CultureInfo.CurrentCulture = tr;
            }
            catch (CultureNotFoundException)
            {
                // Fall back to the system culture.
            }

            try
            {
                Run(Options.Parse(args));
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IDLab");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "crash.log"), $"{DateTime.Now:O}\n{ex}");
                }
                catch (IOException)
                {
                }

                return 1;
            }
        }

        private static void Run(Options o)
        {
            Settings.Load();
            if (o.Quality != null) Settings.Override("grafik", o.Quality);
            Theme.Apply(o.Light ? false : Settings.Get("tema", "koyu") != "acik");
            SetConfigFlags((Quality.WantsMsaa ? ConfigFlags.Msaa4xHint : 0) | ConfigFlags.ResizableWindow | (o.Demo == null ? ConfigFlags.VSyncHint : 0));
            InitWindow(o.Width, o.Height, "ID Lab");
            SetExitKey(KeyboardKey.Null); // Esc must not close the app in class
            SetWindowMinSize(960, 600);
            using (var iconStream = typeof(Program).Assembly.GetManifestResourceStream("icon.png"))
            using (var ms = new MemoryStream())
            {
                iconStream.CopyTo(ms);
                var icon = LoadImageFromMemory(".png", ms.ToArray());
                ImageResize(ref icon, 64, 64);
                SetWindowIcon(icon);
                UnloadImage(icon);
            }

            SetTargetFPS(60);
            if (!o.Windowed)
            {
                ToggleBorderlessWindowed();
            }

            PointerCursor.Ensure();
            var startup = System.Diagnostics.Stopwatch.StartNew();
            TouchInput.Install();
            Quality.Load();
            Gfx.Init();
            Ui.Init();
            // Start-up screen with the logo while the lab is being built.
            bool splash = o.Demo == null || o.Demo == "about";
            string splashShot = Environment.GetEnvironmentVariable("IDLAB_SPLASH_SHOT");
            void Step(float p, string what)
            {
                if (!splash) return;
                Brand.Splash(p, what);
                if (splashShot != null && p > 0.3f && p < 0.4f)
                {
                    Brand.Splash(p, what);
                    TakeScreenshot(splashShot);
                }
            }
            Step(0.05f, "Laboratuvar hazırlanıyor…");
            Textures.Init();
            Step(0.2f, "Laboratuvar odası kuruluyor…");
            Room.Build();
            Step(0.35f, "Kimyasal kataloğu yükleniyor (40 000+ kimyasal)…");
            Thumbnails.Build();
            Step(0.7f, "Malzemeler hazırlanıyor…");
            var lab = new Lab();
            LabInstance = lab;
            var micro = new MicroView();
            micro.Init();
            Step(0.82f, "Periyodik tablo hazırlanıyor…");
            var periodic = new PeriodicView();
            periodic.Init();
            var info = new InfoView(lab);
            info.Select(o.Topic);
            var hud = new Hud(lab, micro);
            var chemicals = new ChemicalsView(lab, hud);
            hud.ExperimentCount = lab.Experiments.All.Count(e => e.Status == IDLab.Core.Model.ExperimentStatus.Playable);
            Step(1f, "Hazır!");
            hud.ChemicalCount = chemicals.Count;
            if (o.Profile) Console.WriteLine($"startup: {startup.ElapsedMilliseconds} ms after the window opened");
            if (o.Demo != null && o.Demo.StartsWith("chem:", StringComparison.Ordinal))
            {
                hud.Tab = HudTab.Chemicals;
                chemicals.Preselect(o.Demo.Substring(5).Replace('_', ' '));
            }
            else if (o.Demo != null)
            {
                Demo.Setup(o.Demo, lab, hud);
            }

            if (o.Camera != null)
            {
                lab.Yaw = o.Camera[0];
                lab.Pitch = o.Camera[1];
                lab.Distance = o.Camera[2];
            }

            int frame = 0;
            float microClock = 0;
            while (!WindowShouldClose() && !hud.QuitRequested)
            {
                float dt = o.Demo != null ? 1f / 60f : Math.Min(GetFrameTime(), 0.1f);
                if (IsKeyPressed(KeyboardKey.F11))
                {
                    ToggleBorderlessWindowed();
                    PointerCursor.Ensure();
                }

                PointerCursor.Update(dt);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                Ui.BeginFrame();
                Ui.Suspended = hud.ModalOpen;
                hud.Layout();
                bool labTab = hud.Tab == HudTab.Lab;
                lab.KeysEnabled = labTab;

                // The experiment keeps running while another tab is open; only pointer input is withheld.
                lab.Update(dt, pointerFree: labTab && !Ui.Captured && !hud.ModalOpen, overInventory: hud.IsOverInventory(Ui.Mouse));
                if (o.Demo != null) Demo.Step(o.Demo, lab, hud, frame);
                Scene.Update(lab, dt);
                if (labTab)
                {
                    micro.Update(lab, dt);
                    // The molecule view is a small inset: 30 fps is plenty (15 in the economy modes).
                    microClock += GetFrameTime();
                    if (microClock >= (Quality.Economy ? 1f / 15f : 1f / 30f) || o.Demo != null)
                    {
                        microClock = 0;
                        micro.Render();
                    }
                }
                else if (hud.Tab == HudTab.Periodic)
                {
                    periodic.Update(dt, hud.ContentArea, pointerFree: !Ui.Captured && !hud.ModalOpen);
                }
                else if (hud.Tab == HudTab.Chemicals)
                {
                    chemicals.Update(dt, hud.ContentArea);
                }
                else
                {
                    info.Update(dt, hud.ContentArea);
                }

                double tUpdate = sw.Elapsed.TotalMilliseconds;
                if (o.Demo == null || o.Quality != null) Quality.Update(GetFrameTime());
                if (labTab) Props.UpdateDisplays();
                bool offscreen = labTab && Quality.BeginScene(Theme.Scene);
                if (offscreen)
                {
                    BeginMode3D(lab.Camera);
                    Scene.Draw(lab, dt);
                    EndMode3D();
                    Quality.EndScene();
                }

                BeginDrawing();
                ClearBackground(Theme.Scene);
                switch (hud.Tab)
                {
                    case HudTab.Lab:
                        if (offscreen)
                        {
                            Quality.Present();
                        }
                        else
                        {
                            BeginMode3D(lab.Camera);
                            Scene.Draw(lab, dt);
                            EndMode3D();
                        }

                        if (lab.Effects.ScreenFlash > 0.01f)
                        {
                            DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Gfx.WithAlpha(new Color(255, 236, 205, 255), lab.Effects.ScreenFlash));
                        }

                        Scene.DrawLabels(lab);
                        break;
                    case HudTab.Periodic:
                        periodic.Draw();
                        break;
                    case HudTab.Chemicals:
                        chemicals.Draw();
                        break;
                    default:
                        info.Draw();
                        break;
                }

                double tScene = sw.Elapsed.TotalMilliseconds;
                hud.Draw(dt);
                PointerCursor.Draw();
                double tHud = sw.Elapsed.TotalMilliseconds;
                EndDrawing();
                Ui.EndFrame();
                if (o.Profile)
                {
                    Profiler.Add(tUpdate, tScene - tUpdate, tHud - tScene, sw.Elapsed.TotalMilliseconds - tHud);
                }

                frame++;
                if (o.Trace && frame % 30 == 0) Demo.Trace(lab, frame);
                if (o.Screenshot != null && frame == o.Frames)
                {
                    TakeScreenshot(o.Screenshot);
                }

                if (o.Frames > 0 && frame > o.Frames)
                {
                    break;
                }
            }

            TouchInput.Uninstall();
            CloseWindow();
        }

        private sealed class Options
        {
            public bool Windowed;
            public int Width = 1600, Height = 900;
            public string Demo;
            public int Frames;
            public string Screenshot;
            public bool Trace, Light, Profile;
            public string Quality;
            public float[] Camera;
            public int Topic;

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--windowed":
                            o.Windowed = true;
                            if (i + 1 < args.Length && args[i + 1].Contains('x'))
                            {
                                var parts = args[++i].Split('x');
                                o.Width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                                o.Height = int.Parse(parts[1], CultureInfo.InvariantCulture);
                            }

                            break;
                        case "--demo": o.Demo = args[++i]; break;
                        case "--frames": o.Frames = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--screenshot": o.Screenshot = args[++i]; break;
                        case "--trace": o.Trace = true; break;
                        case "--light": o.Light = true; break;
                        case "--profile": o.Profile = true; break;
                        case "--quality": o.Quality = args[++i]; break;
                        case "--topic": o.Topic = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--camera": o.Camera = Array.ConvertAll(args[++i].Split(','), v => float.Parse(v, CultureInfo.InvariantCulture)); break;
                    }
                }

                return o;
            }
        }
    }

    /// <summary>Scripted scenarios used to test the build headlessly and to produce screenshots.</summary>
    internal static class Demo
    {
        public static void Setup(string name, Lab lab, Hud hud)
        {
            switch (name)
            {
                case "heating":
                {
                    lab.LoadExperiment("chem.heating-curve");
                    var burner = lab.Items.First(i => i.Heat != null);
                    var beaker = lab.Items.First(i => i.Container != null);
                    var thermo = lab.Items.First(i => i.Probe != null);
                    SnapOnto(lab, beaker, burner.Position + new Vector3(0, 0.25f, 0));
                    SnapOnto(lab, thermo, beaker.Position + new Vector3(0.018f, 0.15f, 0.008f));
                    burner.Heat.GasValve = 0.85;
                    hud.SetSpeed(3);
                    lab.Selected = beaker;
                    break;
                }

                case "acid":
                {
                    lab.LoadExperiment("chem.acid-base");
                    var beaker = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker250);
                    var c = beaker.Container;
                    var m = new Mixture(lab.World.Substances);
                    ReagentPreset.Solution("", 60, SubstanceIds.HydrochloricAcid, 1.0).FillInto(m);
                    ReagentPreset.Solution("", 5, SubstanceIds.UniversalIndicator, 0.002).FillInto(m);
                    c.ReceiveLiquid(m);
                    var probe = lab.Items.First(i => i.Probe != null);
                    SnapOnto(lab, probe, beaker.Position + new Vector3(0.018f, 0.15f, 0.008f));
                    var naoh = lab.Items.First(i => i.Def.Id == EquipmentIds.NaohBottle);
                    naoh.Tilt = 100; // assisted pour: tipping it lifts it over the nearest container
                    lab.Selected = naoh;
                    hud.SetSpeed(1);
                    break;
                }

                case "titration":
                {
                    lab.LoadExperiment("chem.titration");
                    var burette = lab.Items.First(i => i.Tap != null);
                    var flask = lab.Items.First(i => i.Def.Id == EquipmentIds.TitrationFlask);
                    var probe = lab.Items.First(i => i.Probe != null);
                    SnapOnto(lab, probe, flask.Position + new Vector3(0.012f, 0.16f, -0.01f));
                    burette.Tap.Dispense(24);
                    hud.SetSpeed(3);
                    lab.Selected = burette;
                    break;
                }

                case "precip":
                {
                    lab.LoadExperiment("chem.precipitation");
                    var beaker = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker100);
                    var m = new Mixture(lab.World.Substances);
                    ReagentPreset.Solution("", 40, SubstanceIds.CopperSulfate, 0.5).FillInto(m);
                    beaker.Container.ReceiveLiquid(m);
                    var naoh = lab.Items.First(i => i.Def.Id == EquipmentIds.NaohBottle);
                    naoh.Position = beaker.Position + new Vector3(0.14f, 0, 0);
                    naoh.Tilt = 105;
                    lab.Selected = naoh;
                    hud.SetSpeed(1);
                    break;
                }

                case "metals":
                {
                    lab.LoadExperiment("chem.gases");
                    var beaker = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker250);
                    var m = new Mixture(lab.World.Substances);
                    ReagentPreset.Solution("", 90, SubstanceIds.HydrochloricAcid, 1.0).FillInto(m);
                    beaker.Container.ReceiveLiquid(m);
                    var jar = lab.Items.First(i => i.Def.Id == EquipmentIds.ZnJar);
                    beaker.Position = jar.Position + new Vector3(-0.13f, 0, 0.02f);
                    jar.Tilt = 115;
                    lab.Selected = jar;
                    hud.SetSpeed(1);
                    break;
                }

                case "periodic":
                    hud.Tab = HudTab.Periodic;
                    break;

                case "info":
                    hud.Tab = HudTab.Info;
                    break;

                case "cabinet":
                {
                    // Compounds added from the chemical cabinet, poured with the quick-pour buttons.
                    lab.LoadExperiment("chem.acid-base");
                    foreach (var it in lab.Items.ToList()) lab.Remove(it);
                    var cat = lab.World.Catalog;
                    cat.Register(EquipmentCatalog.CreateJar("custom.jar.nacl.20", "NaCl 20 g", SubstanceIds.SodiumChloride, 20, 0xEEEEEE));
                    cat.Register(EquipmentCatalog.CreateBottle("custom.sol.cuso4.1", "CuSO4 1 M", ReagentPreset.Solution("1 M CuSO4", 250, SubstanceIds.CopperSulfate, 1.0), 0x3B82F6));
                    var water = lab.Spawn(EquipmentIds.Beaker250Water, new Vector3(-0.05f, 0, 0.0f));
                    var jar = lab.Spawn("custom.jar.nacl.20", new Vector3(0.12f, 0, -0.02f));
                    var naoh = lab.Spawn(EquipmentIds.Beaker100, new Vector3(-0.25f, 0, 0.0f));
                    var m = new Mixture(lab.World.Substances);
                    ReagentPreset.Solution("", 40, SubstanceIds.SodiumHydroxide, 1.0).FillInto(m);
                    naoh.Container.ReceiveLiquid(m);
                    lab.Spawn("custom.sol.cuso4.1", new Vector3(-0.4f, 0, 0.05f));
                    Thumbnails.Ensure(cat.Get("custom.jar.nacl.20"));
                    Thumbnails.Ensure(cat.Get("custom.sol.cuso4.1"));
                    jar.PourInto = water;
                    lab.StartAutoPour(jar, (jar.Container.SolidVolumeMilliliters + jar.Container.LiquidVolumeMilliliters) / 2);
                    lab.Selected = jar;
                    hud.SetSpeed(1);
                    break;
                }

                case "hotplate":
                {
                    lab.LoadExperiment("chem.heating-curve");
                    foreach (var it in lab.Items.ToList()) lab.Remove(it);
                    var plate = lab.Spawn(EquipmentIds.HotPlate, new Vector3(0.12f, 0, 0));
                    var burner = lab.Spawn(EquipmentIds.BunsenBurner, new Vector3(-0.14f, 0, 0.02f));
                    plate.Heat.GasValve = 0.8;
                    burner.Heat.GasValve = 0.75;
                    lab.Distance = 0.75f;
                    hud.SetSpeed(1);
                    break;
                }

                case "burn":
                {
                    // Magnesium burning on the gauze; sulfur in a second beaker over another burner.
                    lab.LoadExperiment("chem.heating-curve");
                    foreach (var it in lab.Items.ToList()) lab.Remove(it);
                    var db = lab.World.Substances;
                    var b1 = lab.Spawn(EquipmentIds.BunsenBurner, new Vector3(0.1f, 0, 0));
                    var b2 = lab.Spawn(EquipmentIds.BunsenBurner, new Vector3(-0.15f, 0, 0));
                    var c1 = lab.Spawn(EquipmentIds.Beaker100, new Vector3(0.1f, 0.3f, 0));
                    var c2 = lab.Spawn(EquipmentIds.Beaker100, new Vector3(-0.15f, 0.3f, 0));
                    SnapOnto(lab, c1, b1.Position + new Vector3(0, 0.25f, 0));
                    SnapOnto(lab, c2, b2.Position + new Vector3(0, 0.25f, 0));
                    c1.Container.AddSubstance(SubstanceIds.Magnesium, 0.03);
                    c2.Container.AddSubstance(db.FindElement("S").Id, 0.05);
                    b1.Heat.GasValve = 1;
                    b2.Heat.GasValve = 1;
                    lab.Selected = c1;
                    hud.SetSpeed(2);
                    break;
                }

                case "toothpaste":
                {
                    lab.LoadExperiment("chem.elephant-toothpaste");
                    var flask = lab.Items.First(i => i.Def.Id == EquipmentIds.Erlenmeyer);
                    var m = new Mixture(lab.World.Substances);
                    ReagentPreset.Solution("", 50, SubstanceIds.HydrogenPeroxide, 3.5).FillInto(m);
                    ReagentPreset.Liquid("", SubstanceIds.Detergent, 6, 1.03, 376.5).FillInto(m);
                    ReagentPreset.Solution("", 1, SubstanceIds.FoodDyeRed, 0.02).FillInto(m);
                    flask.Container.ReceiveLiquid(m);
                    lab.Selected = flask;
                    lab.Distance = 0.9f;
                    hud.SetSpeed(1);
                    break;
                }

                case "gallery":
                case "gallery2":
                {
                    // Close-up line-up of the equipment (for checking how the models look).
                    lab.LoadExperiment(IDLab.Core.Model.ExperimentCatalog.SandboxId);
                    foreach (var it in lab.Items.ToList()) lab.Remove(it);
                    var ids = name == "gallery"
                        ? new[] { EquipmentIds.Beaker250Water, EquipmentIds.Erlenmeyer, EquipmentIds.RoundFlask, EquipmentIds.GraduatedCylinder, EquipmentIds.HclBottle, EquipmentIds.Cuso4Jar, EquipmentIds.PhenolphthaleinBottle }
                        : new[] { EquipmentIds.BunsenBurner, EquipmentIds.HotPlate, EquipmentIds.Balance, EquipmentIds.TubeRack, EquipmentIds.Thermometer, EquipmentIds.PetriDish, EquipmentIds.Burette };
                    float x = 0.33f;
                    foreach (var id in ids)
                    {
                        var it = lab.Spawn(id, new Vector3(x, 0, 0));
                        x -= Math.Max(0.11f, it.Size.X + 0.03f);
                    }

                    lab.Distance = 0.95f;
                    lab.Pitch = 14;
                    hud.SetSpeed(1);
                    break;
                }

                case "chemicals":
                    hud.Tab = HudTab.Chemicals;
                    break;

                case "about":
                    hud.OpenAbout();
                    break;

                case "pour":
                {
                    lab.LoadExperiment("chem.acid-base");
                    var hcl = lab.Items.First(i => i.Def.Id == EquipmentIds.HclBottle);
                    lab.StartAutoPour(hcl, 50);
                    lab.Selected = hcl;
                    hud.SetSpeed(1);
                    break;
                }

                case "safety":
                {
                    lab.LoadExperiment("chem.safety");
                    var beaker = lab.Spawn(EquipmentIds.Beaker250Water, new Vector3(0, 0, 0.05f));
                    lab.Spawn(EquipmentIds.BleachBottle, new Vector3(-0.2f, 0, 0));
                    lab.Spawn(EquipmentIds.SodiumJar, new Vector3(0.18f, 0, 0));
                    lab.Selected = beaker;
                    break;
                }
            }
        }

        public static void Step(string name, Lab lab, Hud hud, int frame)
        {
            if (Environment.GetEnvironmentVariable("IDLAB_POURLOG") != null)
            {
                foreach (var it in lab.Items)
                {
                    if (it.AutoPourMl < 0 && it.Tilt < 0.5f) continue;
                    var c = it.Container;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "pour {0} {1} tilt={2:0.0} vis={3:0.0} y={4:0.0000} x={5:0.0000} rate={6:0.00} arrived={7}",
                        frame, it.Def.Id, it.Tilt, it.VisualTilt, it.Position.Y, it.Position.X, c.PourRateLitersPerSecond * 1000, it.AutoPourArrived));
                }
            }

            if (name == "metals" && frame == 200)
            {
                var jar = lab.Items.First(i => i.Def.Id == EquipmentIds.ZnJar);
                jar.Tilt = 0;
                lab.Selected = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker250);
            }


            if (name == "toothpaste" && frame == 40)
            {
                lab.Items.First(i => i.Def.Id == EquipmentIds.Erlenmeyer).Container.AddSubstance(SubstanceIds.DryYeast, 0.025);
            }

            if (name == "cabinet" && frame == 420)
            {
                var bottle = lab.Items.First(i => i.Def.Id == "custom.sol.cuso4.1");
                bottle.PourInto = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker100);
                lab.StartAutoPour(bottle, 25);
                lab.Selected = bottle;
            }

            if (name == "safety" && frame == 30)
            {
                var beaker = lab.Items.First(i => i.Def.Id == EquipmentIds.Beaker250Water);
                beaker.Container.AddSubstance(SubstanceIds.Sodium, 0.06);
            }
        }

        /// <summary>Prints screen positions of items for automated UI tests (xdotool).</summary>
        public static void Trace(Lab lab, int frame)
        {
            var sb = new System.Text.StringBuilder($"frame {frame} t={lab.World.Time.ToString("0.0", CultureInfo.InvariantCulture)} sel={lab.Selected?.Def.Id ?? "-"} yaw={lab.Yaw.ToString("0", CultureInfo.InvariantCulture)}\n");
            foreach (var item in lab.Items)
            {
                var center = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, item.Size.Y * 0.5f, 0)), lab.Camera);
                var top = GetWorldToScreen(item.Inst.Pose.TransformPoint(new Vector3(0, item.Size.Y, 0)), lab.Camera);
                var host = lab.World.GetHost(item.Inst);
                sb.Append(CultureInfo.InvariantCulture, $"item {item.Inst.Id} {item.Def.Id} center={center.X:0},{center.Y:0} top={top.X:0},{top.Y:0} pos={item.Position.X:0.000},{item.Position.Y:0.000},{item.Position.Z:0.000} host={host?.Id.ToString() ?? "-"}");
                if (item.KnobPosition is Vector3 kp)
                {
                    var k = GetWorldToScreen(kp, lab.Camera);
                    sb.Append(CultureInfo.InvariantCulture, $" knob={k.X:0},{k.Y:0} valve={item.KnobValue:0.00}");
                }

                if (item.Container != null)
                {
                    sb.Append(CultureInfo.InvariantCulture, $" T={item.Container.TemperatureC:0.0} V={item.Container.VolumeMilliliters:0.0} pH={item.Container.PH:0.00}");
                }

                if (item.Probe != null)
                {
                    sb.Append(CultureInfo.InvariantCulture, $" reading={item.Probe.Reading:0.00}");
                }

                sb.Append('\n');
            }

            Console.Out.Write(sb.ToString());
            Console.Out.Flush();
        }

        private static void SnapOnto(Lab lab, Item item, Vector3 hover)
        {
            if (lab.Snaps.TryFindDropSnap(item.Inst, new Pose3(hover, Quaternion.Identity), 0.05f, 0.4f, out var c))
            {
                lab.Snap(item, c);
            }
        }
    }

    /// <summary>--profile: average milliseconds per frame section, printed every 120 frames.</summary>
    internal static class Profiler
    {
        private static double _u, _s, _h, _p;
        private static long _bytes;
        private static int _n;

        public static void Add(double update, double scene, double hud, double present)
        {
            _u += update; _s += scene; _h += hud; _p += present; _n++;
            if (_n < 120) return;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "profile: update {0:0.00} ms, scene {1:0.00} ms, hud {2:0.00} ms, present {3:0.00} ms, total {4:0.00} ms, {5} draws/frame, {6:0.0} MB allocated/s, gen0 GCs {7}",
                _u / _n, _s / _n, _h / _n, _p / _n, (_u + _s + _h + _p) / _n, Gfx.DrawCalls / _n,
                (GC.GetTotalAllocatedBytes() - _bytes) / 1e6 / Math.Max(1e-3, (_u + _s + _h + _p) / 1000.0), GC.CollectionCount(0)));
            _bytes = GC.GetTotalAllocatedBytes();
            Gfx.DrawCalls = 0;
            _u = _s = _h = _p = 0;
            _n = 0;
        }
    }
}
