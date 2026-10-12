using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmSense
{
    /// <summary>Eine Wachstumsstufe einer Pflanze.</summary>
    public class CropStage
    {
        public int BlockId;
        public string Name = "";
        public double Seconds;      // Zeit bis zur naechsten Stufe (0 = Endstufe)
        public bool IsGrowing;      // diese Stufe waechst weiter
    }

    /// <summary>Wachstumskette einer Pflanze (Mais: Samen -> Anbau -> Ernte).</summary>
    public class CropChain
    {
        public List<CropStage> Stages = new List<CropStage>();
        public string DisplayName = "";
        public bool IsPlayerCrop = true;

        public double SecondsFrom(int stageIndex)
        {
            double sum = 0;
            for (int i = stageIndex; i < Stages.Count; i++) sum += Stages[i].Seconds;
            return sum;
        }

        public double SecondsAfter(int stageIndex)
        {
            double sum = 0;
            for (int i = stageIndex + 1; i < Stages.Count; i++) sum += Stages[i].Seconds;
            return sum;
        }
    }

    /// <summary>Ergebnis der Zustandsauswertung fuer eine konkrete Pflanze in der Welt.</summary>
    public class PlantInfo
    {
        public bool IsPlant;
        public bool Growing;
        public bool IsReady;
        public bool IsDead;
        public bool IsWild;
        public float Percent;
        public double SecondsLeft;
        public double SecondsTotal;
        public int Stage;
        public int StageCount;
        public string BlockName = "";
        public string DisplayName = "";
        public string WarnKey = "";
    }

    /// <summary>
    /// Baut beim Start ein Register aller Pflanzenketten aus Block.list auf und wertet zur Laufzeit
    /// den Wachstumszustand aus. Datenquelle fuer die Restzeit ist der World-Ticker
    /// (world.GetWBT().scheduledTicksDict, Schluessel = WorldBlockTickerEntry.ToHashCode(pos, blockID)).
    /// </summary>
    public static class CropRegistry
    {
        static readonly Dictionary<int, CropChain> chainByBlock = new Dictionary<int, CropChain>();
        static readonly Dictionary<int, int> stageByBlock = new Dictionary<int, int>();
        static bool built;
        static bool buildTried;

        public static int Count { get { return chainByBlock.Count; } }

        /// <summary>
        /// Register bei Bedarf aufbauen. Wichtig: zur InitMod-Zeit ist Block.list noch NULL
        /// (Bloecke werden erst nach dem XML-Laden erzeugt) – deshalb lazy mit Wiederholung.
        /// </summary>
        public static void EnsureBuilt()
        {
            if (built) return;
            Build();
        }

        public static void Build()
        {
            if (built) return;

            if (Block.list == null || Block.list.Length == 0)
            {
                if (!buildTried)
                {
                    buildTried = true;
                    Log.Out("[FarmSense] Bloecke noch nicht geladen – Pflanzenregister wird spaeter aufgebaut.");
                }
                return;
            }
            built = true;

            HashSet<int> hasParent = new HashSet<int>();
            for (int i = 0; i < Block.list.Length; i++)
            {
                Block b = Block.list[i];
                if (b == null) continue;
                BlockPlantGrowing pg = b as BlockPlantGrowing;
                if (pg == null) continue;
                if (pg.nextPlant.isair) continue;
                Block nb = pg.nextPlant.Block;
                if (nb == null) continue;
                hasParent.Add(nb.blockID);
            }

            int chains = 0;
            for (int i = 0; i < Block.list.Length; i++)
            {
                Block b = Block.list[i];
                if (b == null) continue;
                BlockPlantGrowing pg = b as BlockPlantGrowing;
                if (pg == null) continue;
                if (hasParent.Contains(b.blockID)) continue;
                CropChain c = ChainFrom(b);
                if (c == null || c.Stages.Count == 0) continue;
                Register(c);
                chains++;
            }

            // Einzeln stehende Ernte-Bloecke (wilde Kulturen, POI-Pflanzen) nach Vanilla-Namenskonvention
            int singles = 0;
            for (int i = 0; i < Block.list.Length; i++)
            {
                Block b = Block.list[i];
                if (b == null) continue;
                if (chainByBlock.ContainsKey(b.blockID)) continue;
                string n = b.GetBlockName();
                if (string.IsNullOrEmpty(n)) continue;
                if (n.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) < 0) continue;
                CropChain c = new CropChain();
                c.IsPlayerCrop = n.EndsWith("HarvestPlayer", StringComparison.OrdinalIgnoreCase);
                c.DisplayName = WildDisplayName(b);
                CropStage st = new CropStage();
                st.BlockId = b.blockID;
                st.Name = n;
                st.Seconds = 0;
                st.IsGrowing = false;
                c.Stages.Add(st);
                Register(c);
                singles++;
            }

            Log.Out("[FarmSense] Pflanzenregister: " + chains + " Ketten, " + singles + " Einzelbloecke, "
                + chainByBlock.Count + " Bloecke gesamt.");
        }

        static CropChain ChainFrom(Block root)
        {
            CropChain c = new CropChain();
            c.IsPlayerCrop = true;

            Block cur = root;
            HashSet<int> seen = new HashSet<int>();
            while (cur != null && !seen.Contains(cur.blockID))
            {
                seen.Add(cur.blockID);

                CropStage st = new CropStage();
                st.BlockId = cur.blockID;
                st.Name = cur.GetBlockName();

                BlockPlantGrowing pg = cur as BlockPlantGrowing;
                st.IsGrowing = false;
                if (pg != null && !pg.nextPlant.isair)
                {
                    st.IsGrowing = true;
                    try { st.Seconds = (double)pg.GetTickRate() / 20.0; }
                    catch (Exception) { st.Seconds = 0; }
                }

                if (c.Stages.Count == 0) c.DisplayName = PrettyName(cur);
                c.Stages.Add(st);

                if (pg == null || pg.nextPlant.isair) break;
                Block nb = pg.nextPlant.Block;
                if (nb == null) break;
                cur = nb;
            }

            return c;
        }

        static void Register(CropChain c)
        {
            for (int i = 0; i < c.Stages.Count; i++)
            {
                int id = c.Stages[i].BlockId;
                if (chainByBlock.ContainsKey(id)) continue;
                chainByBlock[id] = c;
                stageByBlock[id] = i;
            }
        }

        /// <summary>Anzeigename: lokalisierter Blockname ohne Klammerzusatz ("Mais (Samen)" -> "Mais").</summary>
        static string PrettyName(Block b)
        {
            string n = "";
            try { n = b.GetLocalizedBlockName(); }
            catch (Exception) { n = ""; }
            if (string.IsNullOrEmpty(n)) n = b.GetBlockName();
            int p = n.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0) n = n.Substring(0, p);
            return n.Trim();
        }

        /// <summary>
        /// Anzeigename fuer einzeln stehende Ernte-Bloecke (wilde Kulturen).
        /// Vanilla-Namen wie "Heidelbeerpflanze 3 (Sehenswuerdigkeit)" tragen eine Stufennummer,
        /// die im HUD wie eine Zahl/Ertragsangabe wirkt. Deshalb datengetrieben den Namen des
        /// Ernte-Erzeugnisses aus der Drop-Tabelle nehmen ("resourceCotton" -> "Baumwolle").
        /// </summary>
        static string WildDisplayName(Block b)
        {
            try
            {
                List<Block.SItemDropProb> list;
                if (b.itemsToDrop != null && b.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out list) && list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string dropName = list[i].name;
                        if (string.IsNullOrEmpty(dropName) || dropName == "[recipe]") continue;
                        ItemClass ic = ItemClass.GetItemClass(dropName, true);
                        if (ic == null) continue;
                        string loc = ic.GetLocalizedItemName();
                        if (!string.IsNullOrEmpty(loc)) return PrettyNameText(loc);
                    }
                }
            }
            catch (Exception)
            {
            }
            // Fallback: Blockname ohne Klammerzusatz und ohne angehaengte Stufenziffer
            string name = PrettyName(b);
            int lastSpace = name.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                string tail = name.Substring(lastSpace + 1);
                bool digits = tail.Length > 0;
                for (int i = 0; i < tail.Length; i++)
                    if (!char.IsDigit(tail[i])) { digits = false; break; }
                if (digits) name = name.Substring(0, lastSpace).Trim();
            }
            return name;
        }

        static string PrettyNameText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int p = text.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0) text = text.Substring(0, p);
            return text.Trim();
        }

        public static bool TryGetInfo(int blockId, out CropChain chain, out int stage)
        {
            chain = null;
            stage = -1;
            EnsureBuilt();
            if (!chainByBlock.TryGetValue(blockId, out chain)) { chain = null; return false; }
            if (!stageByBlock.TryGetValue(blockId, out stage)) { chain = null; return false; }
            return true;
        }

        /// <summary>true, wenn dieser Block die letzte (erntbare) Stufe einer Pflanze ist.</summary>
        public static bool IsReadyBlock(int blockId)
        {
            CropChain c;
            int s;
            if (!TryGetInfo(blockId, out c, out s)) return false;
            return s == c.Stages.Count - 1;
        }

        public static bool IsKnownPlant(int blockId)
        {
            EnsureBuilt();
            return chainByBlock.ContainsKey(blockId);
        }

        public static PlantInfo Get(World world, Vector3i pos, BlockValue bv)
        {
            PlantInfo info = new PlantInfo();
            if (world == null || bv.isair) return info;
            EnsureBuilt();

            CropChain chain;
            int stage;
            if (!TryGetInfo(bv.type, out chain, out stage)) return info;

            info.IsPlant = true;
            info.BlockName = chain.Stages[stage].Name;
            info.DisplayName = chain.DisplayName;
            info.Stage = stage + 1;
            info.StageCount = chain.Stages.Count;
            info.IsWild = !chain.IsPlayerCrop;
            info.IsReady = (stage == chain.Stages.Count - 1);
            info.IsDead = info.BlockName.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0;
            info.SecondsTotal = chain.SecondsFrom(stage);

            if (info.IsDead)
            {
                info.Percent = 0f;
                return info;
            }

            if (info.IsReady)
            {
                info.Percent = 1f;
                info.SecondsLeft = 0;
                info.Growing = false;
                return info;
            }

            double rest = RemainingInStage(world, pos, bv);
            if (rest < 0)
            {
                info.Growing = false;
                info.SecondsLeft = info.SecondsTotal;
                info.Percent = 0f;
                if (FarmSenseMain.Config.Hud_ShowWarnings) info.WarnKey = WarnReason(world, pos, bv);
            }
            else
            {
                info.Growing = true;
                info.SecondsLeft = rest + chain.SecondsAfter(stage);
                if (info.SecondsTotal > 0.5)
                {
                    double p = 1.0 - info.SecondsLeft / info.SecondsTotal;
                    if (p < 0.0) p = 0.0;
                    if (p > 1.0) p = 1.0;
                    info.Percent = (float)p;
                }
                else
                {
                    info.Percent = 1f;
                }
            }

            return info;
        }

        /// <summary>Restzeit der aktuellen Stufe aus dem World-Ticker; -1 wenn kein Eintrag existiert.</summary>
        public static double RemainingInStage(World world, Vector3i pos, BlockValue bv)
        {
            try
            {
                WorldBlockTicker wbt = world.GetWBT();
                if (wbt == null || wbt.scheduledTicksDict == null) return -1;
                int key = WorldBlockTickerEntry.ToHashCode(pos, bv.type);
                WorldBlockTickerEntry entry;
                if (!wbt.scheduledTicksDict.TryGetValue(key, out entry) || entry == null) return -1;
                long rem = (long)entry.scheduledTime - (long)GameTimer.Instance.ticks;
                if (rem < 0) rem = 0;
                return (double)rem / 20.0;
            }
            catch (Exception e)
            {
                if (FarmSenseMain.Config.LogDebug) Log.Warning("[FarmSense] Ticker-Lookup: " + e.Message);
                return -1;
            }
        }

        /// <summary>Grund, warum eine Pflanze nicht waechst (Lokalisierungsschluessel).</summary>
        static string WarnReason(World world, Vector3i pos, BlockValue bv)
        {
            BlockPlantGrowing pg = bv.Block as BlockPlantGrowing;
            if (pg == null) return "farmSenseWarnUnknown";
            try
            {
                if (!pg.CanGrowOn(world, pos + Vector3i.down, bv)) return "farmSenseWarnSoil";
                if (pg.lightLevelGrow > 0 && world.GetBlockLightValue(pos + Vector3i.up) < pg.lightLevelGrow)
                    return "farmSenseWarnLight";
            }
            catch (Exception)
            {
                return "farmSenseWarnUnknown";
            }
            return "farmSenseWarnUnknown";
        }
    }
}
