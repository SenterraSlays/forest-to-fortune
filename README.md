# A Game About Chopping Trees

A first-person incremental game made in Unity 6 (6000.6.6f1, URP). You start with a rusty
hatchet in a pine clearing: chop trees, collect the logs, sell them at the Lumber Market and
spend the money at the Workbench. Better axes cut tougher trees, Auto-Choppers and Sawmills
work for you (even while the game is closed), and two gated forests open up as you grow.

## Playing

**In the editor:** open this folder in Unity 6000.6.6f1, open
`Assets/_ChoppingTrees/Scenes/Main.unity` and press Play.
**Built game:** `Builds/Windows/ChoppingTrees.exe`. The build output isn't part of the source;
rebuild it with *Tools > Chopping Trees > Build Windows Player*.

| Action | Input |
| --- | --- |
| Move / sprint / jump | WASD / Left Shift / Space |
| Look | Mouse |
| Chop (hold to keep swinging) | Left mouse button |
| Use (sell, workbench, gates) | E |
| Hide tutorial hints | H |
| Cycle the FPS counter (off / FPS / detailed) | F3 |
| Pause menu / close menus | Esc |

No input setup is needed: the game reads the keyboard and mouse directly through the Input
System package. Gamepads aren't supported.

### The loop

1. **Chop.** Aim at a trunk and swing. Each hit deepens a cut in the bark. Badly damaged trees
   lean, then topple with a scripted fall (no rigidbodies), break into logs and regrow later.
   Trees your axe can't handle clang off and tell you which axe you need.
2. **Collect.** Logs fly into your **backpack** when you get close. The backpack has a limit
   (50 wood in the starting Burlap Sack, up to 15,000 in the Hauler's Harness). When it's full, logs
   stay on the ground, a log that only partly fits is split, and the HUD tells you to go and sell.
   Uncollected logs collect themselves after 25 s if there is room. The **Log Magnet** perk
   (10 levels, from $1,000) raises the pickup range from 4 m to 19 m.
3. **Sell** at the Lumber Market (E): it buys your backpack and the **lumber yard** stock.
   Machines never fill your backpack. Their output goes to the yard (shown separately in the HUD),
   and the pile next to the market grows with it.
4. **Grow the skill tree** at the Workbench (E). Upgrades are nodes on a tree that grows
   from a trunk into four colour-coded branches (Forests, Axe, Automation, Economy), with curved
   links following the prerequisites. Hover a node to see its effect, requirements and price, then
   click **Unlock**/**Upgrade** (or double-click the node). Milestones (new axes, machines, forests,
   harvesters) are larger, double-ringed nodes. States are shown by shape as well as
   colour: a lock badge (locked, with dashed links), an up-arrow badge and a breathing ring (can
   buy), a progress arc around the rim (owned levels) and a gold check (maxed). Owned paths glow
   and carry a slow flow of light, and buying sends a pulse along the link into the node.
   The tree is movable: drag it with any mouse button, scroll to zoom towards the cursor, and use
   the buttons in the legend to zoom or go back to the whole-tree view.
   - **Axe:** Better Axe (Rusty Hatchet → Steel → Lumberjack's → Tempered Broadaxe → Ironbreaker) → Whetstone (damage) → Grip Training (speed) → Lucky Strike (chance of double damage); Better Axe → Backpack (Burlap Sack → Canvas Satchel → Leather Rucksack → Frame Pack → Logger's Pack → Expedition Pack → Hauler's Harness) → Clean Cuts (more wood per tree) → Trail Boots (move speed).
   - **Automation:** Auto-Chopper (up to 8) → Chopper Motors → Harvester Line (rebuilds every chopper as a Heavy, then Titan Harvester); Auto-Chopper → Foreman's Whistle → Spare Parts (cheaper machines, up to -30%); Auto-Chopper → Sawmill (up to 3, 2 wood → 1 plank) → Sawmill Blades → Precision Saws (more planks from the same wood) / Furniture Buyers.
   - **Economy:** Market Contacts (wood price) → Log Magnet (pickup range), Night Shift (longer offline time) and Haggling (every sale pays more) → Lucky Finds (felled trees sometimes drop cash).
   - **Forests:** Old Grove ($25,000; needs the Lumberjack's Axe) → Fertile Soil (faster regrowth); Old Grove → Ironwood Ridge ($350,000; also needs the Tempered Broadaxe) → Gold Prospector (more Goldbark cash).
5. **Automate.** Bought machines appear in the yard south of the start and animate (spinning
   blades, swinging saw arm, feeding logs, rollers). Each one shows what it produces when you
   are nearby. A wooden stake marks where the next machine will go.
6. **Rebirth.** From the skill tree header (or the pause menu), pay to start over with a
   permanent bonus: every rebirth adds +10% to all money you earn (sales, Goldbark cash and
   lucky finds). The first costs $5,000 and each one after that costs 1.08 times the previous one.
   Buy x1, x5, x10 or as many as you can afford at once (the price is the sum of the single prices).
   A rebirth resets money, every upgrade (axes, backpack, machines, forests) and all wood; it keeps
   your rebirths, lifetime stats and settings. The HUD shows the current multiplier next to your money.

| Tree | Area | Axe needed | HP | Wood |
| --- | --- | --- | --- | --- |
| Pine | Clearing | Any | 3 | 4 |
| Birch | Clearing | Any | 5 | 7 |
| Oak | Clearing / Grove | Steel Axe | 16 | 18 |
| Maple | Old Grove | Lumberjack's Axe | 70 | 55 |
| Redwood | Grove / Ridge | Lumberjack's Axe | 170 | 150 |
| Ironwood | Ironwood Ridge | Tempered Broadaxe | 650 | 560 |
| Goldbark | Ironwood Ridge | Ironbreaker | 2,600 | 1,500 + $12,000 |

### Graphics and performance

*Settings > Graphics* offers four upscalers through URP's upscaler framework, each with a
quality level (Native/DLAA, Quality, Balanced, Performance):

| Upscaler | Needs |
| --- | --- |
| NVIDIA DLSS 4 | NVIDIA RTX GPU with a current driver |
| AMD FSR 2 | DirectX 11/12 or Vulkan with compute shaders |
| Unity STP (Spatial-Temporal Post-Processing) | Compute shader support |
| AMD FSR 1 | Any GPU |

Each upscaler is only offered when the hardware check passes; unavailable ones are skipped and
listed with the reason. If an upscaler throws an error while rendering, the game switches back
to native rendering by itself and says so. Temporal upscalers (DLSS, FSR 2, STP) do their own
anti-aliasing; otherwise choose SMAA (default), FXAA, TAA or off, plus a render scale (50–100%).
Also: monitor, display mode, resolution, V-Sync and a frame rate cap (30–240 or unlimited).

The game starts at the monitor's native resolution (rather than the size Unity last saved in
the registry). The resolution list is built from the monitor's native size plus common 16:9
sizes, because the modes Windows reports for exclusive fullscreen can be capped by devices in
the signal chain. For example, an Elgato HD60 S limits them to 1080p. If exclusive fullscreen
can't switch to the chosen size, the game uses borderless and says so in Settings. Resolutions
saved by version 1.1 (which could only offer the capped list) reset to native once.

The FPS counter (*Settings > Interface* or F3) shows FPS; *Detailed* adds frame time, the
worst frame, CPU/GPU times (when the platform reports them), the active upscaler and the
resolution.

### Saving

Progress is saved to `%USERPROFILE%\AppData\LocalLow\Hazelwood\A Game About Chopping Trees\progress.json`:
after every purchase, every 30 s, on pause/quit, and from *Pause > Save Game*. Settings are
saved separately in `settings.json`, so *Reset Progress* keeps them. Saves are versioned JSON,
written atomically with a `.bak` copy. A damaged file falls back to the backup, and invalid
numbers are cleaned up on load. Machines, upgrades and unlocked gates are rebuilt from the save
on load, so reloading never duplicates anything.

**Offline production:** when you return, machines are credited for the time away, capped at
2 h (+2 h per Night Shift level), and a "Welcome back" summary shows what they made. A clock
that moved backwards gives nothing.

## How it's built

Everything in the game is generated by repeatable editor tools under
**Tools > Chopping Trees** (or *Build Everything* to run them in order):

1. **Setup Project:** layers, player settings, URP quality, TextMesh Pro and UI fonts.
2. **Build Materials:** one shared URP material per `M_*` colour slot.
3. **Build Models in Blender:** runs `Blender/ct_models.py` headless (Blender 5.2, falls back to 4.2) and imports 46 models.
4. **Generate Audio:** synthesises the sound effects and loops (WAV) and builds the AudioLibrary. The
   tree hit, tree falling and tree landing sounds are recordings in `Audio/Recorded` (trimmed WAVs);
   *Import Recorded Audio* puts them in the library without re-synthesising anything else.
5. **Build Game Data:** the ScriptableObjects for trees, upgrades, machines, areas and resources (starting balance).
6. **Build Scene:** the terrain and its painted texture, ~330 trees, ~1,700 props, the yard, gates, lighting, post-processing, player and UI.

Re-running a step overwrites what it generated. Balance can be tuned on the data assets in
`Assets/_ChoppingTrees/Data`, but *Build Game Data* resets them to the values in
`ContentBuilder.cs`, so make lasting balance changes there.

### Blender assets

`Blender/ct_models.py` builds every model from code: 7 tree species, 5 stumps, the cut mark,
5 axes, 3 Auto-Chopper tiers and the Sawmill (with separate, correctly pivoted moving parts),
the market, workbench, gate, fence, cabin, lantern, signpost, rocks, bushes, grass, flowers,
mushrooms, logs, crates, hills and mountains. It writes:

- `Assets/_ChoppingTrees/Art/Models/*.fbx`: what Unity uses
- `Blender/Source/*.blend`: an editable source file per model

Run it yourself with:

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Blender/ct_models.py -- Assets/_ChoppingTrees/Art/Models
```

Conventions: metres, Z up, front = +Y (Unity +Z). `M_*` material slots are remapped to the
project materials on import. `COL_*` boxes become BoxColliders. Empties (`WorkPoint`,
`SignAnchor`, …) become transforms.

### Code layout (`Assets/_ChoppingTrees/Scripts`, namespace `ChoppingTrees`)

| Folder | What's in it |
| --- | --- |
| `Data/` | ScriptableObject definitions: `TreeType`, `UpgradeDef`, `MachineDef`, `ForestAreaDef`, `ResourceDef`, `GameConfig`, `GameDatabase`, `AudioLibrary` |
| `Core/` | Scene-independent rules: `GameModel` (owns everything), `Upgrades` (purchases and aggregated effects), `Market`, `Production` (continuous, frame-rate independent, also used for offline time), `GameState`/`SaveData`/`SaveSystem`, `NumberUtil` (overflow-safe maths, 1.50M-style formatting), plus `Game` (scene entry point, autosave, phases) and `SmokeTest` |
| `Player/` | `PlayerMotor`, `PlayerLook`, `CameraFx` (bob, kicks, FOV), `AxeController` (swing animation and a single spherecast per swing), `Interactor` |
| `World/` | `ChoppableTree`, `WoodPickup` (pooled), `MachineYard` + `MachineAnimator` (visuals only), `SellPoint`, `UpgradeStation`, `AreaGate`, `StockPileDisplay`, `AmbientAudio`, `MenuCamera` |
| `FX/` | `AudioManager` (pooled sources), `Effects` (four particle systems emitted into; no per-burst objects) |
| `Graphics/` | `GraphicsService` (upscaler and render scale, with automatic fallback), `GraphicsCapabilities` (hardware checks), `CameraGraphics` (per-camera anti-aliasing), `PerfStats` (measured FPS and frame times) |
| `UI/` | Built in code with one style kit (`UIFactory`, `UIButton`, and `UIIcons` for icons and sprites drawn with distance fields, so there are no texture assets): `UIRoot`, `Hud`, `UpgradeMenu` (the skill tree) + `SkillNode`, `MainMenu`, `PauseMenu`, `SettingsMenu`, `ConfirmDialog`, `OfflinePopup`, `Tutorial`, `Toasts`, `FloatingText` |

Adding content is data-only. For example, a new resource is a `ResourceDef`, and a new machine
is a `MachineDef` plus `MachineCount`/`MachineSpeed` upgrades and slots in the yard. The economy,
HUD, upgrade menu and saving pick it up automatically.

## Testing

**Automated (EditMode, 22 tests, all passing):** formatting and overflow safety, costs,
purchase rules and prerequisites, selling, exact machine output, frame-rate-independent
production, sawmill supply limits, the offline cap and backwards clocks, area unlocks,
auto-sell, data validation, skill-tree layout and connectivity, save round-trips, backup fallback, garbage and invalid files, and
migration. A pacing simulation plays the economy with a cautious simulated player at half
chopping efficiency:

| Milestone | Simulated time |
| --- | --- |
| Steel Axe | 1.4 min |
| First Auto-Chopper | 4.2 min |
| Lumberjack's Axe | 10.7 min |
| First Sawmill | 44 min |
| Old Grove | ~2 h 10 min |
| Ironwood Ridge | ~3 h 50 min |

A player who saves up for the forests will get there sooner.

Run the tests from *Window > General > Test Runner > EditMode*, or with the Unity CLI:

```bash
bash Tools/test.sh
```

**Built-player smoke test:** this plays the real game with the real axe, pickups, market and
upgrade code, then prints a `[Smoke]` summary and PASS/FAIL to the log:

```bash
Builds/Windows/ChoppingTrees.exe -ct-smoke -ct-quit-after 90 -ct-nosave -ct-upscaler DLSS -ct-screenshot shot.png -logFile smoke.log
```

`-ct-upscaler` forces DLSS, FSR2, STP, FSR1 or Off. `-ct-screenshot` also saves
`shot_tree.png` and `shot_settings.png`. `-ct-nosave` neither loads nor writes progress or
settings. Latest runs on an RTX 5060 at 1080p, all PASS with 0 errors: DLSS Quality ~500 FPS,
FSR 2 Quality ~590 FPS, STP Quality ~610 FPS.

### Verified vs. not verified

Verified in play mode (editor screenshots) or the built player: the redesigned menus, the skill
tree, settings, the FPS counter, all four upscalers activating without errors, the HUD, the tutorial
and its marker, chopping and cut marks, falling trees, log pickup, selling, the upgrade menu,
buying axes and machines, chopper and sawmill animation and output, gate unlocking, save and
reload, the offline summary, pause and settings.

Not verified by a person playing with a real mouse and keyboard. All input during testing
was driven by automation, so mouse-look feel, sensitivity defaults and swing timing deserve a
hands-on pass. Not tested: resolution and display-mode switching in the built player, and very
long sessions (8+ machines with every upgrade maxed).

## Assumptions and design decisions

- No existing project was provided, so this is a new project in `Games/ChoppingTrees`, set up the same way as the other projects in that folder.
- Machines put wood straight into your stock. Hauling wood around would turn this into the factory game the brief rules out. Selling stays a trip to the market.
- Sawmills automatically turn stock wood into planks, which are worth 75% more than the wood they use.
- Offline production is 100% efficient but capped (2 h base) so idle time stays rewarding without making active play pointless.
- Sounds are synthesised in code (no third-party audio). Fonts are Inter (SIL OFL, licence in `Art/Fonts/OFL.txt`).
- Only packages that ship with the template (URP, Input System, uGUI/TextMesh Pro, Test Framework), Unity's built-in NVIDIA and AMD modules (for DLSS and FSR), and Unity's automation package are used. No paid assets.
- The upscaler framework needs the `ENABLE_UPSCALER_FRAMEWORK` scripting define (set by *Setup Project*). The NVIDIA and AMD code paths are switched on by version defines in `ChoppingTrees.asmdef`.
