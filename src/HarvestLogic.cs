using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FarmSense
{
    /// <summary>
    /// Ernten mit E: haengt an erntbare Pflanzen ein Aktivierungskommando an und erntet dann
    /// ueber die Drop-Tabelle des Blocks (EnumDropEvent.Harvest + Destroy fuer Samen).
    /// Kein Nachwachsen – die Pflanze verschwindet (so gewuenscht); wilde Pflanzen entstehen
    /// ueber das Wildwuchs-System neu.
    /// </summary>
    public static class HarvestLogic
    {
        public const string CommandName = "farmSenseHarvest";

        public static bool CanHarvest(Block b, BlockValue bv)
        {
            if (b == null || bv.isair) return false;
            if (FarmSenseMain.Config == null || !FarmSenseMain.Config.HarvestByE_Enabled) return false;

            // Baeume sind bewusst ausgenommen: Vanilla faellt sie per Schlagen (Holz + Setzling).
            // Ein E-Kommando an der Endstufe wuerde den ganzen Baum mit einem Tastendruck einsammeln.
            if (CropRegistry.IsReadyBlock(bv.type) && CropRegistry.IsTree(bv.type))
            {
                if (FarmSenseMain.Config.LogDebug)
                    DiagHarvest(b.GetBlockName() + " (id " + bv.type + ") ist ein Baum - kein E-Kommando");
                return false;
            }

            bool ready = CropRegistry.IsHarvestable(bv.type);
            if (ready && FarmSenseMain.Config.LogDebug) DiagHarvest(b.GetBlockName() + " (id " + bv.type + ") ist erntbar");
            return ready;
        }

        static float diagTimer;
        static string diagLast = "";
        static void DiagHarvest(string text)
        {
            float now = Time.time;
            if (text == diagLast && now - diagTimer < 10f) return;
            diagTimer = now;
            diagLast = text;
            Log.Out("[FarmSense][DIAG] " + text);
        }

        /// <summary>Wird vom Aktivierungskommando aufgerufen (Client- oder Serverseite).</summary>
        public static bool Activate(WorldBase worldBase, Vector3i pos, BlockValue bv, EntityPlayerLocal player)
        {
            if (worldBase == null || player == null) return false;
            World world = worldBase as World;
            if (world == null) return false;

            bool isClientOnly = ConnectionManager.Instance != null
                && ConnectionManager.Instance.IsConnected
                && !ConnectionManager.Instance.IsServer;

            if (isClientOnly)
            {
                // Multiplayer-Client: Ernte vom Server ausfuehren lassen
                NetPackageFarmSenseHarvest pkg = NetPackageManager.GetPackage<NetPackageFarmSenseHarvest>()
                    .Setup(player.entityId, pos);
                ConnectionManager.Instance.SendToServer(pkg);
                if (FarmSenseMain.Config.LogDebug) Log.Out("[FarmSense] Ernte-Anfrage an Server gesendet: " + pos.ToString());
                return true;
            }

            return HarvestNow(world, pos, bv, player);
        }

        /// <summary>Eigentliche Ernte – laeuft auf Server/Host.</summary>
        public static bool HarvestNow(World world, Vector3i pos, BlockValue bv, EntityPlayer player)
        {
            if (world == null) return false;
            BlockValue current = world.GetBlock(pos);
            if (current.isair || current.type != bv.type) return false;

            Block b = current.Block;
            if (b == null) return false;

            // Sicherheitsnetz fuer alle Aufrufer (auch den MP-Pfad ueber NetPackageFarmSenseHarvest):
            // Baeume werden nie per E geerntet, nur ueber die normale Schlag-Ernte.
            if (!CropRegistry.IsHarvestable(current.type))
            {
                if (FarmSenseMain.Config != null && FarmSenseMain.Config.LogDebug)
                    Log.Out("[FarmSense] E-Ernte abgelehnt: " + b.GetBlockName()
                        + " ist kein Ernteziel (Baum oder keine Endstufe).");
                return false;
            }

            FarmSenseConfig cfg = FarmSenseMain.Config;
            EntityPlayerLocal local = player as EntityPlayerLocal;

            // 1:1 wie Vanilla ernten: eigene Nachbildung der Vanilla-Rechnung, sonst einfacher Roll.
            bool vanillaPath = GrantDropsVanilla(world, b, current, local, player, cfg);
            if (!vanillaPath)
            {
                List<ItemStack> drops = CollectDrops(world, b, current);
                Deliver(drops, local, player, cfg);
            }

            // Klang wie beim Ernten durch Schlagen: das Zerstoerungs-Geraeusch des Pflanzenblocks
            // (Material Mplants: surface_category=plant -> SoundDataNode "plantdestroy").
            if (cfg.Harvest_PlaySound)
            {
                try
                {
                    Vector3 soundPos = pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
                    GameManager.Instance.PlaySoundAtPositionClient(soundPos, "plantdestroy",
                        AudioRolloffMode.Logarithmic, 20, 1f);
                }
                catch (Exception e)
                {
                    if (cfg.LogDebug) Log.Warning("[FarmSense] Ernte-Sound fehlgeschlagen: " + e.Message);
                }
            }

            world.SetBlockRPC(pos, BlockValue.Air);

            if (cfg.LogDebug)
                Log.Out("[FarmSense] Geerntet: " + b.GetBlockName() + " bei " + pos.ToString()
                    + (vanillaPath ? " (Vanilla-Erntepfad)" : " (eigener Roll)"));
            return true;
        }

        /// <summary>
        /// Ertrag wie Vanilla — eigener Nachbau der Rechnung aus <c>GameUtils.HarvestOnAttack</c>
        /// (V 3.3.0 b18, per IL verifiziert).
        /// <para>Wichtig: Vanilla laeuft dort <b>zwei</b> Schleifen ueber zwei verschiedene Tabellen
        /// (<c>EnumDropEvent.Destroy = 0</c>, <c>Harvest = 2</c>) — mit <b>unterschiedlichem
        /// Basis-Faktor</b>:</para>
        /// <list type="bullet">
        /// <item><b>Destroy-Tabelle</b> (Schluessel 0, IL_0228): Basis <c>1</c>, laeuft nur beim
        /// vernichtenden Treffer (<c>bKilled</c>), <b>keine</b> Raten-Teilung, <c>prob</c> wird vorher
        /// gewuerfelt, Abgabe mit <c>prob=1</c>. → hierher gehoert der Setzling der Beet-Pflanzen
        /// (<c>Destroy plantedX1 count=1 prob=0.5</c>).</item>
        /// <item><b>Harvest-Tabelle</b> (Schluessel 2, IL_04ce): Basis <c>0</c>, laeuft bei jedem
        /// Treffer, Ausgabe in <b>zwei Raten</b> (<c>gesamt − gesamt/3</c> sofort, <c>gesamt/3</c> beim
        /// Zerstoeren). Eintraege, deren <c>tag</c> der Spieler nicht bedient, fallen mit Basis 0 auf
        /// Faktor 0 und entfallen — genau wie im Spiel (z. B. <c>farmerBonusSeed</c> ohne
        /// Landwirt-Stiefel).</item>
        /// </list>
        /// <para>Je Eintrag:</para>
        /// <list type="number">
        /// <item>Basis <c>f</c> = 1 (Destroy) bzw. 0 (Harvest); bei Eintraegen mit
        /// <c>tool_category</c> = 0 (Bonus aus ToolBonuses).</item>
        /// <item>Bei <c>tag</c>: <c>f = EffectManager.GetValue(PassiveEffects.HarvestCount, gehaltenesItem, f,
        /// Spieler, null, FastTags.Parse(tag))</c> → Perk „Farmer“ + Kleidungs-/Werkzeugeffekte.</item>
        /// <item><c>gesamt = (int)(RandomRange(minCount, maxCount+1) * f)</c>; bei &lt;= 0 entfaellt der Eintrag.</item>
        /// <item>Ausgabemodifikator <c>m</c> aus <c>XUiM_Recipes</c> (seedMulti / crop / ore / sonst).</item>
        /// <item>Abgabe: Destroy in einer Rate, Harvest in zwei Raten (zusammen genau <c>gesamt</c>).</item>
        /// </list>
        /// <para>
        /// <b>Wichtig:</b> Die Menge wird hier mit <c>_bScaleCountOnDamage = false</c> an
        /// <c>GameUtils.collectHarvestedItem</c> uebergeben. Vanilla uebergibt in der ersten Rate <c>true</c>
        /// und skaliert ueber <c>attackDetails.damageMax/damageGiven/damageTotalOfTarget</c>; mit selbst
        /// gebauten Trefferdaten teilt diese Rechnung durch <c>damageMax/count = 0</c> und vernichtet die
        /// Menge (Ergebnis: gar keine Items). Ohne Werkzeug-Attacke sind die echten Schadenswerte nicht
        /// reproduzierbar — die volle Menge ist das gewuenschte Ergebnis („Pflanze mit einem Hieb ernten“).
        /// </para>
        /// </summary>
        static bool GrantDropsVanilla(World world, Block b, BlockValue bv, EntityPlayerLocal player,
            EntityPlayer entity, FarmSenseConfig cfg)
        {
            ItemActionData ad = GetHeldActionData(player);
            if (ad == null || b.itemsToDrop == null) return false;

            ItemValue heldValue = null;
            try { if (ad.invData != null) heldValue = ad.invData.itemValue; }
            catch (Exception) { }

            HarvestDiagnostics.LogMath(b, bv, player, ad);

            string log = "";
            RollVanillaTable(world, b, bv, player, entity, ad, heldValue, EnumDropEvent.Harvest, true, cfg, ref log);
            // Destroy-Tabelle (Setzling): rollt Vanilla beim tatsaechlichen Zerstoeren des Blocks.
            RollVanillaTable(world, b, bv, player, entity, ad, heldValue, EnumDropEvent.Destroy, false, cfg, ref log);

            if (cfg.LogDebug)
                Log.Out("[FarmSense] Ernte " + b.GetBlockName() + ": " + (log.Length == 0 ? "keine Items" : log));
            return true;
        }

        /// <summary>Eine Drop-Tabelle wie Vanilla auswerten (siehe <see cref="GrantDropsVanilla"/>).</summary>
        static void RollVanillaTable(World world, Block b, BlockValue bv, EntityPlayerLocal player, EntityPlayer entity,
            ItemActionData ad, ItemValue heldValue, EnumDropEvent ev, bool twoInstalments, FarmSenseConfig cfg, ref string log)
        {
            List<Block.SItemDropProb> list;
            if (!b.itemsToDrop.TryGetValue(ev, out list) || list == null) return;

            GameRandom rnd = world.GetGameRandom();
            for (int i = 0; i < list.Count; i++)
            {
                Block.SItemDropProb d = list[i];
                if (string.IsNullOrEmpty(d.name)) continue;

                // Basis-Faktor der Tabelle, exakt wie Vanilla (IL 3.3.0 b18):
                // Harvest-Schleife (Schluessel 2) startet mit 0 (IL_04ce), Destroy-Schleife
                // (Schluessel 0) mit 1 (IL_0228). Eintraege, deren `tag` kein passiver Wert
                // zugeordnet ist (z. B. `farmerBonusSeed` ohne Landwirt-Stiefel), fallen damit
                // auf 0 und entfallen – genau wie im Spiel.
                float factor = (ev == EnumDropEvent.Harvest) ? 0f : 1f;
                if (!string.IsNullOrEmpty(d.toolCategory)) factor = 0f;
                if (!string.IsNullOrEmpty(d.tag))
                {
                    try
                    {
                        factor = EffectManager.GetValue(PassiveEffects.HarvestCount, heldValue, factor, player,
                            null, FastTags<TagGroup.Global>.Parse(d.tag));
                    }
                    catch (Exception)
                    {
                        factor = 0f;
                    }
                }
                if (factor <= 0f) continue;

                ItemValue iv = null;
                if (d.name == "*")
                {
                    try { iv = bv.ToItemValue(); }
                    catch (Exception) { iv = null; }
                }
                else
                {
                    ItemClass ic = ItemClass.GetItemClass(d.name, true);
                    if (ic != null) iv = new ItemValue(ic.Id, false);
                }
                if (iv == null || iv.type == 0 || iv.ItemClass == null) continue;

                float outMod = 1f;
                if (iv.ItemClass.HasAnyTags(GameUtils.seedMultiTag)) outMod *= XUiM_Recipes.SeedDropOutputModifier;
                else if (b.HasAnyFastTags(GameUtils.cropTag)) outMod *= XUiM_Recipes.CropOutputModifier;
                else if (b.HasAnyFastTags(GameUtils.oreTag)) outMod *= XUiM_Recipes.MiningOutputModifier;
                else outMod *= XUiM_Recipes.HarvestingOutputModifier;
                if (outMod == 0f) continue;

                int total = (int)(rnd.RandomRange(d.minCount, d.maxCount + 1) * factor);
                if (cfg.Harvest_YieldMultiplier != 1f)
                    total = (int)(total * cfg.Harvest_YieldMultiplier + 0.5f);
                if (total <= 0) continue;

                if (!twoInstalments)
                {
                    // Destroy-Tabelle: Vanilla gibt die volle Menge in EINER Rate aus
                    // (IL_0228 … IL_0409, `prob` wird vorher gewuerfelt, Abgabe mit prob=1) –
                    // hier gibt es keine Drittel-Teilung wie in der Harvest-Tabelle. Genau aus
                    // dieser Tabelle kommt der Setzling der Beet-Pflanzen
                    // (`Destroy plantedX1 count=1 prob=0.5`).
                    Grant(ad, iv, Mathf.Max(1, (int)(total * outMod)), d.prob, entity, cfg, rnd, ref log);
                    continue;
                }

                // Harvest-Tabelle: zwei Raten wie Vanilla (2/3 sofort + 1/3 beim Zerstoeren) –
                // zusammen genau `total`.
                int first = total - total / 3;
                if (first > 0) Grant(ad, iv, Mathf.Max(1, (int)(first * outMod)), d.prob, entity, cfg, rnd, ref log);

                int second = total / 3;
                float prob = d.prob;
                if (d.resourceScale > 0f && d.resourceScale < 1f)
                {
                    prob = d.prob / d.resourceScale;
                    second = (int)(second * d.resourceScale);
                    if (second < 1) second += 1;
                }
                if (second > 0) Grant(ad, iv, Mathf.Max(1, (int)(second * outMod)), prob, entity, cfg, rnd, ref log);
            }
        }

        /// <summary>
        /// Items vergeben. Der Wahrscheinlichkeitswurf wird hier selbst gewuerfelt (Vanilla tut das in
        /// <c>collectHarvestedItem</c> und meldet ein verfehltes Wurf mit <c>false</c>) — sonst wuerde der
        /// Ausweich-Pfad unten auch bei verfehltem Wurf einlagern und jeder 50-%-Eintrag zum Garantie-Drop.
        /// Die Abgabe selbst laeuft ueber den Spiel-Weg (Inventar, Popup, XP, Quest-Zaehler); der Ausweich-Pfad
        /// greift nur, wenn *das* fehlschlaegt.
        /// </summary>
        static void Grant(ItemActionData ad, ItemValue iv, int count, float prob, EntityPlayer entity,
            FarmSenseConfig cfg, GameRandom rnd, ref string log)
        {
            if (count <= 0) return;

            string name = iv.ItemClass != null ? iv.ItemClass.GetItemName() : iv.type.ToString();
            if (prob <= 0.999f && rnd.RandomFloat > prob)
            {
                if (cfg.LogDebug)
                    log += (log.Length > 0 ? ", " : "") + name + " (Wurf verfehlt, " + prob.ToString("0.##") + ")";
                return;
            }

            bool ok = false;
            if (ad != null)
            {
                try
                {
                    grantingFromMod = true;
                    ok = GameUtils.collectHarvestedItem(ad, iv, count, 1f, false);
                }
                catch (Exception) { ok = false; }
                finally { grantingFromMod = false; }
            }
            if (!ok && entity != null)
            {
                List<ItemStack> one = new List<ItemStack>();
                one.Add(new ItemStack(new ItemValue(iv.type, false), count));
                Deliver(one, entity as EntityPlayerLocal, entity, cfg);
            }
            if (cfg.LogDebug)
                log += (log.Length > 0 ? ", " : "") + name + " x" + count + (ok ? "" : " (Fallback)");
        }

        /// <summary>ItemActionData des gehaltenen Items (leere Hand = Bare-Hand-Item des Spiels).</summary>
        static ItemActionData GetHeldActionData(EntityPlayerLocal player)
        {
            try
            {
                if (player == null || player.inventory == null) return null;
                ItemInventoryData inv = player.inventory.holdingItemData;
                if (inv == null || inv.actionData == null || inv.actionData.Count == 0) return null;
                for (int i = 0; i < inv.actionData.Count; i++)
                    if (inv.actionData[i] != null) return inv.actionData[i];
            }
            catch (Exception)
            {
            }
            return null;
        }

        /// <summary>Items einlagern (Fallback-Pfad): XUiM-Sammelweg mit Popup/Sound, sonst Rucksack, sonst Boden.</summary>
        static void Deliver(List<ItemStack> drops, EntityPlayerLocal local, EntityPlayer player, FarmSenseConfig cfg)
        {
            LocalPlayerUI ui = null;
            if (local != null)
            {
                try { ui = LocalPlayerUI.GetUIForPlayer(local); }
                catch (Exception) { ui = null; }
            }

            for (int i = 0; i < drops.Count; i++)
            {
                ItemStack st = drops[i];
                if (st.IsEmpty()) continue;
                bool added = false;

                if (ui != null && ui.xui != null && ui.xui.PlayerInventory != null)
                {
                    try { added = ui.xui.PlayerInventory.AddItem(st, cfg.Harvest_PlayCollectSound); }
                    catch (Exception) { added = false; }
                }
                if (!added && local != null && local.bag != null)
                {
                    try { added = local.bag.AddItem(st); }
                    catch (Exception) { added = false; }
                }
                if (!added && GameManager.Instance != null && player != null)
                {
                    GameManager.Instance.ItemDropServer(st, player.position, Vector3.zero, player.entityId, 120f, false);
                }
            }
        }

        /// <summary>Ertrag aus der Vanilla-Drop-Tabelle (Harvest + Destroy), inkl. prob/count.</summary>
        static List<ItemStack> CollectDrops(World world, Block b, BlockValue bv)
        {
            List<ItemStack> result = new List<ItemStack>();
            if (b.itemsToDrop == null) return result;

            skippedInfo = "";
            RollTable(world, b, EnumDropEvent.Harvest, result);
            RollTable(world, b, EnumDropEvent.Destroy, result);

            if (FarmSenseMain.Config.LogDebug)
            {
                HudSystem.Diag("Drops " + b.GetBlockName() + ": " + result.Count + " Stapel"
                    + (skippedInfo.Length > 0 ? " | uebersprungen (Bonus-Tag): " + skippedInfo : ""));
            }
            return result;
        }

        /// <summary>
        /// Vanilla gibt Eintraege mit Bonus-Tags nur heraus, wenn das gehaltene Werkzeug bzw. der Perk
        /// den Wert <c>HarvestCount</c> fuer genau diesen Tag liefert. Belege (V 3.3.0 b18):
        /// <list type="bullet">
        /// <item><c>entityclasses.xml</c> setzt fuer den Spieler <c>HarvestCount base_set 0</c> mit den Tags
        /// <c>bonusCropHarvest</c> und <c>farmerBonusHarvest</c>.</item>
        /// <item><c>progression.xml</c> (Perk Farmer) hebt <c>bonusCropHarvest</c> ab Stufe 2 auf 1.</item>
        /// <item><c>items.xml</c> gibt <c>farmerBonusHarvest</c>/<c>farmerBonusSeed</c> nur ueber
        /// <c>RequirementItemTier</c> + <c>RandomRoll</c> (je Tierschritt) frei.</item>
        /// <item>Der Ernte-Pfad <c>GameUtils.HarvestOnAttack(ItemActionData, Dictionary&lt;string,
        /// ItemActionAttack.Bonuses&gt;)</c> bekommt diese Boni vom Werkzeug — die E-Ernte hat keine
        /// Item-Action, also werden die Bonus-Eintraege hier uebersprungen (sonst: Garantie-Samen).</item>
        /// </list>
        /// </summary>
        static bool IsBonusTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            string[] parts = tag.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string t = parts[i].Trim();
                if (t.Length == 0) continue;
                // Basis-Eintraege, die Vanilla ohne Werkzeug/Perk ausgibt
                if (t == "cropHarvest" || t == "wildCropsHarvest") return false;
            }
            return true;
        }

        static string skippedInfo = "";

        /// <summary>Wahr, solange die Mod selbst Items vergibt (fuer die Diagnose-Unterscheidung).</summary>
        internal static bool grantingFromMod;

        static void RollTable(World world, Block b, EnumDropEvent ev, List<ItemStack> result)
        {
            List<Block.SItemDropProb> list;
            if (!b.itemsToDrop.TryGetValue(ev, out list) || list == null) return;

            FarmSenseConfig cfg = FarmSenseMain.Config;
            GameRandom rnd = world.GetGameRandom();
            for (int i = 0; i < list.Count; i++)
            {
                Block.SItemDropProb d = list[i];
                if (string.IsNullOrEmpty(d.name)) continue;

                if (cfg.Harvest_IgnoreBonusTags && IsBonusTag(d.tag))
                {
                    skippedInfo += (skippedInfo.Length > 0 ? ", " : "") + d.name
                        + (string.IsNullOrEmpty(d.tag) ? "" : " [" + d.tag + "]");
                    continue;
                }

                if (d.prob > 0f && d.prob < 1f && rnd.RandomFloat > d.prob) continue;

                int count = d.minCount;
                if (d.maxCount > d.minCount) count = rnd.RandomRange(d.minCount, d.maxCount + 1);
                if (count <= 0) continue;

                // Vanilla-Sonderfall: Rezept-Referenz ("[recipe]")
                string itemName = d.name;
                if (itemName == "[recipe]")
                {
                    List<Recipe> recipes = CraftingManager.GetRecipes(b.GetBlockName());
                    if (recipes == null || recipes.Count == 0) continue;
                    Recipe r = recipes[0];
                    if (r.ingredients == null || r.ingredients.Count == 0) continue;
                    ItemStack ing = r.ingredients[0];
                    if (ing.IsEmpty()) continue;
                    result.Add(new ItemStack(ing.itemValue, count));
                    continue;
                }

                ItemClass ic = ItemClass.GetItemClass(itemName, true);
                if (ic == null)
                {
                    if (FarmSenseMain.Config.LogDebug) Log.Warning("[FarmSense] Unbekanntes Drop-Item: " + itemName);
                    continue;
                }
                ItemValue iv = new ItemValue(ic.Id, false);
                result.Add(new ItemStack(iv, count));
            }
        }
    }

    [HarmonyPatch(typeof(Block), "HasBlockActivationCommands")]
    internal static class Patch_HasBlockActivationCommands
    {
        static void Postfix(Block __instance, BlockValue _blockValue, ref bool __result)
        {
            if (__result) return;
            if (HarvestLogic.CanHarvest(__instance, _blockValue)) __result = true;
        }
    }

    [HarmonyPatch(typeof(Block), "GetBlockActivationCommands")]
    internal static class Patch_GetBlockActivationCommands
    {
        static void Postfix(Block __instance, BlockValue _blockValue, ref BlockActivationCommand[] __result)
        {
            if (!HarvestLogic.CanHarvest(__instance, _blockValue)) return;

            BlockActivationCommand[] old = __result;
            int len = (old == null) ? 0 : old.Length;
            BlockActivationCommand[] neu = new BlockActivationCommand[len + 1];
            for (int i = 0; i < len; i++) neu[i] = old[i];
            neu[len] = new BlockActivationCommand(HarvestLogic.CommandName, "knife", true, true, null);
            __result = neu;
        }
    }

    [HarmonyPatch(typeof(Block), "OnBlockActivated",
        new Type[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    internal static class Patch_OnBlockActivatedCommand
    {
        static bool Prefix(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue,
            EntityPlayerLocal _player, ref bool __result)
        {
            if (FarmSenseMain.Config.LogDebug)
                Log.Out("[FarmSense][DIAG] OnBlockActivated-Kommando: '" + _commandName + "' bei " + _blockPos.ToString());
            if (_commandName != HarvestLogic.CommandName) return true;
            __result = HarvestLogic.Activate(_world, _blockPos, _blockValue, _player);
            return false;
        }
    }
}
