using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CustomModules;
using CustomModules.LegacyModule;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace RandomAdditions.PatchBatch
{
    internal class NuterraPatches
    {
        internal static class ModuleCustomBlockPatches
        {
            internal static Type target = typeof(ModuleCustomBlock);
            [HarmonyPriority(-9001)]
            internal static void Update_Postfix(ModuleCustomBlock __instance, float ___emissionTimeDelay)
            {
                if (__instance != null && ___emissionTimeDelay <= 0)
                {   // SLEEP and save valuable CPU cycles!
                    __instance.enabled = false;
                }
            }
            [HarmonyPriority(-9001)]
            internal static void ChangeTimeEmission_Postfix(ModuleCustomBlock __instance)
            {   // Wake it up!
                if (__instance != null)
                    __instance.enabled = true;
            }
        }


        // By abusing Instanciate, we can create blocks HOLY F*BRON faster
        /*
        internal static class SuperFastBlockLoading
        {
            internal static Type target = typeof(ManMods);
            private static GameObject PrefabBlockDefault = null;
            private static bool ShouldDoFirst = true;
            [HarmonyPriority(9002)]
            internal static void Update_Prefix(ManMods __instance, ModSessionInfo ___m_RequestedSession,
                ModSessionInfo ___m_CurrentSession, bool ___m_LoadingRequestedSessionInProgress)
            {
                DebugRandAddi.Log("SuperFastBlockLoading");
                if (ShouldDoFirst && ___m_RequestedSession?.BlockIDs != null)
                {
                    //ShouldDoFirst = false;
                    foreach (KeyValuePair<int, string> keyValuePair in ___m_RequestedSession.BlockIDs)
                    {
                        int key = keyValuePair.Key;
                        ModdedBlockDefinition moddedBlockDefinition = __instance.FindModdedAsset<ModdedBlockDefinition>(keyValuePair.Value);
                        if (moddedBlockDefinition != null)
                            GeneratePrefabBlockFast(moddedBlockDefinition);
                    }
                }
            }
            private static void GeneratePrefabBlockFast(ModdedBlockDefinition MBD)
            {
                if (MBD == null)
                    return;
                if (MBD.m_PhysicalPrefab?.GetComponent<Damageable>() == null)
                {
                    if (PrefabBlockDefault == null)
                    {
                        PrefabBlockDefault = new GameObject("Unknown Block");
                        if (!PrefabBlockDefault.GetComponent<Damageable>())
                            PrefabBlockDefault.AddComponent<Damageable>();
                        if (!PrefabBlockDefault.GetComponent<ModuleDamage>())
                            PrefabBlockDefault.AddComponent<ModuleDamage>();
                        if (!PrefabBlockDefault.GetComponent<TankBlock>())
                            PrefabBlockDefault.AddComponent<TankBlock>();
                        if (!PrefabBlockDefault.GetComponent<Rigidbody>())
                            PrefabBlockDefault.AddComponent<Rigidbody>();
                        if (!PrefabBlockDefault.GetComponent<TankBlockTemplate>())
                            PrefabBlockDefault.AddComponent<TankBlockTemplate>();
                        if (!PrefabBlockDefault.GetComponent<SwitchableUpdater>())
                            PrefabBlockDefault.AddComponent<SwitchableUpdater>();
                        if (!PrefabBlockDefault.GetComponent<MaterialSwapper>())
                            PrefabBlockDefault.AddComponent<MaterialSwapper>();
                        DebugRandAddi.Log("Created rapid block prefab base, will use for non-altered m_PhysicalPrefabs");
                    }
                    var GO = UnityEngine.Object.Instantiate(PrefabBlockDefault);
                    var TBT2 = GO.GetComponent<TankBlockTemplate>();
                    var TBT = MBD.m_PhysicalPrefab;
                    if (TBT != null)
                    {
                        foreach (var item in TBT.GetComponentsInChildren<Component>())
                        {
                            DebugRandAddi.Log("Found " + item.GetType() + " inside prefab of " + TBT.name);
                        }
                        if (TBT.transform.childCount > 0)
                            DebugRandAddi.Log("Found " + TBT.transform.childCount + " children inside prefab of " + TBT.name);
                        GO.name = TBT.name;
                        TBT2.attachPoints = TBT.attachPoints;
                        TBT2.filledCells = TBT.filledCells;
                    }

                    MBD.m_PhysicalPrefab = TBT2;
                    UnityEngine.Object.Destroy(TBT.gameObject);
                }
            }
        }//*/


        /*
        public static Stopwatch profilingJObj = new Stopwatch();
        public static Stopwatch profilingPooler = new Stopwatch();
        public static Stopwatch profilingUnity = new Stopwatch();
        public static Stopwatch profilingUnity2 = new Stopwatch();
        public static long profilingUnity2Total = 0;
        public static Stopwatch profilingNML = new Stopwatch();
        public static Stopwatch profilingBlockTest = new Stopwatch();
        public static Stopwatch profilingJSONBL = new Stopwatch();
        internal static class JObjectPatches
        {
            internal static Type target = typeof(Newtonsoft.Json.Linq.JObject);
            [HarmonyPriority(-9001)]
            [MassPatchTypes(new Type[] { typeof(string) })]
            private static void Parse_Prefix()
            {
                profilingJObj.Start();
            }
            [HarmonyPriority(9001)]
            [MassPatchTypes(new Type[] { typeof(string) })]
            private static void Parse_Postfix()
            {
                profilingJObj.Stop();
            }
        }
        internal static class ComponentPoolPatches
        {
            internal static Type target = typeof(ComponentPool);
            [HarmonyPriority(-9001)]
            private static void InitPool_Prefix()
            {
                profilingPooler.Start();
            }
            [HarmonyPriority(9001)]
            private static void InitPool_Postfix()
            {
                profilingPooler.Stop();
            }
        }
        internal static class UnityObjectPatches
        {
            internal static Type target = typeof(UnityEngine.Object);
            private static void StartProfiling()
            {
                profilingUnity.Start();
            }
            private static void FinishProfiling()
            {
                profilingUnity.Stop();
            }
            [HarmonyPriority(-9001)]
            [MassPatchTypes(new Type[] { typeof(UnityEngine.Object) })]//
            private static void Instantiate_Prefix() => StartProfiling();
            [HarmonyPriority(9001)]
            [MassPatchTypes(new Type[] { typeof(UnityEngine.Object) })]//
            private static void Instantiate_Postfix() => FinishProfiling();
        }
        internal static class GameObjectPatches
        {
            internal static Type target = typeof(UnityEngine.GameObject);
            [HarmonyPriority(-9001)]
            [MassPatchTypes(new Type[] { typeof(Type) })]//
            private static void AddComponent_Prefix(Type componentType)
            {
                //DebugRandAddi.Log("Adding component: " + componentType.Name);
                profilingUnity2.Restart();
            }
            [HarmonyPriority(9001)]
            [MassPatchTypes(new Type[] { typeof(Type) })]//
            private static void AddComponent_Postfix()
            {
                profilingUnity2.Stop();
                profilingUnity2Total += profilingUnity2.ElapsedMilliseconds;
                //DebugRandAddi.Log("took: " + profilingUnity2.ElapsedMilliseconds);
            }
        }
        internal static class NuterraModuleLoaderPatches
        {
            internal static Type target = typeof(NuterraModuleLoader);
            [HarmonyPriority(-9001)]
            private static void InjectBlock_Prefix(ModuleCustomBlock __instance, ModdedBlockDefinition def)
            {
                profilingNML.Start();
            }
            [HarmonyPriority(9001)]
            private static void InjectBlock_Postfix(ModuleCustomBlock __instance, ModdedBlockDefinition def)
            {
                profilingNML.Stop();
            }
        }
        internal static class ManModsPatches
        {
            internal static Type target = typeof(ManMods);
            [HarmonyPriority(-9001)]
            private static void RunBlockSpawnTest_Prefix()
            {
                profilingBlockTest.Start();
            }
            [HarmonyPriority(9001)]
            private static void RunBlockSpawnTest_Postfix()
            {
                profilingBlockTest.Stop();
            }
        }
        internal static class JSONBlockLoaderPatches
        {
            internal static Type target = typeof(JSONBlockLoader);
            [HarmonyPriority(-9001)]
            private static void Inject_Prefix(ModdedBlockDefinition def)
            {
                DebugRandAddi.Log("Block " + def.name + " starting profiler...");
                profilingJSONBL.Restart();
            }
            [HarmonyPriority(9001)]
            private static void Inject_Postfix(ModdedBlockDefinition def)
            {
                profilingJSONBL.Stop();
                float totalTime = profilingJSONBL.ElapsedMilliseconds + profilingNML.ElapsedMilliseconds + profilingJObj.ElapsedMilliseconds +
                     profilingPooler.ElapsedMilliseconds + profilingUnity.ElapsedMilliseconds + profilingUnity2.ElapsedMilliseconds +
                     profilingBlockTest.ElapsedMilliseconds;
                if (totalTime > 0)
                {
                    DebugRandAddi.Log("Block " + def.name + " took est " + totalTime + " to load.");
                    DebugRandAddi.Log("JsonBlock took " + profilingJSONBL.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("NuterraBlock took " + profilingNML.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("Parsing took " + profilingJObj.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("Pooling took " + profilingPooler.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("Instanciation took " + profilingUnity.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("AddComponent took " + profilingUnity2.ElapsedMilliseconds + " to finish.");
                    DebugRandAddi.Log("BlockTest took " + profilingBlockTest.ElapsedMilliseconds + " to finish.");
                }
                profilingJObj.Reset();
                profilingPooler.Reset();
                profilingUnity.Reset();
                profilingUnity2.Reset();
                profilingNML.Reset();
                profilingBlockTest.Reset();
            }
        }//*/
    }
}
