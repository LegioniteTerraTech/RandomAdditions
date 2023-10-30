using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using HarmonyLib;
using UnityEngine;
using TerraTechETCUtil;
#if !STEAM
using ModHelper.Config;
#else
using ModHelper;
#endif
using Nuterra.NativeOptions;
using RandomAdditions.RailSystem;
using RandomAdditions.PhysicsTethers;
using RandomAdditions.Minimap;


namespace RandomAdditions
{
    // A mod that does exactly as it says on the tin
    //   A bunch of random additions that make little to no sense, but they be.
    //
    public class KickStart
    {
        internal const string ModName = "RandomAdditions";
        internal const string ModID = "Random Additions";
        internal const float TerrainLowestAlt = -50;

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
        public static int ForceIntoModeStartup = 0;
        public static bool AllowIngameQuitToDesktop = false;


        // CONTROLS
        public static bool LockPropEnabled => LockPropPitch || LockPropRoll || LockPropYaw;
        public static bool LockPropWhenPropBoostOnly = true;
        public static bool LockPropPitch = false;
        public static bool LockPropYaw = false;
        public static bool LockPropRoll = false;
        public static KeyCode SnapBlockButton = KeyCode.KeypadPlus;
        public static int _snapBlockButton = (int)SnapBlockButton;
        public static KeyCode HangarButton = KeyCode.H;
        public static int _hangarButton = (int)HangarButton;

        // EVENTS
        public static bool CanUseMenu { get { return !ManPauseGame.inst.IsPaused; } }
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
                try
                {
                    if (isWaterModPresent)
                        outValue = WaterMod.QPatch.WaterHeight + 0.25f;
                }
                catch { }
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
                    LegModExt.InsurePatches();
                    if (MassPatcherRA.MassPatchAll())
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
                SafeSaves.ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly(), OnSaveManagers, OnLoadManagers);
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on RegisterSaveSystem");
                DebugRandAddi.Log(e);
            }
            try
            {
                if (ForceIntoModeStartup > 0)
                {
                    // FORCE STARTUP SWITCH
                    DebugRandAddi.Log("RandomAdditions: Prepping Quick Start...");
                    harmonyInstance.MassPatchAllWithin(typeof(PatchStartup), ModName);
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Prepping Quick Start");
            }
            OfficialEarlyInited = true;
        }


        public static void MainOfficialInit()
        {
            //SafeSaves.DebugSafeSaves.LogAll = true;
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
                    LegModExt.InsurePatches();
                    if (MassPatcherRA.MassPatchAll())
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
            ModHelpers.Initiate();
            GUIClock.Initiate();
            ManTileLoader.Initiate();
            ManPhysicsExt.InsureInit();
            ManRails.Initiate();
            RandAddDebugGUI.Initiate();

            // Net hooks
            ModuleHangar.InsureNetHooks();
            ManRails.InsureNetHooks();

            // etc
            IngameQuit.Init();
#if DEBUG
            DebugExtUtilities.AllowEnableDebugGUIMenu_KeypadEnter = true;
#endif
        }

#if STEAM
        public static void DeInitALL()
        {
            if (patched)
            {
                try
                {
                    if (MassPatcherRA.MassUnPatchAll())
                    {
                        DebugRandAddi.Log("RandomAdditions: UnPatched");
                        patched = false;
                    }
                    else
                        DebugRandAddi.Log("RandomAdditions: Error on UnPatch");
                    LegModExt.RemovePatches();
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
            RandAddDebugGUI.DeInit();
            ManRadio.DeInit();
            CircuitExt.Unload();
            ManMinimapExt.DeInitAll();
            ManRails.DeInit();
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
            LegModExt.InsurePatches();
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
            GetAvail
            
            
            ();
#endif
        }
#endif
        public static void OnSaveManagers(bool Doing)
        {
            if (Doing)
            {
                ManTileLoader.OnWorldSave();
                ManRails.PrepareForSaving();
            }
            else
            {
                ManRails.FinishedSaving();
                ManTileLoader.OnWorldFinishSave();
            }
        }
        public static void OnLoadManagers(bool Doing)
        {
            if (!Doing)
            {
                ManTileLoader.OnWorldLoad();
                ManRails.FinishedLoading();
            }
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

        public static Type LookForType(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var typeGet = assembly.GetType(name);
                if (typeGet != null)
                {
                    return typeGet;
                }
            }
            return null;
        }
        public static Transform HeavyTransformSearch(Transform trans, string name)
        {
            if (name.NullOrEmpty())
                return null;
            return trans.gameObject.GetComponentsInChildren<Transform>().FirstOrDefault(delegate (Transform cand)
            {
                if (cand.name.NullOrEmpty())
                    return false;
                return cand.name.CompareTo(name) == 0;
            });
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

        // Additional section for immedeate game entering
        private static bool forcedToGame = false;
        public static bool QuickStartGame()
        {
            if (forcedToGame)
                return true;
            forcedToGame = true;
#if DEBUG
            DebugExtUtilities.AllowEnableDebugGUIMenu_KeypadEnter = true;
#endif
            if (Input.GetKey(KeyCode.Backspace) || Input.GetKey(KeyCode.Escape))
                return true;
            try
            {
                if (ForceIntoModeStartup > 0)
                {
                    ManProfile.Profile prof = ManProfile.inst.GetCurrentUser();
                    if (prof != null)
                    {
                        DebugRandAddi.Log("RandomAdditions: Quick-Starting...");
                        ManGameMode.GameType GT = ManGameMode.GameType.Creative;
                        bool hasSaveName = false;
                        switch (ForceIntoModeStartup)
                        {
                            case 1:
                                if (prof.m_LastUsedSaveName != null)
                                {
                                    if (prof.m_LastUsedSaveType == ManGameMode.GameType.RaD && !ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                                        break;
                                    hasSaveName = true;
                                    GT = prof.m_LastUsedSaveType;
                                }
                                break;
                            case 2:
                                GT = ManGameMode.GameType.Creative;
                                break;
                            case 3:
                                if (!ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                                    break;
                                GT = ManGameMode.GameType.RaD;
                                break;
                            default:
                                break;
                        }
                        DebugRandAddi.Log("RandomAdditions: Next mode, " + GT.ToString());
                        ManUI.inst.ExitAllScreens();
                        ManGameMode.inst.ClearModeInitSettings();
                        if (hasSaveName)
                            ManGameMode.inst.SetupSaveGameToLoad(GT, prof.m_LastUsedSaveName, prof.m_LastUsedSave_WorldGenVersionData);
                        else
                            ManGameMode.inst.SetupModeSwitchAction(ManGameMode.inst.NextModeSetting, GT);
                        ManGameMode.inst.NextModeSetting.SwitchToMode();
                        ManUI.inst.FadeToBlack(0, true);
                        DebugRandAddi.Log("RandomAdditions: Success on QuickStartGame");
                        return false;
                    }
                    else
                        DebugRandAddi.Log("RandomAdditions: QuickStartGame failed!  User profile is not set!  Cannot Execute!");
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Failed in quickstarting " + e);
            }
            ManGameMode.inst.ModeStartEvent.Subscribe(OnFinishedQuickstart);
            return true;
        }
        public static void OnFinishedQuickstart(Mode unused)
        {
            ManUI.inst.ClearFade(1);
            harmonyInstance.MassUnPatchAllWithin(typeof(PatchStartup), ModName);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(OnFinishedQuickstart);
        }

        public static BlockTypes GetProperBlockType(BlockTypes BTVanilla, string blockTypeString)
        {
            if (Singleton.Manager<ManMods>.inst.IsModdedBlock(BTVanilla, false))
                return (BlockTypes)Singleton.Manager<ManMods>.inst.GetBlockID(blockTypeString);
            return BTVanilla;
        }

        internal static TankBlock SpawnBlockS(BlockTypes type, Vector3 position, Quaternion quat, out bool worked)
        {
            if (Singleton.Manager<ManWorld>.inst.CheckIsTileAtPositionLoaded(position))
            {
                if (Singleton.Manager<ManSpawn>.inst.IsBlockAllowedInCurrentGameMode(type))
                {
                    worked = true;

                    TankBlock block = Singleton.Manager<ManLooseBlocks>.inst.HostSpawnBlock(type, position, quat, false);
                    var dmg = block.GetComponent<Damageable>();
                    if (dmg)
                    {
                        if (!dmg.IsAtFullHealth)
                            block.InitNew();
                    }
                    return block;
                }
                try
                {
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Error on block " + type.ToString());
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Error on unfetchable block");
                }
                if (!Singleton.Manager<ManSpawn>.inst.IsTankBlockLoaded(type))
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Could not spawn block!  Block does not exist!");
                else
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Could not spawn block!  Block is invalid in current gamemode!");

            }
            else
            {
                try
                {
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Error on block " + type.ToString());
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Error on unfetchable block");
                }
                DebugRandAddi.Log("RandomAdditions: SpawnBlockS - Could not spawn block!  Block tried to spawn out of bounds!");
            }
            worked = false;
            return null;
        }


        public static AnimetteController[] FetchAnimettes(Transform transform, AnimCondition condition)
        {
            try
            {
                AnimetteController[] MA = transform.GetComponentsInChildren<AnimetteController>(true);
                if (MA != null && MA.Length > 0)
                {
                    List<AnimetteController> MAs = new List<AnimetteController>();
                    foreach (var item in MA)
                    {
                        if (item.Condition == condition || item.Condition == AnimCondition.Any)
                        {
                            MAs.Add(item);
                        }
                    }
                    if (MAs.Count > 0)
                    {
                        DebugRandAddi.Log("RandomAdditions: FetchAnimette - fetched " + MAs.Count + " animettes");
                        return MAs.ToArray();
                    }
                }
            }
            catch { }
            return null;
        }
        public static AnimetteController FetchAnimette(Transform transform, string gameObjectName, AnimCondition condition)
        {
            try
            {
                AnimetteController[] MA;
                if (gameObjectName == null)
                    MA = transform.GetComponentsInChildren<AnimetteController>(true);
                else
                    MA = transform.Find(gameObjectName).GetComponentsInChildren<AnimetteController>(true);
                if (MA != null && MA.Length > 0)
                {
                    foreach (var item in MA)
                    {
                        if (item.Condition == condition || item.Condition == AnimCondition.Any)
                        {
                            DebugRandAddi.Log("RandomAdditions: FetchAnimette - fetched animette in " + item.name);
                            return item;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        public static Vector3 GetClosestPoint(Vector3[] points, Vector3 scenePos, out float percentPos)
        {
            Vector3 closest = Vector3.zero;
            float posDist = int.MaxValue;
            float posCase;
            int step = 0;
            percentPos = 0;
            foreach (var item in points)
            {
                posCase = (item - scenePos).sqrMagnitude;
                if (posCase < posDist)
                {
                    posDist = posCase;
                    closest = item;
                    percentPos = (float)step / points.Length;
                }
                step++;
            }
            return closest;
        }
        public static Vector3 GetClosestPoint(Vector3[] points, Vector3 scenePos)
        {
            Vector3 closest = Vector3.zero;
            float posDist = int.MaxValue;
            float posCase;
            int step = 0;
            foreach (var item in points)
            {
                posCase = (item - scenePos).sqrMagnitude;
                if (posCase < posDist)
                {
                    posDist = posCase;
                    closest = item;
                }
                step++;
            }
            return closest;
        }
        public static int GetClosestIndex(List<Vector3> points, Vector3 scenePos)
        {
            int closest = 0;
            float posDist = int.MaxValue;
            float posCase;
            int step = 0;
            foreach (var item in points)
            {
                posCase = (item - scenePos).sqrMagnitude;
                if (posCase < posDist)
                {
                    posDist = posCase;
                    closest = step;
                }
                step++;
            }
            return closest;
        }

    }

    public class KickStartOptions
    {
        internal static ModConfig config;

        // NativeOptions Parameters
        // GENERAL
        public static OptionToggle altDateFormat;
        public static OptionToggle noCameraShake;
        public static OptionToggle scaleBlocksInSCU;
        public static OptionToggle realShields;
        public static OptionToggle moddedPopupReset;

        public static OptionRange replaceChance;
        public static OptionToggle rpSea;
        public static OptionToggle rpLand;

        // CONTROLS
        public static OptionToggle lockP_BoostProps;
        public static OptionToggle lockP_Pitch;
        public static OptionToggle lockP_Yaw;
        public static OptionToggle lockP_Roll;
        public static OptionKey hangarKey;

        // DEVELOPMENT
        public static OptionKey blockSnap;
        public static OptionToggle allowQuitFromIngameMenu;
        public static OptionToggle allowPopups;
        public static OptionList<string> startup;

        // Tony Rails
        public static OptionRange RailRenderRange;
        public static OptionRange RailPathingUpdateSpeed;


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

                // CONTROLS
                thisModConfig.BindConfig<KickStart>(null, "LockPropWhenPropBoostOnly");
                thisModConfig.BindConfig<KickStart>(null, "LockPropPitch");
                thisModConfig.BindConfig<KickStart>(null, "LockPropRoll");
                thisModConfig.BindConfig<KickStart>(null, "LockPropYaw");
                thisModConfig.BindConfig<KickStart>(null, "_hangarButton");

                // DEVELOPMENT
                thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
                thisModConfig.BindConfig<KickStart>(null, "_snapBlockButton");
                thisModConfig.BindConfig<KickStart>(null, "ForceIntoModeStartup");
                thisModConfig.BindConfig<KickStart>(null, "AllowIngameQuitToDesktop");

                // Tony Rails
                thisModConfig.BindConfig<ManRails>(null, "MaxRailLoadRange");
                thisModConfig.BindConfig<ManTrainPathing>(null, "QueueStepRepeatTimes");


                config = thisModConfig;

                var RandomProperties = KickStart.ModName + " - General";
#if !STEAM
                realShields = new OptionToggle("<b>Use Correct Shield Typing</b> \n[Vanilla has them wrong!] - (Restart to apply changes)", RandomProperties, KickStart.TrueShields);
                realShields.onValueSaved.AddListener(() => { KickStart.TrueShields = realShields.SavedValue; });
#endif
                altDateFormat = new OptionToggle("Clock Y/M/D Format", RandomProperties, KickStart.UseAltDateFormat);
                altDateFormat.onValueSaved.AddListener(() => { KickStart.UseAltDateFormat = altDateFormat.SavedValue; });
                noCameraShake = new OptionToggle("Disable Damage Feedback Rattle", RandomProperties, KickStart.NoShake);
                noCameraShake.onValueSaved.AddListener(() => { KickStart.NoShake = noCameraShake.SavedValue; });
                scaleBlocksInSCU = new OptionToggle("Shrink Blocks Grabbed by SCU", RandomProperties, KickStart.AutoScaleBlocksInSCU);
                scaleBlocksInSCU.onValueSaved.AddListener(() => { KickStart.AutoScaleBlocksInSCU = scaleBlocksInSCU.SavedValue; });
                moddedPopupReset = new OptionToggle("Reset All Mod Hints", RandomProperties, KickStart.ResetModdedPopups);
                moddedPopupReset.onValueSaved.AddListener(() => {
                    if (moddedPopupReset.SavedValue)
                    {
                        ExtUsageHint.ResetHints();
                        moddedPopupReset.ResetValue();
                    }
                });

                var RandomBlocks = KickStart.ModName + " - Population Tweaks";
                replaceChance = new OptionRange("Chance for Custom Block replacement", RandomBlocks, KickStart.GlobalBlockReplaceChance, 0, 100, 10);
                replaceChance.onValueSaved.AddListener(() => { KickStart.GlobalBlockReplaceChance = Mathf.RoundToInt(replaceChance.SavedValue); });
                rpLand = new OptionToggle("Force Land Custom Block", RandomBlocks, KickStart.MandateLandReplacement);
                rpLand.onValueSaved.AddListener(() => { KickStart.MandateLandReplacement = rpLand.SavedValue; });
                rpSea = new OptionToggle("Force Sea Custom Block Replacement", RandomBlocks, KickStart.MandateSeaReplacement);
                rpSea.onValueSaved.AddListener(() => { KickStart.MandateSeaReplacement = rpSea.SavedValue; });


                var RandomControls = KickStart.ModName + " - Controls";
                lockP_BoostProps = new OptionToggle("Lock Propeller Steering Only When Pressing Prop Button", RandomControls, KickStart.LockPropWhenPropBoostOnly);
                lockP_BoostProps.onValueSaved.AddListener(() => { KickStart.LockPropWhenPropBoostOnly = lockP_BoostProps.SavedValue; });
                lockP_Pitch = new OptionToggle("Lock Propellers Pitch Steering", RandomControls, KickStart.LockPropPitch);
                lockP_Pitch.onValueSaved.AddListener(() => { KickStart.LockPropPitch = lockP_Pitch.SavedValue; });
                lockP_Roll = new OptionToggle("Lock Propellers Roll Steering", RandomControls, KickStart.LockPropRoll);
                lockP_Roll.onValueSaved.AddListener(() => { KickStart.LockPropRoll = lockP_Roll.SavedValue; });
                lockP_Yaw = new OptionToggle("Lock Propellers Yaw Steering", RandomControls, KickStart.LockPropYaw);
                lockP_Yaw.onValueSaved.AddListener(() => { KickStart.LockPropYaw = lockP_Yaw.SavedValue; });

                hangarKey = new OptionKey("Hangar Docking Hotkey [+ Left Click]", RandomControls, KickStart.HangarButton);
                hangarKey.onValueSaved.AddListener(() => {
                    KickStart.HangarButton = hangarKey.SavedValue;
                    KickStart._hangarButton = (int)hangarKey.SavedValue;
                });

                var RandomDev = KickStart.ModName + " - Development";
                try
                {
                    KickStartOptionsSafeSaves.TryInitOptionAndConfig(RandomDev, thisModConfig);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup SafeSaves");
                    DebugRandAddi.Log(e);
                }
                allowPopups = new OptionToggle("Enable custom block debug popups", RandomDev, KickStart.DebugPopups);
                allowPopups.onValueSaved.AddListener(() => { KickStart.DebugPopups = allowPopups.SavedValue; });
                blockSnap = new OptionKey("Snapshot Block Hotkey [+ Left Click]", RandomDev, KickStart.SnapBlockButton);
                blockSnap.onValueSaved.AddListener(() => {
                    KickStart.SnapBlockButton = blockSnap.SavedValue;
                    KickStart._snapBlockButton = (int)blockSnap.SavedValue;
                });
                List<string> gamemodeSwitch = new List<string> {
                    "Don't Skip",
                    "Last Save",
                    "Creative",
                };
                if (ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                    gamemodeSwitch.Add("R&D");
                startup = new OptionList<string>("Skip Title Screen", RandomDev, gamemodeSwitch, KickStart.ForceIntoModeStartup);
                startup.onValueSaved.AddListener(() =>
                {
                    KickStart.ForceIntoModeStartup = startup.SavedValue;
                });
                allowQuitFromIngameMenu = new OptionToggle("Quit to Desktop Ingame", RandomDev, KickStart.AllowIngameQuitToDesktop);
                allowQuitFromIngameMenu.onValueSaved.AddListener(() =>
                {
                    KickStart.AllowIngameQuitToDesktop = allowQuitFromIngameMenu.SavedValue;
                    IngameQuit.SetExitButtonIngamePauseMenu(allowQuitFromIngameMenu.SavedValue);
                });

                var TonyRails = KickStart.ModName + " - Tony Rails";
                RailRenderRange = new OptionRange("Rail Render Range", TonyRails, ManRails.MaxRailLoadRange, 250, 750, 50);
                RailRenderRange.onValueSaved.AddListener(() =>
                {
                    ManRails.MaxRailLoadRange = RailRenderRange.SavedValue;
                    ManRails.MaxRailLoadRangeSqr = ManRails.MaxRailLoadRange * ManRails.MaxRailLoadRange;
                });
                RailPathingUpdateSpeed = new OptionRange("Train Pathing Speed", TonyRails, ManTrainPathing.QueueStepRepeatTimes, 1, 6, 1);
                RailPathingUpdateSpeed.onValueSaved.AddListener(() =>
                {
                    ManTrainPathing.QueueStepRepeatTimes = Mathf.FloorToInt(RailPathingUpdateSpeed.SavedValue);
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

    public class KickStartOptionsSafeSaves
    {
        public static OptionToggle saveExternal;
        public static void TryInitOptionAndConfig(string RandomDev, ModConfig thisModConfig)
        {
            //Initiate the madness
            try
            {
                thisModConfig.BindConfig<SafeSaves.ManSafeSaves>(null, "DisableExternalBackupSaving");
                saveExternal = new OptionToggle("Save Mod Information in External File", RandomDev, SafeSaves.ManSafeSaves.DisableExternalBackupSaving);
                saveExternal.onValueSaved.AddListener(() => { SafeSaves.ManSafeSaves.DisableExternalBackupSaving = saveExternal.SavedValue; });
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup SafeSaves");
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

        public static bool WithinBox(this Vector3 vec, float extents)
        {
            return vec.x >= -extents && vec.x <= extents && vec.y >= -extents && vec.y <= extents && vec.z >= -extents && vec.z <= extents;
        }

        private static FieldInfo blockFlags = typeof(TankBlock).GetField("m_Flags", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void TrySetBlockFlag(this TankBlock block, TankBlock.Flags flag, bool trueState)
        {
            if (block == null)
                throw new NullReferenceException("TrySetBlockFlag was given a NULL block to handle!");
            TankBlock.Flags val = (TankBlock.Flags)blockFlags.GetValue(block);
            if (GetSetFlag(ref val, flag, trueState))
            {
                blockFlags.SetValue(block, val);
                DebugRandAddi.Log("TrySetBlockFlag " + val + ", value: " + trueState);
            }
        }
        public static bool GetSetFlag<T>(ref T val, T flagBit, bool trueState) where T : Enum
        {
            int valM = (int)Enum.Parse(typeof(T), val.ToString());
            int valF = (int)Enum.Parse(typeof(T), flagBit.ToString());
            bool curState = (valM & valF) != 0;
            if (curState != trueState)
            {
                if (curState)
                    valM = valM - valF;
                if (trueState)
                    valM = valM + valF;
                val = (T)Enum.ToObject(typeof(T), valM);
                return true;
            }
            return false;
        }

        public static ManSaveGame.StoredTech GetUnloadedTech(this TrackedVisible TV)
        {
            if (TV.visible != null)
                return null;
            try
            {
                if (Singleton.Manager<ManSaveGame>.inst.CurrentState.m_StoredTiles.TryGetValue(TV.GetWorldPosition().TileCoord, out var val))
                {
                    if (val.m_StoredVisibles.TryGetValue(1, out List<ManSaveGame.StoredVisible> techs))
                    {
                        var techD = techs.Find(x => x.m_ID == TV.ID);
                        if (techD != null && (techD is ManSaveGame.StoredTech tech))
                        {
                            return tech;
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        public static bool SubToLogicReceiverCircuitUpdate(this ExtModule module, Action<Circuits.BlockChargeData> OnRec, bool unsub)
        {
            if (module.block.CircuitNode?.Receiver)
            {
                if (unsub)
                    module.block.CircuitNode?.Receiver.UnSubscribeFromChargeData(null, OnRec, null, null);
                else
                    module.block.CircuitNode?.Receiver.SubscribeToChargeData(null, OnRec, null, null, false);
                return true;
            }
            return false;
        }
        public static bool SubToLogicReceiverFrameUpdate(this ExtModule module, Action<Circuits.BlockChargeData> OnRec, bool unsub)
        {
            if (module.block.CircuitNode?.Receiver)
            {
                if (unsub)
                    module.block.CircuitNode?.Receiver.UnSubscribeFromChargeData(null, null, null, OnRec);
                else
                    module.block.CircuitNode?.Receiver.SubscribeToChargeData(null, null, null, OnRec, false);
                return true;
            }
            return false;
        }


        public static float GetMaxStableForceThisFixedFrame(this Rigidbody rbody)
        {
            return Mathf.Pow(rbody.mass, 1.4f) / Time.fixedDeltaTime;
        }

        public static Vector3 GetForceDifference(this Rigidbody rbody, Rigidbody rbodyO, Vector3 pointWorldSpace)
        {
            Vector3 veloM = rbody.GetPointVelocity(pointWorldSpace) * rbody.mass;
            Vector3 veloO = rbodyO.GetPointVelocity(pointWorldSpace) * rbodyO.mass;
            return veloM - veloO;
        }
        /// <summary>
        /// UNFINISHED
        /// </summary>
        /// <param name="rbody"></param>
        /// <param name="rbodyO"></param>
        /// <returns></returns>
        public static ForceEqualizer GetForceEqualizer(this Rigidbody rbody, Rigidbody rbodyO)
        {
            return new ForceEqualizer(rbody, rbodyO);
        }
    }
    public class ForceEqualizer
    {
        Rigidbody main;
        Rigidbody other;
        Vector3 mainToOther;
        internal ForceEqualizer(Rigidbody rbody, Rigidbody rbody2)
        {
            main = rbody;
            other = rbody2;
            //mainToOther = rbody.GetForceDifference(rbody2, )
        }
        public static void ApplyForces()
        {

        }
    }

    internal class PatchStartup : MassPatcherRA
    {
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // The NEW crash handler with useful mod-crash-related information

        internal static class ModePatches
        {
            internal static Type target = typeof(Mode);
            /// <summary>
            /// Startup
            /// </summary>
            //[HarmonyPatch(MethodType.Normal, new Type[1] { typeof(Type)})]
            private static bool UpdateMode_Prefix()
            {
                return KickStart.QuickStartGame();
            }
        }
    }
}
