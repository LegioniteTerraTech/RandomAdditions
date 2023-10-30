using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;

namespace RandomAdditions
{
    [AutoSaveManager]
    public class ManTileLoader : MonoBehaviour
    {
        [SSManagerInst]
        public static ManTileLoader inst;
        [SSaveField]
        public IntVector2[] TilesNeedLoadedNextLoad;

        private static HashSet<IntVector2> emptyHash = new HashSet<IntVector2>();
        public static Dictionary<IntVector2, WorldTile.State> RequestedLoaded => ManWorldTileExt.LoadedTileCoords;
        public static Dictionary<IntVector2, WorldTile.State> Perimeter => ManWorldTileExt.PerimeterTileSubLoaded;


        [SSaveField]
        public HashSet<IntVector2> LoadedTileCoords = new HashSet<IntVector2>();

        public HashSet<IntVector2> PerimeterTileSubLoaded = new HashSet<IntVector2>();


        public static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("ManTileLoader").AddComponent<ManTileLoader>();
            DebugRandAddi.Log("RandomAdditions: Created ManTileLoader.");
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Destroy(inst.gameObject);
            inst = null;
            DebugRandAddi.Log("RandomAdditions: DeInit ManTileLoader.");
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
                        DebugRandAddi.Log("TRYING TO FETCH LAST PLAYER TECH!!!");
                        foreach (IntVector2 tile in Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys)
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
                                            ManWorldTileExt.TempLoadTile(tile);
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
        
        private static void ToHashSet(Dictionary<IntVector2, WorldTile.State> inC, HashSet<IntVector2> outC)
        {
            outC.Clear();
            foreach (var item in inC)
            {
                outC.Add(item.Key);
            }
        }
        private static void ToDictionary(HashSet<IntVector2> inC, Dictionary<IntVector2, WorldTile.State> outC)
        {
            outC.Clear();
            foreach (var item in inC)
            {
                outC.Add(item, WorldTile.State.Loaded);
            }
        }



        public static List<IntVector2> GetAllCenterTileLoadedTiles()
        {
            List<IntVector2> tileLoaders = new List<IntVector2>();
            try
            {
                foreach (IntVector2 tile in Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.Keys)
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
                ToHashSet(ManWorldTileExt.LoadedTileCoords, inst.LoadedTileCoords);
                ToHashSet(ManWorldTileExt.PerimeterTileSubLoaded, inst.PerimeterTileSubLoaded);
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
                ToDictionary(inst.LoadedTileCoords, ManWorldTileExt.LoadedTileCoords);
                ToDictionary(inst.PerimeterTileSubLoaded, ManWorldTileExt.PerimeterTileSubLoaded);
                if (inst.TilesNeedLoadedNextLoad == null)
                    return;
                foreach (var tile in inst.TilesNeedLoadedNextLoad)
                {
                    ManWorldTileExt.TempLoadTile(tile);
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

        internal class GUIManaged
        {
            private static bool display = false;

            public static void GUIGetTotalManaged()
            {
                if (!inst || inst.LoadedTileCoords == null)
                {
                    GUILayout.Box("--- Tile Loading [DISABLED] --- ");
                    return;
                }
                GUILayout.Box("--- Tile Loading --- ");
                display = AltUI.Toggle(display, "Show: ");
                if (display)
                {
                    for (int step = 0; step < inst.LoadedTileCoords.Count; step++)
                    {
                        var item = inst.LoadedTileCoords.ElementAt(step);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(step.ToString());
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(item.ToString());
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }
    }
}
