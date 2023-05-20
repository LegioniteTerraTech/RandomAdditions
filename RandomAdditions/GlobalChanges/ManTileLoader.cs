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
        void GetActiveTiles(List<IntVector2> tileCache);
    }

    [AutoSaveManager]
    public class ManTileLoader : MonoBehaviour
    {
        [SSManagerInst]
        public static ManTileLoader inst;
        [SSaveField]
        public IntVector2[] TilesNeedLoadedNextLoad;

        public static HashSet<IntVector2> RequestedLoaded => (inst && inst.LoadedTileCoords != null) ? inst.LoadedTileCoords : new HashSet<IntVector2>();
        public static HashSet<IntVector2> Perimeter => (inst && inst.PerimeterTileSubLoaded != null) ? inst.PerimeterTileSubLoaded : new HashSet<IntVector2>();
        private static readonly Dictionary<IntVector2, float> TempLoaders = new Dictionary<IntVector2, float>();
        private static readonly HashSet<IntVector2> FixedTileLoaders = new HashSet<IntVector2>();
        private static readonly List<ITileLoader> DynamicTileLoaders = new List<ITileLoader>();

        private const float TempDurationDefault = 6; // In seconds
        private const float MaxWorldPhysicsSafeDistance = 100_000; // In blocks
        private static int MaxWorldPhysicsSafeDistanceTiles = Mathf.CeilToInt(100_000/ ManWorld.inst.TileSize) - 1; // In blocks


        [SSaveField]
        public HashSet<IntVector2> LoadedTileCoords = new HashSet<IntVector2>();

        public HashSet<IntVector2> PerimeterTileSubLoaded = new HashSet<IntVector2>();


        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("ManTileLoader").AddComponent<ManTileLoader>();
            GlobalClock.SlowUpdateEvent.Subscribe(UpdateTileLoading);
            DebugRandAddi.Log("RandomAdditions: Created ManTileLoader.");
        }
        public static void DeInit()
        {
            if (!inst)
                return;
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
        public static bool TempLoadTile(IntVector2 posTile, float loadTime = TempDurationDefault)
        {
            if (!TempLoaders.ContainsKey(posTile))
            {
                //DebugRandAddi.Info("TEMP LOADING TILE (extended) " + posTile.ToString());
                TempLoaders.Add(posTile, loadTime + Time.time);
            }
            else
            {
                DebugRandAddi.Info("TEMP LOADING TILE " + posTile.ToString());
                TempLoaders[posTile] = loadTime + Time.time;
            }
            return true;
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
            if (inst == null)
                return;
            inst.UpdateTileLoadingInternal();
        }
        private static readonly List<IntVector2> tilesPosCache = new List<IntVector2>();
        private void UpdateTileLoadingInternal()
        {
            RequestedLoaded.Clear();
            foreach (var item in TempLoaders.Keys)
            {
                if (!RequestedLoaded.Contains(item))
                    RequestedLoaded.Add(item);
            }
            foreach (var item in FixedTileLoaders)
            {
                if (!RequestedLoaded.Contains(item))
                    RequestedLoaded.Add(item);
            }

            // UPDATE THE DYNAMIC TILES
            foreach (var item in DynamicTileLoaders)
            {
                item.GetActiveTiles(tilesPosCache);
                foreach (var pos in tilesPosCache)
                {
                    if (!RequestedLoaded.Contains(pos))
                        RequestedLoaded.Add(pos);
                }
                tilesPosCache.Clear();
            }

            // UPDATE THE TEMP TILES
            int length = TempLoaders.Count;
            for (int step = 0; step < length;)
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
            IntVector2 loadOrigin = ManWorld.inst.FloatingOriginTile;
            int minCoordsX = loadOrigin.x - MaxWorldPhysicsSafeDistanceTiles;
            int minCoordsY = loadOrigin.y - MaxWorldPhysicsSafeDistanceTiles;
            int maxCoordsX = loadOrigin.x + MaxWorldPhysicsSafeDistanceTiles;
            int maxCoordsY = loadOrigin.y + MaxWorldPhysicsSafeDistanceTiles;
            for (int step = RequestedLoaded.Count - 1; step > -1; step--)
            {
                IntVector2 pos = RequestedLoaded.ElementAt(step);
                if (pos.x < minCoordsX || pos.y < minCoordsY || pos.x > maxCoordsX || pos.y > maxCoordsY)
                    RequestedLoaded.Remove(pos);
            }
            GetActiveTilePerimeterForRequestedLoaded();
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

        public static List<IntVector2> GetAllCenterTileLoadedTiles()
        {
            List<IntVector2> tileLoaders = new List<IntVector2>();
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
                                        tileLoaders.Add(tile);
                                    }
                                }
                                //DebugRandAddi.Log("Evaluating: " + tech.m_TechData.Name + "  TileLoader? " + tileLoader + " Active? " + tileLoaderActive);
                            }
                        }
                    }
                }
            }
            catch { }
            return tileLoaders;
        }
        public static void OnWorldSave()
        {
            try
            {
                List<IntVector2> loadTiles = GetAllCenterTileLoadedTiles();
                if (loadTiles.Count > 0)
                    inst.TilesNeedLoadedNextLoad = loadTiles.ToArray();
            }
            catch { }
        }
        public static void OnWorldFinishSave()
        {
            try
            {
                inst.TilesNeedLoadedNextLoad = null;
            }
            catch { }
        }
        public static void OnWorldLoad()
        {
            try
            {
                if (inst.TilesNeedLoadedNextLoad == null)
                    return;
                foreach (var tile in inst.TilesNeedLoadedNextLoad)
                {
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
            catch { }
        }

        private void GetActiveTilePerimeterForRequestedLoaded()
        {
            PerimeterTileSubLoaded.Clear();
            foreach (var item in LoadedTileCoords)
            {
                AddActiveTilePerimeterAroundPosition(item, ref PerimeterTileSubLoaded);
            }
            foreach (var item in LoadedTileCoords)
            {
                PerimeterTileSubLoaded.Remove(item);
            }
        }
        private static void AddActiveTilePerimeterAroundPosition(IntVector2 posSpot, ref HashSet<IntVector2> perimeter)
        {
            IntVector2 newSpot;
            newSpot = posSpot + new IntVector2(-1, -1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(0, -1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(1, -1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(-1, 0);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(1, 0);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(-1, 1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(0, 1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
            newSpot = posSpot + new IntVector2(1, 1);
            TryAddTilePerimeterAroundPosition(ref newSpot, ref perimeter);
        }
        private static void TryAddTilePerimeterAroundPosition(ref IntVector2 perimeterToAdd, ref HashSet<IntVector2> perimeter)
        {
            if (perimeter.Contains(perimeterToAdd))
                return;
            else
                perimeter.Add(perimeterToAdd);
        }

        public static void GetActiveTilesAround(List<IntVector2> cache, WorldPosition WP, int MaxTileLoadingDiameter)
        {
            IntVector2 centerTile = WP.TileCoord;
            int radCentered;
            Vector2 posTechCentre;
            Vector2 posTileCentre;

            switch (MaxTileLoadingDiameter)
            {
                case 0:
                case 1:
                    cache.Add(centerTile);
                    break;
                case 2:
                    posTechCentre = WP.ScenePosition.ToVector2XZ();
                    posTileCentre = ManWorld.inst.TileManager.CalcTileCentreScene(centerTile).ToVector2XZ();
                    if (posTechCentre.x > posTileCentre.x)
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            cache.Add(centerTile);
                            cache.Add(centerTile + new IntVector2(1, 0));
                            cache.Add(centerTile + new IntVector2(1, 1));
                            cache.Add(centerTile + new IntVector2(0, 1));
                        }
                        else
                        {
                            cache.Add(centerTile);
                            cache.Add(centerTile + new IntVector2(1, 0));
                            cache.Add(centerTile + new IntVector2(1, -1));
                            cache.Add(centerTile + new IntVector2(0, -1));
                        }
                    }
                    else
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            cache.Add(centerTile);
                            cache.Add(centerTile + new IntVector2(-1, 0));
                            cache.Add(centerTile + new IntVector2(-1, 1));
                            cache.Add(centerTile + new IntVector2(0, 1));
                        }
                        else
                        {
                            cache.Add(centerTile);
                            cache.Add(centerTile + new IntVector2(-1, 0));
                            cache.Add(centerTile + new IntVector2(-1, -1));
                            cache.Add(centerTile + new IntVector2(0, -1));
                        }
                    }
                    break;
                case 3:
                    radCentered = 1;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            cache.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    break;
                case 4:
                    radCentered = 1;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            cache.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    posTechCentre = WP.ScenePosition.ToVector2XZ();
                    posTileCentre = ManWorld.inst.TileManager.CalcTileCentreScene(centerTile).ToVector2XZ();
                    if (posTechCentre.x > posTileCentre.x)
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            cache.Add(centerTile + new IntVector2(2, -1));
                            cache.Add(centerTile + new IntVector2(2, 0));
                            cache.Add(centerTile + new IntVector2(2, 1));
                            cache.Add(centerTile + new IntVector2(2, 2));
                            cache.Add(centerTile + new IntVector2(1, 2));
                            cache.Add(centerTile + new IntVector2(0, 2));
                            cache.Add(centerTile + new IntVector2(-1, 2));
                        }
                        else
                        {
                            cache.Add(centerTile + new IntVector2(2, 1));
                            cache.Add(centerTile + new IntVector2(2, 0));
                            cache.Add(centerTile + new IntVector2(2, -1));
                            cache.Add(centerTile + new IntVector2(2, -2));
                            cache.Add(centerTile + new IntVector2(1, -2));
                            cache.Add(centerTile + new IntVector2(0, -2));
                            cache.Add(centerTile + new IntVector2(-1, -2));
                        }
                    }
                    else
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            cache.Add(centerTile + new IntVector2(-2, -1));
                            cache.Add(centerTile + new IntVector2(-2, 0));
                            cache.Add(centerTile + new IntVector2(-2, 1));
                            cache.Add(centerTile + new IntVector2(-2, 2));
                            cache.Add(centerTile + new IntVector2(-1, 2));
                            cache.Add(centerTile + new IntVector2(0, 2));
                            cache.Add(centerTile + new IntVector2(1, 2));
                        }
                        else
                        {
                            cache.Add(centerTile + new IntVector2(-2, 1));
                            cache.Add(centerTile + new IntVector2(-2, 0));
                            cache.Add(centerTile + new IntVector2(-2, -1));
                            cache.Add(centerTile + new IntVector2(-2, -2));
                            cache.Add(centerTile + new IntVector2(-1, -2));
                            cache.Add(centerTile + new IntVector2(0, -2));
                            cache.Add(centerTile + new IntVector2(1, -2));
                        }
                    }
                    break;
                default:
                    radCentered = MaxTileLoadingDiameter / 2;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            cache.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    break;
            }
        }
    }
}
