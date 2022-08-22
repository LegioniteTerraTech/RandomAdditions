using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
#if !STEAM
using ModHelper.Config;
#else
using ModHelper;
#endif
using Nuterra.NativeOptions;


namespace RandomAdditions
{
    // A mod that does exactly as it says on the tin
    //   A bunch of random additions that make little to no sense, but they be.
    //
    public class KickStart
    {
        internal const string ModName = "RandomAdditions";

        // MOD SUPPORT
        internal static bool isWaterModPresent = false;
        internal static bool isTweakTechPresent = false;
        internal static bool isNuterraSteamPresent = false;

        public static GameObject logMan;

        public static bool DebugPopups = false;
        public static bool ResetModdedPopups = false;
        public static bool ImmediateLoadLastSave = true; // Load directly into the last saved save on game startup
        public static bool UseAltDateFormat = false; //Change the date format to Y M D (requested by Exund [Weathermod])
        public static bool NoShake = false;
        public static bool AutoScaleBlocksInSCU = false;
        public static bool TrueShields = true;
        public static bool InterceptedExplode = true;   // Projectiles intercepted will explode
        //public static bool CheapInterception = false;   // Force the Point-Defense Systems to grab the first target it "finds"
        public static float ProjectileHealthMultiplier = 10;
        public static int GlobalBlockReplaceChance = 50;
        public static bool MandateSeaReplacement = true;
        public static bool MandateLandReplacement = false;


        public static bool IsIngame { get { return !ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked; } }

        public static void ReleaseControl(string Name = null)
        {
            string focused = GUI.GetNameOfFocusedControl();
            if (Name == null)
            {
                GUI.FocusControl(null);
                GUI.UnfocusWindow();
                GUIUtility.hotControl = 0;
            }
            else
            {
                if (focused == Name)
                {
                    GUI.FocusControl(null);
                    GUI.UnfocusWindow();
                    GUIUtility.hotControl = 0;
                }
            }
        }

        public static float WaterHeight
        {
            get
            {
                float outValue = -75;
#if !STEAM
                try { outValue = GetWaterHeight(); } catch { }
#endif
                return outValue;
            }
        }

        private static bool patched = false;
        internal static Harmony harmonyInstance = new Harmony("legionite.randomadditions");
        //private static bool patched = false;
        internal static bool isSteamManaged = false;
        public static bool VALIDATE_MODS()
        {
            isWaterModPresent = false;
            isTweakTechPresent = false;
            isNuterraSteamPresent = false;

            if (!LookForMod("NLogManager"))
            {
                isSteamManaged = false;
            }
            else
                isSteamManaged = true;

            if (!LookForMod("0Harmony"))
            {
                DebugRandAddi.FatalError("This mod NEEDS Harmony to function!  Please subscribe to it on the Steam Workshop.");
                return false;
            }
            if (!LookForMod("SafeSaves"))
            {
#if STEAM
                DebugRandAddi.FatalError("This mod NEEDS Mod Saves to function!  Please subscribe to it on the Steam Workshop.");
#else
                DebugRandAddi.FatalError("This mod NEEDS SafeSaves to function!  Please install it via TTMM.");
#endif
                return false;
            }
            if (LookForMod("WaterMod"))
            {
                DebugRandAddi.Log("RandomAdditions: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }
            if (LookForMod("TweakTech"))
            {
                DebugRandAddi.Log("RandomAdditions: Found TweakTech!  Adding compatability!");
                isTweakTechPresent = true;
            }
            if (LookForMod("NuterraSteam"))
            {
                DebugRandAddi.Log("RandomAdditions: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }
            return true;
        }

        private static bool OfficialEarlyInited = false;
        public static void OfficialEarlyInit()
        {
            //Where the fun begins
            DebugRandAddi.Log("RandomAdditions: OfficialEarlyInit");
#if STEAM
            if (!VALIDATE_MODS())
            {
                return;
            }
            try
            { // init changes
                LocalCorpAudioExt.ManExtendAudio.Subscribe();
                DebugRandAddi.Log("RandomAdditions: ManExtendAudio - Sub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManExtendAudio");
                DebugRandAddi.Log(e);
            }
#endif

            //Initiate the madness
            if (!patched)
            {
                try
                { // init changes
                    if (MassPatcher.MassPatchAll())
                    {
                        DebugRandAddi.Log("RandomAdditions: Patched");
                        patched = true;
                    }
                    else
                        DebugRandAddi.Log("RandomAdditions: Error on patch");
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on patch");
                    DebugRandAddi.Log(e);
                }
            }
            if (!logMan)
            {
                logMan = new GameObject("logMan");
                logMan.AddComponent<LogHandler>();
                logMan.GetComponent<LogHandler>().Initiate();
            }
            JSONRandAddModules.CompileLookupAndInit();

            try
            {
                KickStartOptions.TryInitOptionAndConfig();
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Could not init Option & Config since ConfigHelper and/or Nuterra.NativeOptions is absent?");
                DebugRandAddi.Log(e);
            }
            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on RegisterSaveSystem");
                DebugRandAddi.Log(e);
            }
            OfficialEarlyInited = true;
        }



        public static void MainOfficialInit()
        {
            //Where the fun begins
#if STEAM
            DebugRandAddi.Log("RandomAdditions: MAIN (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }
            if (!OfficialEarlyInited)
            {
                DebugRandAddi.Log("RandomAdditions: MainOfficialInit was called before OfficialEarlyInit was finished?! Trying OfficialEarlyInit AGAIN");
                OfficialEarlyInit();
            }
            DebugRandAddi.Log("RandomAdditions: MainOfficialInit");
            try
            { // init changes
                LocalCorpAudioExt.ManExtendAudio.Subscribe();
                DebugRandAddi.Log("RandomAdditions: ManExtendAudio - Sub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManExtendAudio");
                DebugRandAddi.Log(e);
            }
#else
            DebugRandAddi.Log("RandomAdditions: Startup was invoked by TTSMM!  Set-up to handle LATE initialization");
            OfficialEarlyInit();
#endif

            //Initiate the madness
            if (!patched)
            {
                try
                {
                    if (MassPatcher.MassPatchAll())
                    {
                        DebugRandAddi.Log("RandomAdditions: Patched");
                        patched = true;
                    }
                    else
                        DebugRandAddi.Log("RandomAdditions: Error on patch");
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on patch");
                    DebugRandAddi.Log(e);
                }
            }
            GlobalClock.ClockManager.Initiate();
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            ManTethers.Init();
            LazyRender.Initiate();
            GUIClock.Initiate();
            ManTileLoader.Initiate();

        }

#if STEAM
        public static void DeInitALL()
        {
            if (patched)
            {
                try
                {
                    if (MassPatcher.MassUnPatchAll())
                    {
                        DebugRandAddi.Log("RandomAdditions: UnPatched");
                        patched = false;
                    }
                    else
                        DebugRandAddi.Log("RandomAdditions: Error on UnPatch");
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on UnPatch");
                    DebugRandAddi.Log(e);
                }
            }
            try
            { // init changes
                LocalCorpAudioExt.ManExtendAudio.UnSub();
                DebugRandAddi.Log("RandomAdditions: ManExtendAudio - UnSub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManExtendAudio");
                DebugRandAddi.Log(e);
            }
            ManTileLoader.DeInit();
            GUIClock.DeInit();
            ManModeSwitch.DeInit();
            ManTethers.DeInit();
            ReplaceManager.RemoveAllBlocks();
            GlobalClock.ClockManager.DeInit();
        }

        // UNOFFICIAL
#else
        public static void Main()
        {
            //Where the fun begins
            DebugRandAddi.Log("RandomAdditions: MAIN (TTMM Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }


            //Initiate the madness
            if (!MassPatcher.MassPatchAll())
            {
                DebugRandAddi.Log("RandomAdditions: Error on patch");
            }

            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on RegisterSaveSystem");
                DebugRandAddi.Log(e);
            }
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            ManTethers.Init();
            logMan = new GameObject("logMan");
            logMan.AddComponent<LogHandler>();
            logMan.GetComponent<LogHandler>().Initiate();
            LazyRender.Initiate();

            // After everything else since this calls updates in the others
            GlobalClock.ClockManager.Initiate();
            GUIClock.Initiate();

            try
            {
                KickStartOptions.TryInitOptionAndConfig();
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Could not init Option & Config since ConfigHelper and/or Nuterra.NativeOptions is absent?");
                DebugRandAddi.Log(e);
            }
        }

        public static float GetWaterHeight()
        {
            return WaterMod.QPatch.WaterHeight;
        }

        /// <summary>
        /// Fires after blockInjector
        /// </summary>
        public static void DelayedInitAll()
        {
            ManTileLoader.Initiate();
#if DEBUG
            GetAvailSFX();
#endif
        }
#endif

        public static bool LookForMod(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }
        public static Transform HeavyObjectSearch(Transform trans, string name)
        {
            return trans.gameObject.GetComponentsInChildren<Transform>().ToList().Find(delegate (Transform cand) { return cand.name == name; });
        }

        public static void GetAvailSFX()
        {
            DebugRandAddi.Log("----- GETTING ALL SFX -----");
            FieldInfo FI = typeof(FMODEventInstance).GetField("m_ParamDatabase", BindingFlags.NonPublic | BindingFlags.Static);
            Dictionary<string, Dictionary<string, int>> w = (Dictionary<string, Dictionary<string, int>>)FI.GetValue(null);
            foreach (var item in w)
            {
                DebugRandAddi.Log(item.Key);
                foreach (var item2 in item.Value)
                {
                    DebugRandAddi.Log(item2.Key + " | " + item2.Value);
                }
            }
        }

    }

    public class KickStartOptions
    {
        internal static ModConfig config;

        // NativeOptions Parameters
        public static OptionToggle allowPopups;
        public static OptionToggle altDateFormat;
        public static OptionToggle noCameraShake;
        public static OptionToggle scaleBlocksInSCU;
        public static OptionToggle realShields;
        public static OptionToggle moddedPopupReset;

        public static OptionRange replaceChance;
        public static OptionToggle rpSea;
        public static OptionToggle rpLand;

        private static bool launched = false;

        public static void TryInitOptionAndConfig()
        {
            if (launched)
                return;
            launched = true;
            //Initiate the madness
            try
            {
                ModConfig thisModConfig = new ModConfig();
                thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
                thisModConfig.BindConfig<KickStart>(null, "ImmediateLoadLastSave");
                thisModConfig.BindConfig<KickStart>(null, "UseAltDateFormat");
                thisModConfig.BindConfig<KickStart>(null, "NoShake");
                thisModConfig.BindConfig<KickStart>(null, "AutoScaleBlocksInSCU");
#if !STEAM
                thisModConfig.BindConfig<KickStart>(null, "TrueShields");
#endif
                thisModConfig.BindConfig<KickStart>(null, "GlobalBlockReplaceChance");
                thisModConfig.BindConfig<KickStart>(null, "MandateLandReplacement");
                thisModConfig.BindConfig<KickStart>(null, "MandateSeaReplacement");
                thisModConfig.BindConfig<KickStart>(null, "ResetModdedPopups");
                thisModConfig.BindConfig<ExtUsageHint>(null, "HintsSeenSAV");
                config = thisModConfig;
                ExtUsageHint.SaveToHintsSeen();

                var RandomProperties = KickStart.ModName;
#if !STEAM
                realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, KickStart.TrueShields);
                realShields.onValueSaved.AddListener(() => { KickStart.TrueShields = realShields.SavedValue; });
#endif
                allowPopups = new OptionToggle("Enable custom block debug popups", RandomProperties, KickStart.DebugPopups);
                allowPopups.onValueSaved.AddListener(() => { KickStart.DebugPopups = allowPopups.SavedValue; });
                altDateFormat = new OptionToggle("Y/M/D Format", RandomProperties, KickStart.UseAltDateFormat);
                altDateFormat.onValueSaved.AddListener(() => { KickStart.UseAltDateFormat = altDateFormat.SavedValue; });
                noCameraShake = new OptionToggle("Disable Camera Shake", RandomProperties, KickStart.NoShake);
                noCameraShake.onValueSaved.AddListener(() => { KickStart.NoShake = noCameraShake.SavedValue; });
                scaleBlocksInSCU = new OptionToggle("Scale Blocks Grabbed by SCU", RandomProperties, KickStart.AutoScaleBlocksInSCU);
                scaleBlocksInSCU.onValueSaved.AddListener(() => { KickStart.AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; });
                replaceChance = new OptionRange("Chance for modded block spawns", RandomProperties, KickStart.GlobalBlockReplaceChance, 0, 100, 10);
                replaceChance.onValueSaved.AddListener(() => { KickStart.GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); });
                rpLand = new OptionToggle("Force Land Block Replacement", RandomProperties, KickStart.MandateLandReplacement);
                rpLand.onValueSaved.AddListener(() => { KickStart.MandateLandReplacement = rpLand.SavedValue; });
                rpSea = new OptionToggle("Force Sea Block Replacement", RandomProperties, KickStart.MandateSeaReplacement);
                rpSea.onValueSaved.AddListener(() => { KickStart.MandateSeaReplacement = rpSea.SavedValue; });

                moddedPopupReset = new OptionToggle("Reset all modded popups", RandomProperties, KickStart.ResetModdedPopups);
                moddedPopupReset.onValueSaved.AddListener(() => {
                    if (moddedPopupReset.SavedValue)
                    {
                        ExtUsageHint.HintsSeen.Clear();
                        ExtUsageHint.HintsSeenToSave();
                        config.WriteConfigJsonFile();
                    }
                });
                NativeOptionsMod.onOptionsSaved.AddListener(() => { config.WriteConfigJsonFile(); });
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup");
                DebugRandAddi.Log(e);
            }

        }

        public static void TrySaveConfigData()
        {
            try
            {
                config.WriteConfigJsonFile();
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Config Saving");
                DebugRandAddi.Log(e);
            }

        }
    }


    internal static class ExtraExtensions
    {

        public static T[] GetComponentsLowestFirst<T>(this GameObject GO) where T : MonoBehaviour
        {
            List<KeyValuePair<int, T>> depthTracker = new List<KeyValuePair<int, T>>();

            for (int step = 0; step < GO.transform.childCount; step++)
            {
                Transform transCase = GO.transform.GetChild(step);
                var fetch = transCase.GetComponent<T>();
                if (fetch)
                    depthTracker.Add(new KeyValuePair<int, T>(0, fetch));
                GetComponentsLowestFirstRecurse<T>(transCase, 1, ref depthTracker);
            }
            List<T> insure = depthTracker.OrderBy(x => x.Key).ToList().ConvertAll(x => x.Value);
            if (insure.Count > 0)
                return insure.ToArray();
            return null;
        }
        private static void GetComponentsLowestFirstRecurse<T>(this Transform trans, int depth, ref List<KeyValuePair<int, T>> depthTracker) where T : MonoBehaviour
        {
            for (int step = 0; step < trans.childCount; step++)
            {
                Transform transCase = trans.GetChild(step);
                var fetch = transCase.GetComponent<T>();
                if (fetch)
                    depthTracker.Add(new KeyValuePair<int, T>(depth, fetch));
                GetComponentsLowestFirstRecurse<T>(transCase, depth + 1, ref depthTracker);
            }
        }

        public static List<T> GetExtModules<T>(this BlockManager BM) where T : ExtModule
        {
            List<T> got = new List<T>();
            foreach (var item in BM.IterateBlocks())
            {
                T get = item.GetComponent<T>();
                if (get)
                    got.Add(get);
            }
            return got;
        }
        public static List<T> GetChildModules<T>(this BlockManager BM) where T : ChildModule
        {
            List<T> got = new List<T>();
            foreach (var item in BM.IterateBlocks())
            {
                T[] get = item.GetComponentsInChildren<T>();
                if (get != null)
                    got.AddRange(get);
            }
            return got;
        }

        public static bool CanDamageBlock(this Projectile inst, Damageable damageable)
        {
            var validation = damageable.GetComponent<TankBlock>();
            if (!validation)
            {
                //DebugRandAddi.Log("RandomAdditions: did not hit possible block");
                return false;
            }
            if (damageable.Invulnerable)
                return false;// No damage Invinci
            Tank tank = validation.transform.root.GetComponent<Tank>();
            if (tank)
            {
                //DebugRandAddi.Log("RandomAdditions: tank");
                if (inst.Shooter)
                {
                    if (!Tank.IsEnemy(inst.Shooter.Team, tank.Team))
                    {
                        //DebugRandAddi.Log("RandomAdditions: not enemy");
                        return false;// Stop friendly-fire
                    }
                    else if (inst.Shooter == tank)
                    {
                        //DebugRandAddi.Log("RandomAdditions: self");
                        return false;// Stop self-fire 
                    }
                    else
                    {
                        //DebugRandAddi.Log("RandomAdditions: enemy " + inst.Shooter.Team + " | " + tank.Team);
                    }
                }
                else if (tank.IsNeutral())
                {
                    //DebugRandAddi.Log("RandomAdditions: neutral");
                    return false;// No damage Invinci
                }
                else
                {
                    //DebugRandAddi.Log("RandomAdditions: no shooter");
                    return false;
                }
            }
            else
                DebugRandAddi.Log("RandomAdditions: no tank!");
            return true;
        }
    }
}
