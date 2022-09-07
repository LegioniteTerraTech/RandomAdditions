using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;

namespace RandomAdditions
{
    public interface ITileLoader
    {
        List<IntVector2> GetActiveTiles();
    }

    [AutoSaveManager]
    public class ManTileLoader : MonoBehaviour
    {
        [SSManagerInst]
        public static ManTileLoader inst;

        public static List<IntVector2> RequestedLoaded => inst ? inst.LoadedTileCoords : new List<IntVector2>();
        private static readonly Dictionary<IntVector2, float> TempLoaders = new Dictionary<IntVector2, float>();
        private static readonly List<IntVector2> FixedTileLoaders = new List<IntVector2>();
        private static readonly List<ITileLoader> DynamicTileLoaders = new List<ITileLoader>();

        private static readonly float TempDuration = 6; // In seconds

        [SSaveField]
        public List<IntVector2> LoadedTileCoords = new List<IntVector2>();


        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("ManTileLoader").AddComponent<ManTileLoader>();
            GlobalClock.SlowUpdateEvent.Subscribe(UpdateTileLoading);
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(OnWorldLoad);
            DebugRandAddi.Log("RandomAdditions: Created ManTileLoader.");
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Unsubscribe(OnWorldLoad);
            GlobalClock.SlowUpdateEvent.Unsubscribe(UpdateTileLoading);
            Destroy(inst.gameObject);
            inst = null;
            DebugRandAddi.Log("RandomAdditions: DeInit ManTileLoader.");
        }


        public static void ClearAll()
        {
            FixedTileLoaders.Clear();
            DynamicTileLoaders.Clear();
        }
        public static void SetTileLoading(IntVector2 worldTilePos, bool Yes)
        {
            if (Yes)
            {
                if (!FixedTileLoaders.Contains(worldTilePos))
                {
                    FixedTileLoaders.Add(worldTilePos);
                }
            }
            else
            {
                if (FixedTileLoaders.Remove(worldTilePos))
                {
                }
            }
        }
        public static bool TempLoadTile(IntVector2 posTile)
        {
            if (!TempLoaders.TryGetValue(posTile, out _))
            {
                DebugRandAddi.Log("TEMP LOADING TILE " + posTile.ToString());
                TempLoaders.Add(posTile, TempDuration + Time.time);
                return true;
            }
            else
                return false;
        }
        public static bool RegisterDynamicTileLoader(ITileLoader loader)
        {
            if (loader != null)
            {
                if (!DynamicTileLoaders.Contains(loader))
                {
                    DynamicTileLoaders.Add(loader);
                    return true;
                }
            }
            else
            {
            }
            return false;
        }
        public static bool UnregisterDynamicTileLoader(ITileLoader loader)
        {
            if (loader != null)
            {
                return DynamicTileLoaders.Remove(loader);
            }
            else
            {
            }
            return false;
        }

        internal static void UpdateTileLoading()
        {
            RequestedLoaded.Clear();
            RequestedLoaded.AddRange(TempLoaders.Keys);
            RequestedLoaded.AddRange(FixedTileLoaders);

            // UPDATE THE DYNAMIC TILES
            foreach (var item in DynamicTileLoaders)
            {
                List<IntVector2> tilesPos = item.GetActiveTiles();
                foreach (var pos in tilesPos)
                {
                    if (!RequestedLoaded.Contains(pos))
                    {
                        RequestedLoaded.Add(pos);
                    }
                }
            }

            // UPDATE THE TEMP TILES
            int length = TempLoaders.Count;
            for (int step = 0; step < length; )
            {
                var pos = TempLoaders.ElementAt(step);
                if (pos.Value < Time.time)
                {
                    TempLoaders.Remove(pos.Key);
                    length--;
                }
                else
                    step++;
            }
        }

        public static int lastTechID = -1;
        public static float lastTechUpdateTime = -1;

        /*
        public void Update()
        {
            if (!ManGameMode.inst.GetIsInPlayableMode())
                MaintainPlayerTech();
        }*/

        private static readonly float EmergencyTileLoad = 0.4f; // In seconds
        public static void MaintainPlayerTech()
        {
            int error = 0;
            try
            {
                if (Singleton.playerTank == null)
                {
                    if (lastTechUpdateTime < Time.time)
                    {
                        error++;
                        List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys.ToList();
                        error++;
                        DebugRandAddi.Log("TRYING TO FETCH LAST PLAYER TECH!!!");
                        foreach (IntVector2 tile in tiles)
                        {
                            error += 10;
                            ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                            if (tileInst == null)
                                continue;
                            if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                            {
                                error += 10000;
                                foreach (ManSaveGame.StoredVisible Vis in techs)
                                {
                                    error += 10000000;
                                    if (Vis is ManSaveGame.StoredTech tech)
                                    {
                                        if (tech.m_ID == lastTechID)
                                        {
                                            TempLoadTile(tile);
                                            DebugRandAddi.Log("Fetched last player Tech");
                                            lastTechUpdateTime = Time.time + EmergencyTileLoad;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                    lastTechID = Singleton.playerTank.visible.ID;
            }
            catch {
                DebugRandAddi.LogError("ERROR CODE lvl 0 - " + error);
            }
        }

        public static void OnWorldLoad(Mode mode)
        {
            try
            {
                List<IntVector2> tiles = Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys.ToList();
                foreach (IntVector2 tile in tiles)
                {
                    ManSaveGame.StoredTile tileInst = Singleton.Manager<ManSaveGame>.inst.GetStoredTile(tile, false);
                    if (tileInst == null)
                        continue;
                    if (tileInst.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        foreach (ManSaveGame.StoredVisible Vis in techs)
                        {
                            if (Vis is ManSaveGame.StoredTech tech)
                            {
                                //bool tileLoader = false;
                                //bool tileLoaderActive = false;
                                BlockTypes[] blockTypes = tech.m_TechData.m_BlockSpecs.Select(x => x.m_BlockType).Distinct().ToArray();
                                foreach (var id in blockTypes)
                                {
                                    var prefab = ManSpawn.inst.GetBlockPrefab(id);
                                    ModuleTileLoader MTL = prefab?.GetComponent<ModuleTileLoader>();
                                    if (MTL)
                                    {
                                        //tileLoader = true;
                                        TempLoadTile(tile);
                                        //tileLoaderActive = true;
                                        /*
                                        if (MTL.AnchorOnly)
                                        {
                                            if (tech.m_TechData.CheckIsAnchored())
                                            {
                                                TempLoadTile(tile);
                                                tileLoaderActive = true;
                                            }
                                        }
                                        else
                                        {
                                            TempLoadTile(tile);
                                            tileLoaderActive = true;
                                        }*/
                                    }
                                }

                                //DebugRandAddi.Log("Evaluating: " + tech.m_TechData.Name + "  TileLoader? " + tileLoader + " Active? " + tileLoaderActive);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
