using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityStandardAssets.ImageEffects;
using SafeSaves;

namespace RandomAdditions
{
    public class SpawnHelper
    {
        private static SpawnHelper inst;
        private MethodInfo death = typeof(ResourceDispenser).GetMethod("PlayDeathAnimation", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo deathParticles = typeof(ResourceDispenser).GetField("m_BigDebrisPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo deathSound = typeof(ManSFX).GetField("m_SceneryDebrisEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        private MethodInfo deathSoundGet = typeof(ManSFX).GetMethod("GetScenerySFXType", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo sce = typeof(ManSpawn).GetField("spawnableScenery", BindingFlags.NonPublic | BindingFlags.Instance);
        private Dictionary<SceneryTypes, Dictionary<BiomeTypes, List<TerrainObject>>> objs = new Dictionary<SceneryTypes, Dictionary<BiomeTypes, List<TerrainObject>>>();


        public static BiomeTypes GetBiomeFromName(string name)
        {
            if (name.Contains("SaltFlats"))
                return BiomeTypes.SaltFlats;
            if (name.Contains("Mountain"))
                return BiomeTypes.Mountains;
            if (name.Contains("Pillar"))
                return BiomeTypes.Pillars;
            if (name.Contains("Desert"))
                return BiomeTypes.Desert;
            if (name.Contains("Biome7"))
                return (BiomeTypes)7;
            if (name.Contains("Ice"))
                return BiomeTypes.Ice;
            return BiomeTypes.Grassland;
        }
     
        public static void GrabInitList()
        {
            inst = new SpawnHelper();
            if (inst.objs.Count != 0)
                return;
            try
            {
                List<Visible> objsRaw = ((List<Visible>)inst.sce.GetValue(ManSpawn.inst)).FindAll(x => x != null && x.GetComponent<ResourceDispenser>());
                foreach (var item in objsRaw)
                {
                    if (item.GetComponent<TerrainObject>() == null)
                        throw new NullReferenceException("SpawnHelpers.GrabInitList assumes each entry has a valid TerrainObject, but " + item.name + " has NONE!");
                    SceneryTypes ST = (SceneryTypes)item.ItemType;
                    BiomeTypes BT = GetBiomeFromName(item.name);
                    //DebugRandAddi.Log("- " + item.name + " | " + BT + " | " + ST);
                    List<TerrainObject> objRand;
                    Dictionary<BiomeTypes, List<TerrainObject>> objBiome;
                    if (inst.objs.TryGetValue(ST, out objBiome))
                    {
                        if (objBiome.TryGetValue(BT, out objRand))
                            objRand.Add(item.GetComponent<TerrainObject>());
                        else
                        {
                            objRand = new List<TerrainObject> { item.GetComponent<TerrainObject>() };
                            objBiome.Add(BT, objRand);
                        }
                    }
                    else
                    {
                        objRand = new List<TerrainObject> { item.GetComponent<TerrainObject>() };
                        objBiome = new Dictionary<BiomeTypes, List<TerrainObject>>();
                        objBiome.Add(BT, objRand);
                        inst.objs.Add(ST, objBiome);
                    }
                }
                DebugRandAddi.Log("Resources:");
                foreach (var item in inst.objs)
                {
                    DebugRandAddi.Log("  Type: " + item.Key);
                    foreach (var item2 in item.Value)
                    {
                        DebugRandAddi.Log("   Biome: " + item2.Key);
                        foreach (var item3 in item2.Value)
                        {
                            DebugRandAddi.Log("     Name: " + item3.name);
                        }
                    }
                }
                DebugRandAddi.Log("END");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("FAILED TO FETCH - " + e);
            }
        }


        public static TerrainObject GetResourceNodePrefab(SceneryTypes type, BiomeTypes biome)
        {
            GrabInitList();
            try
            {
                TerrainObject TO = null;
                if (inst.objs.TryGetValue(type, out var objBiome))
                {
                    if (objBiome.TryGetValue(biome, out var objRand))
                    {
                        TO = objRand.GetRandomEntry();
                    }
                    else
                        TO = objBiome[BiomeTypes.Grassland].GetRandomEntry();
                    if (TO == null)
                        throw new NullReferenceException("GetResourceNodePrefab entry for Biome " + biome.ToString() + " has NO entries!");
                }
                else
                    throw new NullReferenceException("GetResourceNodePrefab entry for Scenery " + type.ToString() + " has NO entries!");
                return TO;
            }
            catch (Exception)
            {
                throw new NullReferenceException("GetResourceNodePrefab entry for " + type.ToString() + " | " + biome.ToString() + " has NO entries!");
            }
        }
        public static void SpawnResourceNodeExplosion(Vector3 scenePos, SceneryTypes type, BiomeTypes biome)
        {
            var resDisp = GetResourceNodePrefab(type, biome).GetComponent<ResourceDispenser>();
            ((Transform)inst.deathParticles.GetValue(resDisp)).Spawn(null, scenePos, Quaternion.LookRotation(Vector3.up, Vector3.back));
            ((FMODEvent[])inst.deathSound.GetValue(ManSFX.inst))[(int)inst.deathSoundGet.Invoke(ManSFX.inst, new object[1]{type})].PlayOneShot(scenePos);
        }
        public static void SpawnResourceNodeExplosion(Vector3 scenePos, ResourceDispenser resDisp)
        {
            var type = resDisp.GetSceneryType();
            ((Transform)inst.deathParticles.GetValue(resDisp)).Spawn(null, scenePos, Quaternion.LookRotation(Vector3.up, Vector3.back));
            ((FMODEvent[])inst.deathSound.GetValue(ManSFX.inst))[(int)inst.deathSoundGet.Invoke(ManSFX.inst, new object[1] { type })].PlayOneShot(scenePos);
        }
        public static ResourceDispenser SpawnResourceNodeSnapTerrain(Vector3 scenePos, SceneryTypes type, BiomeTypes biome)
        {
            Vector3 pos = scenePos;
            ManWorld.inst.GetTerrainHeight(pos, out pos.y);
            Quaternion flatRot = Quaternion.LookRotation((UnityEngine.Random.rotation * Vector3.forward).SetY(0).normalized, Vector3.up);
            return SpawnResourceNode(pos, flatRot, type, biome);
        }
        public static ResourceDispenser SpawnResourceNode(Vector3 scenePos, Quaternion rotation, SceneryTypes type, BiomeTypes biome)
        {
            try
            {
                TerrainObject TO = GetResourceNodePrefab(type, biome);
                TrackableObject track = TO.SpawnFromPrefabAndAddToSaveData(scenePos, rotation).TerrainObject;
                ResourceDispenser RD = track.GetComponent<ResourceDispenser>();
                RD.RemoveFromWorld(false, false, true, false);
                RD.SetRegrowOverrideTime(2); // Check later - spawns too quickly after first spawn
                return RD;
            }
            catch (Exception e)
            {
                throw new NullReferenceException("RandomAdditions: SpawnResourceNode encountered an error - " + e.Message, e);
            }
        }
        public static void DestroyResourceNode(ResourceDispenser resDisp, Vector3 impactVec, bool spawnChunks)
        {
            if (spawnChunks)
                resDisp.RemoveFromWorld(true, true, false, false);
            else
            {
                inst.death.Invoke(resDisp, new object[1] { impactVec });
                ManSFX.inst.PlaySceneryDestroyedSFX(resDisp);
                resDisp.RemoveFromWorld(false, true, true, true);
            }
        }
    }
}
