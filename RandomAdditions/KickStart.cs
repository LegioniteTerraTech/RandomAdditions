using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ModHelper.Config;
using Nuterra.NativeOptions;


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

        internal static ModConfig config;

        // NativeOptions Parameters
        public static OptionToggle allowPopups;
        public static OptionToggle altDateFormat;
        public static OptionToggle noCameraShake;
        public static OptionToggle scaleBlocksInSCU;
        public static OptionToggle realShields;

        public static OptionRange replaceChance;
        public static OptionToggle rpSea;
        public static OptionToggle rpLand;

        public static float WaterHeight
        {
            get
            {
                float outValue = -25;
                try { outValue = WaterMod.QPatch.WaterHeight; } catch { }
                return outValue;
            }
        }


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
            GlobalClock.ClockManager.Initiate();
            GUIClock.Initiate();
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            logMan = new GameObject("logMan");
            logMan.AddComponent<LogHandler>();
            logMan.GetComponent<LogHandler>().Initiate();

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
            config = thisModConfig;

            var RandomProperties = ModName;
            realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, TrueShields);
            realShields.onValueSaved.AddListener(() => { TrueShields = realShields.SavedValue; config.WriteConfigJsonFile(); });
            allowPopups = new OptionToggle("Enable custom block debug popups", RandomProperties, DebugPopups);
            allowPopups.onValueSaved.AddListener(() => { DebugPopups = allowPopups.SavedValue; config.WriteConfigJsonFile(); });
            altDateFormat = new OptionToggle("Y/M/D Format", RandomProperties, UseAltDateFormat);
            altDateFormat.onValueSaved.AddListener(() => { UseAltDateFormat = altDateFormat.SavedValue; config.WriteConfigJsonFile(); });
            noCameraShake = new OptionToggle("Disable Camera Shake", RandomProperties, NoShake);
            noCameraShake.onValueSaved.AddListener(() => { NoShake = noCameraShake.SavedValue; config.WriteConfigJsonFile(); });
            scaleBlocksInSCU = new OptionToggle("Scale Blocks Grabbed by SCU", RandomProperties, AutoScaleBlocksInSCU);
            scaleBlocksInSCU.onValueSaved.AddListener(() => { AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; config.WriteConfigJsonFile(); });
            replaceChance = new OptionRange("Chance for modded block spawns", RandomProperties, GlobalBlockReplaceChance, 0, 100, 10);
            replaceChance.onValueSaved.AddListener(() => { GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); config.WriteConfigJsonFile(); });
            rpLand = new OptionToggle("Force Land Block Replacement", RandomProperties, MandateLandReplacement);
            rpLand.onValueSaved.AddListener(() => { MandateLandReplacement = rpLand.SavedValue; config.WriteConfigJsonFile(); });
            rpSea = new OptionToggle("Force Sea Block Replacement", RandomProperties, MandateSeaReplacement);
            rpSea.onValueSaved.AddListener(() => { MandateSeaReplacement = rpSea.SavedValue; config.WriteConfigJsonFile(); });
        }

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
            return trans.gameObject.GetComponentsInChildren<Transform>().ToList().Find(delegate (Transform cand) { return cand.name == "_Target"; });
        }

    }
}
