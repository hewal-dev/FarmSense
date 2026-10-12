# FarmSense - Crop Info and Harvest with E

**Crop growth info and one-key harvesting for 7 Days to Die v3.x (PC / Linux).**

FarmSense shows the growth stage, the percentage and the remaining time for every crop you aim at,
adds a Harvest action on `E` for ripe crops and wild plants, and lets corn, potatoes, blueberries
and pumpkins grow wild in the pine forest.

An independent development by **Eltern vs Zombies**, released under the **MIT License**.

---

## Features

| Feature | Description |
|---------|-------------|
| 📈 **Growth info** | Stage (for example `2/3`), growth percentage and the time remaining until the plant is ripe |
| ⏳ **Live progress bar** | Green fill while the plant grows, so the state is readable at a glance |
| 🖐 **Works with empty hands** | The info appears while aiming, with or without an item in your hand |
| ⚡ **Instant** | Appears as fast as the vanilla name line and disappears again when you look away |
| 🌿 **Wild plants too** | Wild crops are marked as such in the display |
| ⚠️ **Warnings** | Tells you when a plant cannot grow here (light, soil) or has died |
| 🌳 **Trees** | Optional: multi-stage plants such as trees, including the overall time left |
| ⛏ **Harvest with E** | Ripe crops and wild plants offer a `Harvest` action on `E` |
| 🎁 **Vanilla yields** | Amounts, seed chance, perk and outfit bonuses come from the game's own drop tables |
| 🔁 **Wild crops that return** | Wild plants reappear over time, similar to vanilla goldenrod or cotton |
| 🌲 **Pine forest crops** | Corn, potatoes, blueberries and pumpkins grow wild in the pine forest |
| ⚙️ **Configurable** | Display mode, warnings, trees, harvest sounds and the wild growth system |

**FarmSense does not change crop yields, growth speed or recipes.** It reads the game's crop data and
adds one interaction command. A planted crop, a fully skilled farmer and the farmer outfit produce
exactly the amounts you would get from hitting the plant yourself.

---

## Usage

1. Look at any crop, planted or wild. The info field appears below the vanilla interaction prompt.
2. Read the stage, the percentage and the remaining time. A ripe plant shows `ready`.
3. Press `E` on a ripe plant to harvest it, or hit it with a tool as usual.

The field stays visible with an empty hand, so you can inspect a field before deciding what to
harvest.

---

## Installation

1. Download `FarmSense-Crop-Info-and-Harvest-with-E-<version>.zip` from the
   [Releases page](https://github.com/hewal-dev/FarmSense/releases) (or build from source, below).
2. Extract the `FarmSense` folder into your mods directory:

   ```
   .../7 Days To Die/Mods/FarmSense/
   ```

   After extracting you should see `FarmSense.dll`, `ModInfo.xml`, `Config.xml` and `Config/` in that
   folder.
   **Do not add `0Harmony.dll` here:** the game ships Harmony itself in `<Mods>/0_TFP_Harmony/`
   (Harmony 2.13) and an extra copy inside a mod folder can produce errors at startup.
3. **Disable EAC** (Easy Anti-Cheat) for the game - required for any Harmony mod.
4. Start the game. `[FarmSense] Version 1.0.0 wird geladen ...` appears in the client log.

Multiplayer: client and server must run the **same mod version**. The harvest request is sent from
the client to the host or server and the wild growth system runs on the hosting side.

---

## Configuration

FarmSense reads `Config.xml` inside its own mod folder:

```
.../7 Days To Die/Mods/FarmSense/Config.xml
```

| Setting | Default | Meaning |
|---------|---------|---------|
| `HarvestByE_Enabled` | `true` | Harvest ripe plants with `E` |
| `Harvest_PlaySound` | `true` | Play the vanilla plant break sound |
| `Harvest_PlayCollectSound` | `true` | Use the vanilla collect popup and pickup sound |
| `Harvest_YieldMultiplier` | `1.0` | Multiplier for the harvested amount, `1.0` = vanilla values |
| `Hud_Mode` | `window` | `window` = own info field, `prompt` = vanilla prompt only, `off` = disabled |
| `Hud_UseVanillaPrompt` | `true` | Add the info to the vanilla interaction prompt as well |
| `Hud_ShowTrees` | `true` | Also show growth info for multi-stage plants such as trees |
| `Hud_ShowWarnings` | `true` | Show warnings for light, soil and dead plants |
| `Hud_UpdateInterval` | `0` | `0` = update every frame, higher values slow the display down |
| `WildGrowth_Enabled` | `true` | Regrow wild crops in existing worlds |
| `WildGrowth_IntervalSeconds` | `120` | Check interval in seconds |
| `WildGrowth_Radius` | `40` | Radius in blocks around every player |
| `WildGrowth_AttemptsPerInterval` | `12` | Attempts per interval and player |
| `WildGrowth_ChancePerAttempt` | `0.25` | Chance per attempt |
| `WildGrowth_MaxPerChunk` | `2` | Upper limit per 16x16 chunk since server start |
| `WildGrowth_MinLight` | `11` | Minimum light level at the target position |
| `WildGrowth_Biomes` | `pine_forest` | Biome where wild crops appear |
| `WildGrowth_Blocks` | the four crops | The wild crops that can appear |

After editing the file, restart the game.

---

## Building from Source

The mod is plain C# compiled against the game's own assemblies. Nothing is bundled from the game -
you compile against your own local installation.

### Requirements

- **7 Days to Die v3.x** installed locally (used only as a compile-time reference)
- **Mono** (`mcs`) on Linux
- `0Harmony.dll` from your own installation, at `<Game>/Mods/0_TFP_Harmony/0Harmony.dll`
  (Harmony 2.13, MIT-licensed). The build script picks it up automatically - never copy a Harmony
  DLL into a mod folder.

### Linux (Mono)

```bash
# auto-detect the game directory
bash build.sh

# or point it explicitly
bash build.sh "/path/to/7 Days To Die"
GAME_DIR="/path/to/7 Days To Die" bash build.sh
```

The script checks its prerequisites (compiler, game assemblies, Harmony, `netstandard.dll`) and
fails with a clear message if something is missing. It writes `bin/FarmSense.dll` and prepares the
ready-to-copy runtime tree in `dist/FarmSense/` (DLL, `ModInfo.xml`, `Config.xml` and `Config/`).

### Windows

No MSBuild project is shipped yet - the compile line from `build.sh` maps directly to the .NET
compiler:

```bat
csc -target:library -out:FarmSense.dll ^
    -lib:"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed" ^
    -r:Assembly-CSharp.dll -r:LogLibrary.dll -r:UnityEngine.dll -r:UnityEngine.CoreModule.dll ^
    -r:UnityEngine.IMGUIModule.dll -r:UnityEngine.UI.dll -r:UnityEngine.UIModule.dll ^
    -r:UnityEngine.AudioModule.dll ^
    -r:"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\0_TFP_Harmony\0Harmony.dll" ^
    src\*.cs
```

Then copy `FarmSense.dll`, `ModInfo.xml`, `Config.xml` and `Config/` into
`.../7 Days To Die/Mods/FarmSense/`.

### After building

```bash
bash sync.sh "/path/to/Mods"     # dry run first, then deploy dist/FarmSense
```

`sync.sh` is a development helper. It resolves the mods folder from the argument or `MODS_DIR`,
defaults to `<Game>/Mods`, and refuses to ship Harmony.

---

## Compatibility

FarmSense works with the vanilla farming system. It reads the crop blocks and their drop tables and
adds one interaction command to plants.

Conflicts are possible with mods that rewrite the crop blocks, replace the plant drop tables, or
heavily modify the interaction prompt and the block HUD. Overhauls with their own farming system are
not supported. Universal compatibility with every UI or farming mod is not guaranteed.

---

## Known issues

- **Multiplayer has not been play-tested yet.** The harvest request is sent from the client to the
  host or server, and wild growth runs on the hosting side. Install the same version on the host or
  server and on every client.
- **Experience and challenge progress** for harvesting are not credited by the `E` harvest yet. Use
  the normal melee harvest if you need quest or challenge progress.
- **Biome decoration** for wild crops only applies to worlds generated with the mod installed.
  Existing worlds use the regrowth system instead.
- **Creative menu:** placing a plant places the wild variant, which yields the wild amounts
  (1 item instead of 2 for a planted crop).

---

## Project Layout

```
FarmSense/
├── src/FarmSenseMain.cs             Mod entry point + Harmony bootstrap
├── src/FarmSenseConfig.cs           Config.xml reader
├── src/CropRegistry.cs              Crop registry built from the game's own block data
├── src/HudSystem.cs                 Target detection and HUD text
├── src/FarmSenseHudController.cs    XUi controller (global namespace, resolved by reflection)
├── src/HarvestLogic.cs              Harvest from the vanilla drop tables
├── src/NetPackageFarmSenseHarvest.cs Client -> host/server harvest request
├── src/WildGrowthSystem.cs          Wild crop regrowth in existing worlds
├── src/HarvestDiagnostics*.cs       Diagnostics used while LogDebug is enabled
├── Config/                          Biome decoration, block tweak, XUi modlet, localization
├── Config.xml                       User settings
├── tools/build_nexus_media.py       Promo image generator (optional, needs Pillow)
├── build.sh                         Linux/Mono build script
└── sync.sh                          Development helper: deploy dist/ into a mods folder
```

Developer documentation (German working notes, analysis of the vanilla harvest mechanics) lives in
`PROTOKOLL.md` and in the wiki of the author's modding workspace. The README you are reading is the
authoritative source for users.

---

## License & Credits

**MIT License** - see [LICENSE](LICENSE). Copyright (c) 2026 Eltern vs Zombies.

You may modify this mod and include it in other projects, including overhauls, as long as
**Eltern vs Zombies** is credited as the original author.

- **Author:** Eltern vs Zombies - [YouTube](https://www.youtube.com/@ElternVSZombies)
- **Third-party:** the mod is compiled against the [Harmony](https://github.com/pardeike/Harmony)
  library by Andreas Pardeike (MIT). **No Harmony binary is included in this repository or in the
  release ZIP** - 7 Days to Die ships its own copy in `<Mods>/0_TFP_Harmony/`. 7 Days to Die game
  assemblies are **not** included and are referenced only at compile time.

**Not affiliated with The Fun Pimps.** 7 Days to Die is a trademark of The Fun Pimps Entertainment LLC.
