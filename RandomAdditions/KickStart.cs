﻿using System;
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
        public static GameObject logMan;

        public static bool DebugPopups = false;
        public static bool UseAltDateFormat = false; //Change the date format to Y M D (requested by Exund [Weathermod])

        // NativeOptions Parameters
        public static OptionToggle allowPopups;
        public static OptionToggle AltDateFormat;


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
            logMan = new GameObject("logMan");
            logMan.AddComponent<LogHandler>();
            logMan.GetComponent<LogHandler>().Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("RandomAdditions: Found Water Mod!  Enabling water-related features!");
                isWaterModPresent = true;
            }


            ModConfig thisModConfig = new ModConfig();
            thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
            thisModConfig.BindConfig<KickStart>(null, "UseAltDateFormat");


            var RandomProperties = ModName;
            allowPopups = new OptionToggle("Enable custom block debug popups", RandomProperties, DebugPopups);
            allowPopups.onValueSaved.AddListener(() => { DebugPopups = allowPopups.SavedValue; });
            AltDateFormat = new OptionToggle("Y/M/D Format", RandomProperties, UseAltDateFormat);
            AltDateFormat.onValueSaved.AddListener(() => { UseAltDateFormat = AltDateFormat.SavedValue; });
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
    }
}
