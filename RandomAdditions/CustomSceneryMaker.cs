using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using static WaterMod.SurfacePool;

namespace RandomAdditions
{
    /// <summary>
    /// Placeholder
    /// </summary>
    public class CustomSceneryMaker
    {
        private static readonly FieldInfo ResLook = typeof(ResourceDispenser).GetField("m_ResourceSpawnChances", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ResCount = typeof(ResourceDispenser).GetField("m_TotalChunks", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ResStages = typeof(ResourceDispenser).GetField("m_DamageStages", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Res_YL = typeof(ResourceDispenser).GetField("m_MinY", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Res_YU = typeof(ResourceDispenser).GetField("m_MaxY", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo SpawnHelperInst = typeof(SpawnHelper).GetField("inst", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo allScenerySpawners = typeof(SpawnHelper).GetField("objs", BindingFlags.NonPublic | BindingFlags.Instance);

        public static Dictionary<SceneryTypes, Dictionary<string, List<TerrainObject>>> listALL = null;
        public static List<TerrainObject> Tessellite;
        public static List<TerrainObject> Adaranthite;
        public static float healthMulti = 4;

        private static void InsureForceAquireMainLookup()
        {
            if (listALL == null)
            {
                listALL = (Dictionary<SceneryTypes, Dictionary<string, List<TerrainObject>>>)
                    allScenerySpawners.GetValue((SpawnHelper)SpawnHelperInst.GetValue(null));
            }
        }

        public static void MakeScenery()
        {
            if (Tessellite == null)
            {
                Tessellite = GenerateSceneryStats("Mod_Tessellite", ChunkTypes._deprecated_SmallMetalOre,
                    3, healthMulti, 500f, 535f);
            }
            if (Adaranthite == null)
            {
                Adaranthite = GenerateSceneryStats("Mod_Adaranthite", ChunkTypes._deprecated_SenseOre,
                    6, 6f, -3f, 3f);
            }
        }

        /// <summary>
        /// INCOMPLETE, NEEDS TO BE ABLE TO HANDLE MESHES
        /// </summary>
        /// <param name="TOs"></param>
        private static void AlterSceneryTextures(List<TerrainObject> TOs)
        {
            foreach (TerrainObject TO in TOs)
            {
                ResourceDispenser RD = TO.GetComponent<ResourceDispenser>();
                ResourceDispenser.DamageStage[] DS = (ResourceDispenser.DamageStage[])ResStages.GetValue(RD);
                for (int i = 0; i < DS.Length; i++)
                {
                    ResourceDispenser.DamageStage ds1 = DS[i];
                    Transform newGeo = ds1.m_Geometry.UnpooledSpawn(null);
                    newGeo.name = newGeo.name + "_Mod";
                    var comps = newGeo.GetComponentsInChildren<MeshRenderer>(true);
                    if (comps != null)
                    {
                        foreach (MeshRenderer comp in comps)
                        {
                            comp.material = ResourcesHelper.GetMaterialFromBaseGameAllFast("");
                        }
                    }
                    ds1.m_Geometry = newGeo;
                    DS[i] = ds1;
                }
            }
        }

        private static List<TerrainObject> GenerateSceneryStats(string name, ChunkTypes storedChunkType, 
            int countChunks, float healthMultiplier, float minY, float maxY)
        {
            List<TerrainObject> output = new List<TerrainObject>();
            List<TerrainObject> listCopy = ForceFind(SceneryTypes.PlumbiteSeam, "grasslands_rock");
            foreach (var item in listCopy)
            {
                TerrainObject TO = item.UnpooledSpawn(null);
                ResourceDispenser RD = TO.GetComponent<ResourceDispenser>();
                ResourceDispenser.DamageStage[] DS = (ResourceDispenser.DamageStage[])ResStages.GetValue(RD);
                for (int i = 0; i < DS.Length; i++)
                {
                    ResourceDispenser.DamageStage ds1 = DS[i];
                    ds1.m_Health = ds1.m_Health * healthMultiplier;
                    DS[i] = ds1;
                }
                ResStages.SetValue(RD, DS);
                ResCount.SetValue(RD, countChunks);
                ResourceSpawnChance[] RSC = new ResourceSpawnChance[]
                    {
                    new ResourceSpawnChance()
                    {
                        chunkType = storedChunkType,
                        spawnWeight = 1,
                    }
                    };
                ResLook.SetValue(RD, RSC);
                Res_YL.SetValue(RD, minY);
                Res_YU.SetValue(RD, maxY);
                output.Add(TO);
            }
            listALL[SceneryTypes.PlumbiteSeam].Add(name, output);
            return output;
        }
        /// <summary>
        /// Throws exception when it cannot find
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private static List<TerrainObject> ForceFind(SceneryTypes type, string name)
        {
            InsureForceAquireMainLookup();
            List<TerrainObject> TOs = null;
            if (listALL.TryGetValue(type, out var dict))
            {
                if (dict.TryGetValue(name, out TOs))
                {
                    if (TOs != null)
                        return TOs;
                }
                else
                {
                    DebugRandAddi.Log("TryFind could not find " + name + " for type " + type.ToString() + 
                        ", however there are other options available");
                    foreach (var item in dict)
                    {
                        DebugRandAddi.Log(" - " + item.Key);
                    }
                    throw new InvalidOperationException();
                }
            }
            return TOs;
        }
    }
}
