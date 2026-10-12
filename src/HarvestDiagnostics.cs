using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmSense
{
    /// <summary>
    /// Diagnose der Ertragsrechnung (nur bei <c>LogDebug=true</c>). Bildet genau die Rechnung nach, die
    /// <c>GameUtils.HarvestOnAttack</c> fuer jeden Drop-Eintrag anstellt, und schreibt die tatsaechlich
    /// vom Spiel gelieferten Werte ins Log:
    /// <list type="number">
    /// <item>Basiswert: 1 (bzw. 0 bei Eintraegen mit <c>tool_category</c>)</item>
    /// <item>bei Eintraegen mit <c>tag</c>: <c>EffectManager.GetValue(PassiveEffects.HarvestCount,
    /// gehaltenesItem, Basis, Spieler, null, FastTags.Parse(tag))</c> – enthaelt Perk- und
    /// Werkzeug-Skalierung</item>
    /// <item>Mengenwurf <c>RandomRange(minCount, maxCount+1) * Faktor</c>, Verwerfen bei &lt;= 0</item>
    /// <item>Ausgabemodifikator (<c>SeedDropOutputModifier</c>/<c>CropOutputModifier</c>/
    /// <c>MiningOutputModifier</c>/<c>HarvestingOutputModifier</c>), Ergebnis <c>Max(1, …)</c></item>
    /// </list>
    /// Damit laesst sich ohne Raetselraten pruefen, wo eine Menge verloren geht.
    /// </summary>
    internal static class HarvestDiagnostics
    {
        static float timer;
        static string last = "";

        public static void LogMath(Block b, BlockValue bv, EntityPlayerLocal player, ItemActionData ad)
        {
            if (b == null || b.itemsToDrop == null) return;
            if (FarmSenseMain.Config == null || !FarmSenseMain.Config.LogDebug) return;

            List<Block.SItemDropProb> list;
            if (!b.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out list) || list == null) return;

            string held = "(leer)";
            ItemValue heldValue = null;
            try
            {
                if (ad != null && ad.invData != null)
                {
                    heldValue = ad.invData.itemValue;
                    if (heldValue != null && !heldValue.IsEmpty() && heldValue.ItemClass != null)
                        held = heldValue.ItemClass.GetItemName();
                }
            }
            catch (Exception) { }

            string line = "[FarmSense][DIAG] Ertrag " + b.GetBlockName() + " | Hand: " + held
                + " | Modifikatoren: harvest=" + XUiM_Recipes.HarvestingOutputModifier.ToString("0.###")
                + " crop=" + XUiM_Recipes.CropOutputModifier.ToString("0.###")
                + " seed=" + XUiM_Recipes.SeedDropOutputModifier.ToString("0.###")
                + " mining=" + XUiM_Recipes.MiningOutputModifier.ToString("0.###")
                + " | BlockTags=" + (b.HasAnyFastTags(GameUtils.cropTag) ? "crop " : "")
                + (b.HasAnyFastTags(GameUtils.oreTag) ? "ore " : "");

            for (int i = 0; i < list.Count; i++)
            {
                Block.SItemDropProb d = list[i];
                float factor = 1f;
                if (!string.IsNullOrEmpty(d.toolCategory)) factor = 0f;
                if (!string.IsNullOrEmpty(d.tag))
                {
                    try
                    {
                        factor = EffectManager.GetValue(PassiveEffects.HarvestCount, heldValue, factor, player,
                            null, FastTags<TagGroup.Global>.Parse(d.tag));
                    }
                    catch (Exception e)
                    {
                        factor = -1f;
                        line += " | FEHLER GetValue(" + d.tag + "): " + e.Message;
                    }
                }

                int min = d.minCount;
                int max = d.maxCount;
                string result;
                if (factor <= 0f) result = "uebersprungen (Faktor <= 0)";
                else
                {
                    int full = (int)(max * factor);
                    result = "voll " + (int)(min * factor) + ".." + full
                        + " (bei Kill-Pfad: " + (full / 3) + ")";
                }

                line += " | " + d.name + (string.IsNullOrEmpty(d.tag) ? "" : " [" + d.tag + "]")
                    + " prob=" + d.prob.ToString("0.##")
                    + " count=" + min + ".." + max
                    + " faktor=" + factor.ToString("0.###")
                    + " => " + result;
            }

            // Nur einmal pro Sekunde und nur bei geaenderter Aussage loggen.
            float now = Time.time;
            if (line == last && now - timer < 5f) return;
            if (now - timer < 1f) return;
            timer = now;
            last = line;
            Log.Out(line);
        }
    }
}
