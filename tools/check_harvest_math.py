#!/usr/bin/env python3
"""FarmSense – Ernteertrag gegen die Vanilla-Rechnung prüfen (read-only).

Liest die echten Drop-Tabellen der installierten Spielversion aus
(``Data/Config/blocks.xml``), wertet sie mit der Rechnung aus
``GameUtils.HarvestOnAttack`` (V 3.3.0 b18, per IL verifiziert) aus und zeigt
pro Pflanze den Erwartungswert je Ernte.

Zwei Modelle werden gegenübergestellt:

* **Vanilla** (so erntet das Spiel): Harvest-Tabelle mit Basis-Faktor **0**,
  Destroy-Tabelle mit Basis-Faktor **1**.
* **Mod bis 1.0.2** (Fehler): beide Tabellen mit Basis-Faktor **1** — dadurch
  greift der Eintrag ``farmerBonusSeed`` auch ohne Landwirt-Stiefel und der
  Samenertrag verdreifacht sich.

Ausgabe: Früchte / Samen pro Ernte (Erwartungswert, Samen inkl. 50 %-Wurf).

Aufruf:
    python3 tools/check_harvest_math.py [pfad/zu/blocks.xml]
"""

import os
import re
import sys

# --- Werte aus Data/Config (belegt, siehe docs/v3.0/farming-harvest.md) ------
# entityclasses.xml:270  HarvestCount base_set 1 tags="allHarvest,oreWoodHarvest,cropHarvest,wildCropsHarvest"
# entityclasses.xml:275  HarvestCount base_set 0 tags="…,bonusCropHarvest,farmerBonusHarvest"
# progression.xml:3532   perkLivingOffTheLand: perc_add 1,1,2 auf cropHarvest,wildCropsHarvest
# progression.xml:3533   perkLivingOffTheLand: base_set 1 (ab Stufe 2) auf bonusCropHarvest
# items.xml:14769ff      Landwirt-Stiefel: base_add 1 auf farmerBonusSeed (2–20 % je Qualität)
LOTL_PERC = (0.0, 1.0, 1.0, 2.0)   # perkLivingOffTheLand: perc_add 1,1,2 für Stufe 1..3

DEFAULT_XML = [
    "/home/hewal/.local/share/Steam/steamapps/common/7 Days To Die/Data/Config/blocks.xml",
    "/home/hewal/.steam/steam/steamapps/common/7 Days To Die/Data/Config/blocks.xml",
]


def load_blocks(path):
    """Blockname -> eigene <drop>-Einträge (die …HarvestPlayer-Blöcke sind vollständig)."""
    text = open(path, encoding="utf-8", errors="replace").read()
    blocks = {}
    for m in re.finditer(r'<block name="([^"]+)"[^>]*>(.*?)</block>', text, re.S):
        name, body = m.group(1), m.group(2)
        drops = []
        for d in re.finditer(r'<drop\s+([^/>]*)/>', body):
            attrs = dict(re.findall(r'(\w+)="([^"]*)"', d.group(1)))
            cnt = attrs.get("count", "1")
            parts = [p.strip() for p in cnt.split(",")]
            lo = hi = int(parts[0])
            if len(parts) > 1 and parts[1]:
                hi = int(parts[1])
            drops.append({
                "event": attrs.get("event", ""),
                "name": attrs.get("name", ""),
                "min": lo,
                "max": hi,
                "prob": float(attrs.get("prob", "1")),
                "tag": attrs.get("tag", ""),
            })
        blocks[name] = drops
    return blocks


def expected(drops, event, base, lotl, boots):
    """Erwartungswert einer Tabelle – Rechnung wie GameUtils.HarvestOnAttack.

    ``count = (int)(RandomRange(min, max+1) * faktor)``, danach der ``prob``-Wurf.
    Destroy gibt alles in einer Rate aus, Harvest in zwei Raten (2/3 + 1/3) —
    hier wird die Summe gerechnet, weil die E-Ernte in einem Schritt erntet und
    zerstoert. Geteilt wird nach Erzeugnis: Fruechte vs. Samen (``planted…``).
    """
    fruit = seed = 0.0
    for d in drops:
        if d["event"] != event or not d["name"]:
            continue
        factor = base
        if d["tag"] == "farmerBonusSeed":
            factor = base + boots                  # Stiefel: base_add 1 (2–20 % je Qualität)
        elif d["tag"] in ("cropHarvest", "wildCropsHarvest"):
            factor = 1.0 * (1.0 + LOTL_PERC[lotl])  # entityclasses base_set 1 + Perk perc_add
        elif d["tag"] == "bonusCropHarvest":
            factor = 1.0 if lotl >= 2 else 0.0     # progression base_set 1 ab Stufe 2
        elif d["tag"] == "farmerBonusHarvest":
            factor = 0.0                           # nur Landwirt-Outfit (hier aus)

        count = (d["min"] + d["max"]) / 2.0 * factor
        if count <= 0:
            continue
        value = count * d["prob"]
        if event == "Destroy" or d["name"].startswith("planted"):
            seed += value
        else:
            fruit += value
    return fruit, seed


def report(blocks, name, lotl, boots):
    drops = blocks.get(name)
    if not drops:
        return None
    # Vanilla: Destroy (Schlüssel 0) mit Basis 1, Harvest (Schlüssel 2) mit Basis 0.
    fruit_v, seed_v = expected(drops, "Harvest", 0.0, lotl, boots)
    df, ds = expected(drops, "Destroy", 1.0, lotl, boots)
    fruit = fruit_v + df
    seed = seed_v + ds
    # Mod bis 1.0.2: beide Tabellen mit Basis-Faktor 1.
    fruit_o, seed_o = expected(drops, "Harvest", 1.0, lotl, boots)
    seed_bug = seed_o + ds
    return fruit, seed, seed_bug


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else next(
        (p for p in DEFAULT_XML if os.path.isfile(p)), None)
    if not path or not os.path.isfile(path):
        print("blocks.xml nicht gefunden – Pfad als Argument angeben.")
        return 2

    blocks = load_blocks(path)
    print("blocks.xml:", path)
    print()
    header = "%-30s %-22s %-24s %-24s" % ("Block (aus Samen)", "Szenario",
                                          "vor Fix (1.0.2)", "mit Fix")
    print(header)
    print("-" * len(header))

    names = sorted(n for n in blocks if n.endswith("3HarvestPlayer")) + \
        [n for n in ("mushroomBiome2", "mushroomBiome4", "mushroomRadiated02", "mushroomRadiated04")
         if n in blocks]

    ok = True
    for name in names:
        r0 = report(blocks, name, 0, 0.0)
        # geprueft werden die Beet-Kulturen mit eigener Tabelle; die Schneebeere hat keine
        # (bekannte Vanilla-Ausnahme), POI-Pilze stehen ausserhalb des Sollwerts.
        checked = name.endswith("3HarvestPlayer") and r0 is not None
        for lotl, boots, label in ((0, 0.0, "ohne Perk/Stiefel"),
                                   (2, 0.0, "LotL 2/3"),
                                   (3, 0.2, "LotL 3/3 + Stiefel Q6")):
            r = report(blocks, name, lotl, boots)
            if r is None:
                continue
            fruit, seed, seed_bug = r
            print("%-30s %-22s %-24s %-24s" % (
                name if label == "ohne Perk/Stiefel" else "",
                label,
                "%.2f Frucht / %.2f Samen" % (fruit, seed_bug),
                "%.2f Frucht / %.2f Samen" % (fruit, seed)))
        if checked and r0 is not None:
            want = (2.0, 0.5, 1.5)
            if any(abs(a - b) > 1e-6 for a, b in zip(r0, want)):
                ok = False
                print("   !! Sollwertabweichung", name, r0, "erwartet", want)
        print()
    print("Kernaussage:", "ohne Stiefel 0,5 Samen je Beet-Pflanze (Vanilla); "
                          "vor dem Fix 1,5 (dreifach)." if ok else "PRÜFUNG FEHLGESCHLAGEN")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
