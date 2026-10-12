using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace FarmSense
{
    /// <summary>
    /// Liest Config.xml (liegt neben der FarmSense.dll). Fehlende Einträge → Standardwert.
    /// </summary>
    public class FarmSenseConfig
    {
        public bool LogDebug = false;

        // Ernte per E
        public bool HarvestByE_Enabled = true;
        public bool Harvest_PlaySound = true;          // Zerstoerungs-Sound des Pflanzenblocks abspielen
        public bool Harvest_PlayCollectSound = true;   // Vanilla-Sammel-Sound/Popup beim Einlagern
        public bool Harvest_IgnoreBonusTags = true;    // Bonus-Eintraege (farmerBonus*/bonusCropHarvest)
                                                       // ueberspringen – Vanilla gibt sie nur mit
                                                       // Werkzeug-/Perk-Bonus heraus (sonst Garantie-Samen)
        // 1.0 = Vanilla-Erntemenge. Nur bewusst aendern, wenn du mit mehr/weniger Ernte spielen willst.
        public float Harvest_YieldMultiplier = 1f;
        public bool Hud_UseVanillaPrompt = false;

        // HUD
        public string Hud_Mode = "window";          // window | prompt | both | off
        public bool Hud_ShowTrees = true;
        public bool Hud_ShowWarnings = true;
        public bool Hud_ShowStage = true;           // Wachstumsstufe kurz ("2/3") in der Kopfzeile
        public bool Hud_ShowName = false;           // Pflanzenname zusaetzlich im Feld (Standard: aus,
                                                    // weil der Name schon in der Bildschirmmitte steht)
        public float Hud_UpdateInterval = 0f;        // 0 = jeden Frame aktualisieren (sofort sichtbar),
                                                     // >0 = Sekunden zwischen den Aktualisierungen
        public float Hud_HideDelay = 0f;             // 0 = sofort ausblenden wie Vanilla,
                                                     // >0 = Sekunden Nachlauf gegen Flackern

        // Wildwuchs
        public bool WildGrowth_Enabled = true;
        public int WildGrowth_IntervalSeconds = 120;
        public int WildGrowth_Radius = 40;
        public int WildGrowth_AttemptsPerInterval = 12;
        public float WildGrowth_ChancePerAttempt = 0.25f;
        public int WildGrowth_MaxPerChunk = 2;
        public int WildGrowth_MinLight = 11;
        public List<string> WildGrowth_Biomes = new List<string>();
        public List<string> WildGrowth_Blocks = new List<string>();

        public FarmSenseConfig()
        {
            WildGrowth_Biomes.Add("pine_forest");
            WildGrowth_Blocks.Add("plantedCorn3Harvest");
            WildGrowth_Blocks.Add("plantedPotato3Harvest");
            WildGrowth_Blocks.Add("plantedBlueberry3Harvest");
            WildGrowth_Blocks.Add("plantedPumpkin3Harvest");
        }

        public static string ModDirectory
        {
            get
            {
                try
                {
                    string loc = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(loc)) return ".";
                    return Path.GetDirectoryName(loc);
                }
                catch (Exception)
                {
                    return ".";
                }
            }
        }

        public static FarmSenseConfig Load()
        {
            FarmSenseConfig c = new FarmSenseConfig();
            string path = Path.Combine(ModDirectory, "Config.xml");
            if (!File.Exists(path))
            {
                Log.Warning("[FarmSense] Config.xml nicht gefunden (" + path + ") – Standardwerte aktiv.");
                return c;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement root = doc.DocumentElement;
            if (root == null) return c;

            c.LogDebug = GetBool(root, "LogDebug", c.LogDebug);

            c.HarvestByE_Enabled = GetBool(root, "HarvestByE_Enabled", c.HarvestByE_Enabled);
            c.Harvest_PlaySound = GetBool(root, "Harvest_PlaySound", c.Harvest_PlaySound);
            c.Harvest_PlayCollectSound = GetBool(root, "Harvest_PlayCollectSound", c.Harvest_PlayCollectSound);
            c.Harvest_IgnoreBonusTags = GetBool(root, "Harvest_IgnoreBonusTags", c.Harvest_IgnoreBonusTags);
            c.Harvest_YieldMultiplier = GetFloat(root, "Harvest_YieldMultiplier", c.Harvest_YieldMultiplier);
            c.Hud_UseVanillaPrompt = GetBool(root, "Hud_UseVanillaPrompt", c.Hud_UseVanillaPrompt);

            c.Hud_Mode = GetString(root, "Hud_Mode", c.Hud_Mode).Trim().ToLowerInvariant();
            c.Hud_ShowTrees = GetBool(root, "Hud_ShowTrees", c.Hud_ShowTrees);
            c.Hud_ShowWarnings = GetBool(root, "Hud_ShowWarnings", c.Hud_ShowWarnings);
            c.Hud_ShowStage = GetBool(root, "Hud_ShowStage", c.Hud_ShowStage);
            c.Hud_ShowName = GetBool(root, "Hud_ShowName", c.Hud_ShowName);
            c.Hud_UpdateInterval = GetFloat(root, "Hud_UpdateInterval", c.Hud_UpdateInterval);
            c.Hud_HideDelay = GetFloat(root, "Hud_HideDelay", c.Hud_HideDelay);

            c.WildGrowth_Enabled = GetBool(root, "WildGrowth_Enabled", c.WildGrowth_Enabled);
            c.WildGrowth_IntervalSeconds = GetInt(root, "WildGrowth_IntervalSeconds", c.WildGrowth_IntervalSeconds);
            c.WildGrowth_Radius = GetInt(root, "WildGrowth_Radius", c.WildGrowth_Radius);
            c.WildGrowth_AttemptsPerInterval = GetInt(root, "WildGrowth_AttemptsPerInterval", c.WildGrowth_AttemptsPerInterval);
            c.WildGrowth_ChancePerAttempt = GetFloat(root, "WildGrowth_ChancePerAttempt", c.WildGrowth_ChancePerAttempt);
            c.WildGrowth_MaxPerChunk = GetInt(root, "WildGrowth_MaxPerChunk", c.WildGrowth_MaxPerChunk);
            c.WildGrowth_MinLight = GetInt(root, "WildGrowth_MinLight", c.WildGrowth_MinLight);

            List<string> biomes = GetList(root, "WildGrowth_Biomes");
            if (biomes.Count > 0) c.WildGrowth_Biomes = biomes;
            List<string> blocks = GetList(root, "WildGrowth_Blocks");
            if (blocks.Count > 0) c.WildGrowth_Blocks = blocks;

            if (c.Hud_UpdateInterval < 0.05f) c.Hud_UpdateInterval = 0.05f;
            if (c.WildGrowth_IntervalSeconds < 10) c.WildGrowth_IntervalSeconds = 10;
            if (c.WildGrowth_Radius < 8) c.WildGrowth_Radius = 8;
            if (c.WildGrowth_Radius > 200) c.WildGrowth_Radius = 200;
            if (c.WildGrowth_AttemptsPerInterval < 1) c.WildGrowth_AttemptsPerInterval = 1;
            if (c.WildGrowth_AttemptsPerInterval > 200) c.WildGrowth_AttemptsPerInterval = 200;
            if (c.WildGrowth_MaxPerChunk < 0) c.WildGrowth_MaxPerChunk = 0;

            if (c.LogDebug) Log.Out("[FarmSense] Config geladen: " + path);
            return c;
        }

        static XmlElement Find(XmlElement root, string name)
        {
            XmlNodeList list = root.GetElementsByTagName(name);
            if (list == null || list.Count == 0) return null;
            return list[0] as XmlElement;
        }

        static bool GetBool(XmlElement root, string name, bool def)
        {
            XmlElement e = Find(root, name);
            if (e == null) return def;
            string v = e.InnerText.Trim().ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes" || v == "ja") return true;
            if (v == "false" || v == "0" || v == "no" || v == "nein") return false;
            return def;
        }

        static int GetInt(XmlElement root, string name, int def)
        {
            XmlElement e = Find(root, name);
            if (e == null) return def;
            int result;
            if (int.TryParse(e.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) return result;
            return def;
        }

        static float GetFloat(XmlElement root, string name, float def)
        {
            XmlElement e = Find(root, name);
            if (e == null) return def;
            float result;
            if (float.TryParse(e.InnerText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
            return def;
        }

        static string GetString(XmlElement root, string name, string def)
        {
            XmlElement e = Find(root, name);
            if (e == null) return def;
            return e.InnerText;
        }

        static List<string> GetList(XmlElement root, string name)
        {
            List<string> result = new List<string>();
            XmlElement e = Find(root, name);
            if (e == null) return result;
            string[] parts = e.InnerText.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length > 0) result.Add(p);
            }
            return result;
        }
    }
}
