using System;
using HarmonyLib;
using UnityEngine;

namespace FarmSense
{
    /// <summary>
    /// Diagnose: protokolliert <b>jede</b> Item-Abgabe der Ernte-Routine des Spiels
    /// (<c>GameUtils.collectHarvestedItem</c>) — also auch die normale Vanilla-Ernte per Schlag.
    /// Damit laesst sich in einem einzigen Log vergleichen, was Vanilla (Schlagen) und was die
    /// E-Ernte der Mod vergibt. Nur bei <c>LogDebug=true</c>, max. eine Zeile pro Sekunde.
    /// </summary>
    [HarmonyPatch(typeof(GameUtils), "collectHarvestedItem")]
    internal static class Patch_CollectHarvestedItem_Diag
    {
        static void Postfix(ItemActionData _actionData, ItemValue _iv, int _count, float _prob,
            bool _bScaleCountOnDamage, bool __result)
        {
            if (FarmSenseMain.Config == null || !FarmSenseMain.Config.LogDebug) return;

            try
            {
                string name = (_iv != null && _iv.ItemClass != null) ? _iv.ItemClass.GetItemName() : "?";
                string held = "?";
                if (_actionData != null && _actionData.invData != null && _actionData.invData.itemValue != null
                    && _actionData.invData.itemValue.ItemClass != null)
                    held = _actionData.invData.itemValue.ItemClass.GetItemName();

                // Direkt loggen (nicht ueber HudSystem.Diag): dessen 1-Sekunden-Takt wuerde die Zeilen
                // neben der Ertrag-Diagnose verschlucken.
                Log.Out("[FarmSense][DIAG] Abgabe (" + (HarvestLogic.grantingFromMod ? "E-Ernte" : "Vanilla-Schlag")
                    + "): " + name + " x" + _count
                    + " prob=" + _prob.ToString("0.##")
                    + " Hand=" + held
                    + " Schadensskalierung=" + (_bScaleCountOnDamage ? "an" : "aus")
                    + " -> " + (__result ? "vergeben" : "Wurf verfehlt/keine"));
            }
            catch (Exception)
            {
            }
        }
    }
}
