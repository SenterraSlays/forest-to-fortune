# Forest to Fortune

(Formerly "A Game About Chopping Trees"; the Unity project folder and C# namespace keep the old name.)

A first-person incremental game made in Unity 6 (6000.6.6f1, URP). You start with a rusty
hatchet in a pine clearing: chop trees, collect the logs, sell them at the Lumber Market and
spend the money at the Workbench. Better axes cut tougher trees, Auto-Choppers and Sawmills
work for you (even while the game is closed), and eight gated forests open up as you grow.

## Playing

**In the editor:** open this folder in Unity 6000.6.6f1, open
`Assets/_ChoppingTrees/Scenes/Main.unity` and press Play.
**Built game:** `Builds/Windows/ForestToFortune.exe`. The build output isn't part of the source;
rebuild it with *Tools > Forest to Fortune > Build Windows Player*.

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Move / sprint / jump | WASD / Left Shift / Space | Left stick / click left stick / A (Cross) |
| Look | Mouse | Right stick |
| Chop (hold to keep swinging) | Left mouse button | RT (R2) |
| Use (sell, workbench, gates) | E | X (Square) |
| Hide tutorial hints | H | View / Select |
| Cycle the FPS counter (off / FPS / detailed) | F3 | (Settings) |
| Menu (the game keeps running) / close menus | Esc | Start (Options) / B (Circle) |
| Menus | Mouse | D-pad or stick to move, A to select, B to go back |
| Skill tree | Drag, scroll | D-pad between nodes (A selects, A again buys), right stick pans, LT / RT zoom |

No input setup is needed: the game reads the devices directly through the Input System
package. Whichever you used last decides the button prompts (keys or controller buttons), and a
controller always has a focused element in menus. *Settings > Controls > Controller buttons*
switches the prompts between Xbox, PlayStation and Switch button icons (until you pick one, the
connected controller decides). With a controller, a bold animated frame marks the selected
control, a hint bar shows the Select / Back / Switch tab buttons, LB / RB switch tabs in Settings
and Rebirth, and left / right on a setting changes its value. Controller look speed, invert, dead zone and
vibration (rumble on hits, felled trees and clangs) are in *Settings > Controls*.

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
   The tree has 54 upgrades and is bigger than the window: it opens near full size around the trunk;
   drag it with any mouse button (or the right stick), scroll to zoom towards the cursor (or LT / RT), and use
   the buttons in the legend to zoom or go back to the whole-tree view.
   - **Axe:** Better Axe (Rusty Hatchet → Steel → Lumberjack's → Tempered Broadaxe → Ironbreaker) → Whetstone (damage) → Grip Training (speed) → Lucky Strike (chance of double damage); Better Axe → Backpack (Burlap Sack → Canvas Satchel → Leather Rucksack → Frame Pack → Logger's Pack → Expedition Pack → Hauler's Harness) → Clean Cuts (more wood per tree) → Trail Boots (move speed).
   - **Automation:** Auto-Chopper (up to 8) → Chopper Motors → Harvester Line (rebuilds every chopper as a Heavy, then Titan Harvester); Auto-Chopper → Foreman's Whistle → Spare Parts (cheaper machines, up to -30%); Auto-Chopper → Sawmill (up to 3, 2 wood → 1 plank) → Sawmill Blades → Precision Saws (more planks from the same wood) / Furniture Buyers; Sawmill → Carpentry Workshop (up to 3, 3 planks → 1 furniture) → Workshop Tools → Fine Joinery, and Carpentry Workshop → Cabin Builder (up to 3, 5 furniture → 1 cabin kit) → Power Cranes → Master Builders. Furniture Showroom and Cabin Realtors (Economy) raise their prices.
   - **The production chain (2.2):** wood → planks (Sawmill) → wood panels (**Panel Press**, 2 planks each) → furniture (Carpentry Workshop, 2 panels each) → room sets (**Interior Studio**, 3 furniture each) → cabin kits (Cabin Builder, 3 room sets each) → lodges (**Lodge Builder**, 3 cabin kits each). Each step sells for more than what it uses. The new machines stand in the south yard; each has a speed upgrade and a price upgrade (Hydraulic Press, Glulam, Panel Contracts; Design Team, Interior Magazines; Tower Cranes, Ski Resorts). Saves that already had workshops or cabin builders get a free Panel Press or Interior Studio so nothing stands idle.
   - **Economy:** Market Contacts (wood price) → Log Magnet (pickup range), Night Shift (longer offline time) and Haggling (every sale pays more) → Lucky Finds (felled trees sometimes drop cash).
   - **Forests:** Old Grove ($25,000; needs the Lumberjack's Axe) → Fertile Soil (faster regrowth); Old Grove → Ironwood Ridge ($350,000; also needs the Tempered Broadaxe) → Gold Prospector (more Goldbark cash). Ironwood Ridge → Frostpine Peaks ($8M; Ironbreaker) → Blossom Vale ($150M; Frostbite Axe) → Crystal Wilds ($4B; Emberforge Axe), each with two perks of its own: Frost Tempering, Snow Chains; Blossom Festival, Ember Kilns; Crystal Resonance, Starlight Harvest. (Prices here are before the skill tree's half-price scale.) Then Crystal Wilds → Willow Marsh ($100B; Starsteel) → Sunstone Canyon ($3T; Marshwarden Axe) → Moonlit Highlands ($80T; Sunforged Axe), with Bog Iron, Firefly Lanterns, Sun Kilns, Canyon Winds, Moonlit Harvest and Lunar Fortune. Trading posts in Blossom Vale and Sunstone Canyon buy whatever you carry.
   - **Later perks in every branch:** Honed Edge, Steady Rhythm, Cleave (chance to fell a tree in one
     hit), Lucky Blade, Power Swing (bigger double-damage hits), Pack Mule, Timber Sense, Long Strides
     (Axe); Twin Engines, Turbo Choppers, Fuel Efficiency, Assembly Line, Overtime Crew, Kiln Drying,
     Overdrive Saws (Automation); Wood Brokers, Premium Planks, Strong Magnet, Long Nights, Export Deals,
     Fortune's Favor, Bulk Buyer (cheaper non-machine upgrades) (Economy); Old Growth, Timber Rights,
     Pathfinder, Ranger's Eye, Deep Roots, Forest Spirits, Golden Canopy (Forests).
   - **Top of the tree (2.1):** Master Smith, Timberfall, Devastating Blows, Second Wind, Sherpa Frame
     (Axe); Hydraulic Rams, Graveyard Shift, Conveyor Belts, Band Saws, Power Lathes, Prefab Panels
     (Automation); Designer Furniture, Luxury Cabins, Global Trade, Treasure Hunter (Economy). Better Axe
     goes on to the Frostbite, Emberforge, Starsteel, Marshwarden, Sunforged and Moonsplitter axes, and the Backpack to the Glacier Pack,
     Ember Hauler, Starlight Satchel, Marsh Pack, Canyon Hauler and Moonlit Satchel.
5. **Automate.** Bought machines appear in the yard south of the start and animate (spinning
   blades, swinging saw arm, feeding logs, rollers). Each one shows what it produces when you
   are nearby. A wooden stake marks where the next machine will go.
6. **Rebirth.** From the skill tree header at the Workbench, pay to start over with a
   permanent bonus: every rebirth adds +15% to all money you earn (sales, Goldbark cash and
   lucky finds). The first costs $2,500 and each one after that costs 1.03 times the previous one.
   Buy x1, x5, x10 or as many as you can afford at once (the price is the sum of the single prices).
   A rebirth resets money, every upgrade (axes, backpack, machines, forests) and all wood; it keeps
   your rebirths, achievements, lifetime stats and settings. The HUD shows the current multiplier next
   to your money. Each rebirth also gives **3 skill points**. Spend them in the **Legacy Tree**
   (the button in the Rebirth screen's header): a second skill tree of 49 perks that are never reset,
   in four branches:

   | Branch | Perks |
   | --- | --- |
   | Heritage | Heirloom Axe, Head Start, Starter Kit, Forest Charter, Heirloom Pack (start with a bigger backpack), Sawmill Deed, Workshop Deed, Press Deed, Studio Deed, Builder's Charter, Lodge Charter (start with those machines), Scholar (+1 skill point per rebirth per level), Ancestral Wisdom (+2% earnings per rebirth per level), Phoenix Feather (rebirths up to 50% cheaper) |
   | Might | Titan Grip, Quick Hands, Deep Pockets, Lodestone, Keen Eye, Crushing Blows, Legend's Swing, Giant's Strength, Woodsman's Instinct, Fleet Foot |
   | Industry | Master Engineer, Ancestral Engines, Family Sawmill, Guild Carpenters, Master Architects, Steam Presses, Design Dynasty, Lodge Masons (much faster machines), Perfect Joinery, Blueprints, Night Watch, Market Courier and Express Courier (the yard stock sells itself every 30 s, then 10 s) |
   | Fortune | Silver Tongue, Timber Baron, Plank Empire, Furniture Dynasty, Cabin Tycoon, Panel Guild, Showroom Chain, Lodge Magnate (higher prices), Fortune's Child, Golden Touch, Old Connections, Druid's Blessing |

   Head starts apply as soon as you buy them and again at the start of every run. Saves from before
   2.1 get the extra skill points their earlier rebirths now earn (3 each) the first time they load.
7. **Achievements.** 73 achievements in eight groups (Chopping, Wealth, Rebirth, Gear, Industry,
   Explorer, Forests, Milestones), in the pause menu. Each gives a reward once: money, skill points, or a
   permanent bonus for that save (+5% axe damage, +10% on all sales...). They belong to the save slot
   (every slot earns its own) and stay through rebirths. A banner drops in at the top when one unlocks;
   an older save collects everything its progress already earned when you first play it, shown as one
   summary banner.

| Tree | Area | Axe needed | HP | Wood |
| --- | --- | --- | --- | --- |
| Pine | Clearing | Any | 3 | 4 |
| Birch | Clearing | Any | 5 | 7 |
| Oak | Clearing / Grove | Steel Axe | 16 | 18 |
| Maple | Old Grove | Lumberjack's Axe | 70 | 55 |
| Redwood | Grove / Ridge | Lumberjack's Axe | 170 | 150 |
| Ironwood | Ridge / Peaks | Tempered Broadaxe | 650 | 560 |
| Goldbark | Ironwood Ridge | Ironbreaker | 2,600 | 1,500 + $12,000 |
| Frostpine | Peaks / Vale | Frostbite Axe | 8,000 | 4,800 |
| Cherry | Vale / Wilds | Emberforge Axe | 26,000 | 15,000 |
| Emberwood | Vale / Wilds | Emberforge Axe | 42,000 | 24,000 |
| Crystalwood | Crystal Wilds / Marsh | Starsteel Axe | 140,000 | 60,000 + $450,000 |
| Willow | Willow Marsh | Marshwarden Axe | 400,000 | 180,000 |
| Cypress | Marsh / Canyon | Marshwarden Axe | 600,000 | 260,000 |
| Palm | Canyon / Highlands | Sunforged Axe | 1,600,000 | 700,000 |
| Sunbark | Canyon / Highlands | Sunforged Axe | 2,400,000 | 1,000,000 |
| Moonwood | Moonlit Highlands | Moonsplitter | 7,000,000 | 2,800,000 + $25,000,000 |

### Graphics and performance

*Settings* has five tabs: Graphics, Display, Audio, Controls and Interface. The **quality preset**
(Low, Medium, High, Ultra) sets shadows (off to 140 m soft shadows with 4 cascades), texture
quality, anisotropic filtering, scenery detail (how much grass, flowers and small props are shown),
effects density, bloom, render scale and anti-aliasing; changing any of them by hand shows
*Custom*. Display adds brightness; Audio has separate effects, ambience/machines and interface
volumes; Controls has mouse and controller look, vibration, dead zone, field of view, head bob and
camera shake; Interface has UI scale, the FPS counter, tutorial hints and damage numbers.

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

### Cloud saves

Players can sign in with a Unity Player Account (Google, Apple or email, in the browser) from
*Settings > Account* or the save screen. Save slots A, B and C then follow them between devices,
PC and phone; settings stay per device. Saves upload about 45 s after the game saves, when the app
goes to the background and on quit, and download when the game starts or after signing in. Each
slot is compared by a fingerprint of its progress (not its timestamp, so re-saving unchanged
progress isn't a change). If a slot changed on two devices since they last synced, a dialog shows
both versions and the player picks one. Offline, the game plays from its local files and retries
every minute. Code: `Scripts/Core/CloudSync.cs`, `Scripts/UI/CloudConflictDialog.cs`.

**One-time setup (needed before cloud saves work):**
1. In Unity: *Edit > Project Settings > Services*, link the project to a Unity Cloud project.
2. In the Unity Dashboard (cloud.unity.com) for that project, open *Player Authentication >
   Identity providers*, add **Unity Player Accounts** and save. Cloud Save needs no extra setup.
3. Back in Unity: *Project Settings > Services > Authentication*, open the Unity Player Accounts
   settings and press *Generate settings*. This creates `Assets/Resources/UnityPlayerAccountSettings.asset`
   with the client id. Builds without it show "Cloud saves aren't available" and play normally.

### Android

*Tools > Forest to Fortune > Build Android APK* writes `Builds/Android/ForestToFortune.apk`
(ARM64, IL2CPP, Vulkan with GLES3 fallback, landscape, Android 8.0+, debug-signed for sideloading).
The build switches the editor to Android; *Switch Editor to Windows* switches back.

Touch controls (`Scripts/UI/TouchControls.cs`):
- **Move:** a floating stick appears under the left thumb. Push it fully forward to run.
- **Look:** drag anywhere else.
- **Chop:** hold the big axe button. Dragging on it aims while chopping.
- **Jump:** the jump button.
- **Use:** a button that pops up, labelled, when something can be used.
- **Menu:** the top-right button, or Android's back button.
- Every finger is tracked separately, so moving, looking and chopping work together. Presses, hits and falling trees vibrate the phone.
- *Settings > Controls > Touch* sets look sensitivity, invert, a fixed stick, button size, opacity and vibration.

The skill tree pans with one finger and pinch-zooms with two. Windows shrink to fit phone screens,
the HUD stays out of notches, and a first launch on a phone starts at Medium quality and 60 FPS.

### Discord

The game shows Rich Presence on your Discord profile, for example "Chopping with the Lumberjack's Axe", plus money
and rebirths. To turn it on, create an application named "Forest to Fortune" at
discord.com/developers/applications. Under *Rich Presence > Art Assets*, upload
`Assets/_ChoppingTrees/Art/Icon/GameIcon.png` with the name `icon`. Then paste the Application
ID into `GameConfig.discordApplicationId` and rebuild. It uses Discord's local IPC pipe, so
there is no SDK. Players can turn it off with *Settings > Interface > Discord status*.

### Game icon

`Art/Icon/GameIcon.png` is rendered from the game's own models. Re-render it with
`blender -b --factory-startup -P Blender/icon_render.py -- render.png`, then run
`python Blender/icon_compose.py render.png Assets/_ChoppingTrees/Art/Icon/GameIcon.png`.

### Saving

There are three save slots, A, B and C. *Play* on the main menu opens the save screen, where
each slot shows its money, rebirths, trees felled, wood chopped, axe, time played and when it
was last played. A used slot can be continued or deleted, and an empty one starts a new game. The
main menu shows the stats of the slot played last. Slots are saved to
`%USERPROFILE%\AppData\LocalLow\Hazelwood\Forest to Fortune\progress_A.json` (`_B`, `_C`):
after every purchase, every 30 s, on quit, and from *Menu > Save Game*. On first launch, saves
from the old "A Game About Chopping Trees" folder are copied over, and an old single
`progress.json` becomes slot A. The Esc menu doesn't pause: machines and trees keep going behind it. Settings are
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
Builds/Windows/ForestToFortune.exe -ct-smoke -ct-quit-after 90 -ct-nosave -ct-upscaler DLSS -ct-screenshot shot.png -logFile smoke.log
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
- The chain continues: Carpentry Workshops (west side of the yard) turn 3 planks into furniture ($16 vs $10.50 of planks), and Cabin Builders (east side) turn 5 furniture into a flat-pack cabin kit ($130 vs $80 of furniture). Each step only uses what's in the yard, so a later machine waits when the one before it can't keep up.
- The yard is wider now, with a red barn, a water tower, lumber racks, wagons, a well and more cabins around it.
- Offline production is 100% efficient but capped (2 h base) so idle time stays rewarding without making active play pointless.
- Sounds are synthesised in code (no third-party audio). Fonts are Inter (SIL OFL, licence in `Art/Fonts/OFL.txt`).
- Only packages that ship with the template (URP, Input System, uGUI/TextMesh Pro, Test Framework), Unity's built-in NVIDIA and AMD modules (for DLSS and FSR), and Unity's automation package are used. No paid assets.
- The upscaler framework needs the `ENABLE_UPSCALER_FRAMEWORK` scripting define (set by *Setup Project*). The NVIDIA and AMD code paths are switched on by version defines in `ChoppingTrees.asmdef`.
