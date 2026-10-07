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
    /// Command line (for testing): --windowed [WxH]  --demo heating|acid|safety  --frames N  --screenshot file.png
    /// </summary>
    internal static class Program
    {
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
            SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.ResizableWindow | (o.Demo == null ? ConfigFlags.VSyncHint : 0));
            InitWindow(o.Width, o.Height, "ID Lab — 3D Fen Laboratuvarı");
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

            Gfx.Init();
            Ui.Init();
            var lab = new Lab();
            var micro = new MicroView();
            micro.Init();
            var hud = new Hud(lab, micro);
            if (o.Demo != null)
            {
                Demo.Setup(o.Demo, lab, hud);
            }

            int frame = 0;
            while (!WindowShouldClose() && !hud.QuitRequested)
            {
                float dt = o.Demo != null ? 1f / 60f : Math.Min(GetFrameTime(), 0.1f);
                if (IsKeyPressed(KeyboardKey.F11)) ToggleBorderlessWindowed();

                Ui.BeginFrame();
                hud.Layout();
                lab.Update(dt, pointerFree: !Ui.Captured, overInventory: hud.IsOverInventory(Ui.Mouse));
                if (o.Demo != null) Demo.Step(o.Demo, lab, frame);
                foreach (var item in lab.Items.Where(i => i.Container != null && i.Container.TemperatureC > 70))
                {
                    var c = item.Container;
                    lab.Effects.Steam(c.MouthPosition, (float)c.RadiusM * 0.8f, c.IsBoiling ? 30f : 5f, dt);
                }

                micro.Update(lab, dt);
                micro.Render();

                BeginDrawing();
                ClearBackground(Theme.Scene);
                BeginMode3D(lab.Camera);
                Scene.Draw(lab, dt);
                EndMode3D();
                Scene.DrawLabels(lab);
                hud.Draw(dt);
                EndDrawing();
                Ui.EndFrame();

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

            CloseWindow();
        }

        private sealed class Options
        {
            public bool Windowed;
            public int Width = 1600, Height = 900;
            public string Demo;
            public int Frames;
            public string Screenshot;
            public bool Trace;

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

        public static void Step(string name, Lab lab, int frame)
        {
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
                if (item.Heat != null)
                {
                    var k = GetWorldToScreen(item.KnobPosition, lab.Camera);
                    sb.Append(CultureInfo.InvariantCulture, $" knob={k.X:0},{k.Y:0} valve={item.Heat.GasValve:0.00}");
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
}
