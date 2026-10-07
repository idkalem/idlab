# ID Lab

**A native 3D interactive science laboratory for desktop and mobile.** ID Lab brings high-school Physics,
Chemistry and Biology experiments into one app, as a 3D next-generation take on PhET-style simulations.

* **Engine:** Unity 6 (C#). Targets Windows, macOS, Linux, iOS and Android.
* **Simulation core:** a pure C# library with no Unity dependency. It is unit tested with `dotnet test` and
  shared with Unity as a local UPM package.

See **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** for the full architecture, data model, frame loop and
module roadmap.

## What the prototype does

* **3D lab bench** in a dark, modern lab room. All equipment is procedural 3D: glassware with a liquid
  surface that stays level when tilted, a Bunsen burner with a tripod and gauze, and probes with floating
  readouts.
* **Equipment inventory** (left). Drag items onto the bench, or tap to place them. Drop an item back on the
  inventory to remove it.
* **Drag, drop and snap.** Items hover over whatever is beneath them. A beaker snaps onto the burner gauze, and
  thermometers or pH probes snap into beakers. Snapped items move with their host.
* **Bunsen burner.** Turn the red 3D gas valve, or use the inspector slider. The flame, light and gauze glow
  scale with the valve, and heat goes into whatever sits on the gauze.
* **Thermal model.** Heating, convective cooling and boiling, with a temperature plateau at 100 °C while
  water evaporates.
* **Pouring.** Tilt a bottle or beaker with `Q`/`E` or the Pour slider. Liquid reaches the lip at a tilt that
  depends on fill level, and flows into the container below or spills onto the bench.
* **Chemistry.** Mixing, neutralisation heat, pH (strong and weak acids, buffers) and universal indicator
  colours. Hazards include sodium + water (fizz or explosion) and bleach + acid (chlorine gas), shown with
  particle effects, flashes, camera shake and warnings.
* **Live data panel** (right). Every probe on the bench gets a live chart of value vs. time. You can switch
  between a 2-minute and a full window, and clear the data.
* **Micro view inset.** An atomic-scale view of the selected container. Molecules speed up with temperature
  and escape when boiling. Ions dissociate (H₃O⁺/Cl⁻, Na⁺/OH⁻), and weak acids show their real degree of
  dissociation.
* **Simulation speed** of Pause, 1×, 5× or 20×, plus an experiment selector: Free Lab, Heating Curve,
  Acids/Bases & pH, and Lab Safety.

## Getting started

### Run in Unity

1. Install **Unity 6** (6000.0 LTS) with Unity Hub.
2. In Unity Hub, choose **Add → Add project from disk** and select the `app/` folder. The core package is
   linked from `../core/com.idlab.core` through `Packages/manifest.json`.
3. Open any scene, even the default empty one, and press **Play**. `LabBootstrap` builds the whole lab in
   code. There are no binary scene or prefab assets.

Before making a player build, run **ID Lab → Generate Shader References** once (it also runs automatically
before every build). This keeps the shader variants for runtime-created materials in the build.

### Controls

| | Desktop | Touch |
| --- | --- | --- |
| Add equipment | drag from inventory / click | drag from inventory / tap |
| Move / attach | left-drag; release on a green snap ring | one-finger drag |
| Pour | `Q` / `E` (or Inspector → Pour) | Inspector → Pour slider |
| Gas valve | drag the red knob | drag knob / Inspector slider |
| Camera | right-drag or drag empty space · wheel | drag empty space · two-finger drag · pinch |
| Delete | `Delete` / drop on inventory | drop on inventory / Inspector → Remove |

### Test the simulation core (no Unity needed)

```bash
dotnet test core/tests/IDLab.Core.Tests          # 35 unit tests
dotnet build tools/unity-typecheck                # type-checks all Unity scripts against Unity reference assemblies
```

### Headless simulation runner

```bash
dotnet run --project tools/sim-cli -- heating     # heating curve CSV (time, thermometer, liquid temp, volume)
dotnet run --project tools/sim-cli -- neutralize  # pH and temperature while adding NaOH to HCl
```

## Repository layout

```
core/   com.idlab.core (simulation, data model, chemistry, sensors, micro-view model) + tests
app/    Unity 6 project: views, interaction, HUD, micro view, VFX
tools/  unity-typecheck (CI), sim-cli (headless scenarios)
docs/   ARCHITECTURE.md
```
