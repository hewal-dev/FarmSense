# FarmSense – Arbeitsprotokoll (Fortsetzung durch jede Session)

**Stand:** 2026-10-12 · Version **1.0.0** (Nexus/GitHub, veröffentlicht) · Status: **im Livetest bestätigt** (Wachstumsfeld, E-Ernte, Wildwuchs, Vanilla-Erntemengen)
**Neuester Stand (2026-10-13):** **Prozent-Fix §4x** (Prozent gegen das Gesamtwachstum statt gegen die Restkette) gebaut, geprüft und synchronisiert — DLL `de901eec…`; In-Game-Gegenprobe und Release 1.0.1 offen.
**Arbeitsordner:** `/home/hewal/Hermes/7d2d-v3-mods/FarmSense/` · **Livetest:** `/home/hewal/7 Days/0 einzelne mods/FarmSense/`
**Spielversion (Belege):** V 3.3.0 (b18), Linux/Mono

> Dieses Dokument ist der Einstieg für jede neue Session. Es enthält Ziel, Entscheidungen, belegte
> Mechanismen, den Prüfstand und die offenen Punkte. Vor Änderungen den Abschnitt „Stand der
> Verifikation" lesen und danach hier wieder eintragen.

---

## 1. Ziel und Umfang (mit Hewal im Interview geklärt)

| Frage | Entscheidung |
|---|---|
| Feature-Umfang | **alle drei**: (a) Wachstumsanzeige %, (b) Ernten mit E, (c) wilde Kulturen im Kiefernwald |
| E-Ernte technisch | **C#/Harmony** (nicht der XML/Game-Event-Weg) – nutzt die Vanilla-Drop-Tabellen |
| Anzeigeort | **eigenes kleines Feld am Bildschirmrand**, sichtbar beim Anvisieren, **auch mit leerer Hand** |
| Bestehende Welten | wilde Pflanzen **wachsen nach** (Nachwachsen-System rund um den Spieler) |
| Nach der E-Ernte | **alle Pflanzen verschwinden** (kein Nachwachsen auf Stufe 1), wilde Pflanzen erscheinen wie Vanilla-Wildpflanzen neu |
| Anzeige-Inhalt (Default, da Interview-Runde 2 abgebrochen) | % + Restzeit, Wachstumsstufe/Gesamtdauer, Warnhinweise (Licht/Boden), auch Bäume und wilde Pflanzen – alles per Config abschaltbar |
| Name/Sprache (Default) | `FarmSense`, Texte **DE + EN** (`Config/Localization.csv`) |

## 2. Dateien und ihr Zweck

```
FarmSense/
├── ModInfo.xml                     Name/Version/Author/SkipWithAntiCheat=true
├── Config.xml                      alle Schalter (Ernte, HUD, Wildwuchs) – wird beim Start gelesen
├── PROTOKOLL.md                    dieses Dokument
├── build.sh                        mcs-Build gegen die installierten v3.3-Assemblies → dist/FarmSense
├── sync.sh                         Trockenlauf + rsync in den Livetest (Harmony ausgeschlossen)
├── src/
│   ├── FarmSenseMain.cs            IModApi-Einstieg, Harmony-Injektion, Startmeldungen
│   ├── FarmSenseConfig.cs          liest Config.xml (XmlDocument, ohne LINQ)
│   ├── CropRegistry.cs             Pflanzenregister (Block.list → Next-Ketten) + Zustandsauswertung
│   ├── HarvestLogic.cs             E-Erntelogik + 3 Harmony-Patches (Aktivierungskommandos)
│   ├── NetPackageFarmSenseHarvest.cs  MP: Client → Server Ernte-Anfrage
│   ├── HudSystem.cs                HUD-Controller (XUiC_FarmSenseHud) + Texte + Prompt-Patch
│   └── WildGrowthSystem.cs         Nachwachsen-System + zentraler Tick-Patch
├── Config/
│   ├── biomes.xml                  wilde Kulturen als Biome-Dekoration im Kiefernwald (nur neue Welten)
│   ├── Localization.csv            DE/EN-Texte (v3-Format, 20 Spalten)
│   └── XUi_InGame/{windows,xui}.xml  HUD-Fenster + Fenstergruppe
└── dist/FarmSense/                 Build-Ausgabe (= 7 Dateien, wird synchronisiert)
```

## 3. Belegte Mechanismen (alle gegen V 3.3.0 b18 verifiziert)

1. **Restzeit/Prozent** – Scheduled Block Updates:
   `BlockPlantGrowing.addScheduledTick` → `world.GetWBT().AddScheduledBlockUpdate(pos, blockID, ticks)`
   mit `scheduledTime = GameTimer.Instance.ticks + ticks`.
   → Restzeit der Stufe = `(entry.scheduledTime − GameTimer.Instance.ticks) / 20` (20 Ticks/s).
   Eintrag finden über `WorldBlockTickerEntry.ToHashCode(pos, blockID)` als Schlüssel in
   `WorldBlockTicker.scheduledTicksDict` (kein Query-API vorhanden).
   Stufendauer = `BlockPlantGrowing.GetTickRate() / 20` (`= growthRate × CropGrowthModifier × 1200/20`).
2. **Pflanzenregister** – `Block.list` (public static Block[]) → alle `BlockPlantGrowing` ohne Vorgänger
   sind Kettenwurzeln; `nextPlant` folgt bis zur Endstufe. Zusätzlich werden Blöcke mit „Harvest“ im
   Namen als Einzel-Endstufen registriert (wilde Kulturen stehen in keiner Kette).
3. **E-Ernte** – `Block.HasBlockActivationCommands` + `Block.GetBlockActivationCommands` (Postfix,
   Kommando `farmSenseHarvest`, Icon `knife`, highlighted) und
   `Block.OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)`
   (Prefix). E kurz drücken öffnet die Vanilla-Radial (vorausgewählt = unser Kommando), Loslassen erntet.
4. **Ertrag** – eigene Auswertung der Vanilla-Drop-Tabellen `Block.itemsToDrop[Harvest]` **und** `[Destroy]`
   (Destroy liefert die Samen mit `prob`, z. B. 0,5 bei `plantedCorn3HarvestPlayer`). Items gehen ins
   Inventar (`player.bag.AddItem`), sonst per `GameManager.ItemDropServer` auf den Boden.
5. **Boden-/Lichtregel** – `BlockPlantGrowing.CanGrowOn` vergleicht
   `ground.Block.blockMaterial.FertileLevel >= BlockPlant.fertileLevel` (FertileLevel 0 = egal);
   Licht über `BlockPlant.lightLevelGrow` + `World.GetBlockLightValue`/`IsOpenSkyAbove`.
   Wichtig: Stufe 1 braucht FertileLevel 15 (Farmpflug), die Endstufen nur 1 (Waldboden) –
   deshalb kann eine wilde Pflanze auf Waldboden **nicht** auf Stufe 1 zurückgesetzt werden.
6. **Wildwuchs** – server-/hostseitig, alle `WildGrowth_IntervalSeconds`: Zufallsposition im Radius,
   Prüfungen (Chunk geladen via `GetChunkFromWorldPos`, Biomname `BiomeDefinition.m_sBiomeName`,
   Naturboden `Block.shape.IsTerrain()` + `FertileLevel>=1`, drei Luftblöcke, `IsOpenSkyAbove`,
   Licht ≥ `WildGrowth_MinLight`, Chunk-Obergrenze), dann `world.SetBlockRPC(pos, plantBv)`.
7. **Anzeige** – `EntityPlayerLocal.HitInfo` (WorldRayHitInfo mit `hit.blockPos`/`hit.blockValue`) liefert
   das anvisierte Ziel **ohne Item-Action** (also auch mit leerer Hand). Fenster wird zur Laufzeit
   geöffnet/geschlossen wie Vanilla `focusedBlockHealth`: `windowManager.Open("farmSenseHud", false)`.

## 4. Stand der Verifikation

| Prüfung | Ergebnis |
|---|---|
| Build (`mcs`, Mono) | ✅ erfolgreich, 0 Fehler/Warnungen |
| DLL-Hash | `8f7ed524ccb1d2640a48aec4aeadeb986fc3e004fc14eee2ceeab3df54856d65` |
| Harmony-Patch-Bindung (Probe-Skript, 5 Patch-Klassen) | ✅ „5 aufgelöst und angewendet“ |
| Typeref-Abgleich gegen Assembly-CSharp v3.3 | ✅ keine fehlenden Typen (nur 3 bekannte Falschmeldungen: `DictionaryList'2`, `SingletonMonoBehaviour'1`, `XMLData.Item.ItemData`) |
| Assembly-Referenzen | mscorlib, Assembly-CSharp, LogLibrary, System.Core, System.Xml, **0Harmony 2.13**, UnityEngine.CoreModule – keine Harmony/MonoMod im Paket |
| XML-Ziele (XPath gegen Vanilla-Dateien geprüft) | ✅ `worldgeneration/biomes/biome[@name='pine_forest']/subbiome[1|2]/decorations` existieren; Root `windows` und `xui` passen; alle vier Wildpflanzen-Blöcke existieren |
| Livetest-Sync | ✅ dry-run + echtes rsync, Dev- und Live-DLL identischer SHA-256, keine Löschungen |
| **In-Game-Test** | ⬜ **offen** – siehe Abschnitt 5 |

## 4b. Livetest-Befund 2026-10-12 und Fixes (WICHTIG – zuerst lesen)

**Befund** (Log `~/.local/share/7DaysToDie/logs/output_log_client__2026-10-12__11-37-20.txt`):
„Ingame funktioniert gar nichts“ – im Log standen genau zwei Ursachen:

1. Zeile 504: `WRN [FarmSense] Block.list ist null – kein Pflanzenregister.` und Zeile 506
   `bereit. Erkannte Pflanzenblöcke=0 …` → das Register war leer, damit ist `IsReadyBlock(...)`
   immer `false`: **kein E-Kommando, keine HUD-Auswertung**. (Die Harmony-Patches waren gebunden –
   nur die Daten fehlten.)
2. Zeilen 1159/1160: `WRN Type:FarmSenseHud was missing when we looked it up via Type.GetType()`
   und `ERR [XUi] Controller 'FarmSenseHud' not found, using base XUiController` → das HUD-Fenster
   bekam nur einen Basis-Controller, also keine Bindings.

**Ursachen (belegt, nicht vermutet):**

1. **`Block.list` ist zur `InitMod`-Zeit leer.** Die Blöcke entstehen erst nach dem XML-Laden
   (im selben Log: `Block IDs total 24810 …` erst ~48 s später als `InitMod`).
   → Register jetzt **lazy**: `CropRegistry.EnsureBuilt()` in `TryGetInfo`/`IsReadyBlock`/`Get` und im
   Tick; `Build()` bricht bei leerer Liste ab, ohne sich als „fertig“ zu markieren. Im Tick erscheint
   nach dem Laden: `[FarmSense] Pflanzenregister bereit: <N> Pflanzenbloecke erkannt.`
2. **Mod-XUi-Controller sind nur assembly-qualifiziert auflösbar.** `XUiFromXml::parseController`
   ruft `ReflectionHelpers.GetTypeWithPrefix("XUiC_", <controller-Attribut>)`, und das macht
   `Type.GetType(...)` — **ohne Assembly-Angabe sucht das nur in Assembly-CSharp + mscorlib**, ein
   Mod-Typ ist damit grundsätzlich unauffindbar (gleiches gilt für namespacet geschriebene Namen).
   → Controller liegt jetzt im **globalen Namespace** (`src/FarmSenseHudController.cs`) und das XML
   nutzt `controller="FarmSenseHud, FarmSense"`.
   Per Mono-Test gegen die echte DLL verifiziert:
   `Type.GetType("XUiC_FarmSenseHud, FarmSense")` → `XUiC_FarmSenseHud` (`IsAbstract=False`);
   `FarmSense.XUiC_FarmSenseHud, FarmSense` und `XUiC_FarmSenseHud` ohne Assembly → **NULL**.

**Weitere Erkenntnis zur E-Ernte:** Vanilla-Kulturen setzen kein `ActivationDistance`; dann gilt
`Constants.cCollectItemDistance` (Sammel-Reichweite, ca. 2,5 m) — Bäume haben dagegen
`ActivationDistance=12`. E erntet also nur in unmittelbarer Nähe. Falls das zu knapp wirkt:
`<property name="ActivationDistance" value="5"/>` an den Kulturblöcken ergänzen (noch nicht gemacht,
bewusst als Einzeländerung nach dem Test).

**Prüfstand nach dem Fix:** Build fehlerfrei · DLL `96c33ee0634c50b91c77220e353bb135f0d9d9082917334a8cbf8b9813de34ec`
· Harmony-Probe **5/5** · Sync (dry-run + rsync) mit identischem Hash · **zweiter In-Game-Test offen**.

## 4c. Zweiter Livetest-Befund 2026-10-12 (nachmittags) und Fixes

**Befund** (Log `output_log_client__2026-10-12__12-52-14.txt`):
* **Das Register funktioniert jetzt**: Zeile 1252/1253 → `Pflanzenregister: 58 Ketten, 27 Einzelbloecke,
  131 Bloecke gesamt` + `Pflanzenregister bereit: 131 Pflanzenbloecke erkannt.` (der Lazy-Build greift,
  und `FarmSenseTick.Update` läuft – sonst käme diese Zeile nie).
* **Der XUi-Controller wird jetzt aufgelöst**: keine `Type:… missing`/`Controller … not found`-Meldung mehr.
* Hewal: „Immer noch nichts“ – und wichtig: **der Pflanzenname im Vanilla-Prompt ist verschwunden.**
  Das ist der Beweis, dass `HasBlockActivationCommands` bereits `true` liefert (dieser Zweig
  überspringt in `PlayerMoveController.TryHandleBlock` die Namens-/Beschreibungsanzeige). Der Prompt
  blieb aber leer, weil der Mod-Text nur bei `Hud_UseVanillaPrompt=true` geliefert wurde
  (Standard war `false`) und eigene `CustomCmds` fehlen.

**Erkenntnisse und Fixes:**
1. **Bindings werden nicht von selbst nachgezogen.** `XUiController.Update` ruft `RefreshBindings()`
   nur bei einem Eingabestil-Wechsel; Vanilla-Controller mit Bindings (z. B. `XUiC_ItemInfoWindow`)
   prüfen `IsDirty` in ihrem eigenen `Update` und rufen `RefreshBindings()` selbst.
   → Der HUD-Controller ruft jetzt nach dem Setzen der Felder `RefreshBindings()` auf **und** schreibt
   die Labels/Balken zusätzlich direkt in die Views (`XUiV_Label.Text`, `XUiV_Sprite.fillAmount`).
2. **Prompt-Text ist Pflicht, sobald die Mod das Aktivierungskommando setzt.**
   → `Hud_UseVanillaPrompt` steht für den Test auf `true`; der Text enthält den Hinweis
   „Drücke {0} zum Ernten“ (Vanilla ersetzt `{0}` mit der Tastenanzeige).
3. **Aktivierungs-Reichweite:** Kulturen haben kein `ActivationDistance` (→ Sammel-Reichweite ~2,5 m).
   Neue Modlet-Datei `Config/blocks.xml` setzt `ActivationDistance=5` an `cropsGrowingMaster` und
   `cropsHarvestableMaster` (gilt für alle Kulturen inkl. Wildpflanzen).
4. **Diagnose-Modus:** `LogDebug=true` in `Config.xml`. Neue `[FarmSense][DIAG]`-Zeilen (max. 1×/s):
   `HitInfo ist null` / `Ziel ungueltig` / `angvisiert: Luft bei …` / `kein Pflanzenblock: <Name> (id …)`
   / `Pflanze: <Block> Stufe x/y waechst=… reif=… pct=… rest=…s warn=…` / `HUD-Inhalt: …`
   / `HUD-Fenster geoeffnet.` / `OnBlockActivated-Kommando: '<name>' bei …` / `<Block> ist erntbar`.

**Stand nach dem Fix:** DLL `929d8742a567e76a636853142c92e0dc78634f9e41bf642e3ba4e5188fb50e85`,
Harmony-Probe 5/5, Sync inkl. `Config/blocks.xml` (8 Dateien im Livetest), **dritter In-Game-Test offen**.

## 4d. Dritter Livetest-Befund 2026-10-12 (spät) und Fixes

**Log-Beweis** (`output_log_client__2026-10-12__12-59-*.txt`, der Lauf mit dem Hewal getestet hat):
* `Geerntet: plantedCotton3Harvest … (2 Stapel)` → **E-Ernte läuft** (von Hewal bestätigt: Wildpflanzen
  lassen sich aufsammeln).
* `HUD-Inhalt: Chrysantheme 3 (Wildpflanze) | Chrysantheme 3: reif · Ernten mit E` → das HUD **war**
  sichtbar; die vom Nutzer gemeldete „Zahl“ ist die **Stufenziffer aus dem Vanilla-Blocknamen**
  (`plantedChrysanthemum3Harvest` → „Chrysanthemum Plant 3 (POI)“; DE „Chrysantheme 3“), nicht der Ertrag.
* `Pflanze: plantedCotton2 Stufe 2/3 waechst=True reif=False pct=81 rest=714s` und
  `HUD-Inhalt: Pilzsporen | Pilzsporen: 93 % – reif in 5 min · Stufe 2/3` → auch **selbst gepflanzte**
  Kulturen werden berechnet und beschrieben. Die Wertezeile war bei Kulturen nur deutlich länger und
  wurde im schmalen 320-px-Fenster abgeschnitten → Nutzer sah dort nichts.

**Fixes:**
1. **Anzeigename datengetrieben** (`CropRegistry.WildDisplayName`): Für einzeln stehende Ernte-Blöcke
   (wilde Kulturen) wird der lokalisierte Name des **Ernte-Erzeugnisses** aus der Harvest-Drop-Tabelle
   genommen (`resourceCotton` → „Baumwolle“ statt „Baumwolle 3“); Fallback = Blockname ohne
   Klammerzusatz und ohne angehängte Stufenziffer.
2. **Texte entzerrt** (`HudSystem.BuildTexts`): Zeile 1 = Name (wild: „(Wildpflanze)“), Zeile 2 nur
   Werte – `81 % – reif in 11 h 54 min`, `reif · Ernten mit E`, `abgestorben`. Kein doppelter Name mehr;
   Stufe nur noch bei `Hud_ShowStage=true` (neu, Standard `false`) als „· Stufe 2/3“.
3. **Fenster vergrößert** (`windows.xml`): 320×92 → **430×96**, Labels 406 px breit, Schrift 27/23/20,
   Balken 402 px. Neue Lokalisierungs-Keys: `farmSenseGrowDetail`, `farmSenseReadyShort`,
   `farmSenseDeadShort`, `farmSenseStageShort`, `farmSenseWarnUnknownShort`.
4. **Ernte-Sound** (`HarvestLogic`): Vanilla-Verhalten nachgebildet.
   * Zerstoerungs-Geraeusch des Pflanzenblocks: `GameManager.PlaySoundAtPositionClient(pos, "plantdestroy", AudioRolloffMode.Logarithmic, 20, 1f)`.
     Beleg: Material `Mplants` hat `surface_category=plant` → SoundDataNode `plantdestroy` (sounds.xml);
     `GameManager::PlaySoundAtPositionClient(Vector3, string, AudioRolloffMode, int32, float32)` existiert.
   * Sammel-Sound/Popup: `XUiM_PlayerInventory.AddItem(ItemStack, bool _playCollectSound)` — im IL wird bei
     `true` `Audio.Manager.PlayInsidePlayerHead("item_pickup", -1, 0, false, false)` gerufen und der Stapel
     in `XUi.CollectedItemList` eingetragen. Fallback bleibt `player.bag.AddItem`, dann `ItemDropServer`.
   * Neue Config-Schalter: `Harvest_PlaySound`, `Harvest_PlayCollectSound` (Standard je `true`).
   * Build-Referenz ergänzt: `UnityEngine.AudioModule.dll` (für `AudioRolloffMode`).
5. **Offen (v0.2):** XP-/Challenge-Gutschrift (`GameUtils.collectHarvestedItem` ruft
   `QuestEventManager.Current.HarvestedItem(...)`, `player.Progression.AddLevelExp(..., "_xpFromHarvesting", ...)`),
   MP-Soundübertragung (Vanilla nutzt `NetPackageSoundAtPosition`).

**Stand nach dem Fix:** DLL SHA-256 `569997e10ae4391877c4d6cada20c9714421a83785fc0858db562568a7692de2`,
Harmony-Probe 5/5, Typeref-Prüfung ohne Befund, XML/CSV valide, Sync (8 Dateien) mit identischem Hash.

## 4e. Vierter Livetest-Befund 2026-10-12 (13:22) — HUD-Fenster stand neben dem Bildschirm

**Nutzer-Bild + Log** (`output_log_client__2026-10-12__13-17-32.txt`):
* Beim Anvisieren einer **selbst gepflanzten Heidelbeere** (`plantedBlueberry2`, Stufe 2/3) läuft die
  Erkennung durchgehend: `Pflanze: plantedBlueberry2 Stufe 2/3 waechst=True reif=False pct=84 rest=612s`
  und `HUD-Fenster geoeffnet.` → Inhalt war korrekt, nur **nicht sichtbar**. Der Nutzer sah am rechten
  Rand nur die **ersten 1–3 Zeichen** jeder Zeile („C…“ / „re…“).
* **Ursache (jetzt belegt):** Bei `anchor="BottomRight"` ist `pos`-x der Abstand der **linken Fensterkante**
  von der rechten Bildschirmkante. Mit `pos="-28,150"` lag das 430 px breite Fenster also bis auf 28 px
  **außerhalb** des Bildes. Gegenprobe aus Vanilla: `windowCompass` = `anchor="Top" pos="-240,-9" width="480"`
  → genau zentriert, also misst x die linke Kante ab dem Ankerpunkt.
* **Fix:** `pos="-458,155"` (= Breite 430 + 28 px Rand) in `Config/XUi_InGame/windows.xml`.
* **Zusatz:** Der Vanilla-Prompt liefert denselben Text (im Bild sichtbar: „Chrysantheme: reif · Drücke (E) zum
  Ernten“ — der Anzeigename ohne Stufenziffer ist damit bestätigt). Neue Diagnosezeile `Prompt-Text: …` im
  `Patch_GetActivationText`, damit der nächste Log zeigt, ob auch **wachsende** Pflanzen einen Prompt-Text bekommen.

**Stand:** DLL SHA-256 `8fea023f6197991b2608c74b616bc8d3d39a3549e39b560fccc7e0ce9313dbe1`,
Harmony-Probe 5/5, XML valide, Sync mit identischem Hash.

## 4f. Layout-Wunsch 2026-10-12 (14:00) — Stufe kurz unter dem Namen, kein doppelter Name

**Hewals Wunsch:** Stufe unterhalb des Pflanzennamens, „zentriert etwas unterhalb“, kurz und knapp („2/3“);
der Pflanzenname soll im Mod-Feld nicht wiederholt werden, weil er ohnehin direkt darüber steht.

**Zusammenhang (im Log belegt):** Bei **wachsenden** Pflanzen bietet die Mod das E-Kommando noch nicht an
(`Patch_HasBlockActivationCommands` setzt `__result=true` nur, wenn `HarvestLogic.CanHarvest(...)` gilt),
deshalb läuft der Vanilla-Zweig und die Bildschirmmitte zeigt den **Vanilla-Blocknamen** – im Log sichtbar
daran, dass für wachsende Pflanzen **keine** `Prompt-Text:`-Zeile auftaucht, für reife dagegen schon
(`GetActivationText` wird nur im Aktivierungszweig gefragt). Genau diese Namenszeile steht direkt über dem
Feld.

**Umsetzung:**
* Kopfzeile des Feldes = **nur die Stufe in Kurzform** (`2/3`, neuer Lokalisierungstext `farmSenseStageShort`),
  zentriert, klein (20) und leicht gedämpft; Stufe erscheint nur bei mehrstufigen Kulturen (`StageCount > 1`).
* Zweite Zeile = Werte wie bisher (`86 % – reif in 9 min`, `reif · Ernten mit E`), jetzt **zentriert**.
* **Kein Pflanzenname** mehr im Feld. Neue Config `<Hud_ShowName>false</Hud_ShowName>` holt ihn zurück
  (dann wandert die Stufe als Suffix „ · 2/3“ in die Wertezeile); `<Hud_ShowStage>true</Hud_ShowStage>` ist
  jetzt Standard.
* `XUiC_FarmSenseHud`: Feld/Binding `title` → `stage`, Label `lblTitle` → `lblStage`; Binding-Namen im XML
  entsprechend `{stage}`.

**Stand:** DLL SHA-256 `6273b2597f59ecb7b680897a503a65d1688069fbe37b2e6d8e5a2db78185d7a4`,
Harmony-Probe 5/5, Typeref-Abgleich ohne Befund, XML valide, Sync mit identischem Hash.

## 4g. Position 2026-10-12 (abends) — Feld sitzt jetzt zentriert unter dem Pflanzennamen

**Wunsch (Bild mit Einzeichnung):** Das Feld soll **direkt unter** der zentrierten Namenszeile stehen
(„Goldrute (Anbau)“), nicht unten rechts.

**Umsetzung (`Config/XUi_InGame/windows.xml`):**
* `anchor="BottomRight" pos="-458,150"` → **`anchor="Center" pos="-215,-26"`** (x = -(Breite/2) ⇒ waagerecht
  zentriert; y negativ = nach unten, genau unter den Vanilla-Prompt `interactionPrompt` bei `0,55`).
* Hintergrund dezenter (Alpha 170 → 130), Balken 2 px höher, Warnzeile bei -68 (Höhe des Feldes 90).
* Reine XML-Änderung, DLL unverändert (`6273b259…`).

**Pitfall (diesmal selbst erlebt):** `sync.sh`/`rsync` kopieren aus **`dist/FarmSense/`** — dieses Verzeichnis
erzeugt `build.sh`. Wird nur `Config/…` im Entwicklungsordner geändert und **ohne** `./build.sh` synchronisiert,
bleibt das Ziel still auf dem alten Stand (die Datei hat im Ziel denselben Hash wie vorher, rsync meldet
nichts Verdächtiges). **Regel: nach *jeder* Änderung — auch reinen XUi-/Config-XML-Dateien — erst
`./build.sh`, dann syncen und anschließend die geänderte Zeile im **Ziel** gegenprüfen.**

## 4h. Anzeige-Tempo 2026-10-12 — jetzt so schnell wie die Vanilla-Namenszeile

**Frage Hewal:** Kann das Feld genauso schnell erscheinen wie die Vanilla-Anzeige mit dem Pflanzennamen?

**Ursache (im IL belegt):** Der Haken sitzt schon an der richtigen Stelle — `GameManager.Update` ruft
`gmUpdate`, dieses ruft `UpdateTick` (einziger Aufrufer), das HUD wurde also ohnehin pro Frame angestossen.
Gebremst hat allein die eigene Drossel: `Hud_UpdateInterval = 0.25` ⇒ bis zu **250 ms** Verzögerung plus
das Auf-/Zuklappen des Fensters. Die Vanilla-Namenszeile läuft ungedrosselt im Interaktionspfad.

**Fixes:**
* `Hud_UpdateInterval` Standard **0** = „jeden Aufruf/jeden Frame“ (`<= 0` wird im Tick als Sofort-Pfad
  behandelt); >0 bleibt als Sekunden-Intervall erhalten. `Config.xml` entsprechend auf `0`.
* `XUiC_FarmSenseHud.Set` hat jetzt eine **Änderungserkennung** (`hasShown/shownStage/shownDetail/
  shownWarning/shownFill`): nur bei echtem Unterschied werden `RefreshBindings()` und die direkten
  View-Schreibvorgänge ausgeführt — der Frame-Aufruf kostet dadurch praktisch nichts.
* `HudSystem` hält das Feld nach Zielverlust **0,2 s** vor (`hideDelay`), damit einzelne ungültige
  Frames (`Ziel ungueltig (bHitValid=false)` tritt im Log sporadisch auf) kein Flackern erzeugen.

**Stand:** DLL SHA-256 `9ae654da46335ce772e9382500b8a96c94ef4e47a77c247567355ae03a572356`,
Harmony-Probe 5/5, XML valide, Sync mit identischem Hash (Ziel-Gegenprüfung inkl. `Hud_UpdateInterval=0`).

## 4i. Ausblenden 2026-10-12 — Feld verschwindet sofort wie die Vanilla-Anzeige

**Wunsch Hewal:** Das Feld soll ohne Vorlaufzeit verschwinden, sobald man nicht mehr darüber ist — wie die
Vanilla-Anzeige.

**Umsetzung:** Die 0,2-s-Vorhaltezeit aus 4h ist jetzt konfigurierbar: neue Option `Hud_HideDelay`
(Standard **0** = sofort ausblenden). `HudSystem` vergleicht nur noch `hideDelay >= cfg.Hud_HideDelay`;
bei 0 wird also im ersten Frame ohne gültiges Pflanzziel geschlossen (`fade_window="false"` bleibt,
damit auch keine Ausblend-Animation läuft). Bleibt als Schalter für den Fall, dass einzelne ungültige
Frames Flackern erzeugen (dann z. B. `0.1`).

**Stand:** DLL SHA-256 `5b7d0e88a6dea433fdd24163ec09ef70867f17ea64201c275b308ad5754f6b19`,
Harmony-Probe 5/5, XML valide, Sync inkl. Ziel-Gegenprüfung (`Hud_HideDelay=0`, `Hud_UpdateInterval=0`).

## 4j. Samen-Inflation der E-Ernte gefunden und behoben (2026-10-12)

**Hewals Verdacht:** Samenernte bei selbst gepflanzten Pflanzen wirkt deutlich erhöht. **Bestätigt.**

**Belege (V 3.3.0 b18, `Data/Config/blocks.xml`) — Beispiel `plantedCorn3HarvestPlayer`:**

| Eintrag | Tag | Vanilla-Bedingung |
|---|---|---|
| `Harvest foodCropCorn` count 2 | `cropHarvest` | immer |
| `Harvest foodCropCorn` prob 0.5 | `bonusCropHarvest` | nur Perk **Farmer** Stufe 2/3 (`progression.xml:3533` → `HarvestCount base_set 1`) |
| `Harvest foodCropCorn` count 1 | `farmerBonusHarvest` | nur per Werkzeug-Roll (`items.xml` `RequirementItemTier` + `RandomRoll`, 10 % bei Tier 1) |
| `Harvest plantedCorn1` count 1 | `farmerBonusSeed` | nur per Werkzeug-Roll (**2 %** bei Tier 1 … 20 % bei Tier 6) |
| `Destroy plantedCorn1` prob 0.5 | – | immer (Rückgewinn des Setzlings) |

Der Spieler-Entity setzt zusätzlich `HarvestCount base_set 0` für `bonusCropHarvest`/`farmerBonusHarvest`
(`entityclasses.xml:275`). Der Ernte-Pfad `GameUtils.HarvestOnAttack(ItemActionData, Dictionary<string,
ItemActionAttack.Bonuses> ToolBonuses)` bekommt diese Boni vom **gehaltenen Werkzeug** — die E-Ernte hat
keine Item-Action, würfelte aber **alle** Einträge: dadurch 1 Samen **garantiert** (`farmerBonusSeed`)
plus 50 % Setzling (`Destroy`) ≈ **1,5 Samen statt ~0,5** und zusätzlich +1 Frucht mit 50 %.

**Fix:** neue Option `Harvest_IgnoreBonusTags` (Standard `true`). `HarvestLogic.IsBonusTag` überspringt
Einträge, deren Tag nicht `cropHarvest`/`wildCropsHarvest` ist. Ergebnis pro Ernte: 2 Mais + 50 % Setzling
= wie Vanilla mit bloßer Hand. Gegenprobe per Skript über **alle 13** `…HarvestPlayer`-Blöcke: nur der
Basis-Eintrag (prob 1) und der `Destroy`-Setzling bleiben, die drei Bonus-Einträge fallen weg.
Neue Diagnosezeile `Drops <Block>: N Stapel | uebersprungen (Bonus-Tag): …`.

**Offen (v0.2):** `bonusCropHarvest` kommt aus der **Spieler-Progression** (Perk Farmer), nicht vom
Werkzeug — das ließe sich über den `HarvestCount`-Wert des Spielers für diesen Tag nachbilden. Solange
das fehlt, liefert die E-Ernte mit hohem Farmer-Level etwas **weniger** als Vanilla (nie mehr).
`Harvest_IgnoreBonusTags=false` stellt das alte Verhalten wieder her (nicht empfohlen).

**Stand:** DLL SHA-256 `0c905abdb358a300fd2f3419cffc6a4235edefd7ed378ce1de75b60f20515245`,
Harmony-Probe 5/5, XML valide, Sync inkl. Ziel-Gegenprüfung (`Harvest_IgnoreBonusTags=true`).

## 4k. Ertrag jetzt 1:1 wie Vanilla — Ernte läuft über den Spiel-Pfad (2026-10-12)

**Wunsch Hewal:** Der E-Ertrag soll exakt wie beim Ernten mit Werkzeug skalieren, inklusive Perk.

**Weg (im IL belegt, V 3.3.0 b18):** Statt die Drop-Tabelle selbst auszuwerten, ruft die Mod jetzt den
Spiel-Pfad selbst auf: `GameUtils.HarvestOnAttack(ItemActionData, Dictionary<string, ItemActionAttack.Bonuses>)`
(public static, keine Nebenwirkungen außer der Item-Vergabe). Das Spiel macht darin alles Weitere:

* Für jeden Drop-Eintrag mit `tag` wertet es
  `EffectManager.GetValue(PassiveEffects.HarvestCount, gehaltenesItem, Basiswert, Spieler, null, FastTags<TagGroup.Global>.Parse(entry.tag), …)`
  aus → **Perk „Farmer“** (`bonusCropHarvest`) **und** die Werkzeug-Tier-Effekte
  (`farmerBonusHarvest`/`farmerBonusSeed`) skalieren genau wie beim Schlagen.
* `prob`, `minCount`/`maxCount` und die Ausgabemodifikatoren
  (`XUiM_Recipes.SeedDropOutputModifier`/`CropOutputModifier`) werden wie in Vanilla angewandt.
* Vergabe über `GameUtils.collectHarvestedItem(...)` → Inventar, Sammel-Popup/Sound, **XP** und Quest-Zähler.
* Der `ToolBonuses`-Parameter bleibt `null`: er betrifft nur Einträge mit `tool_category`
  (Butcher/Disassemble) — Kulturen nutzen ausschließlich `tag`.
* `bKilled=true` entspricht dem vernichtenden Schlag einer reifen Pflanze → die **Destroy-Tabelle** läuft mit
  (Setzling mit 50 %). Alle Kulturen besitzen eine Destroy-Liste (`cropsHarvestableMaster` →
  `<drop event="Destroy" count="0"/>`), der Vanilla-Sonderfall „Block-Item selbst ausgeben“ greift nie.

**Technik:** `attackDetails` wird kurzzeitig mit einer eigenen `ItemActionAttack.AttackHitInfo`
(`itemsToDrop`, `blockBeingDamaged=bv`, `bBlockHit=true`, `bKilled=true`) bestückt und danach im
`finally` wieder auf den alten Wert gesetzt. Der `ItemActionData` kommt aus
`player.inventory.holdingItemData.actionData` (auch mit leerer Hand vorhanden = Bare-Hand-Item der Engine).
Nur wenn das fehlschlägt, greift der alte eigene Roll (`Harvest_IgnoreBonusTags`, dann konservativ ohne
Bonus-Tags).

**Stand:** DLL SHA-256 `c67d35c618936abe514dbfe2e2c9f987df092f90d53689f9d05bcaa7cefc9519`,
Harmony-Probe 5/5, Typeref- und Signaturabgleich gegen Assembly-CSharp ohne Befund, XML valide, Sync mit
identischem Hash. **Im Spiel noch nicht gegengeprüft** (vierter Test offen): erwartet werden beim Mais
2 Früchte + 50 % Zusatzfrucht (Perk-Stufe ≥ 2) + Setzling 50 % und mit Tier-6-Saatgut entsprechend mehr.

## 4l. Ertrag war gedrittelt — `bKilled=false` ist der Pfad der reifen Ernte (2026-10-12)

**Hewals Befund:** LotL 2/3 + Landwirt-Outfit, trotzdem **konsequent 1 Frucht** (Samen passten).

**Ursache (im IL belegt):** Im zweiten Schleifendurchlauf von `GameUtils.HarvestOnAttack` (Harvest-Tabelle)
hängt die Abgabe an `attackDetails.bKilled`: ist es `true`, rechnet das Spiel
`menge = menge / 3` (mindestens 1, dazu die `resourceScale`-Verrechnung). Mit `bKilled=true` ergab die
Rechnung beim Mais/Aloe (Basis `count=2`, Perk-Faktor **2** ⇒ 4) genau **4/3 = 1** — die beobachtete Menge.
Ohne Perk wäre `2/3 = 0` herausgekommen und der Eintrag ganz entfallen.

`bKilled=true` ist damit nicht „der vernichtende Schlag“, sondern der Pfad für einen **bereits sterbenden**
Block. Die reife Ernte läuft mit `bKilled=false` und voller Menge.

**Fix:** `det.bKilled = false`. Die **Destroy**-Tabelle (Setzling, 50 %) rollt die Mod weiterhin selbst –
Vanilla macht das beim tatsächlichen Zerstoeren des Blocks, hier verschwindet die Pflanze durch den
Mod-eigenen `SetBlockRPC`.

**Neue Diagnosezeile** (`LogDebug=true`, max. 1×/s) zeigt die komplette Rechnung pro Drop-Eintrag:
```
[FarmSense][DIAG] Ertrag plantedAloe3HarvestPlayer | Hand: (leer) | Modifikatoren: harvest=1 crop=1 seed=1 mining=1
  | resourceCropAloeLeaf [cropHarvest] prob=1 count=2..2 faktor=2 => voll 4..4 (bei Kill-Pfad: 1)
  | resourceCropAloeLeaf [bonusCropHarvest] prob=0.5 count=1..1 faktor=1 => voll 1..1 (bei Kill-Pfad: 0)
  | plantedAloe1 [farmerBonusSeed] … | plantedAloe1 [Destroy] …
```
Damit ist in einem Durchgang sichtbar, welcher Faktor wirklich ankommt (Perk, Werkzeug-Tier) und welche
Modifikatoren der Sandbox/Spielstand setzt.

**Erwartung nach dem Fix:** Aloe/Heidelbeere/Kartoffel (Basis 2) mit LotL 2 → **4 Früchte**, mit
`bonusCropHarvest` zusätzlich 1 bei 50 % (Perk-Stufe ≥ 2) und ggf. +1 über die Werkzeug-Tier-Rolle.

**Stand:** DLL SHA-256 `45b21cd63ee1a94d415a9c1f4eecdc4e5d1de04662a6b930786168cadd1cf0f8`,
Harmony-Probe 5/5, Sync inkl. Ziel-Gegenprüfung. In-Game noch offen.

## 4m. Ursache für „gar nichts": Schadens-Skalierung in `collectHarvestedItem` teilte durch 0 (2026-10-12)

**Befund Hewal:** Nach dem `bKilled=false`-Fix kam **gar nichts** mehr an; der Log zeigte aber weiter
`Geerntet: plantedBlueberry3HarvestPlayer … (Vanilla-Erntepfad)`.

**Echte Ursache (IL, V 3.3.0 b18):** `GameUtils.collectHarvestedItem(ad, iv, count, prob, _bScaleCountOnDamage)`
skaliert die Menge, wenn `_bScaleCountOnDamage = true`, ueber die Trefferdaten:

```
f      = attackDetails.damageMax / count          // Schaden je Item-Einheit
remain = (int)((FastMin(damageTotalOfTarget, damageMax) - damageGiven) / f + 0.5f)
...
count  = Mathf.Min(count, …)
```

`HarvestOnAttack` uebergibt bei der **ersten Rate** `true`, bei der **zweiten Rate (beim Kill)** `false`.
Mit meinen selbst gebauten `AttackHitInfo`-Werten (`damageMax = 0`) wurde `f = 0` → Division durch 0 →
`count` zerstoert → **keine Items**. In der vorigen Version (`bKilled=true`) kam daher nur die zweite,
nicht schadensskalierte Rate durch: `gesamt/3` = 4/3 = **1 Frucht** — exakt Hewals Beobachtung „konsequent 1“.
Und mit `bKilled=false` fiel diese zweite Rate weg → **gar nichts**.

**Korrekte Vanilla-Rechnung (komplett aus dem IL rekonstruiert, jetzt selbst implementiert):**

| Schritt | Wert |
|---|---|
| Faktor `f` | 1 (bzw. 0 bei `tool_category` ohne ToolBonus) |
| bei `tag` | `f = EffectManager.GetValue(PassiveEffects.HarvestCount, gehaltenesItem, f, Spieler, null, FastTags.Parse(tag), true,…,1,true,false)` |
| Gesamtmenge | `gesamt = (int)(RandomRange(minCount, maxCount+1) * f)` (≤ 0 → Eintrag entfaellt) |
| Ausgabemodifikator `m` | `XUiM_Recipes.SeedDropOutputModifier` (Item hat `seedMulti`) / `CropOutputModifier` (Block `crop`) / `MiningOutputModifier` (`ore`) / sonst `HarvestingOutputModifier` |
| **Rate 1** | `gesamt − gesamt/3`, vergeben mit `entry.prob` |
| **Rate 2** (nur beim Zerstoeren) | `gesamt/3`, dazu `resourceScale`-Verrechnung (prob/scale, min. 1) |
| Abgabe | `GameUtils.collectHarvestedItem(ad, iv, Max(1, (int)(menge * m)), prob, **false**)` → Inventar, Popup, XP, Quest-Zaehler |

Die Mod rechnet das jetzt selbst (`GrantDropsVanilla`/`RollVanillaTable`/`Grant`) und vergibt **Rate 1 + Rate 2**,
weil E-Ernte und Zerstoeren in einem Schritt passieren → Gesamtmenge wie beim Schlagen.
`_bScaleCountOnDamage` ist dabei **false** (die echten Schadenswerte einer Werkzeug-Attacke sind ohne
Item-Action nicht reproduzierbar).

**Neue Log-Zeile** zeigt die tatsaechlich vergebenen Items:
`[FarmSense] Ernte plantedBlueberry3HarvestPlayer: foodCropBlueberries x4, foodCropBlueberries x1, plantedBlueberry1 x1`

**Erwartung für Hewals Fall** (Heidelbeere, Basis 2, LotL 2, Landwirt-Outfit, Eisen-Spitzhacke in der Hand):
4 Blaubeeren + 1 bei 50 % (`bonusCropHarvest`) + ggf. 1 aus dem Outfit-Wurf + 50 % Setzling.

**Stand:** DLL SHA-256 `e464dee9b78be9303c99cc6559b43cd84a59ccc1ddbb13efe1aac707a21e13bc`,
Harmony-Probe 5/5, Sync inkl. Ziel-Gegenprüfung. In-Game-Test offen.

## 4n. Zu hohe Mengen: Ausweich-Pfad umging den Wahrscheinlichkeitswurf (2026-10-12)

**Befund Hewal:** „Jetzt ist es wieder viel zu hoch, sowohl Samenernte als auch Fruchternte.“

**Ursache (aus dem Log bewiesen):**
```
Ernte plantedPotato3HarvestPlayer: foodCropPotato x3, x1, x1 (Fallback), x1, plantedPotato1 x2, x1 (Fallback)
```
`GameUtils.collectHarvestedItem` würfelt den `prob`-Wert selbst und meldet ein **verfehltes Wurf mit `false`**.
Mein Code hat dieses `false` als „Einlagern fehlgeschlagen“ gedeutet und die Items über den Ausweich-Pfad
trotzdem eingelagert → **jeder 50-%-Eintrag wurde zum Garantie-Drop** (Extrafrucht, Setzling). Das erklärt
sowohl die zu hohe Frucht- als auch die zu hohe Samenmenge (Setzling = „Samen“).

**Fix:** Der Wurf wird jetzt in der Mod geworfen (`rnd.RandomFloat > d.prob` → `Wurf verfehlt` im Log, kein
Item). Die Abgabe an `collectHarvestedItem` erfolgt mit `prob = 1f`; nur ein *dann* noch `false` (Ausnahme,
fehlende Item-Action) nutzt den Ausweich-Pfad. Damit kommen Extrafrucht und Setzling nur noch in 50 % der
Fälle — wie in Vanilla.

**Erwartung für Hewals Fall** (Kartoffel/Heidelbeere, Basis 2, LotL 2, Landwirt-Outfit/-Stiefel):
| Eintrag | Menge |
|---|---|
| `cropHarvest` (2 × Perk-Faktor 2) | 4 (3 + 1) |
| `bonusCropHarvest` (Perk ≥ 2) | +1 in 50 % |
| `farmerBonusHarvest` (Outfit) | +1 je Outfit-Qualität (10–100 %) |
| `farmerBonusSeed` (Stiefel) | 1 (Basis) + 1 je Stiefel-Qualität (2–20 %) |
| `Destroy` | Setzling in 50 % |

**Stand:** DLL SHA-256 `0da629e3d0e84b601f9236f9de08bdb095e611f88b09a18009611ab1bad81680`,
Harmony-Probe 5/5, Sync inkl. Ziel-Gegenprüfung. In-Game-Test offen.

**Warum das so fummelig war (für die Wiki-Lehre):** Das Vanilla-Verhalten steckt in drei Schichten —
(1) `HarvestOnAttack` würfelt Menge + Tag-Faktor und teilt sie in zwei Raten, (2) `collectHarvestedItem`
würfelt `prob`, skaliert über Trefferdaten und lagert ein, (3) die Werte des Tag-Faktors kommen aus Perk
(`progression.xml`), Kleidung (`items.xml`, `RandomRoll` je Qualitätsstufe) und Entity-Basiswerten
(`entityclasses.xml`). Jede Schicht wurde in einem eigenen Livetest sichtbar; ohne Werkzeug-Attacke fehlen
die Trefferdaten, und ein selbst gebauter Ersatz verfälscht entweder die Menge (Division durch 0) oder den
Wurf. **Lehre:** Nur die Item-Abgabe delegieren, Rechnung selbst nachbauen und jede Abgabe loggen.

## 4o. Verifikation gegen dokumentierte Vanilla-Werte — Mod trifft sie exakt (2026-10-12)

Hewal erwartete „max 4 Früchte“ (LotL 2 + Landwirt-Outfit 30 %). **Geprüft gegen drei unabhängige Quellen
und die Spieltexte selbst:**

* **Perk-Texte im Spiel** (`Localization.csv`): Rang 1 = „**Verdopple** die Ernte von wilden oder
  angepflanzten Feldfrüchten“ (EN „Double the harvest“), Rang 2 = „**Chance von 50 %**, 1 zusätzliche
  Feldfrucht zu ernten“, Rang 3 = „**Verdreifache** die Ernte“ (EN „Triple the harvest“).
* **Community-Dokumentation** (Steam-Diskussion „farming?“ und r/7daystodie, Stand v2.x): Ernte je
  **angepflanzter** Pflanze im Schnitt **LotL 0 = 2, LotL 1 = 4, LotL 2 = 4,5, LotL 3 = 6,5** Früchte;
  „a farmed plant gives twice what a wild one does“, Extra-Frucht über Perk + Landwirt-Outfit, Samen über
  Stiefel.
* **XML-Daten** (`blocks.xml`, `progression.xml`, `entityclasses.xml`, `items.xml`): angepflanzt
  `count="2"` / wild `count="1"`; `HarvestCount perc_add level="1,2,3" = 1,1,2` (×2/×2/×3) auf
  `cropHarvest`; `base_set 1` für `bonusCropHarvest` ab Rang 2; Outfit/Stiefel als Zufallswurf je
  Qualitätsstufe.

**Nachrechnung der Mod-Logik (`GrantDropsVanilla`, per Skript gegen die Sollwerte geprüft):**

| LotL | Faktor | Basis-Ernte | + Perk-Stufe 2 (50 %) | Schnitt | dokumentiert | |
|---|---|---|---|---|---|---|
| 0 | ×1 | 2 (1 + 1) | – | 2,0 | 2,0 | ✅ |
| 1 | ×2 | 4 (3 + 1) | – | 4,0 | 4,0 | ✅ |
| 2 | ×2 | 4 (3 + 1) | +0,5 | 4,5 | 4,5 | ✅ |
| 3 | ×3 | 6 (4 + 2) | +0,5 | 6,5 | 6,5 | ✅ |

**Mit Hewals Ausrüstung** (LotL 2, Landwirt-Outfit Stufe 3 = 30 %): 4 immer, +1 in 50 % (Perk 2), +1 in 30 %
(Outfit) → **4,0 / 4,3 / 4,5 / 4,8** im Schnitt, im besten Fall 6. Sein „immer mindestens 4, manchmal mehr“
ist damit **korrektes Vanilla-Verhalten**, seine Erwartung („max 3–4“) zu niedrig.

**Samen je Ernte:** `farmerBonusSeed` 1 (Basis, kein `base_set 0` für diesen Tag) + 1 wenn der
Stiefel-Wurf trifft (2–20 % je Qualitätsstufe) + `Destroy`-Setzling in 50 % ⇒ 1,5 bzw. 2,5.

**Bewusste Restabweichung (ehrlich):** Vanilla skaliert die *erste* Rate über die Trefferdaten
(`damageMax`/`damageGiven`/`damageTotalOfTarget`) — das greift nur bei einem bereits beschädigten Block;
bei einer frischen, reifen Pflanze ist das Ergebnis identisch. Ohne Werkzeug-Attacke sind diese Werte nicht
reproduzierbar, deshalb fest aus (Vollmenge).

**Neuer (nicht-Vanilla) Regler:** `<Harvest_YieldMultiplier>` (Standard `1.0` = exakt Vanilla; z. B. `0.5`
für halbe Ernte), falls Hewal bewusst anders spielen möchte.

**Stand:** DLL SHA-256 `6d4265f852978c6016a771513cd1490925e751abc3fabfabb17f9dd6f0897f9c`,
Harmony-Probe 5/5, XML valide, Sync inkl. Ziel-Gegenprüfung.

## 4p. Gegenprobe Hewal („Basis ist 1, nur Pilze 2") — Datenprüfung und A/B-Mitschreiber (2026-10-12)

**Hewals Einwand:** Basisernte sei 1 (außer Pilze = 2), unabhängig von wild/angepflanzt; LotL 1 = 2, LotL 2 = 2–3.

**Datenprüfung über alle 13 Kulturen in `blocks.xml` der geladenen Installation:**

| Block | `…HarvestPlayer` (angepflanzt) | `…Harvest` (wild) |
|---|---|---|
| Aloe, Heidelbeere, Chrysantheme, Kaffee, Mais, Goldrute, Hopfen, Kartoffel, Kürbis, Yucca, Baumwolle, **Pilz** | `cropHarvest` **count=2** | `wildCropsHarvest` **count=1** |

Pilze sind **keine** Ausnahme (ebenfalls 2/1). Damit ist Hewals Modell („1 für alles außer Pilze") durch die
Spieldaten widerlegt; sein „1" entspricht dem Stand älterer Versionen (bis A21), die Beet-Pflanzen wurden
später verdoppelt — seine erwarteten Zahlen sind exakt die Hälfte der aktuellen Vanilla-Werte
(1/2/2–3 vs. 2/4/4,5/6,5). Bestätigt durch Fextralife-Wiki (Stand 10/2026: „a farmed plant gives twice what
a wild one does") und die Steam-/Reddit-Tabellen (2 / 4 / 4,5 / 6,5).

**Konsequenz:** Die Mod trifft die aktuellen Vanilla-Werte. Für Hewals gewohnte (alte) Werte gibt es den
Regler `<Harvest_YieldMultiplier>`: `0.5` liefert genau 1 / 2 / 2–3 (seine Zahlen), `1.0` ist aktuelles Vanilla.

**Neuer Mitschreiber (Beweis statt Theorie):** Postfix auf `GameUtils.collectHarvestedItem` protokolliert
**jede** Abgabe — auch die Vanilla-Schlag-Ernte:
```
[FarmSense][DIAG] Abgabe (Vanilla-Schlag): foodCropPotato x3 prob=1 Hand=meleeToolPickT1IronPickaxe Schadensskalierung=an -> vergeben
[FarmSense][DIAG] Abgabe (E-Ernte):      foodCropPotato x3 prob=1 Hand=meleeToolPickT1IronPickaxe Schadensskalierung=aus -> vergeben
```
Zwei identische reife Pflanzen (eine schlagen, eine mit E) zeigen damit im selben Log beide Mengen.
`HarvestLogic.grantingFromMod` unterscheidet dabei Mod- von Spiel-Abgaben.

**Stand:** DLL SHA-256 `d79184b85138f9c06d3b1829dd22d56a6c13f9ad0bf3abeb13c951e34ea96e5a`,
6/6 Patch-Klassen aufgelöst, Sync inkl. Ziel-Gegenprüfung. A/B-Test durch Hewal offen.

## 4q. A/B-Test Hewal (Goldrute, Axt + E) — Zahlen treffen das Modell exakt (2026-10-12)

**Log `output_log_client__2026-10-12__15-02-34.txt`:** 4× `plantedGoldenrod3HarvestPlayer` mit
`meleeToolAxeT1IronFireaxe` in der Hand; je Ernte z. B.
`resourceCropGoldenrodPlant x3, x1, x1, x1, plantedGoldenrod1 x1, plantedGoldenrod1 x1`
bzw. `… x3, x1, x1 (Wurf verfehlt, 0.5), plantedGoldenrod1 x1, plantedGoldenrod1 (Wurf verfehlt, 0.5)`.

**Auswertung (per Skript aus dem Log gezählt):**

| Ernte | Früchte | Setzlinge | verfehlte Würfe |
|---|---|---|---|
| 1 | 4 | 1 | 2 |
| 2 | 6 | 2 | 0 |
| 3 | 4 | 2 | 1 |
| 4 | 5 | 1 | 1 |

Schnitt **4,75 Früchte / 1,5 Setzlinge**. Modell für Hewals Ausrüstung: 4 (Beet 2 × Perk-Faktor 2)
+ 0,5 (Perk-Stufe 2) + 0,30 (Landwirt-Outfit Stufe 3) = **4,8** Früchte; Samen 1 (Basis)
+ 0,5 (`Destroy`) = **1,5**. → Deckung, das Verhalten ist Vanilla-konform.

Bedeutung der einzelnen Posten im Log:
`x3 + x1` = Basis-Ernte (Rate 1 + Rate 2), jeder weitere `x1` = ein gelungener Bonus-Wurf (50 % Perk / 30 %
Outfit), `(Wurf verfehlt, 0.5)` = Wurf nicht getroffen, `plantedGoldenrod1 x1` (immer) = Samen-Basis und der
zweite Eintrag = 50 %-Setzling aus der `Destroy`-Tabelle.

**Nachtrag:** Der A/B-Mitschreiber (`Patch_CollectHarvestedItem_Diag`) war zuerst unsichtbar, weil er sich
den 1-Sekunden-Takt von `HudSystem.Diag` mit der Ertrag-Zeile teilte → er loggt jetzt direkt per `Log.Out`,
damit auch die **Vanilla-Schlag-Ernte** im selben Log auftaucht.

**Stand:** DLL SHA-256 `5d45f91d70da8fcf474958631d8537a607dc4e741e53669b9c318b0ee826db04`,
6/6 Patch-Klassen, Sync inkl. Ziel-Gegenprüfung.

## 4r. 2.6-Referenzvergleich (Hewals Vorschlag) — Werte sind identisch, Basis 2 ist 2.6-Erbe (2026-10-12)

Hewal war „nahezu 100 % sicher“, dass die Basisernte ohne Skillung/Rüstung **1** ist, und schlug vor, die
**2.6-Referenzdateien** zu prüfen. Vergleich der Drop-Einträge in
`7dtd-mod-builder/reference-xml/v2.6/blocks.xml` gegen `…/3.0/blocks.xml`:

| Block | v2.6 (`…HarvestPlayer`) | 3.0 (`…HarvestPlayer`) | v2.6/3.0 wild |
|---|---|---|---|
| `plantedCorn3Harvest` | `cropHarvest count="2"` | `cropHarvest count="2"` | `count="1"` |
| `plantedPotato3Harvest` | `cropHarvest count="2"` | `cropHarvest count="2"` | `count="1"` |
| (alle weiteren Kulturen) | `count="2"` | `count="2"` | `count="1"` |

**Ergebnis:** Beet-Pflanzen geben in **2.6 und 3.0 gleichermaßen 2** (wild 1). Der Wert wurde also **nicht**
mit 3.0 geändert — Hewals „1 als Basis“ trifft nur auf **wilde** Pflanzen zu (und vermutlich auf
Erinnerungen an noch ältere Versionen). Damit ist die Belegkette vollständig:
2.6-Referenz = 3.0-Spieldaten = Community-Tabellen (2 / 4 / 4,5 / 6,5) = Perk-Texte („Verdopple“) =
Log-Auswertung der Mod (4,75 statt 4,8).

**Sandbox „Ernteertrag“:** Die Modifikatoren stammen aus Sandbox-Optionen
(`SandboxOptionManager::UpdateInGameValuesWithSandboxOptions` → `XUiM_Recipes.CropOutputModifier` = Option
`0x91`, `SeedDropOutputModifier` = `0x92`, `HarvestingOutputModifier` = `0x66`). Sie wirken als
`Mathf.Max(1, (int)(Menge × Modifikator))` — bei Basis 2 ändert eine Stufe **125 %** also nichts
(`(int)(2 × 1,25) = 2`); verdoppelnd wirkt erst **200 %**. In Hewals Log stehen die tatsächlichen Werte
(`harvest=1 crop=1 seed=1 mining=1`), seine Sandbox ist also neutral eingestellt.

**Stand:** keine Code-Änderung nötig; die Mod trifft die Datenlage. `Harvest_YieldMultiplier` bleibt als
bewusster Regler (0.5 = Hewals gewohnte Zahlen).

## 4s. Test-Modlet „FarmSense-Testbeet“ — Beet-Reifstufen sofort testbar (2026-10-12)

**Ausgangslage:** Der Nutzer konnte den Beet-Block `…3HarvestPlayer` nicht finden und hätte für ein
Samen-Experiment ~2 h Wachstum abwarten müssen. Beides löst Vanilla-eigene Mechanik (alles per IL/XML belegt):

1. **`CreativeMode = None`** steht ausdrücklich an den 14 `…3HarvestPlayer`-Blöcken — deshalb fehlen sie im
   Kreativmenü (und haben auch keinen Lokalisierungsnamen). Setzt man den Wert auf `All`, sind sie direkt
   setzbar; kein Warten nötig.
2. **`GrowthRate`** (Minuten je Stufe) steht auf dem Vanilla-Master `cropsGrowingMaster` = **63.0** — mit dem
   Kommentar *„For testing, this rate (in minutes) should be set no lower than 2“*. Der Testwert ist also vom
   Spiel selbst vorgegeben: **2 Minuten/Stufe** statt 63.
   Kette: `plantedX1 → plantedX2 → plantedX3HarvestPlayer` (alle erben `cropsGrowingMaster`), Tickdauer je
   Stufe `GetTickRate() = growthRate × CropGrowthModifier × 20 × 60`.

**Umsetzung (reines XML-Modlet, keine DLL):** `FarmSense/testbeet-modlet/` →
`/home/hewal/7 Days/0 einzelne mods/FarmSense-Testbeet/`
* `Config/blocks.xml`: 14× `CreativeMode` → `All`, `cropsGrowingMaster/GrowthRate` → `2`.
* `Config/Localization.csv`: 14 Anzeigenamen DE/EN („Beet-Pflanze: Mais (reif)“ / „Farm Plant: Corn (ready)“),
  damit die Blöcke im Kreativmenü auffindbar sind.
* `ModInfo.xml` (Name `FarmSenseTestbeet`, Version 0.0.1) — **Testmodlet, nicht veröffentlichen**.

Es ändert **keine** Erntemengen, nur Sichtbarkeit und Wachstumstempo.

**Erwartete Messwerte im Testspielstand ohne Skillung/Rüstung:**
`Ernte plantedCorn3HarvestPlayer: foodCropCorn x2, plantedCorn1 x1`
→ 2 Früchte (`cropHarvest count=2`, Faktor 1, Rate 1 = 2) + 1 Samen (`farmerBonusSeed`, Faktor 1).
Kommt dort **1** statt 2, ist Hewals Modell richtig und die Mod wird auf Basis 1 umgestellt.

**Stand:** Modlet geprüft (XML valide, CSV 20 Spalten) und ins Live-Modverzeichnis kopiert; Spiel-Neustart
durch Hewal nötig.

## 4t. ERGEBNIS: Vanilla-Test bestätigt 2 bei Beet-Pflanzen (2026-10-12)

Hewal hat mit dem Testmodlet eine reife **Beet**-Pflanze (`plantedPotato3HarvestPlayer`, DIAG `Stufe 3/3`)
ungeskillt und **mit bloßer Faust** geschlagen. Der Diagnose-Hook protokolliert die Vanilla-Abgaben
(`grantingFromMod = false` ⇒ „Vanilla-Schlag“):

```
Abgabe (Vanilla-Schlag): foodCropPotato x2 prob=1 Hand=meleeHandPlayer Schadensskalierung=an  -> vergeben
Abgabe (Vanilla-Schlag): plantedPotato1 x1 prob=1 Hand=meleeHandPlayer Schadensskalierung=aus -> vergeben
```

**⇒ Vanilla vergibt bei der Beet-Reifstufe 2 Früchte** (+ 1 Samen), ungeskillt. Die vom Nutzer erinnerte
„1 ohne Skillung“ gehört zur **Wildvariante** (`plantedCorn3Harvest` → `x1`, Test vom selben Tag).

**Damit gilt endgültig:** wild = 1, Beet aus Samen = 2 — identisch in 2.6 und 3.0. Die Mod trifft mit
`Harvest_YieldMultiplier = 1.0` exakt die Vanilla-Werte; kein Umbau nötig. Die frühere Verwirrung entstand
allein durch die zwei Block-Varianten (CreativeMode=None macht die Beet-Variante praktisch unsichtbar).

**Zusatzbefund (Patch-Engine):** `<set xpath="…/cropsGrowingMaster/property[@name='GrowthRate']/@value">`
lehnt das Spiel ab (Log: *„XML patch for blocks.xml … did not apply“*) — der Master steht offenbar nicht als
patchbares Knoten-Objekt im geladenen Baum. Der CreativeMode-Teil desselben Modlets wirkte dagegen (Hewal
konnte die Reifstufe platzieren). **Fix:** `GrowthRate` per `<append>` als eigene Property in jede der
29 Wachstumsstufen (`plantedX1`, `plantedX2`, `plantedtreeGrassSeed1`) eintragen — eigene Property schlägt
die geerbte. Noch in-game unbestätigt.

## 4u. Abschluss: Vanilla-Erntemengen dokumentiert, Testmodlet entfernt (2026-10-12)

Der Vollvergleich (2.6-Referenz, 3.0-Referenz, installierte 3.3, IL und In-Game-Log) ist als eigenes
Wiki-Modul abgelegt: **`7dtd-mod-builder/docs/v3.0/farming-harvest.md`** (Index + Changelog dort verlinkt).

Kernergebnisse:
* Beet-Pflanze (`plantedX3HarvestPlayer`) = **2** Früchte, Wildpflanze (`plantedX3Harvest`) = **1** —
  in allen drei Fassungen wortgleich; die Beet-Variante ist im Spiel unsichtbar (`CreativeMode=None`).
* LotL `perc_add level="1,2,3" value="1,1,2"`, ab Stufe 2 zusätzlich `bonusCropHarvest base_set 1` (50 % +1).
* Farmer-Outfit Brust 10→100 % (Q6 garantiert) für +1 Frucht, Stiefel 2→20 % für +1 Samen.
* IL-Formel: `count = (int)(RandomRange(min,max+1) × f)`, Abgabe `Mathf.Max(1, (int)(count × f))`,
  Sandbox-Modifikatoren 91/92/101/102, danach `Destroy`-Tabelle (Setzling, 50 %).
* Historie (offizielle Wiki-Seite): **Alpha 20** hob den Ertrag angepflanzter Pflanzen von 1 auf 2 —
  Hewals Erinnerung („1, nur Pilze 2") entspricht exakt dem Stand **A15–A19**.

**Aufgeräumt:** Das Testmodlet `FarmSense-Testbeet` wurde aus dem Livetest-Verzeichnis **und** aus dem
Projekt entfernt (nicht mehr nötig); das Reproduktionsrezept (CreativeMode/GrowthRate) steht im Wiki-Modul.

## 4v. Nachtrag: „1 statt 2"-Erinnerung weiter eingegrenzt (2026-10-12)

Hewal meldete als erste 7DTD-Berührung **Version 1.4** (nicht Alpha) und beschrieb sein Farming-Gefühl:
voll geskillt + T6-Landwirt-Outfit **maximal 7 Früchte** pro Beet-Pflanze, Samenbonus niedrig, deshalb
ständig Samen nachbauen (5 Früchte → 1 Samen). Zusätzliche Prüfungen:

* **UL als Ursache ausgeschlossen:** `UndeadLegacy.dll` (2,28 MB, per `ikdasm` gedumpt) enthält **keine**
  Treffer für `HarvestCount`, `cropHarvest`, `wildCropsHarvest`, `farmerBonus*` oder
  `collectHarvestedItem` — UL fasst die Erntemengen nicht an. ULs Spielbasis-Dateien
  (`…/Apps/7 Days To Die 2.6 - Undead Legacy/Data/Config/blocks.xml`) sind **byte-identisch** mit der
  2.6-Referenz (3.284.613 Bytes) und enthalten ebenfalls `count="2"`.
* **Sandbox-Defaults belegt:** `SandboxOptionManager::SetupOptions` legt „Seed Drop Output" (92),
  „Harvesting Output" (102) und „Crop Growth" (93) jeweils mit Default `ldc.r4 1` an → keine versteckte
  Halbierung in neuen Welten. Die Schrittwerte kommen aus den Werte-Sets `LootAbundanceValues` bzw.
  `CropGrowthSpeed` („Crop Growth" hat u. a. den Anzeigewert `xuiInstant`).
* **`HarvestOverdamage`** steht auf `cropsGrowingMaster` **und** `cropsHarvestableMaster` auf `false` →
  kein Mengenabzug bei überhöhtem Schaden (starkes Werkzeug).
* **Zwischenstufen liefern genau 1:** `plantedX1`/`plantedX2` haben keine Harvest-Tabelle, nur
  `Destroy plantedX1 count=1 prob=1` (Log-Beleg `Abgabe (Vanilla-Schlag): plantedPotato1 x1 … aus`) — eine
  plausible Quelle der „1"-Erinnerung, ebenso wie Wildpflanzen (immer 1) und der Setzling selbst (1, 50 %).
* **Der entscheidende Gegenbeleg:** Hewals eigene Erinnerung „maximal 7 bei voller Skillung" ist nur mit
  Basis 2 erklärbar (`2 × 3 = 6` + 1 Zusatz). Mit Basis 1 läge das Maximum bei 5 (`1 × 3 + 1 + 1`).
* Nicht prüfbar: Vanilla **1.0–1.9** (lokal liegen nur 2.6, 3.0, 3.3 vor); die offizielle Wiki-History
  nennt zwischen 1.0 und 3.x keine Änderung der Erntemenge.

**Dokumentiert im Wiki-Modul** `farming-harvest.md`, Abschnitt 11 („Warum sich so viele an ‚nur 1' erinnern").

## 4w. Release 1.0.0 für Nexus vorbereitet (2026-10-12)

Nach dem etablierten EVZ-Workflow (Skill `evz-nexus-mod-publishing`):

* **Version überall auf 1.0.0** (`ModInfo.xml`, `src/FarmSenseMain.cs`), Build + Sync auf
  `b47c3ad9…`; Nachweis im Binary: `[FarmSense] Version 1.0.0 wird geladen …` (`strings -el`).
  Achtung: `monodis --userstrings` zeigt den zusammengefalteten String nicht — der UTF-16-Scan schon.
* **Release-Defaults:** `LogDebug=false`; `Hud_UseVanillaPrompt` bleibt **an**, weil der Vanilla-Prompt
  sonst den Hinweis „Drücke E zum Ernten" verliert (Kommentar steht jetzt in der `Config.xml`).
* **Release-ZIP:** `releases/FarmSense-Crop-Info-and-Harvest-with-E-1.0.0.zip`
  (8 Laufzeitdateien, SHA-256 `8a0e478ba82566ce0792e41573bc173a3f79b14c2b4dbab34a07326ab75b69a8`),
  `validate_release_zip.py`: **PASS**; keine Harmony/MonoMod-DLL im Paket, `dist/` == Live-Ordner.
* **Englische Beschreibung:** `NEXUS_DESCRIPTION_EN.md` (BBCode nach EVZ-Struktur), Validator **PASS**,
  Kurzbeschreibung 280 Zeichen. Deutscher Entwurf entfiel auf ausdrücklichen Wunsch des Nutzers.
* **Media:** `media/nexus/` mit Thumbnail (1920×1080), Header (1300×372), zwei Galeriebildern
  (1920×1080) und Kontaktbogen; erzeugt mit `tools/build_nexus_media.py`, das Codex CLI
  (Modell `gpt-6.1-sol`) aus den zwei Screenshots und dem EVZ-Referenzheader abgeleitet hat.
  Quellen liegen in `media/sources/`.
* **Referenzabgleich:** Die bestehende ProxiStack-Seite (Nexus-Mod 12082) wurde per Firecrawl gelesen
  (das lokale Brave-Profil war durch einen laufenden Browser gesperrt) — Abschnittsreihenfolge,
  Credits-/Permissions-Text und Bildsprache sind von dort übernommen.
* Für Codex musste im Projekt ein Git-Repository initialisiert werden (`git init`).

## 4x. Fortschritts-Prozent war nicht aussagekräftig — jetzt gegen das Gesamtwachstum (2026-10-13)

**Hewals Befund (2 Screenshots, gleicher Spielstand):** derselbe Pilz zeigt in zwei Wachstumsstadien fast
identische Prozentwerte:

| Bild | Block (Vanilla-Name) | HUD |
|---|---|---|
| B | `plantedMushroom1` = „Pilzsporen (Samen)" | Stufe **1/3**, **4 % – reif in 2 h 0 min** |
| A | `plantedMushroom2` = „Pilze (Anbau)" | Stufe **2/3**, **9 % – reif in 58 min** |

**Ursache (im Code belegt, `src/CropRegistry.cs::Get`):** Der Prozentwert wurde gegen die **Restkette**
gerechnet (`1 − SecondsLeft / chain.SecondsFrom(stage)`), die Restzeit daneben ist aber die
**Gesamt**-Restzeit. Der Nenner ist deshalb von der Stufe abhängig:

| Stufe | Nenner `SecondsFrom(stage)` | Bedeutung |
|---|---|---|
| 1/3 | 63 + 63 + 0 = **126 min** | zufällig = das ganze Wachstum → Prozent stimmt |
| 2/3 | 63 + 0 = **63 min** | nur die Restkette → Prozent startet wieder bei 0 % |

Folge: Beim Stufenwechsel springt der Wert von ~50 % **auf 0 % zurück**, und ein schon weit
gewachsenes Feld zeigt weiter kleine Zahlen — genau das, was Hewal im Bild sieht.
Die Restzeit war dabei die ganze Zeit korrekt (2 h 0 min bzw. 58 min).

**Nachrechnung gegen die echten Spieldaten** (Kette aus `Data/Config/blocks.xml`,
`cropsGrowingMaster` GrowthRate = **63 min/Stufe**, `GetTickRate()/20`): bei 57,5 min Rest in der
aktuellen Stufe — das ist der Wert hinter beiden Screenshots — liefert die alte Formel **exakt 4 % und
9 %**, also die beobachteten Zahlen:

| Fall | alte Formel | neue Formel |
|---|---|---|
| Stufe 1/3, 57,5 min Rest in der Stufe | 4 % | 4 % (unverändert) |
| Stufe 2/3, 57,5 min Rest in der Stufe | 9 % | **54 %** |
| Übergang Stufe 1 → 2 (Rest 0 → 63 min) | 50 % → **0 %** | 50 % → 50 % |

**Fix (`src/CropRegistry.cs`):** Neues Feld `PlantInfo.ChainTotalSeconds = chain.SecondsFrom(0)` (ganzes
Wachstum ab der Saat) und Prozent = `1 − SecondsLeft / ChainTotalSeconds`. `SecondsTotal` (Restkette)
bleibt unverändert als Rückfallwert für den Fall „kein Ticker-Eintrag" (`RemainingInStage < 0`, dort
weiterhin 0 % + Warnhinweis). Stufe 1 zeigt also denselben Wert wie bisher, ab Stufe 2 ist der Wert
monoton und passt zur Restzeit; einzige Annahme ist, dass die vorherigen Stufen ihre Nenn-Dauer
gebraucht haben (dieselbe Annahme steckt schon in der Gesamt-Restzeit).

**Prüfstand:** Build fehlerfrei · DLL SHA-256 `de901eecfc06f804f6aa841b49d50eea72df46a83315ae40280584efcdf3a1dc`
· Harmony-Probe **6/6** („ALLE PATCHES AUFLOESBAR UND ANGEWENDET"; die Mono-Meldung zu
`UnityEngine.CoreModule` am Ende ist ein Umgebungsartefakt des Probes, kein Patch-Fehler) ·
neues Feld in Build **und** Ziel-DLL nachgewiesen (`monodis --fields` → `float64 ChainTotalSeconds`) ·
Sync (dry-run + rsync, keine Löschungen) mit identischem Hash · Doku nachgezogen
(`../FarmSense.md` Funktionsbeschreibung + offener Punkt, `README.md` Feature-Zeile).
**In-Game noch nicht gegengeprüft** — erwartet: `2/3` mit ~54 % (statt 9 %) und 58 min Restzeit,
Stufe 1/3 unverändert 4 %.

**Nicht geändert:** Restzeit, Ernte, Stufenanzeige, Wildwuchs.

## 4y. Release 1.0.1 auf Nexus veröffentlicht (2026-10-13, 20:23 Uhr Ortszeit / 18:23 UTC)

**Auslöser:** Hewals Freigabe nach der In-Game-Sichtprüfung des Prozent-Fixes (§4x).

* **Version 1.0.1** überall gesetzt (`ModInfo.xml`, `src/FarmSenseMain.cs`) und im Binary nachgewiesen
  (`strings -el` → `[FarmSense] Version 1.0.1 wird geladen ...`).
* **DLL SHA-256 `31cde140ecc02507de5c90154fb7ec4f8c4a64633b62e6d53081f05f3ec6ab31`** — identisch in
  Build, `dist/FarmSense/`, **Release-ZIP** und Livetest (`~/7 Days/0 einzelne mods/FarmSense/`):
  Hewal testet in-game byte-genau die veröffentlichte Fassung.
* **Release-ZIP:** `releases/FarmSense-Crop-Info-and-Harvest-with-E-1.0.1.zip` (8 Laufzeitdateien,
  51.112 Bytes, SHA-256 `81bc26b7c0009a4a8150c154ba450dcc90c34f8dafcd47925c129ce2ca2a1a42`),
  `validate_release_zip.py`: **PASS**; die 1.0.0-ZIP bleibt als Archiv daneben erhalten.
* **Nexus (bestehende Mod 13118, kein neuer Eintrag):** Files-Tab → Zeilenknopf **„Update"** →
  Formular „Update existing file" (Ziel `… 1.0.0`) → Datei als neue Version hochgeladen, Anzeigename auf
  `FarmSense Crop Info And Harvest With E 1.0.1` gesetzt, **„Update mod version to match this file's
  version"** aktiviert, **„Save file"** geklickt. Die Mod-Version steht damit auf 1.0.1, die alte 1.0.0
  bleibt unter „Old files" weiter verfügbar. Die Mod-Beschreibung blieb unverändert (Hewals Vorgabe).
* **Öffentlich anonym gegengeprüft** (Firecrawl ohne Cookies, `maxAge: 0`): Kopfzeile „Version 1.0.1",
  „Last updated 13 October 2026, 6:23PM", Main file `FarmSense Crop Info And Harvest With E 1.0.1`
  (23 KB, Version 1.0.1), Old file 1.0.0 gelistet. Nexus-Hinweis beim neuen File:
  _„This file is not yet available for download. Virus scanning is in progress."_ — normal, der
  Download wird nach dem Scan automatisch freigeschaltet.
* **Formular-Pitfall (für den Skill):** Der Zeilenknopf „Update" öffnet einen **nativen Dateidialog**, der
  in einem Headless-Browser nichts bewirkt (kein Dialog, kein DOM-Effekt). Das Formular erscheint erst,
  wenn die ZIP direkt in den versteckten `input[type=file]` **ohne `accept`-Attribut** gesetzt wird
  (`DOM.setFileInputFiles`). Der Input mit `accept=".rar,.zip,…"` gehört zur Box „Add file" und würde
  eine **zweite** Dateizeile anlegen. Anzeigename/Versionsfelder (`#display-name`, `#file-version`) waren
  danach bereits sinnvoll vorbelegt.
* **Offen:** GitHub-Release/Commit v1.0.1 (siehe §4z — erledigt) und der optionale Rückfluss des lokal
  vorbereiteten Changelogs (`NEXUS_DESCRIPTION_EN.md` enthält den Abschnitt „Changelog" bereits,
  er ist **noch nicht** auf Nexus).

## 4z. Quellcode und Release auf GitHub nachgezogen (2026-10-13, 20:32 Uhr)

Nach dem Muster der Erstveröffentlichung (Skill `mod-source-publication`, „Publishing a later version"):

* **Commit `3c9aed0`** auf `main` (`FarmSense 1.0.1 - growth percentage now covers the whole growth`)
  mit Ursache, Nachrechnung und Prüfstand im Commit-Text; nur Quell-/Doku-Dateien im Index
  (Leak-Check gegen `.dll`/`.zip`/`dist/`/`releases/`/`media/`: OK, `.gitignore` deckt das ab).
* **GitHub-Release `v1.0.1`** (Titel `FarmSense 1.0.1`, Notes-Datei außerhalb des Repos) mit
  `FarmSense-Crop-Info-and-Harvest-with-E-1.0.1.zip` als Asset: **23.796 Bytes, State `uploaded`**,
  identisch zur lokalen Datei (`stat` 23.796 Bytes, SHA-256 `81bc26b7…`).
  Notes enthalten u. a. den Update-Hinweis (Ordner ersetzen, keine Datei entfällt, kein neuer Spielstand).
* **Gegengeprüft (nicht nur gepusht):** `gh release list` zeigt `v1.0.1` als **Latest**,
  Tag `v1.0.1` → `3c9aed0c8ce9ba7b2bbef368630e08b3b86cf66c` = `main`-HEAD zum Release-Zeitpunkt,
  Asset-Größe lokal = remote, Repo-Baum enthält nur Quellcode/Doku (keine DLL, kein ZIP).
* Dieser Doku-Nachtrag kommt bewusst **nach** dem Release; der Tag zeigt weiterhin auf `3c9aed0`.

## 4aa. Bäume waren per E „erntbar" — jetzt ausgenommen (2026-10-13)

**Hewals Befund:** Ein ausgewachsener Baum lässt sich mit **E** einsammeln. Er soll wie in Vanilla
nur per Schlagen fallen. Vorgabe: **Version bleibt 1.0.1**.

**Ursache (im Assembly belegt):** `BlockModelTree` — die Blockklasse **aller** Bäume — **erbt von
`BlockPlantGrowing`**. Damit landen Baumketten im Pflanzenregister (`Block.list` → `b as
BlockPlantGrowing`), und das einzige Ernte-Kriterium war „letzte Stufe der Kette":

| Beleg | Ergebnis |
|---|---|
| `monodis --typedef Assembly-CSharp.dll` | `BlockModelTree (flags=0x100001, extends=0x4d8)` → `extends`/4 = TypeDef-Zeile **310 = BlockPlantGrowing** |
| Mono-Reflection-Probe am installierten Spiel (V 3.3.0 b18) | `BlockModelTree: public=True abstract=False`, `erbt von: BlockPlantGrowing → BlockPlant → Block → Object`, `BlockModelTree ist BlockPlantGrowing: True`, `BlockPlantGrowing ist BlockModelTree: False` |
| `blocks.xml` | **50 Blöcke** mit `Class=ModelTree` (treeBurntMaple01 … treeWinterPine28m), davon **14 im Mod-Register** |
| Ketten-Endstufen (= bisher E-erntbar) | `treePlantedOak41m`, `treePlantedMountainPine41m`, `treePlantedWinterPine28m` |

Folge: E an `treePlantedOak41m` vergab die Harvest-Tabelle des Baums (**150 Holz**, `Destroy` 2 Setzlinge)
und ließ den ganzen Baum in einem Tastendruck verschwinden. Zwischenstufen waren nie betroffen (nur die
Endstufe galt als „reif").

**Fix (`src/CropRegistry.cs`, `src/HarvestLogic.cs`, `src/HudSystem.cs`):**

1. **Erkennung über die Blockklasse, nicht über den Namen:** `IsTreeBlock(Block)` prüft
   `b is BlockModelTree` (fängt alle Baumtypen, auch umbenannte aus Mods) und fällt konservativ auf den
   Vanilla-Namenspräfix `tree…` zurück. `CropStage.IsTree` wird beim Registeraufbau je Stufe gesetzt.
2. **Ein Kriterium an einer Stelle:** neues `CropRegistry.IsHarvestable(blockId)` = *Endstufe **und**
   kein Baum*. `IsReadyBlock` bleibt für die Anzeige erhalten, `HarvestLogic.CanHarvest` nutzt jetzt
   `IsHarvestable` (Diagnosezeile `… ist ein Baum - kein E-Kommando`).
3. **Sicherheitsnetz für alle Aufrufer:** `HarvestNow` lehnt Nicht-Erntziele ab — damit ist auch der
   **Multiplayer-Pfad** (`NetPackageFarmSenseHarvest` → Server) abgedeckt, nicht nur das E-Kommando am Client.
4. **Anzeige bleibt, Hinweis fällt:** Bäume werden weiterhin angezeigt (`Hud_ShowTrees` steuert das jetzt
   über `info.IsTree`), aber ohne „Ernten mit E" — sowohl im Mod-Feld als auch im Vanilla-Prompt.
   Unverändert bleiben Restzeit, Prozentwert, Wachstumsstufen und der Wildwuchs.

**Prüfstand:** Build fehlerfrei · DLL SHA-256 `e7d25858ac4d3a090689c543854f22eba8fb4355d66de78fd393d877fd9f4f2f`
· Harmony-Probe **6/6** · Sync (dry-run + rsync) mit identischem Hash im Livetest · Version bleibt **1.0.1**
(`ModInfo.xml`, `FarmSenseMain.cs`).

**Bewusst nicht geändert (dokumentiert, weil gleiche Klasse von Blöcken):** Die Biome-Deko-Pilze
(`mushroomBiome2/4`, `mushroomRadiated02/04`) und `plantedtreeGrassSeedRandomHelper` (Ziergras) bleiben
per E erntbar — sie sind wilde Pflanzen im Sinne der Mod. Ebenfalls außen vor: die beiden Dev-/Helper-Blöcke
`cropsHarvestableMaster` und `terrFertileGrassExample` (kommen in Welten nicht vor).

**Offen:** In-Game-Gegenprobe durch Hewal (E an einem Baum → **kein** Radial/kein Ernten; E an einer reifen
Kultur unverändert) und die Entscheidung, ob die bereits veröffentlichte **1.0.1** (Nexus/GitHub) durch
diesen Build ersetzt wird.

## 4ab. Bäume zeigen jetzt ihren Namen im Feld (2026-10-13)

**Wunsch Hewal:** Bei Bäumen soll der Name angezeigt werden.

**Ausgangslage:** Das Feld zeigt standardmäßig nur die Stufe (`Hud_ShowName=false`), weil der Pflanzenname
bei Kulturen direkt darüber in der Vanilla-Zeile steht. Bei Bäumen fehlt diese Zeile (die Mod bietet dort
seit §4aa kein Aktivierungskommando mehr) — im Feld stand also nur `5/5`.

**Umsetzung (`src/CropRegistry.cs`, `src/HudSystem.cs`):**
* `ChainFrom` setzt `DisplayName` für Baumketten jetzt aus der **Endstufe** der Kette, ohne Größenangabe:
  `PrettyTreeName` macht aus „Baum – angepflanzte Eiche 41 m" → **„Baum – angepflanzte Eiche"**, aus
  „Baum – angepflanzte Bergkiefer, 41 m" → „Baum – angepflanzte Bergkiefer" (Komma-Variante) und aus
  „Tree Planted Oak 41m" → „Tree Planted Oak". Nur ein Endstück aus Ziffern + „m" (mit optionalem Trenner)
  wird entfernt. Der Kettenname der Saat („Eichensamen"/„Oak Seed") wäre unbrauchbar; die Endstufe liefert
  in **jeder** Stufe denselben, sprechenden Namen.
* `HudSystem.BuildTexts`: `if (cfg.Hud_ShowName || info.IsTree)` — Bäume zeigen ihren Namen unabhängig
  von `Hud_ShowName`. Die Stufe wandert dabei wie bei `Hud_ShowName=true` in die Wertezeile
  (`4/5 · 62 % – reif in 1 h 3 min`, bei Reife `5/5 · reif`).
* Gegenprobe der Namenslogik gegen die echten Lokalisierungstexte (DE + EN) aller drei gepflanzten
  Baumarten: „Baum – angepflanzte Eiche 41 m" → „Baum – angepflanzte Eiche", „Baum – angepflanzte
  Bergkiefer, 41 m" → „Baum – angepflanzte Bergkiefer", „Tree Planted Winter Pine 28m" →
  „Tree Planted Winter Pine"; „Eichensamen"/„Oak Seed" bleiben unverändert.

**Prüfstand:** Build fehlerfrei · DLL SHA-256 `f0c5635e7cace1b55331e385fe8226ff30a3fd3729766396eccebe6a787608b4`
· Harmony-Probe 6/6 · Sync (dry-run + rsync) mit identischem Hash · Version bleibt **1.0.1**.
**Offen:** In-Game-Sichtprüfung (erwartet z. B. `Baum – angepflanzte Eiche` / `4/5 · 62 % – reif in 1 h 3 min`).

## 4ac. Feld lag über der Block-Health-Anzeige — jetzt darunter (2026-10-13)

**Hewals Bild:** Beim Schlagen auf einen Baum lagen die HP-Zahlen (`43/300`) und der Mod-Text
(„Baum – Wacholder") übereinander.

**Ursache (aus den Vanilla-XUi-Werten gerechnet):** `focusedBlockHealth` steht auf
`anchor="Center" pos="0,-40"` mit Höhe 50 (Sprite bei 0/-12, Textlabel bei -10, Schrift 30) → die
HP-Anzeige reicht von 40 bis rund 90 px unter die Bildmitte. Das Mod-Feld begann bei `pos="-215,-26"`,
sein Inhalt lief also von ~30 bis ~116 px unter der Mitte — mitten in der HP-Anzeige. Bisher fiel das
kaum auf (im Feld stand nur „5/5"); mit dem Baumnamen (§4ab) wurde es offensichtlich. Bei Kulturen
tritt es nur beim Schlagen auf, weil Vanilla die Leiste erst bei `damage > 0` einblendet.

**Fix:** reine XML-Änderung (`Config/XUi_InGame/windows.xml`): `pos="-215,-26"` → **`pos="-215,-96"`**
(ca. 6 px Abstand unter der HP-Anzeige). Bewusst feste Position statt „springt beim Schlagen nach
unten": kein Flackern, und die Anzeige bleibt beim Zielwechsel an derselben Stelle.

**Prüfstand:** Build + Sync; Gegenprüfung im **Ziel**: Zeile 25 `pos="-215,-96"`, Datei-Hash
dev = live (`083dedd2…`), DLL unverändert `f0c5635e…` (reine XML-Änderung, kein neuer Build nötig).
**Spiel-Neustart nötig** (XUi-XML wird beim Start gepatcht); Sichtprüfung durch Hewal offen.

## 5. Testanleitung (offen – von Hewal durchzuführen)

1. Spiel starten (EAC aus), Client-Log prüfen: `~/.local/share/7DaysToDie/logs/output_client__*.txt`
   Erwartete Zeilen:
   ```
   [FarmSense] Version 0.1.0 wird geladen ...
   [FarmSense] Pflanzenregister: <N> Ketten, <M> Einzelbloecke, <K> Bloecke gesamt.
   [FarmSense] Harmony-Patches registriert.
   [FarmSense] bereit. Erkannte Pflanzenblöcke=... ErnteMitE=True HUD=window ...
   ```
   Auffällig wären `[FarmSense] Tick-Fehler: ...`, `Config.xml nicht gefunden`, `Block nicht gefunden`.
2. Auf eine **wachsende** Kultur (Farmpflug) zielen → unten rechts das Feld mit „Mais: xx % – reif in …“
   und Stufe. Auch mit **leerer Hand** sichtbar.
3. Auf eine **reife** Kultur oder eine wilde Pflanze im Kiefernwald zielen → „…: reif · Ernten mit E“.
4. **E** drücken → Radial blitzt auf, Kommando „Ernten“; Items landen im Rucksack, Pflanze verschwindet.
5. Kiefernwald: `WildGrowth_Enabled` wirkt erst nach `WildGrowth_IntervalSeconds` (Standard 120 s) pro
   Intervall; mit `LogDebug=true` in `Config.xml` erscheinen „Wilde Pflanze gesetzt: …“-Zeilen.
6. Bei Bedarf HUD-Position ändern: `Config/XUi_InGame/windows.xml` (`anchor`/`pos`) – **Neustart** nötig
   (XUi-XML wird beim Spielstart gepatcht). `Config.xml` wird beim Mod-Laden gelesen – ebenfalls Neustart.

## 6. Offene Punkte / Risiken (Priorität von oben nach unten)

1. **HUD-Fenster öffnet sich?** Erste ungeprüfte Annahme: dass `windowManager.Open("farmSenseHud", false)`
   eine selbst definierte Gruppe wie Vanilla-`focusedBlockHealth` einblendet. Falls es nicht erscheint:
   Alternative ist der Vanilla-Prompt (`Hud_UseVanillaPrompt=true`, funktioniert über
   `Block.GetActivationText`), oder das Fenster in eine bestehende Gruppe hängen.
2. **Multi-Block-Platzierung**: Wilde Pflanzen werden per `world.SetBlockRPC` gesetzt. Mais ist
   `MultiBlockDim=1,3,1`. Prüfen, ob die Pflanze vollständig und erntbar erscheint; sonst nur
   `1,2,1`-Kulturen (Kartoffel/Heidelbeere/Kürbis) verwenden oder einen Multi-Block-Aufbau ergänzen.
3. **Perk-Skalierung fehlt**: `PassiveEffects.HarvestCount` (Living off the Land) wird noch **nicht**
   angewendet – die Ernte entspricht der reinen Drop-Tabelle. v3.3-Überladung von
   `EffectManager.GetValue(...)` vor der Umsetzung sauber aus dem IL belegen.
4. **Challenge-/Questfortschritt**: `QuestEventManager.HarvestedItem(...)` wird noch nicht aufgerufen →
   „Sammle X Mais“-Challenges zählen E-Ernten nicht mit.
5. **Multiplayer ungetestet**: Client-Pfad über `NetPackageFarmSenseHarvest` (Server erntet autoritativ,
   Missbrauchsschutz 5 m). Ob die Radial-Aktivierung clientseitig zuverlässig ankommt, ist offen.
6. **Wildwuchs-Zähler ist nur im Speicher** (Chunk-Obergrenze, kein Persistieren über Serverneustart).
7. **Biome-Dekoration wirkt nur bei neuen Welten** – deshalb existiert das Nachwachsen-System.
   In bestehenden Welten werden zudem **unerkundete** Chunks beim ersten Laden neu dekoriert
   (Worldgen-Dekoration läuft pro Chunk; nicht separat verifiziert).
8. **Anzeigename**: `GetLocalizedBlockName()` minus Klammerzusatz → „Mais“, bei POI-Varianten
   „Maispflanze 3“. Feinschliff möglich (z. B. Produktname aus der Drop-Tabelle).
9. `Hud_UseVanillaPrompt` nutzt bewusst `{0}` als Tastenplatzhalter (Vanilla formatiert mit der
   Tastenanzeige) – Nebeneffekt: eigener Text darf **kein weiteres** `{n}` enthalten.

## 7. Arbeitsbefehle

```bash
cd /home/hewal/Hermes/7d2d-v3-mods/FarmSense
./build.sh                 # baut dist/FarmSense (DLL + ModInfo + Config.xml + Config/)
./sync.sh --yes            # Trockenlauf + rsync in den Livetest (ohne --yes: 5 s Abbruchfenster)

# Harmony-Patch-Bindung prüfen (5 Patch-Klassen müssen „angewendet“ sein)
bash ~/.hermes/skills/software-development/7dtd-v30-csharp-modding/scripts/run-harmony-patch-probe.sh \
     dist/FarmSense/FarmSense.dll

# Typeref-Abgleich gegen die Spielassembly (nur die 3 bekannten Falschmeldungen erlaubt)
MANAGED="$HOME/.steam/steam/steamapps/common/7 Days To Die/7DaysToDie_Data/Managed"
monodis --typedef "$MANAGED/Assembly-CSharp.dll" > /tmp/typedefs.txt
monodis --typeref dist/FarmSense/FarmSense.dll | grep "Assembly-CSharp" \
  | sed "s/.*Assembly-CSharp'\]//" | sort -u \
  | while read -r t; do b=$(echo "$t" | sed 's/`[0-9]*$//; s/^.*\.//; s/<.*//; s/+.*//'); \
      grep -q ": $b (\|: $b/\|: $b " /tmp/typedefs.txt || echo "PRUEFEN: $t"; done

# Log prüfen
grep -n "FarmSense" ~/.local/share/7DaysToDie/logs/output_log_client__*.txt | tail -30
```

## 8. Nächste Schritte (Vorschlag)

1. Hewal testet in einer **bestehenden** Welt (Punkt 1–4 der Testanleitung) → Ergebnis hier eintragen.
2. Abhängig vom Ergebnis: HUD-Fenster fixen (Punkt 1) bzw. Multi-Block (Punkt 2).
3. v0.2: Perk-Skalierung + Challenge-Fortschritt + MP-Test.
4. Nach bestandenem Livetest: Wiki-Rückfluss (`pitfalls.md`/`csharp.md`), Skill-Referenzen ergänzen,
   Release-Paket (7 Dateien, Harmony-frei) + Nexus-Beschreibung.

## 9. Verwandte Dokumente

- Analyse-Grundlagen: `../PflanzenWachstumsanzeige-Analyse.md`, `../Wildpflanzen-und-E-Ernte-Analyse.md`
- Mod-Doku (Nutzer-Sicht): `../FarmSense.md`
- Skill-Referenzen: `7dtd-v30-csharp-modding` → `references/v30-world-ticker-and-block-hud.md`,
  `references/v30-block-activation-and-e-interaction.md`, `references/v33-breaking-changes.md`
