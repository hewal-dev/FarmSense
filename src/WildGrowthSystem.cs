using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FarmSense
{
    /// <summary>
    /// "Wilde Kulturen wachsen nach": legt in passenden Biomen rund um die Spieler neue wilde
    /// Pflanzen an – als Ersatz fuer Welten, die ohne diese Mod erzeugt wurden (Biome-Dekoration
    /// passiert nur beim Worldgen, siehe Config/biomes.xml).
    /// Laeuft ausschliesslich server-/hostseitig und ueber world.SetBlockRPC (wird repliziert).
    /// </summary>
    public static class WildGrowthSystem
    {
        static readonly Dictionary<long, int> spawnedPerChunk = new Dictionary<long, int>();
        static readonly List<BlockValue> plantCache = new List<BlockValue>();
        static string cacheKey = "";

        public static void Run(World world)
        {
            FarmSenseConfig cfg = FarmSenseMain.Config;
            if (cfg == null || world == null) return;
            if (!cfg.WildGrowth_Enabled) return;
            if (cfg.WildGrowth_Blocks.Count == 0) return;

            List<BlockValue> plants = GetPlants(cfg);
            if (plants.Count == 0) return;

            if (world.Players == null || world.Players.list == null) return;
            GameRandom rnd = world.GetGameRandom();
            int placed = 0;

            for (int i = 0; i < world.Players.list.Count; i++)
            {
                EntityPlayer p = world.Players.list[i];
                if (p == null) continue;
                if (p.IsDead()) continue;

                for (int a = 0; a < cfg.WildGrowth_AttemptsPerInterval; a++)
                {
                    if (rnd.RandomFloat > cfg.WildGrowth_ChancePerAttempt) continue;
                    int x = (int)p.position.x + rnd.RandomRange(-cfg.WildGrowth_Radius, cfg.WildGrowth_Radius + 1);
                    int z = (int)p.position.z + rnd.RandomRange(-cfg.WildGrowth_Radius, cfg.WildGrowth_Radius + 1);
                    if (TryPlace(world, x, z, rnd, cfg, plants)) placed++;
                }
            }

            if (placed > 0 && cfg.LogDebug)
                Log.Out("[FarmSense] Wildwuchs: " + placed + " neue wilde Pflanzen gesetzt.");
        }

        static List<BlockValue> GetPlants(FarmSenseConfig cfg)
        {
            string key = string.Join(",", cfg.WildGrowth_Blocks.ToArray());
            if (key == cacheKey && plantCache.Count > 0) return plantCache;

            plantCache.Clear();
            cacheKey = key;
            for (int i = 0; i < cfg.WildGrowth_Blocks.Count; i++)
            {
                string name = cfg.WildGrowth_Blocks[i];
                Block b = Block.GetBlockByName(name, true);
                if (b == null)
                {
                    Log.Warning("[FarmSense] Wildwuchs: Block nicht gefunden: " + name);
                    continue;
                }
                plantCache.Add(Block.GetBlockValue(name, true));
            }
            return plantCache;
        }

        static bool TryPlace(World world, int x, int z, GameRandom rnd, FarmSenseConfig cfg, List<BlockValue> plants)
        {
            // Chunk muss geladen sein (sonst kein gueltiger Blockzugriff)
            if (world.GetChunkFromWorldPos(x, z) == null) return false;

            BiomeDefinition bio = world.GetBiome(x, z);
            if (bio == null || bio.m_sBiomeName == null) return false;
            if (!cfg.WildGrowth_Biomes.Contains(bio.m_sBiomeName)) return false;

            long key = ChunkKey(x, z);
            int count;
            if (!spawnedPerChunk.TryGetValue(key, out count)) count = 0;
            if (count >= cfg.WildGrowth_MaxPerChunk) return false;

            int surface = (int)world.GetHeightAt(x + 0.5f, z + 0.5f);

            for (int dy = 2; dy >= -2; dy--)
            {
                int y = surface + dy;
                if (y < 2 || y > 250) continue;

                BlockValue ground = world.GetBlock(x, y - 1, z);
                if (ground.isair) continue;
                Block groundBlock = ground.Block;
                if (groundBlock == null) continue;
                if (groundBlock.shape == null || !groundBlock.shape.IsTerrain()) continue;   // nur Naturboden
                if (groundBlock.blockMaterial == null) continue;
                if (groundBlock.blockMaterial.FertileLevel < 1) continue;                    // wie BlockPlantGrowing.CanGrowOn

                if (!world.GetBlock(x, y, z).isair) continue;
                if (!world.GetBlock(x, y + 1, z).isair) continue;
                if (!world.GetBlock(x, y + 2, z).isair) continue;
                if (!world.IsOpenSkyAbove(x, y, z)) continue;

                Vector3i pos = new Vector3i(x, y, z);
                if (world.GetBlockLightValue(pos) < cfg.WildGrowth_MinLight) continue;

                BlockValue plant = plants[rnd.RandomRange(0, plants.Count)];
                world.SetBlockRPC(pos, plant);
                spawnedPerChunk[key] = count + 1;

                if (cfg.LogDebug)
                    Log.Out("[FarmSense] Wilde Pflanze gesetzt: " + plant.Block.GetBlockName() + " bei " + pos.ToString());
                return true;
            }

            return false;
        }

        static long ChunkKey(int x, int z)
        {
            long cx = (long)(x >> 4);
            long cz = (long)(z >> 4);
            return (cx << 32) ^ (cz & 0xffffffffL);
        }
    }

    /// <summary>Zentraler Tick: HUD-Aktualisierung (clientseitig) + Wildwuchs (serverseitig).</summary>
    public static class FarmSenseTick
    {
        static float hudTimer;
        static float wildTimer;
        static bool registryReady;

        public static void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            World world = gm.World;
            if (world == null) return;

            FarmSenseConfig cfg = FarmSenseMain.Config;
            if (cfg == null) return;

            // Pflanzenregister nachziehen: zur InitMod-Zeit ist Block.list noch leer.
            if (!registryReady)
            {
                CropRegistry.EnsureBuilt();
                if (CropRegistry.Count > 0)
                {
                    registryReady = true;
                    Log.Out("[FarmSense] Pflanzenregister bereit: " + CropRegistry.Count + " Pflanzenbloecke erkannt.");
                }
            }

            bool serverSide = ConnectionManager.Instance == null
                || ConnectionManager.Instance.IsServer
                || ConnectionManager.Instance.IsSinglePlayer;

            if (cfg.Hud_Mode == "window" || cfg.Hud_Mode == "both")
            {
                // Hud_UpdateInterval = 0 -> jeden Aufruf (jeden Frame) aktualisieren.
                // Der Haken sitzt in GameManager.UpdateTick und laeuft damit so oft wie die
                // Vanilla-Namenszeile -> die Anzeige erscheint ohne spuerbare Verzoegerung.
                if (cfg.Hud_UpdateInterval <= 0f)
                {
                    HudSystem.UpdateLocalPlayer(world);
                }
                else
                {
                    hudTimer += Time.deltaTime;
                    if (hudTimer >= cfg.Hud_UpdateInterval)
                    {
                        hudTimer = 0f;
                        HudSystem.UpdateLocalPlayer(world);
                    }
                }
            }

            if (serverSide && cfg.WildGrowth_Enabled)
            {
                wildTimer += Time.deltaTime;
                if (wildTimer >= cfg.WildGrowth_IntervalSeconds)
                {
                    wildTimer = 0f;
                    WildGrowthSystem.Run(world);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "UpdateTick")]
    internal static class Patch_GameManager_UpdateTick
    {
        static void Postfix()
        {
            try
            {
                FarmSenseTick.Update();
            }
            catch (Exception e)
            {
                Log.Warning("[FarmSense] Tick-Fehler: " + e.Message);
            }
        }
    }
}
