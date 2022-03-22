﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
#if !STEAM
using ModHelper.Config;
using Nuterra.NativeOptions;
#endif


namespace RandomAdditions
{
    // A mod that does exactly as it says on the tin
    //   A bunch of random additions that make make little to no sense, but they be.
    //
    public class KickStart
    {
        const string ModName = "RandomAdditions";

        public static bool isWaterModPresent = false;
        public static bool isTweakTechPresent = false;

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
        public static float ProjectileHealthMultiplier = 1;
        public static int GlobalBlockReplaceChance = 50;
        public static bool MandateSeaReplacement = true;
        public static bool MandateLandReplacement = false;

#if !STEAM
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
#endif

        public static float WaterHeight
        {
            get
            {
                float outValue = -25;
#if !STEAM
                try { outValue = WaterMod.QPatch.WaterHeight; } catch { }
#endif
                return outValue;
            }
        }

        private static bool patched = false;
        static Harmony harmonyInstance = new Harmony("legionite.randomadditions");
        //private static bool patched = false;
#if STEAM
        public static void OfficialEarlyInit()
        {
            //Where the fun begins

            //Initiate the madness
            try
            {
                harmonyInstance.PatchAll();
                //EdgePatcher(true);
                Debug.Log("RandomAdditions: Patched");
                patched = true;
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Error on patch");
                Debug.Log(e);
            }
            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem();
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Error on RegisterSaveSystem");
                Debug.Log(e);
            }
            if (!logMan)
            {
                logMan = new GameObject("logMan");
                logMan.AddComponent<LogHandler>();
                logMan.GetComponent<LogHandler>().Initiate();
            }
            GlobalClock.ClockManager.Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("RandomAdditions: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }
            if (LookForMod("TweakTech"))
            {
                Debug.Log("RandomAdditions: Found TweakTech!  Adding compatability!");
                isTweakTechPresent = true;
            }
            if (LookForMod("NuterraSteam"))
            {
                Debug.Log("TACtical_AI: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }


            /*
            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
            thisModConfig.BindConfig<KickStart>(null, "ImmediateLoadLastSave");
            thisModConfig.BindConfig<KickStart>(null, "UseAltDateFormat");
            thisModConfig.BindConfig<KickStart>(null, "NoShake");
            thisModConfig.BindConfig<KickStart>(null, "AutoScaleBlocksInSCU");
            thisModConfig.BindConfig<KickStart>(null, "TrueShields");
            thisModConfig.BindConfig<KickStart>(null, "GlobalBlockReplaceChance");
            thisModConfig.BindConfig<KickStart>(null, "MandateLandReplacement");
            thisModConfig.BindConfig<KickStart>(null, "MandateSeaReplacement");
            thisModConfig.BindConfig<KickStart>(null, "ResetModdedPopups");
            thisModConfig.BindConfig<ExtUsageHint>(null, "HintsSeenSAV");
            config = thisModConfig;
            ExtUsageHint.SaveToHintsSeen();

            var RandomProperties = ModName;
            realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, TrueShields);
            realShields.onValueSaved.AddListener(() => { TrueShields = realShields.SavedValue; });
            allowPopups = new OptionToggle("Enable custom block debug popups", RandomProperties, DebugPopups);
            allowPopups.onValueSaved.AddListener(() => { DebugPopups = allowPopups.SavedValue; });
            altDateFormat = new OptionToggle("Y/M/D Format", RandomProperties, UseAltDateFormat);
            altDateFormat.onValueSaved.AddListener(() => { UseAltDateFormat = altDateFormat.SavedValue; });
            noCameraShake = new OptionToggle("Disable Camera Shake", RandomProperties, NoShake);
            noCameraShake.onValueSaved.AddListener(() => { NoShake = noCameraShake.SavedValue; });
            scaleBlocksInSCU = new OptionToggle("Scale Blocks Grabbed by SCU", RandomProperties, AutoScaleBlocksInSCU);
            scaleBlocksInSCU.onValueSaved.AddListener(() => { AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; });
            replaceChance = new OptionRange("Chance for modded block spawns", RandomProperties, GlobalBlockReplaceChance, 0, 100, 10);
            replaceChance.onValueSaved.AddListener(() => { GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); });
            rpLand = new OptionToggle("Force Land Block Replacement", RandomProperties, MandateLandReplacement);
            rpLand.onValueSaved.AddListener(() => { MandateLandReplacement = rpLand.SavedValue; });
            rpSea = new OptionToggle("Force Sea Block Replacement", RandomProperties, MandateSeaReplacement);
            rpSea.onValueSaved.AddListener(() => { MandateSeaReplacement = rpSea.SavedValue; });

            moddedPopupReset = new OptionToggle("Reset all modded popups", RandomProperties, ResetModdedPopups);
            moddedPopupReset.onValueSaved.AddListener(() => {
                if (moddedPopupReset.SavedValue)
                {
                    ExtUsageHint.HintsSeen.Clear();
                    ExtUsageHint.HintsSeenToSave();
                    config.WriteConfigJsonFile();
                }
            });
            NativeOptionsMod.onOptionsSaved.AddListener(() => { config.WriteConfigJsonFile(); });
            */
        }

        /*
        private static PatchProcessor PCP;
        private static Type BFCW;
        private static MethodBase MB;
        private static MethodInfo MI;
        private static FieldInfo FI;
        private static HarmonyMethod HM;

        private static Type[] AllowedTypes = new Type[] {
                typeof(ModuleLazyAPs),
                typeof(ModuleReinforced),
                typeof(ModuleLazyAPs),
                typeof(ModuleLazyAPs),
                typeof(ModuleLazyAPs),
                typeof(ModuleLazyAPs),
            };
        private static void MakeEdgePatcher()
        {
            int patchStep = 0;
            try
            {
                AssemblyName AN = new AssemblyName("Assembly-CSharp.dll");
                if (AN == null)
                    Debug.Log("RandomAdditions: Error on patching BlockFilterComponentWhiteList - Assembly-CSharp is null?!?");
                BFCW = Assembly.Load(AN).GetType("BlockFilterComponentWhiteList", true);
                patchStep++;
                MB = BFCW.GetMethod("RebuildAllowedModuleTypes", BindingFlags.Instance | BindingFlags.NonPublic);
                patchStep++;
                FI = BFCW.GetField("m_AllowedModuleTypes", BindingFlags.Instance | BindingFlags.NonPublic);
                patchStep++;
                PCP = harmonyInstance.CreateProcessor(MB);
                patchStep++;
                MI = BFCW.GetRuntimeMethod("RebuildAllowedModuleTypes", new Type[] { });
                if (MI == null)
                    Debug.Log("RandomAdditions: Error on patching BlockFilterComponentWhiteList - RebuildAllowedModuleTypes is null?!?");
                patchStep++;
                HM = new HarmonyMethod();
                HM.method = MI;
                HM.declaringType = typeof(KickStart);
                HM.methodName = "PatchNewTypes";
                HM.methodType = MethodType.Normal;
                HM.reversePatchType = HarmonyReversePatchType.Original;
                patchStep++;
                PCP.AddPostfix(HM);
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Error on patching BlockFilterComponentWhiteList at level " + patchStep);
                Debug.Log(e);
            }
        }
        private static void EdgePatcher(bool patchApply)
        {
            return;
            try
            {
                if (PCP == null)
                    MakeEdgePatcher();
                if (patchApply)
                {
                    if (PCP != null)
                        PCP.Patch();
                }
                else
                {
                    if (PCP != null)
                        PCP.Unpatch(HarmonyPatchType.Postfix, harmonyInstance.Id);
                }
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: EdgePatcher - Failed in patching BlockFilterComponentWhiteList | was adding: " + patchApply);
                Debug.Log(e);
            }
        }

        public static void PatchNewTypes(object __instance)
        {
            HashSet<Type> HS = (HashSet<Type>)FI.GetValue(__instance);
            foreach (Type type in AllowedTypes)
            {
                HS.Add(type);
            }
            FI.SetValue(__instance, HS);
        }
        */
        public static void MainOfficialInit()
        {
            //Where the fun begins

            //Initiate the madness
            if (!patched)
            {
                int patchStep = 0;
                try
                {
                    harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                    patchStep++;
                    //EdgePatcher(true);
                    Debug.Log("RandomAdditions: Patched");
                    patched = true;
                }
                catch (Exception e)
                {
                    Debug.Log("RandomAdditions: Error on patch " + patchStep);
                    Debug.Log(e);
                }
            }
            GUIClock.Initiate();
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            LazyRender.Initiate();
        }
        public static void DeInitALL()
        {
            if (patched)
            {
                try
                {
                    harmonyInstance.UnpatchAll();
                    //EdgePatcher(false);
                    Debug.Log("RandomAdditions: UnPatched");
                    patched = false;
                }
                catch (Exception e)
                {
                    Debug.Log("RandomAdditions: Error on UnPatch");
                    Debug.Log(e);
                }
            }
            GlobalClock.ClockManager.DeInit();
            GUIClock.DeInit();
            ManModeSwitch.DeInit();
            ReplaceManager.RemoveAllBlocks();
        }

        // UNOFFICIAL
#else
        public static void Main()
        {
            //Where the fun begins

            //Initiate the madness
            Harmony harmonyInstance = new Harmony("legionite.randomadditions");
            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Error on patch");
                Debug.Log(e);
            }
            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem();
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: Error on RegisterSaveSystem");
                Debug.Log(e);
            }
            GlobalClock.ClockManager.Initiate();
            GUIClock.Initiate();
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            logMan = new GameObject("logMan");
            logMan.AddComponent<LogHandler>();
            logMan.GetComponent<LogHandler>().Initiate();
            LazyRender.Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("RandomAdditions: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }
            if (LookForMod("TweakTech"))
            {
                Debug.Log("RandomAdditions: Found TweakTech!  Adding compatability!");
                isTweakTechPresent = true;
            }
            if (LookForMod("NuterraSteam"))
            {
                Debug.Log("TACtical_AI: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }


            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
            thisModConfig.BindConfig<KickStart>(null, "ImmediateLoadLastSave");
            thisModConfig.BindConfig<KickStart>(null, "UseAltDateFormat");
            thisModConfig.BindConfig<KickStart>(null, "NoShake");
            thisModConfig.BindConfig<KickStart>(null, "AutoScaleBlocksInSCU");
            thisModConfig.BindConfig<KickStart>(null, "TrueShields");
            thisModConfig.BindConfig<KickStart>(null, "GlobalBlockReplaceChance");
            thisModConfig.BindConfig<KickStart>(null, "MandateLandReplacement");
            thisModConfig.BindConfig<KickStart>(null, "MandateSeaReplacement");
            thisModConfig.BindConfig<KickStart>(null, "ResetModdedPopups");
            thisModConfig.BindConfig<ExtUsageHint>(null, "HintsSeenSAV");
            config = thisModConfig;
            ExtUsageHint.SaveToHintsSeen();

            var RandomProperties = ModName;
            realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, TrueShields);
            realShields.onValueSaved.AddListener(() => { TrueShields = realShields.SavedValue; });
            allowPopups = new OptionToggle("Enable custom block debug popups", RandomProperties, DebugPopups);
            allowPopups.onValueSaved.AddListener(() => { DebugPopups = allowPopups.SavedValue; });
            altDateFormat = new OptionToggle("Y/M/D Format", RandomProperties, UseAltDateFormat);
            altDateFormat.onValueSaved.AddListener(() => { UseAltDateFormat = altDateFormat.SavedValue; });
            noCameraShake = new OptionToggle("Disable Camera Shake", RandomProperties, NoShake);
            noCameraShake.onValueSaved.AddListener(() => { NoShake = noCameraShake.SavedValue; });
            scaleBlocksInSCU = new OptionToggle("Scale Blocks Grabbed by SCU", RandomProperties, AutoScaleBlocksInSCU);
            scaleBlocksInSCU.onValueSaved.AddListener(() => { AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; });
            replaceChance = new OptionRange("Chance for modded block spawns", RandomProperties, GlobalBlockReplaceChance, 0, 100, 10);
            replaceChance.onValueSaved.AddListener(() => { GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); });
            rpLand = new OptionToggle("Force Land Block Replacement", RandomProperties, MandateLandReplacement);
            rpLand.onValueSaved.AddListener(() => { MandateLandReplacement = rpLand.SavedValue; });
            rpSea = new OptionToggle("Force Sea Block Replacement", RandomProperties, MandateSeaReplacement);
            rpSea.onValueSaved.AddListener(() => { MandateSeaReplacement = rpSea.SavedValue; });

            moddedPopupReset = new OptionToggle("Reset all modded popups", RandomProperties, ResetModdedPopups);
            moddedPopupReset.onValueSaved.AddListener(() =>
            {
                if (moddedPopupReset.SavedValue)
                {
                    ExtUsageHint.HintsSeen.Clear();
                    ExtUsageHint.HintsSeenToSave();
                    config.WriteConfigJsonFile();
                }
            });
            NativeOptionsMod.onOptionsSaved.AddListener(() => { config.WriteConfigJsonFile(); });
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

    }
    internal static class ExtraExtensions
    {
        public static bool CanDamageBlock(this Projectile inst, Damageable damageable)
        {
            var validation = damageable.GetComponent<TankBlock>();
            if (!validation)
            {
                //Debug.Log("RandomAdditions: did not hit possible block");
                return false;
            }
            if (damageable.Invulnerable)
                return false;// No damage Invinci
            if (validation.tank)
            {
                if (inst.Shooter)
                {
                    if (!inst.Shooter.IsEnemy(validation.tank.Team))
                        return false;// Stop friendly-fire
                    if (inst.Shooter == validation.tank)
                        return false;// Stop self-fire
                }
                if (validation.tank.IsNeutral())
                    return false;// No damage Invinci
            }
            return true;
        }
    }
}
