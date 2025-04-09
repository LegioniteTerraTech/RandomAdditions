using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RandomAdditions.RailSystem;
using static BlockPlacementCollector.Collection;
using static CompoundExpression;
using static Tank.CollisionInfo;

namespace RandomAdditions
{
    internal class ModOptimizationPatches
    {
        internal static class ModContentsPatches
        {
            // Setup with TAC Pack Core + Tony Rails (and respective mods) with NO optimization is around 20.52 seconds
            // Setup with TAC Pack Core + Tony Rails (and respective mods) with WITH optimization is around 19.20 seconds
            //   Not enough time saved to justify changing!
            internal static Type target = typeof(ModContents);

            
            internal static Dictionary<ModContents, Dictionary<string, List<UnityEngine.Object>>> RapidLookup = new Dictionary<ModContents, Dictionary<string, List<UnityEngine.Object>>>();
            private static Dictionary<string, List<UnityEngine.Object>> MakeRapidLookup(ModContents mod, List<UnityEngine.Object> assetList)
            {
                List<UnityEngine.Object> obs = null;
                Dictionary<string, List<UnityEngine.Object>> dictLook = new Dictionary<string, List<UnityEngine.Object>>();
                DebugRandAddi.Log("Making rapid lookup for " + (mod.ModName.NullOrEmpty() ? "NULL" : mod.ModName) +
                    " mod with > 75 entries (" + mod.m_AdditionalAssets.Count + ")");
                RapidLookup.Add(mod, dictLook);
                foreach (var item in assetList)
                {
                    string nameObj = item.name;
                    if (dictLook.TryGetValue(nameObj, out obs))
                        obs.Add(item);
                    else
                        dictLook.Add(nameObj, new List<UnityEngine.Object>() { item });
                    int index2 = nameObj.LastIndexOf('.');
                    if (index2 != -1)
                    {
                        string nameShort = nameObj.Substring(0, index2);
                        if (dictLook.TryGetValue(nameShort, out obs))
                            obs.Add(item);
                        else
                            dictLook.Add(nameShort, new List<UnityEngine.Object>() { item });
                    }
                }
                return dictLook;
            }
            
            /// <summary>
            /// RapidLookupForModContent - note: LOAD PRIORITY IS DIFFERENT FROM THE REPLACED FUNCTION!!!
            ///   This should come at no consequence though, as all that deviates is that the name of the mod is prefixed.
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static bool FindAsset_Prefix(ModContents __instance, ref UnityEngine.Object __result, ref string id)
            {
                List<UnityEngine.Object> objL = __instance.m_AdditionalAssets;
                if (objL != null && objL.Count > 75)
                {
                    int index = id.LastIndexOf('.');
                    List<UnityEngine.Object> obs = null;
                    Dictionary<string, List<UnityEngine.Object>> dictLook = null;
                    if (!RapidLookup.TryGetValue(__instance, out dictLook))
                    {   // make the rapid lookup
                        dictLook = MakeRapidLookup(__instance, objL);
                    }
                    // load the rapid lookup
                    if (dictLook.TryGetValue(id, out obs) || (index != -1 && dictLook.TryGetValue(id.Substring(0, index), out obs)))
                        __result = obs.FirstOrDefault();
                    else
                        __result = null;
                    return false;
                }
                return true;
            }

            private static IEnumerable<UnityEngine.Object> Iterator(Dictionary<string, List<UnityEngine.Object>> dictLook, string id, int index)
            {
                List<UnityEngine.Object> obs = null;
                if (dictLook.TryGetValue(id, out obs))
                    foreach (UnityEngine.Object obj in obs)
                        yield return obj;
                if (index != -1 && dictLook.TryGetValue(id.Substring(0, index), out obs))
                    foreach (UnityEngine.Object obj in obs)
                        yield return obj;
            }

            /// <summary>
            /// RapidLookupForModContent2 - note: LOAD PRIORITY IS DIFFERENT FROM THE REPLACED FUNCTION!!!
            ///   This should come at no consequence though, as all that deviates is that the name of the mod is prefixed.
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static bool FindAllAssets_Prefix(ModContents __instance, ref IEnumerable<UnityEngine.Object> __result, ref string id)
            {
                List<UnityEngine.Object> objL = __instance.m_AdditionalAssets;
                if (objL != null && objL.Count > 75)
                {
                    int index = id.LastIndexOf('.');
                    List<UnityEngine.Object> obs = null;
                    Dictionary<string, List<UnityEngine.Object>> dictLook = null;
                    if (!RapidLookup.TryGetValue(__instance, out dictLook))
                    {   // make the rapid lookup
                        dictLook = MakeRapidLookup(__instance, objL);
                    }
                    // load the rapid lookup
                    __result = Iterator(dictLook, id, index);
                    return false;
                }
                return true;
            }
        }
    }
}
