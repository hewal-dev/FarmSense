Create the Nexus Mods media set for the 7 Days to Die mod "FarmSense". Work in the current repository.

INPUT IMAGES (see media/sources/):
- screenshot-mushroom-farm.png (1907x1044): a farm room; mushrooms grow on a wall; the FarmSense info field is visible in the centre below the plant name, showing the stage "2/3", "11 % - reif in 56 min" and a green progress bar.
- screenshot-wild-yucca.png (1907x1057): wild yucca plants in a pine forest at night; the info field shows "Yucca (Samen)", "1/3", "2 % - reif in 2 h 3 min".
- ref-evz-header.jpg (1300x372): the author's existing EVZ style reference (their previous Nexus header). Match its visual language exactly: dark charcoal panels, orange accent #e8892a, off-white text, bold condensed headings, thin orange divider lines and thin orange frames around framed game screenshots plus a soft drop shadow, uppercase bold English captions.

TASK
1. Write tools/build_nexus_media.py in this repository (Pillow, DejaVu fonts). Adapt the author's existing script at /home/hewal/Hermes/7d2d-v3-mods/Compare/tools/build_nexus_media.py and reuse its helper functions (cover, contain, background, paste_card), its colours (WHITE 244,246,244 / MUTED 177,184,180 / ORANGE 232,137,42 / GREEN 36,225,67 / DARK 10,14,14) and its typography approach.

2. Generate into media/nexus/ (JPEG, quality 94, optimize, progressive):
   - farmsense-thumbnail-1920x1080.jpg
     Brand block top left: "FARMSENSE" very large and bold, a short orange tag rectangle right after the name, beneath it the subtitle "CROP INFO + HARVEST WITH E" in orange and a muted single line "See what your crops are doing - and harvest them with E".
     Below: two framed screenshot cards side by side (mushroom farm left, wild yucca right), each with a small dark chip label reading "FARM PLOTS" and "WILD CROPS".
     Top right: three feature chips in the reference style reading "STAGE 2/3", "TIME LEFT", "HARVEST WITH E".
   - farmsense-header-1300x372.jpg
     Keep the left ~34 percent deliberately quiet because Nexus overlays the page title there (dark panel plus a thin vertical orange divider). Place one framed crop of the mushroom-farm screenshot on the right, with a small chip reading "GROWTH + TIME LEFT".
   - farmsense-gallery-01-1920x1080.jpg
     Heading "GROWTH AND TIME LEFT AT A GLANCE", orange divider, subtitle "Stage, percentage and remaining time for every crop - planted or wild", then one large framed card of the mushroom-farm screenshot.
   - farmsense-gallery-02-1920x1080.jpg
     Heading "WILD CROPS IN THE PINE FOREST", orange divider, subtitle "Corn, potatoes, blueberries and pumpkins now grow wild - and grow back after harvest", then one large framed card of the wild-yucca screenshot.
   - farmsense-nexus-media-preview.jpg
     Contact sheet of the four files above with their file names, same style as the reference script.

3. IMPORTANT
   - Crop the screenshots so the FarmSense info field in the centre of each screenshot stays sharp and legible; never stretch or distort an image; keep important content away from the outer edges since Nexus crops cards.
   - All text must be English. Use only ASCII characters in the rendered text (no en dash, no em dash).
   - Every file must stay below 8 MiB.

4. Run the script, then verify with ImageMagick identify that every file has exactly the required dimensions, and print the file sizes.

Do not modify anything else. Do not touch the game installation or the mod's Config files.
