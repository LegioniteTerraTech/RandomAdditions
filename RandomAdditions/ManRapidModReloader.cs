using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomModules.LegacyModule;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// ON HIATUS
    /// The main purpose of this is to make blocks load FAR faster for big mods
    /// </summary>
    internal static class ManRapidModReloader
    {
        public static Dictionary<string, AssetBundleWatcher> managed = null;
        public static void Init()
        {
            managed = new Dictionary<string, AssetBundleWatcher>();
            foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (!managed.ContainsKey(bundle.name))
                {
                    bundle.
                    AssetBundleWatcher ABW = new AssetBundleWatcher()
                    { 
                    };
                    managed.Add(bundle.name, ABW);
                }
            }
        }
        public static void ReloadForMod()
        {
        }
    }
}
