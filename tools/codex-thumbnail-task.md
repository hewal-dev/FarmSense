Rebuild ONLY the thumbnail image of the existing Nexus media set in this repository. Keep everything else as it is, but update the script so the change is reproducible.

GOAL
The thumbnail must show at a glance what the mod displays. Zoom very close onto the FarmSense info field in both screenshots so the numbers are large and instantly readable.

SOURCE IMAGES (already in media/sources/):
- screenshot-mushroom-farm.png (1907x1044): the FarmSense info field sits in the centre, below the plant name "Pilze (Anbau)". It shows the stage "2/3", "11 % - reif in 56 min" and a green progress bar.
- screenshot-wild-yucca.png (1907x1057): the info field sits in the centre, below "Yucca (Samen)", showing "1/3" and "2 % - reif in 2 h 3 min".
- ref-evz-header.jpg: the author's EVZ style reference.

WHAT TO CHANGE in tools/build_nexus_media.py (the script you wrote earlier)
Replace the two small full-frame screenshot cards in the thumbnail with two large close-up crops of the info field:
- Crop a region around the info field of each screenshot, roughly 480-560 px wide in source resolution, then scale it up with LANCZOS (factor 1.5 to 2.0) so each crop fills its card. Do not stretch, keep the aspect ratio.
- Place both crops side by side, each about 850-880 px wide and about 430-470 px tall, in the lower half of the 1920x1080 canvas.
- Draw a thin orange frame and a soft drop shadow around each crop, exactly like the existing paste_card style.
- Under each crop, add an English caption in the EVZ style that translates what the German field says, so an English reader understands it immediately:
  left:  "2/3 - 11 % - RIPE IN 56 MIN"
  right: "1/3 - 2 % - RIPE IN 2 H 3 MIN"
  Use the bold condensed font for these captions, off-white, and keep them inside the canvas.
- Keep the existing brand block at the top left: "FARMSENSE", the orange tag rectangle, the orange subtitle "CROP INFO + HARVEST WITH E" and the muted line "See what your crops are doing - and harvest them with E".
- Keep the three feature chips at the top right ("STAGE 2/3", "TIME LEFT", "HARVEST WITH E").
- Keep the dark charcoal background, the orange accent #e8892a and the off-white text.
- Small chip labels are no longer needed on the cards because the captions replace them; if a label helps, keep it short ("FARM PLOTS", "WILD CROPS").
- Do not change the other four output files' content; regenerate them unchanged if the script rebuilds all of them.

CONSTRAINTS
- ASCII only in all rendered text (use a normal hyphen, no en dash or em dash).
- Output farmsense-thumbnail-1920x1080.jpg must be exactly 1920x1080, progressive JPEG, below 8 MiB.
- Verify afterwards with ImageMagick identify and report the dimensions and file sizes. Re-run the contact sheet.
- Look at your own result before finishing: open the produced thumbnail and confirm that the stage numbers, the percentage and the remaining time are clearly readable.
- Do not modify anything else and do not touch the mod's Config files or the game installation.
