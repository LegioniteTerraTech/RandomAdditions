using System;
using System.Collections.Generic;
using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    internal class KickStartOptions
    {
#if !STEAM
        internal static ModHelper.Config.ModConfig config;
#else
        internal static ModHelper.ModConfig config;
#endif

        // NativeOptions Parameters
        // GENERAL
        public static Nuterra.NativeOptions.OptionToggle altDateFormat;
        public static Nuterra.NativeOptions.OptionToggle noCameraShake;
        public static Nuterra.NativeOptions.OptionToggle scaleBlocksInSCU;
        public static Nuterra.NativeOptions.OptionToggle realShields;
        public static Nuterra.NativeOptions.OptionToggle moddedPopupReset;
        public static Nuterra.NativeOptions.OptionRange modWrenchIconScale;
        public static Nuterra.NativeOptions.OptionToggle trySaveBrokenSnaps;

        public static Nuterra.NativeOptions.OptionRange replaceChance;
        public static Nuterra.NativeOptions.OptionToggle rpSea;
        public static Nuterra.NativeOptions.OptionToggle rpLand;

        // CHEATS
        public static Nuterra.NativeOptions.OptionToggle AlteredVanilla;
        public static Nuterra.NativeOptions.OptionRange XpMulti;
        public static Nuterra.NativeOptions.OptionRange BBMulti;
        public static Nuterra.NativeOptions.OptionRange BlocksMulti;

        // CONTROLS
        public static Nuterra.NativeOptions.OptionToggle lockP_BoostProps;
        public static Nuterra.NativeOptions.OptionToggle lockP_Pitch;
        public static Nuterra.NativeOptions.OptionToggle lockP_Yaw;
        public static Nuterra.NativeOptions.OptionToggle lockP_Roll;
        public static Nuterra.NativeOptions.OptionKey hangarKey;

        // DEVELOPMENT
        public static Nuterra.NativeOptions.OptionToggle fakeOfflineEpic;
        public static Nuterra.NativeOptions.OptionKey blockSnap;
        public static Nuterra.NativeOptions.OptionToggle allowQuitFromIngameMenu;
        public static Nuterra.NativeOptions.OptionToggle customBlockDebugPopups;
        public static Nuterra.NativeOptions.OptionList<string> startup;
        public static Nuterra.NativeOptions.OptionList<string> SaveMyLargeTechs;
        public static Nuterra.NativeOptions.OptionToggle BypassLargeTechSetting;
        public static Nuterra.NativeOptions.OptionToggle fastPhysics;
        public static Nuterra.NativeOptions.OptionToggle OccuCull;
        public static Nuterra.NativeOptions.OptionRange OccuCullVisDepth;
        public static Nuterra.NativeOptions.OptionRange OccuCullColDepth;
        public static Nuterra.NativeOptions.OptionToggle disColliders;
        public static Nuterra.NativeOptions.OptionToggle hideHoverParticles;
        public static Nuterra.NativeOptions.OptionToggle smartCircuits;
        public static Nuterra.NativeOptions.OptionToggle disableCircuits;
        public static Nuterra.NativeOptions.OptionRange smartHovers;
        public static Nuterra.NativeOptions.OptionToggle smartColliders;
        public static Nuterra.NativeOptions.OptionToggle ignoreAiming;

        public static Nuterra.NativeOptions.OptionRange fastnerFast;

        // Tony Rails
        public static Nuterra.NativeOptions.OptionRange RailRenderRange;
        public static Nuterra.NativeOptions.OptionRange RailPathingUpdateSpeed;


        private static bool launched = false;

        public static void ResetValues()
        {
            AlteredVanilla.Value = RandomWorld.inst.WorldAltered;
            BBMulti.Value = RandomWorld.inst.LootBBMulti;
            XpMulti.Value = RandomWorld.inst.LootXpMulti;
            BlocksMulti.Value = RandomWorld.inst.LootBlocksMulti;
        }

        public static void ResyncValues()
        {
            AlteredVanilla.Value = RandomWorld.inst.WorldAltered;
            if (RandomWorld.inst.WorldAltered)
            {
                RandomWorld.BeginCheating();
            }
            else
            {
                AlteredVanilla.SetExtraTextUIOnly("Off");
            }
            if (RandomWorld.inst.LootBBMulti > 1f)
                BBMulti.Value = ((RandomWorld.inst.LootBBMulti - 1) / 4) + 1;
            else
                BBMulti.Value = RandomWorld.inst.LootBBMulti;
            if (RandomWorld.inst.LootXpMulti > 1f)
                XpMulti.Value = ((RandomWorld.inst.LootXpMulti - 1) / 4) + 1;
            else
                XpMulti.Value = RandomWorld.inst.LootXpMulti;
            if (RandomWorld.inst.LootBlocksMulti > 1f)
                BlocksMulti.Value = ((RandomWorld.inst.LootBlocksMulti - 1) / 4) + 1;
            else
                BlocksMulti.Value = RandomWorld.inst.LootBlocksMulti;
        }

        public static void TryInitOptionAndConfig()
        {
            if (launched)
                return;
            launched = true;
            //Initiate the madness
            try
            {
#if !STEAM
                ModHelper.Config.ModConfig thisModConfig = new ModHelper.ModConfig();
#else
                ModHelper.ModConfig thisModConfig = new ModHelper.ModConfig();
#endif
                thisModConfig.BindConfig<KickStart>(null, "ImmediateLoadLastSave");
                thisModConfig.BindConfig<KickStart>(null, "UseAltDateFormat");
                thisModConfig.BindConfig<KickStart>(null, "NoShake");
                thisModConfig.BindConfig<KickStart>(null, "AutoScaleBlocksInSCU");
                thisModConfig.BindConfig<KickStart>(null, "ModWrenchScale");
                thisModConfig.BindConfig<KickStart>(null, "TrySaveMyTechs");
#if !STEAM
                thisModConfig.BindConfig<KickStart>(null, "TrueShields");
#endif
                thisModConfig.BindConfig<KickStart>(null, "GlobalBlockReplaceChance");
                thisModConfig.BindConfig<KickStart>(null, "MandateLandReplacement");
                thisModConfig.BindConfig<KickStart>(null, "MandateSeaReplacement");
                thisModConfig.BindConfig<KickStart>(null, "ResetModdedPopups");

                // CONTROLS
                thisModConfig.BindConfig<KickStart>(null, "LockPropWhenPropBoostOnly");
                thisModConfig.BindConfig<KickStart>(null, "LockPropPitch");
                thisModConfig.BindConfig<KickStart>(null, "LockPropRoll");
                thisModConfig.BindConfig<KickStart>(null, "LockPropYaw");
                thisModConfig.BindConfig<KickStart>(null, "_hangarButton");

                // DEVELOPMENT
                thisModConfig.BindConfig<KickStart>(null, "IDontTrustEpicAtAll");
                thisModConfig.BindConfig<BlockDebug>(null, "DebugPopups");
                thisModConfig.BindConfig<KickStart>(null, "_snapBlockButton");
                thisModConfig.BindConfig<KickStart>(null, "ForceIntoModeStartup");
                thisModConfig.BindConfig<KickStart>(null, "SaveMyTechMax");
                thisModConfig.BindConfig<KickStart>(null, "OverrideTechMax");
                thisModConfig.BindConfig<KickStart>(null, "AllowIngameQuitToDesktop");
                thisModConfig.BindConfig<KickStart>(null, "FastestPhysics");
                thisModConfig.BindConfig<KickStart>(null, "ColliderDisable2");
                thisModConfig.BindConfig<KickStart>(null, "OcculsionCulling");
                thisModConfig.BindConfig<GraphicsPhysicsCulling>(null, "HideObscurityDepth");
                thisModConfig.BindConfig<GraphicsPhysicsCulling>(null, "NoCollisionDepth");

                thisModConfig.BindConfig<KickStart>(null, "FastenerSpeed");

                thisModConfig.BindConfig<KickStart>(null, "noCircuits");
                thisModConfig.BindConfig<KickStart>(null, "smrtCircuits");
                thisModConfig.BindConfig<KickStart>(null, "hideHov");
                thisModConfig.BindConfig<KickStart>(null, "smrtHov");
                /*  // Doesn't work, TT is too spagetti coded
                thisModConfig.BindConfig<KickStart>(null, "smrtCol");
                thisModConfig.BindConfig<KickStart>(null, "disableAiming");
                */

                // Tony Rails
                thisModConfig.BindConfig<ManRails>(null, "MaxRailLoadRange");
                thisModConfig.BindConfig<ManTrainPathing>(null, "QueueStepRepeatTimes");


                config = thisModConfig;

                var RandomProperties = KickStart.ModName + " - General";
#if !STEAM
                realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, KickStart.TrueShields);
                realShields.onValueSaved.AddListener(() => { KickStart.TrueShields = realShields.SavedValue; });
#endif
                Nuterra.NativeOptions.OptionToggle togTest = new Nuterra.NativeOptions.OptionToggle("Clock Y/M/D Format", RandomProperties, KickStart.UseAltDateFormat);
                togTest.onValueSaved.AddListener(() => { KickStart.UseAltDateFormat = togTest.SavedValue; });
                altDateFormat = togTest;
                noCameraShake = new Nuterra.NativeOptions.OptionToggle("Disable Damage Feedback Rattle", RandomProperties, KickStart.NoShake);
                noCameraShake.onValueSaved.AddListener(() => { KickStart.NoShake = noCameraShake.SavedValue; });
                scaleBlocksInSCU = new Nuterra.NativeOptions.OptionToggle("Shrink Blocks Grabbed by SCU", RandomProperties, KickStart.AutoScaleBlocksInSCU);
                scaleBlocksInSCU.onValueSaved.AddListener(() => { KickStart.AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; });
                moddedPopupReset = new Nuterra.NativeOptions.OptionToggle("Reset All Mod Hints", RandomProperties, KickStart.ResetModdedPopups);
                moddedPopupReset.onValueSaved.AddListener(() => {
                    if (moddedPopupReset.SavedValue)
                    {
                        ExtUsageHint.ResetHints();
                        moddedPopupReset.ResetValue();
                    }
                });
                modWrenchIconScale = SuperNativeOptions.OptionRangeAutoDisplay("Modded Block Wrench Icon Size",
                    RandomProperties, KickStart.ModWrenchScale, 0.25f, 1f, 0.125f, (float value) =>
                    {
                        return value.ToString("P");
                    });
                modWrenchIconScale.onValueSaved.AddListener(() => { KickStart.ModWrenchScale = Mathf.RoundToInt(modWrenchIconScale.SavedValue); });
                trySaveBrokenSnaps = new Nuterra.NativeOptions.OptionToggle("Try Rescue Old Techs On Snaps Load (SLOW)", RandomProperties, KickStart.TrySaveMyTechs);
                trySaveBrokenSnaps.onValueSaved.AddListener(() => { KickStart.TrySaveMyTechs = trySaveBrokenSnaps.Value; });

                var RandomBlocks = KickStart.ModName + " - Population Tweaks";
                replaceChance = SuperNativeOptions.OptionRangeAutoDisplay("Chance for Custom Block replacement",
                    RandomBlocks, KickStart.GlobalBlockReplaceChance, 0, 100, 10, (float value) =>
                    {
                        return value.ToString("0") + "%";
                    });
                replaceChance.onValueSaved.AddListener(() => { KickStart.GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); });
                rpLand = new Nuterra.NativeOptions.OptionToggle("Force Land Custom Block", RandomBlocks, KickStart.MandateLandReplacement);
                rpLand.onValueSaved.AddListener(() => { KickStart.MandateLandReplacement = rpLand.SavedValue; });
                rpSea = new Nuterra.NativeOptions.OptionToggle("Force Sea Custom Block Replacement", RandomBlocks, KickStart.MandateSeaReplacement);
                rpSea.onValueSaved.AddListener(() => { KickStart.MandateSeaReplacement = rpSea.SavedValue; });


                var RandomControls = KickStart.ModName + " - Controls";
                lockP_BoostProps = new Nuterra.NativeOptions.OptionToggle("Lock Propeller Steering Only When Pressing Prop Button", RandomControls, KickStart.LockPropWhenPropBoostOnly);
                lockP_BoostProps.onValueSaved.AddListener(() => { KickStart.LockPropWhenPropBoostOnly = lockP_BoostProps.SavedValue; });
                lockP_Pitch = new Nuterra.NativeOptions.OptionToggle("Lock Propellers Pitch Steering", RandomControls, KickStart.LockPropPitch);
                lockP_Pitch.onValueSaved.AddListener(() => { KickStart.LockPropPitch = lockP_Pitch.SavedValue; });
                lockP_Roll = new Nuterra.NativeOptions.OptionToggle("Lock Propellers Roll Steering", RandomControls, KickStart.LockPropRoll);
                lockP_Roll.onValueSaved.AddListener(() => { KickStart.LockPropRoll = lockP_Roll.SavedValue; });
                lockP_Yaw = new Nuterra.NativeOptions.OptionToggle("Lock Propellers Yaw Steering", RandomControls, KickStart.LockPropYaw);
                lockP_Yaw.onValueSaved.AddListener(() => { KickStart.LockPropYaw = lockP_Yaw.SavedValue; });

                hangarKey = new Nuterra.NativeOptions.OptionKey("Hangar Docking Hotkey [+ Left Click]", RandomControls, KickStart.HangarButton);
                hangarKey.onValueSaved.AddListener(() => {
                    KickStart.HangarButton = hangarKey.SavedValue;
                    KickStart._hangarButton = (int)hangarKey.SavedValue;
                });


                var LagSolutions = KickStart.ModName + " - Lag Reduction";
                smartCircuits = new Nuterra.NativeOptions.OptionToggle("Smart Circuits (MIGHT BREAK C&S DESIGNS) [REDUCE BIG LAG]", LagSolutions, KickStart.smrtCircuits);
                smartCircuits.onValueSaved.AddListener(() =>
                {
                    KickStart.smrtCircuits = smartCircuits.SavedValue;
                });
                disableCircuits = new Nuterra.NativeOptions.OptionToggle("Disable Circuits Entirely (DISABLES C&S) [SP] [REDUCES HUGE LAG]", LagSolutions, KickStart.noCircuits);
                disableCircuits.onValueSaved.AddListener(() =>
                {
                    KickStart.noCircuits = disableCircuits.SavedValue;
                });
                OccuCull = new Nuterra.NativeOptions.OptionToggle("Block Occulsion Culling (MAY MESS WITH ACCURACCY)", LagSolutions, KickStart.OcculsionCulling);
                OccuCull.onValueSaved.AddListener(() =>
                {
                    KickStart.OcculsionCulling = OccuCull.SavedValue;
                    if (KickStart.OcculsionCulling && !KickStart.OcculsionCullingInit)
                    {
                        ManWorldTileExt.HostOnly_ReloadENTIREScene(true);
                        KickStart.OcculsionCullingInit = true;
                    }
                });
                OccuCullVisDepth = SuperNativeOptions.OptionRangeAutoDisplay("Block Occulsion Culling Visual Depth", LagSolutions, GraphicsPhysicsCulling.HideObscurityDepth,
                    0, 8, 1, (float val) =>
                    {
                        val += 1f;
                        if (val < 2f)
                            return "Only !" + val.ToString("0") + "! blocks depth";
                        return "Only " + val.ToString("0") + "blocks depth";
                    });
                OccuCullVisDepth.onValueSaved.AddListener(() =>
                {
                    GraphicsPhysicsCulling.HideObscurityDepth = (uint)Mathf.RoundToInt(OccuCullVisDepth.SavedValue);
                    GraphicsPhysicsCulling.UpdateVisibility();
                });
                OccuCullColDepth = SuperNativeOptions.OptionRangeAutoDisplay("Block Occulsion Culling Collision Depth", LagSolutions, GraphicsPhysicsCulling.NoCollisionDepth,
                    0, 8, 1, (float val) =>
                    {
                        val += 1f;
                        if (val < 2f)
                            return "Only !" + val.ToString("0") + "! blocks depth";
                        return "Only " + val.ToString("0") + " blocks depth";
                    });
                OccuCullColDepth.onValueSaved.AddListener(() =>
                {
                    GraphicsPhysicsCulling.NoCollisionDepth = (uint)Mathf.RoundToInt(OccuCullVisDepth.SavedValue);
                    GraphicsPhysicsCulling.UpdateVisibility();
                });
                fastPhysics = new Nuterra.NativeOptions.OptionToggle("Fast Physics (Doesn't do much)", LagSolutions, KickStart.FastestPhysics);
                fastPhysics.onValueSaved.AddListener(() =>
                {
                    KickStart.FastestPhysics = fastPhysics.SavedValue;
                    Optimax.PrematureOptimization(KickStart.FastestPhysics);
                });
                disColliders = new Nuterra.NativeOptions.OptionToggle("Disable Tech Collision [SP]", LagSolutions, KickStart.ColliderDisable2);
                disColliders.onValueSaved.AddListener(() =>
                {
                    KickStart.ColliderDisable2 = disColliders.SavedValue;
                    Optimax.UpdateColliders();
                });
                hideHoverParticles = new Nuterra.NativeOptions.OptionToggle("Hide hover particles [REDUCES SOME LAG]", LagSolutions, KickStart.hideHov);
                hideHoverParticles.onValueSaved.AddListener(() =>
                {
                    KickStart.hideHov = hideHoverParticles.SavedValue;
                });
                /*  // Doesn't work, TT is too spagetti coded
                smartColliders = new Nuterra.NativeOptions.OptionToggle("Smart Tech Colliders [More frames but lag spikes]", LagSolutions, KickStart.smrtCol);
                smartColliders.onValueSaved.AddListener(() =>
                {
                    KickStart.smrtCol = smartColliders.SavedValue;
                    if (KickStart.smrtCol)
                        TechColliderIgnorer.Init();
                    else
                        TechColliderIgnorer.DeInit();
                });
                */
                smartHovers = SuperNativeOptions.OptionRangeAutoDisplay("Lazy Hovers (LOWERED HOVER RELIABILITY)", LagSolutions, KickStart.smrtHov,
                    0, 64, 4, (float val) =>
                    {
                        if (val == 0)
                            return "Not Lazy";
                        return "Only " + val.ToString("0") + " hovers per update";
                    });
                smartHovers.onValueSaved.AddListener(() =>
                {
                    KickStart.smrtHov = Mathf.RoundToInt(smartHovers.SavedValue);
                    if (KickStart.smrtHov > 0)
                        HoverOpti.Init();
                    else
                        HoverOpti.DeInit();
                });
                ignoreAiming = new Nuterra.NativeOptions.OptionToggle("Disable Tech Aiming [SP]", LagSolutions, KickStart.disableAiming);
                ignoreAiming.onValueSaved.AddListener(() =>
                {
                    KickStart.disableAiming = ignoreAiming.SavedValue;
                });



                var RandomDev = KickStart.ModName + " - Development";

                fakeOfflineEpic = new Nuterra.NativeOptions.OptionToggle("Force Epic Online Services Offline [Slows MP Lobby Loading!]", RandomDev, KickStart.IDontTrustEpicAtAll);
                fakeOfflineEpic.onValueSaved.AddListener(() =>
                {
                    KickStart.IDontTrustEpicAtAll = fakeOfflineEpic.SavedValue;
                    if (KickStart.IDontTrustEpicAtAll == true)
                    {
                        KickStart.CheckShouldDisableEOS();
                        if (ManEOS.inst.IsCrossplayRequestedActive)
                            ManModGUI.ShowErrorPopup("RandomAddtions: Force Epic Online Services Offline cannot do it's job if Crossplay is set to be active.\nMake sure to launch TerraTech WITHOUT Crossplay!");
                    }
                });
                fastnerFast = SuperNativeOptions.OptionRangeAutoDisplay("C&S Fastener Speed", RandomDev, KickStart.FastenerSpeed,
                    0, 20, 1, (float val) =>
                    {
                        if (val == 0)
                            return "Default";
                        if (val > 5)
                            return (10f / (val + 10f)).ToString("P") + " time [UNSAFE]";
                        return (10f / (val + 10f)).ToString("P") + " time";
                    });
                fastnerFast.onValueSaved.AddListener(() =>
                {
                    KickStart.FastenerSpeed = Mathf.RoundToInt(fastnerFast.SavedValue);
                });

                try
                {
                    KickStartOptionsSafeSaves.TryInitOptionAndConfig(RandomDev, thisModConfig);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup SafeSaves");
                    DebugRandAddi.Log(e);
                }
                customBlockDebugPopups = new Nuterra.NativeOptions.OptionToggle("Enable custom block debug popups", RandomDev, BlockDebug.DebugPopups);
                customBlockDebugPopups.onValueSaved.AddListener(() => { BlockDebug.DebugPopups = customBlockDebugPopups.SavedValue; });
                blockSnap = new Nuterra.NativeOptions.OptionKey("Snapshot Block Hotkey [+ Left Click]", RandomDev, KickStart.SnapBlockButton);
                blockSnap.onValueSaved.AddListener(() => {
                    KickStart.SnapBlockButton = blockSnap.SavedValue;
                    KickStart._snapBlockButton = (int)blockSnap.SavedValue;
                });
                startup = new Nuterra.NativeOptions.OptionList<string>("Skip Title Screen", RandomDev, KickStart.gamemodeSwitch, KickStart.ForceIntoModeStartup);
                startup.onValueSaved.AddListener(() =>
                {
                    KickStart.ForceIntoModeStartup = startup.SavedValue;
                    KickStart.quickData.failedLastBoot = false;
                });
                List<string> lister = new List<string>()
                {
                    "64x64x64",
                    "128x128x128",
                    "256x256x256",
                };
                SaveMyLargeTechs = new Nuterra.NativeOptions.OptionList<string>("R&D Fallback Tech Size [CAN CAUSE CRASH]", RandomDev, lister, KickStart.SaveMyTechMax);
                SaveMyLargeTechs.onValueSaved.AddListener(() =>
                {
                    KickStart.SaveMyTechMax = SaveMyLargeTechs.SavedValue;
                });
                BypassLargeTechSetting = new Nuterra.NativeOptions.OptionToggle("Override R&D Tech Size with above", RandomDev, KickStart.OverrideTechMax);
                BypassLargeTechSetting.onValueSaved.AddListener(() =>
                {
                    KickStart.OverrideTechMax = BypassLargeTechSetting.SavedValue;
                });
                allowQuitFromIngameMenu = new Nuterra.NativeOptions.OptionToggle("Quit to Desktop Ingame", RandomDev, KickStart.AllowIngameQuitToDesktop);
                allowQuitFromIngameMenu.onValueSaved.AddListener(() =>
                {
                    KickStart.AllowIngameQuitToDesktop = allowQuitFromIngameMenu.SavedValue;
                    IngameQuit.SetExitButtonIngamePauseMenu(allowQuitFromIngameMenu.SavedValue);
                });

                var TonyRails = KickStart.ModName + " - Tony Rails";
                RailRenderRange = SuperNativeOptions.OptionRangeAutoDisplay("Rail Render Range", TonyRails,
                    ManRails.MaxRailLoadRange, 250, 750, 50, (float value) =>
                    {
                        return value.ToString("0") + "m";
                    });
                RailRenderRange.onValueSaved.AddListener(() =>
                {
                    ManRails.MaxRailLoadRange = RailRenderRange.SavedValue;
                    ManRails.MaxRailLoadRangeSqr = ManRails.MaxRailLoadRange * ManRails.MaxRailLoadRange;
                });
                RailPathingUpdateSpeed = SuperNativeOptions.OptionRangeAutoDisplay("Train Pathing Speed", TonyRails,
                    ManTrainPathing.QueueStepRepeatTimes, 1, 6, 1, (float value) =>
                    {
                        return value.ToString("0") + "x";
                    });
                RailPathingUpdateSpeed.onValueSaved.AddListener(() =>
                {
                    ManTrainPathing.QueueStepRepeatTimes = Mathf.FloorToInt(RailPathingUpdateSpeed.SavedValue);
                });

                var Cheats = KickStart.ModName + " - Host World Tweaks";
                AlteredVanilla = new Nuterra.NativeOptions.OptionToggle("Enable (CANNOT BE UNDONE)", Cheats, RandomWorld.inst.WorldAltered);
                AlteredVanilla.onValueSaved.AddListener(() =>
                {
                    if (AlteredVanilla.Value)
                    {
                        RandomWorld.BeginCheating();
                        RandomWorld.inst.WorldAltered = true;
                    }
                });
                AlteredVanilla.SetExtraTextUIOnly("Off");
                BlocksMulti = SuperNativeOptions.OptionRangeAutoDisplay("Mission Random Blocks Multiplier [0-1-40x]",
                    Cheats, RandomWorld.inst.LootBlocksMulti, 0, 10.75f, 0.25f,
                    (float value) => {
                        if (value > 1f)
                            return (((value - 1) * 4) + 1);
                        else
                            return value;
                    });
                BlocksMulti.onValueSaved.AddListener(() => {
                    if (BlocksMulti.SavedValue > 1f)
                        RandomWorld.inst.LootBlocksMulti = ((BlocksMulti.SavedValue - 1) * 4) + 1;
                    else
                        RandomWorld.inst.LootBlocksMulti = BlocksMulti.SavedValue;
                });
                XpMulti = SuperNativeOptions.OptionRangeAutoDisplay("Mission Xp Multiplier [0-1-40x]", Cheats,
                    RandomWorld.inst.LootXpMulti, 0, 10.75f, 0.25f,
                    (float value) => {
                        if (value > 1f)
                            return (((value - 1) * 4) + 1);
                        else
                            return value;
                    });
                XpMulti.onValueSaved.AddListener(() => {
                    if (XpMulti.SavedValue > 1f)
                        RandomWorld.inst.LootXpMulti = ((XpMulti.SavedValue - 1) * 4) + 1;
                    else
                        RandomWorld.inst.LootXpMulti = XpMulti.SavedValue;
                });
                BBMulti = SuperNativeOptions.OptionRangeAutoDisplay("Mission Build Bucks Multiplier [0-1-40x]", Cheats,
                    RandomWorld.inst.LootBBMulti, 0, 10.75f, 0.25f,
                    (float value) => {
                        if (value > 1f)
                            return (((value - 1) * 4) + 1);
                        else
                            return value;
                    });
                BBMulti.onValueSaved.AddListener(() => {
                    if (BBMulti.SavedValue > 1f)
                        RandomWorld.inst.LootBBMulti = ((BBMulti.SavedValue - 1) * 4) + 1;
                    else
                        RandomWorld.inst.LootBBMulti = BBMulti.SavedValue;
                });

                Nuterra.NativeOptions.NativeOptionsMod.onOptionsSaved.AddListener(() => { config.WriteConfigJsonFile(); });
                if (KickStart.ColliderDisable2)
                    Optimax.UpdateColliders();
                if (KickStart.OcculsionCulling)
                    KickStart.OcculsionCullingInit = true;
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

    internal class KickStartOptionsSafeSaves
    {
        public static Nuterra.NativeOptions.OptionToggle saveExternal;
#if !STEAM
        public static void TryInitOptionAndConfig(string RandomDev, ModHelper.Config.ModConfig thisModConfig)
#else
        public static void TryInitOptionAndConfig(string RandomDev, ModHelper.ModConfig thisModConfig)
#endif
        {
            //Initiate the madness
            try
            {
                thisModConfig.BindConfig<SafeSaves.ManSafeSaves>(null, "DisableExternalBackupSaving");
                saveExternal = new Nuterra.NativeOptions.OptionToggle("Save Mod Information in External File", RandomDev, SafeSaves.ManSafeSaves.DisableExternalBackupSaving);
                saveExternal.onValueSaved.AddListener(() => { SafeSaves.ManSafeSaves.DisableExternalBackupSaving = saveExternal.SavedValue; });
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup SafeSaves");
                DebugRandAddi.Log(e);
            }

        }

    }
}
