using System;
using HarmonyLib;
using UnityEngine;

namespace FarmSense
{
    /// <summary>Erzeugt die Anzeigetexte und schiebt sie ins HUD-Fenster.</summary>
    public static class HudSystem
    {
        static float diagTimer;
        static string diagLast = "";
        static float hideDelay;

        /// <summary>Diagnose: maximal einmal pro Sekunde und nur bei geaenderter Aussage.</summary>
        internal static void Diag(string text)
        {
            if (FarmSenseMain.Config == null || !FarmSenseMain.Config.LogDebug) return;
            float now = Time.time;
            if (text == diagLast && now - diagTimer < 5f) return;
            if (now - diagTimer < 1f) return;
            diagTimer = now;
            diagLast = text;
            Log.Out("[FarmSense][DIAG] " + text);
        }

        public static void UpdateLocalPlayer(World world)
        {
            if (world == null) return;
            FarmSenseConfig cfg = FarmSenseMain.Config;
            if (cfg == null) return;

            bool windowMode = (cfg.Hud_Mode == "window" || cfg.Hud_Mode == "both");
            if (!windowMode) return;
            if (world.Players == null || world.Players.list == null) return;

            for (int i = 0; i < world.Players.list.Count; i++)
            {
                EntityPlayer p = world.Players.list[i];
                if (p == null) continue;
                EntityPlayerLocal lp = p as EntityPlayerLocal;
                if (lp == null) continue;
                if (lp.playerUI == null) continue;
                if (lp.IsDead()) continue;

                string t;
                string d;
                string w;
                float f;
                if (BuildTexts(lp, world, out t, out d, out w, out f))
                {
                    hideDelay = 0f;
                    Diag("HUD-Inhalt: " + t + " | " + d + (string.IsNullOrEmpty(w) ? "" : " | " + w));
                    XUiC_FarmSenseHud.Set(lp.playerUI, t, d, w, f);
                }
                else
                {
                    // Ausblenden wie Vanilla: standardmaessig sofort (Hud_HideDelay = 0).
                    // Ein kleiner Wert (>0) haelt das Feld bei einzelnen ungueltigen Frames stehen.
                    hideDelay += Time.deltaTime;
                    if (hideDelay >= cfg.Hud_HideDelay) XUiC_FarmSenseHud.Hide(lp.playerUI);
                }
            }
        }

        /// <summary>Texte fuer das aktuell anvisierte Ziel bauen. false = nichts anzeigen.</summary>
        public static bool BuildTexts(EntityPlayerLocal player, World world, out string stage, out string detail,
            out string warning, out float fill)
        {
            stage = "";
            detail = "";
            warning = "";
            fill = 0f;

            FarmSenseConfig cfg = FarmSenseMain.Config;
            WorldRayHitInfo hi = player.HitInfo;
            if (hi == null)
            {
                Diag("HitInfo ist null");
                return false;
            }
            if (!hi.bHitValid)
            {
                Diag("Ziel ungueltig (bHitValid=false)");
                return false;
            }

            Vector3i pos = hi.hit.blockPos;
            BlockValue bv = hi.hit.blockValue;
            if (bv.isair)
            {
                Diag("angvisiert: Luft bei " + pos.ToString());
                return false;
            }

            PlantInfo info = CropRegistry.Get(world, pos, bv);
            if (!info.IsPlant)
            {
                Diag("kein Pflanzenblock: " + bv.Block.GetBlockName() + " (id " + bv.type + "), Register=" + CropRegistry.Count);
                return false;
            }

            Diag("Pflanze: " + info.BlockName + " Stufe " + info.Stage + "/" + info.StageCount
                + " waechst=" + info.Growing + " reif=" + info.IsReady + " pct=" + Mathf.RoundToInt(info.Percent * 100f)
                + " rest=" + Mathf.RoundToInt((float)info.SecondsLeft) + "s warn=" + info.WarnKey);

            if (!cfg.Hud_ShowTrees && info.IsTree) return false;

            fill = info.Percent;

            // Kopfzeile: standardmaessig NUR die Wachstumsstufe in Kurzform ("2/3"). Der Pflanzenname
            // steht direkt darueber im Bildschirmzentrum (Vanilla-Blockname bei wachsenden Pflanzen,
            // Mod-Prompt bei reifen) und wird hier deshalb nicht wiederholt.
            // Mit Hud_ShowName=true steht hier wieder der Name, die Stufe rutscht dann in Zeile 2.
            string stageShort = "";
            if (cfg.Hud_ShowStage && info.StageCount > 1)
                stageShort = string.Format(Localization.Get("farmSenseStageShort"), info.Stage, info.StageCount);

            if (cfg.Hud_ShowName || info.IsTree)
            {
                // Baeume zeigen ihren Namen immer: in der Vanilla-Zeile steht bei ihnen keiner
                // (kein Aktivierungskommando), und die Stufe allein sagt nicht, welcher Baum es ist.
                stage = info.IsWild ? string.Format(Localization.Get("farmSenseWild"), info.DisplayName) : info.DisplayName;
                detail = stageShort;
            }
            else stage = stageShort;

            if (info.IsDead)
            {
                AddDetail(ref detail, Localization.Get("farmSenseDeadShort"));
                fill = 0f;
                return true;
            }

            if (info.IsReady)
            {
                // Baeume sind von der E-Ernte ausgenommen (Vanilla faellt sie per Schlagen),
                // also dort keinen "Ernten mit E"-Hinweis anzeigen.
                if (info.IsTree) AddDetail(ref detail, Localization.Get("farmSenseReadyShort"));
                else AddDetail(ref detail, Localization.Get("farmSenseReadyShort") + " \u00b7 " + Localization.Get("farmSensePressHarvestText"));
                fill = 1f;
                return true;
            }

            if (info.Growing)
            {
                AddDetail(ref detail, string.Format(Localization.Get("farmSenseGrowDetail"),
                    Mathf.RoundToInt(info.Percent * 100f), HudSystem.TimeString(info.SecondsLeft)));
                return true;
            }

            if (!string.IsNullOrEmpty(info.WarnKey) && cfg.Hud_ShowWarnings)
            {
                warning = string.Format(Localization.Get(info.WarnKey), info.DisplayName);
                return true;
            }

            AddDetail(ref detail, Localization.Get("farmSenseWarnUnknownShort"));
            return true;
        }

        /// <summary>Wertezeile aufbauen. Traegt bereits einen Text (z. B. Stufe bei Hud_ShowName), wird angehaengt.</summary>
        static void AddDetail(ref string detail, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (string.IsNullOrEmpty(detail)) detail = text;
            else detail = detail + " \u00b7 " + text;
        }

        public static string TimeString(double seconds)
        {
            if (seconds <= 30.0) return Localization.Get("farmSenseTimeNow");
            if (seconds < 3600.0)
                return string.Format(Localization.Get("farmSenseTimeMin"), (int)Math.Ceiling(seconds / 60.0));
            if (seconds < 86400.0)
            {
                int hours = (int)(seconds / 3600.0);
                int minutes = (int)((seconds - hours * 3600.0) / 60.0);
                return string.Format(Localization.Get("farmSenseTimeHour"), hours, minutes);
            }
            int days = (int)(seconds / 86400.0);
            int restHours = (int)((seconds - days * 86400.0) / 3600.0);
            return string.Format(Localization.Get("farmSenseTimeDay"), days, restHours);
        }
    }

    /// <summary>
    /// Optionaler Zusatz: dieselbe Info im Vanilla-Interaktionsprompt (Hud_UseVanillaPrompt).
    /// Der Prompt-Text darf nur ein {0} enthalten – das fuellt das Spiel mit der Tastenanzeige.
    /// </summary>
    [HarmonyPatch(typeof(Block), "GetActivationText")]
    internal static class Patch_GetActivationText
    {
        static void Postfix(Block __instance, WorldBase _world, BlockValue _blockValue, Vector3i _blockPos,
            EntityAlive _entityFocusing, ref string __result)
        {
            FarmSenseConfig cfg = FarmSenseMain.Config;
            if (cfg == null || !cfg.Hud_UseVanillaPrompt)
            {
                if (cfg != null && cfg.LogDebug) Log.Out("[FarmSense][DIAG] GetActivationText: Prompt-Text aus (Hud_UseVanillaPrompt=false)");
                return;
            }

            World world = _world as World;
            if (world == null) return;
            EntityPlayerLocal player = _entityFocusing as EntityPlayerLocal;
            if (player == null) return;

            PlantInfo info = CropRegistry.Get(world, _blockPos, _blockValue);
            if (!info.IsPlant) return;

            string body;
            if (info.IsDead) body = string.Format(Localization.Get("farmSenseDead"), info.DisplayName);
            else if (info.IsReady) body = string.Format(Localization.Get("farmSenseReady"), info.DisplayName);
            else if (info.Growing)
                body = string.Format(Localization.Get("farmSenseGrowing"), info.DisplayName,
                    Mathf.RoundToInt(info.Percent * 100f), HudSystem.TimeString(info.SecondsLeft));
            else if (!string.IsNullOrEmpty(info.WarnKey)) body = string.Format(Localization.Get(info.WarnKey), info.DisplayName);
            else body = string.Format(Localization.Get("farmSenseWarnUnknown"), info.DisplayName);

            // Baeume sind von der E-Ernte ausgenommen: Info anzeigen, aber keinen Tasten-Hinweis.
            if (info.IsReady && !info.IsTree) __result = body + " \u00b7 " + Localization.Get("farmSensePressHarvest");
            else __result = body;

            HudSystem.Diag("Prompt-Text: " + __result);
        }
    }
}
