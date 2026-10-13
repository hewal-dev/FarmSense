using System;
using System.Reflection;
using HarmonyLib;

namespace FarmSense
{
    /// <summary>
    /// Einstiegspunkt der Mod. Registriert die Harmony-Patches und baut das Pflanzenregister auf.
    /// </summary>
    public class FarmSenseMain : IModApi
    {
        public const string HarmonyId = "de.elternvszombies.farmsense";
        public const string Version = "1.0.2";

        public static FarmSenseConfig Config = new FarmSenseConfig();

        public void InitMod(Mod _modInstance)
        {
            Log.Out("[FarmSense] Version " + Version + " wird geladen ...");

            try
            {
                Config = FarmSenseConfig.Load();
            }
            catch (Exception e)
            {
                Log.Error("[FarmSense] Config.xml konnte nicht gelesen werden, Standardwerte aktiv: " + e.Message);
                Config = new FarmSenseConfig();
            }

            try
            {
                CropRegistry.Build();
            }
            catch (Exception e)
            {
                Log.Error("[FarmSense] Pflanzenregister fehlgeschlagen: " + e.ToString());
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Out("[FarmSense] Harmony-Patches registriert.");
            }
            catch (Exception e)
            {
                Log.Error("[FarmSense] Harmony-Patches fehlgeschlagen: " + e.ToString());
            }

            Log.Out("[FarmSense] bereit. Erkannte Pflanzenblöcke=" + CropRegistry.Count
                + " ErnteMitE=" + Config.HarvestByE_Enabled
                + " HUD=" + Config.Hud_Mode
                + " VanillaPrompt=" + Config.Hud_UseVanillaPrompt
                + " Wildwuchs=" + Config.WildGrowth_Enabled);
        }
    }
}
