# 🏎️ Send-it: Australian Burnout Simulator

A love letter to Summernats, Calder Park, and every backyard mechanic who ever dropped a V8 into something ridiculous.

**Send-it** is a Unity-based Australian burnout simulator featuring realistic vehicle physics, authentic Aussie cars, burnout competitions, drag racing, circuit racing, and free roam across iconic Australian locations.

---

## 🔥 Features

### Vehicle Physics
- **RWD / FWD / AWD drivetrain** simulation with configurable torque split
- **Realistic engine simulation** — coolant temperature, oil level, overrev damage, engine seizure
- **Tyre physics** — per-wheel temperature, pressure, compound behaviour, wear, and blowouts
- **Forced induction** — turbo (with spool lag and flutter), supercharger, twin-turbo
- **Mesh deformation** damage system — body panels crumple on collision, mechanical damage affects handling

### Burnout System
- **Line lock** mechanic — hold Left Shift to clamp front wheels and spin the rears
- **Burnout scoring** — points for smoke density, duration, style (donuts/figure-eights), and crowd reaction
- **Tyre wear** — tyres heat up, degrade, and eventually blow during sustained burnouts
- **Skid marks** and volumetric smoke particle effects

### 22 Authentic Australian Cars
From the VK Commodore to the HSV GTSR W1, including:
- Holden Commodore (VK, VL Turbo, VP, VT, VE SSV, HSV GTSR W1)
- Ford Falcon (XY GT-HO Phase III, EA, EF, AU XR8, FG XR6 Turbo, FPV GT)
- Chrysler/Valiant (Charger, Pacer, VH Hemi)
- Toyota Landcruiser 79 Series, Holden Ute, Ford Ranger Raptor, Subaru WRX STI, Mitsubishi Lancer Evo IX, HSV Senator Signature, Holden Monaro CV8

### Garage & Customisation
- **Paint system** — Gloss, Matte, Metallic, Candy, Chrome, Satin finishes with full HSV colour picker
- **Body kits** — front/rear bumpers, side skirts, bonnets, spoilers, exhaust tips, roll cages, seats
- **Wheel fitment** — diameter, width, and rim colour customisation
- **Performance parts** — engines, turbo kits, gearboxes, differentials, suspension, brakes, tyres

### Weather System
8 Australian weather states with real-time transitions:
- ☀️ Clear Hot (38°C Canberra summer)
- 🌥️ Overcast
- ⛈️ Thunderstorm Approaching
- 🌧️ Heavy Rain
- 🌨️ Hailstorm
- 🌫️ Dusty (outback conditions)
- 🌙 Night
- 🌧️ Night Rain

Weather dynamically affects tyre grip, engine cooling, road wetness, and visibility.

### Game Modes
| Mode | Description |
|------|-------------|
| 🔥 Burnout Competition | Summernats-style judged burnout competition with AI competitors and crowd scoring |
| 🏁 Drag Race | Full Christmas tree staging, reaction time, ET, trap speed, bracket racing |
| 🏆 Circuit Race | Multi-lap racing with checkpoints, sector splits, penalties, and standings |
| 🚗 Free Roam | Open world cruising with AI traffic, weather, and photo mode |

### Additional Systems
- **AI Traffic** — Australian vehicles following waypoint paths, reacting to player burnouts with horns and panic stops
- **Replay System** — records and plays back runs with a ghost car, variable playback speed
- **Photo Mode** — free-fly camera, depth of field, film filters (Cinematic, Vintage, B&W, Summernats, Night Neon), rule-of-thirds grid, screenshot export
- **Stats Screen** — career stats, burnout history, drag records, circuit lap times, achievements
- **Save System** — full JSON save/load with auto-save, car ownership, loadouts, and settings persistence
- **Sound Manager** — scene-reactive music crossfading, ambient beds, crowd reactions, SFX pooling

---

## 🛠️ Unity Setup

### Requirements
- **Unity 2022.3 LTS** or newer
- Universal Render Pipeline (URP)
- TextMeshPro

### Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/shutyourole365/Send-it.git
   ```

2. **Open in Unity Hub**
   - Open Unity Hub → Add → Browse to the cloned folder
   - Select Unity 2022.3+ LTS

3. **Install packages** (if prompted)
   - Universal RP
   - TextMeshPro
   - Input System (optional — currently uses legacy Input)

4. **Set up a vehicle prefab**
   - Create a GameObject with a `Rigidbody`
   - Add four `WheelCollider` components and four wheel mesh `Transform` references
   - Attach `VehicleController`, `BurnoutSystem`, `EngineSimulator`, `DamageSystem`, `TyrePhysics` (×4), `CameraController`
   - Assign the references in the Inspector

5. **Run a scene**
   - Start with any scene — ensure `GameManager` and `SaveSystem` singletons are present

---

## 📁 Project Structure

```
Assets/
└── Scripts/
    ├── Vehicle/
    │   ├── VehicleController.cs    — Core physics: engine, drivetrain, steering, brakes
    │   ├── TyrePhysics.cs          — Per-wheel temperature, pressure, wear, blowout
    │   ├── EngineSimulator.cs      — Coolant, oil, boost, fuel, audio
    │   └── DamageSystem.cs         — Mesh deformation, mechanical damage
    ├── Burnout/
    │   ├── BurnoutSystem.cs        — Line lock, smoke, scoring, tyre wear
    │   └── BurnoutCompetition.cs   — Summernats competition mode logic
    ├── GameModes/
    │   ├── DragRace.cs             — Christmas tree, ET timing, AI opponent
    │   ├── CircuitRace.cs          — Lap timing, checkpoints, standings, penalties
    │   └── GameManager.cs          — Singleton: state machine, currency, save/load
    ├── Camera/
    │   └── CameraController.cs     — Chase cam and first-person, burnout shake, FOV
    ├── UI/
    │   ├── HUD.cs                  — Speedo, tacho, boost, temps, tyres, burnout score
    │   ├── GarageUI.cs             — Car select, paint, body kit, wheels, performance
    │   ├── MainMenuUI.cs           — Title screen, event select, settings, credits
    │   ├── MiniMap.cs              — Radar minimap with blips and compass
    │   ├── PhotoMode.cs            — Free-fly camera, filters, DOF, screenshot
    │   └── StatsScreen.cs          — Career stats, history, achievements
    ├── Systems/
    │   ├── SoundManager.cs         — Music crossfade, ambient, crowd, SFX pool
    │   ├── SaveSystem.cs           — JSON save/load, car ownership, loadouts
    │   └── ReplaySystem.cs         — Frame recording and ghost car playback
    ├── World/
    │   ├── WeatherSystem.cs        — 8 Australian weather states with transitions
    │   └── AITraffic.cs            — Traffic spawning, waypoint navigation, burnout reactions
    └── Data/
        ├── AustralianCarData.cs    — 22 Aussie car definitions and factory specs
        └── CarCustomisation.cs     — Visual and performance customisation system
```

---

## 📄 License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

Copyright © 2026 shutyourole365
