FarmSense - Crop Info and Harvest with E

[heading][color=#e8892a]Short description[/color][/heading]

[quote]FarmSense shows the growth stage, percentage and time left for any crop you aim at, adds a Harvest action on E for ripe crops and wild plants, and lets corn, potatoes, blueberries and pumpkins grow wild in the pine forest. Harvest amounts stay identical to a normal melee harvest.[/quote]

[line]

[heading][color=#e8892a]What does the mod do?[/color][/heading]

7 Days to Die never tells you how far a plant has grown. You aim at it, see a name, and guess whether it is worth waiting for or ready to pick. FarmSense adds that missing information and makes harvesting a single key press.

[b]FarmSense does not change crop yields, growth speed, or recipes.[/b] It reads the game's own crop data and shows it. Harvest results are taken from the vanilla drop tables, so a planted crop, a fully skilled farmer, and the farmer outfit all produce exactly the amounts you would get from hitting the plant yourself.

[line]

[heading][color=#e8892a]Features[/color][/heading]

[b]Crop info while aiming[/b]

[list]
[*]Shows the growth stage, for example [b]2/3[/b]
[*]Shows the growth percentage of the whole growth, from seed to ripe, and the time remaining until the plant is ripe
[*]Compact progress bar with a green fill while the plant grows
[*]Works with an empty hand and with any tool or weapon in your hand
[*]Appears immediately, as fast as the vanilla name line, and disappears again as soon as you look away
[*]Also shows wild plants, which are marked as such
[*]Optional: multi-stage plants such as trees, including the overall time left
[*]Optional: warning lines when a plant cannot grow here or has died
[/list]

[b]Harvest with E[/b]

[list]
[*]Ripe crops and wild plants offer a [b]Harvest[/b] action on [b]E[/b]
[*]The plant is removed and the harvest is placed in your inventory with the vanilla pickup sound and collect popup
[*]Amounts, seed chance, perk scaling and outfit bonuses come from the game's own drop tables, verified against a manual harvest with bare hands
[*]Wild plants reappear over time, similar to vanilla wild goldenrod or cotton
[/list]

[b]Wild crops in the pine forest[/b]

[list]
[*]Corn, potatoes, blueberries and pumpkins now also grow wild in the pine forest, the way vanilla goldenrod, cotton and chrysanthemum do
[*]In newly created worlds they are placed as part of the biome decoration during world generation
[*]In existing worlds a regrowth system plants them around players over time, so no new save is required
[*]The biome, the plants, the check interval, the radius and the amount per interval are all configurable
[/list]

[line]

[heading][color=#e8892a]Requirements[/color][/heading]

[list]
[*]7 Days to Die V3.3.0 (b18)
[*]Easy Anti-Cheat must be disabled
[*]No additional dependency download is required
[*]Harmony is provided by the game itself in [b]Mods/0_TFP_Harmony[/b]. This package contains [b]no[/b] Harmony DLL, and it must not be added manually
[/list]

[line]

[heading][color=#e8892a]How to use[/color][/heading]

[list=1]
[*]Look at any crop, planted or wild. The info field appears below the vanilla interaction prompt.
[*]Read the stage, the percentage and the remaining time. A ready plant shows [b]ready[/b].
[*]Press [b]E[/b] on a ripe plant to harvest it, or hit it with a tool as usual.
[/list]

The field stays visible with an empty hand, so you can inspect a field before deciding what to harvest. When a plant cannot grow at its current spot, the field tells you whether the problem is light, soil, or that the plant is dead.

[line]

[heading][color=#e8892a]Configuration[/color][/heading]

FarmSense creates and reads [b]Config.xml[/b] inside its own mod folder:

[code]7 Days To Die/Mods/FarmSense/Config.xml[/code]

[b]Harvest and display[/b]

[code]
HarvestByE_Enabled      true    Harvest ripe plants with E
Harvest_PlaySound       true    Play the vanilla plant break sound
Harvest_PlayCollectSound true   Use the vanilla collect popup and pickup sound
Harvest_YieldMultiplier 1.0     Multiplier for the harvested amount, 1.0 = vanilla values
Hud_Mode                window  window = own info field, prompt = vanilla prompt only, off = disabled
Hud_UseVanillaPrompt    true    Add the info to the vanilla interaction prompt as well
Hud_ShowTrees           true    Also show the growth info for multi-stage plants such as trees
Hud_ShowWarnings        true    Show warnings for light, soil and dead plants
Hud_UpdateInterval      0       0 = update every frame, higher values slow the display down
[/code]

[b]Wild growth[/b]

[code]
WildGrowth_Enabled            true        Regrow wild crops in existing worlds
WildGrowth_IntervalSeconds    120         Check interval in seconds
WildGrowth_Radius             40          Radius in blocks around every player
WildGrowth_AttemptsPerInterval 12         Attempts per interval and player
WildGrowth_ChancePerAttempt   0.25        Chance per attempt
WildGrowth_MaxPerChunk        2           Upper limit per 16x16 chunk since server start
WildGrowth_MinLight           11          Minimum light level at the target position
WildGrowth_Biomes             pine_forest Biome where wild crops appear
WildGrowth_Blocks                         The four wild crops that can appear
[/code]

Set [b]WildGrowth_Enabled[/b] to [b]false[/b] if you only want the information display. After editing the file, restart the game.

[line]

[heading][color=#e8892a]Installation[/color][/heading]

[list=1]
[*]Download and extract the archive.
[*]Copy the complete [b]FarmSense[/b] folder into the [b]Mods[/b] folder of your 7 Days to Die installation.
[*]Disable Easy Anti-Cheat.
[*]Fully restart the game.
[/list]

The resulting path should look similar to this:

[code]7 Days To Die/Mods/FarmSense/ModInfo.xml[/code]

[line]

[heading][color=#e8892a]Compatibility[/color][/heading]

FarmSense works with the vanilla farming system. It reads the crop blocks and their drop tables and adds one interaction command to plants.

Conflicts are possible with mods that rewrite the crop blocks, replace the plant drop tables, or heavily modify the interaction prompt and the block HUD. Overhauls with their own farming system are not supported. Universal compatibility with every UI or farming mod is not guaranteed.

[line]

[heading][color=#e8892a]Changelog[/color][/heading]

[b]1.0.1[/b]

[list]
[*]The growth percentage now describes the whole growth from seed to ripe and no longer restarts when a plant moves on to its next growth stage. A plant in its second stage now shows its real progress, for example [b]54 % - ready in 58 min[/b] instead of a small value close to the one it showed in the first stage.
[*]The percentage and the remaining time now always refer to the same thing, so the two values match each other.
[/list]

[b]1.0.0[/b]

[list]
[*]First public release: crop info while aiming, harvest with [b]E[/b], and wild crops in the pine forest.
[/list]

[line]

[heading][color=#e8892a]Known issues[/color][/heading]

[b]Please read this section before reporting a problem.[/b]

[list]
[*][b]Multiplayer has not been play-tested yet.[/b] The harvest request is sent from the client to the host or server, and the wild growth system runs on the hosting side. Install the same version on the host or server and on every client.
[*][b]Experience and challenge progress[/b] for harvesting are not credited by the [b]E[/b] harvest yet. Use the normal melee harvest if you need quest or challenge progress.
[*][b]Biome decoration[/b] for wild crops only applies to worlds generated with the mod installed. Existing worlds use the regrowth system instead.
[*][b]Creative menu:[/b] placing a plant places the wild variant of that plant, which yields the wild amounts.
[/list]

[line]

[heading][color=#e8892a]Uninstallation[/color][/heading]

Close the game and delete the [b]FarmSense[/b] folder from your [b]Mods[/b] directory. The mod adds no items and does not modify save files. Wild plants and biome decoration that already exist in generated worlds simply stay in place. As always, back up your save first.

[line]

[heading][color=#e8892a]Bug reports[/color][/heading]

Please report problems through the [b]Bugs[/b] tab on this mod page and include the following information:

[list]
[*]Your 7 Days to Die version and build number
[*]Your FarmSense version
[*]Single-player or multiplayer, including host, server or client role
[*]The plant you were looking at or harvesting, and whether it was planted or wild
[*]Clear reproduction steps
[*]Any other installed farming, UI or overhaul mods
[*]A screenshot of the info field when the problem is visible
[*]The relevant section of the client log
[/list]

[line]

[heading][color=#e8892a]Credits[/color][/heading]

Developed by [b]Eltern vs Zombies[/b]

[url=https://www.youtube.com/@ElternVSZombies]Eltern vs Zombies on YouTube[/url]

FarmSense uses Harmony to add the harvest interaction, which 7 Days to Die ships with the game itself.

[line]

[heading][color=#e8892a]Permissions[/color][/heading]

You may modify this mod and include it in other projects, such as overhauls, as long as [b]Eltern vs Zombies[/b] is credited as the original author of the mod.

Source code: [url=https://github.com/hewal-dev/FarmSense]github.com/hewal-dev/FarmSense[/url]
