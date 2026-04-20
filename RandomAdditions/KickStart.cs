using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RandomAdditions.Minimap;
using RandomAdditions.PatchBatch;
using RandomAdditions.PhysicsTethers;
using RandomAdditions.RailSystem;
using TerraTech.Network;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling.Memory.Experimental;
using UnityEngine.UI;
using static UnityEngine.UI.CanvasScaler;


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

        public class KickStartRAData : ITinySettings
        {
            public string DirectoryInExtModSettings => "RandomAdditions";
            public string lastMPSaveName;
            public int gameMode = 0;
            public bool failedLastBoot = false;
        }
        internal static KickStartRAData quickData = new KickStartRAData();

        // MOD SUPPORT
        internal static bool isWaterModPresent = false;
        internal static bool isTweakTechPresent = false;
        internal static bool isNuterraSteamPresent = false;
        internal static bool isNoBugReporterPresent = false;

        public static GameObject logMan;

        //public static bool DebugPopups = false;
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
        public static float ModWrenchScale = 1;
        public static bool MandateSeaReplacement = true;
        public static bool MandateLandReplacement = false;
        public static int ForceIntoModeStartup = 0;
        public static bool AllowIngameQuitToDesktop = false;
        public static bool FastestPhysics = false;
        public static bool ColliderDisable2 = false;
        public static bool IDontTrustEpicAtAll = false;

        // CHEATS


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

        public static int FastenerSpeed = 0;

        public static bool hideHov = false;
        public static bool noCircuits = false;
        public static bool smrtCircuits = false;
        public static bool smrtCol = false;
        public static int smrtHov = 0;
        public static bool disableAiming = false;

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
            if (LookForMod("NoBugReporter"))
            {
                DebugRandAddi.Log("RandomAdditions: Found NoBugReporter!  Holding back on bug report popup..." + 
                    "\n  I do not endorse this but I respect the player's decision to ignore the bug report, " +
                    "just note that mod makers will be reluctant to troubleshoot long MP sessions with the error");
                isNoBugReporterPresent = true;
            }
            return true;
        }

        private static void InsurePatches()
        {
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
                    quickData.TryLoadFromDisk(ref quickData);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on patch");
                    DebugRandAddi.Log(e);
                }
            }
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
                ManMusicEnginesExt.Subscribe();
                DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt - Sub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManMusicEnginesExt");
                DebugRandAddi.Log(e);
            }
            Optimax.PrematureOptimization(FastestPhysics);
#endif

            //Initiate the madness
            InsurePatches();
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
                if (quickData.failedLastBoot)
                {
                }
                else if (ForceIntoModeStartup > 0)
                {
                    // FORCE STARTUP SWITCH
                    DebugRandAddi.Log("RandomAdditions: Prepping Quick Start...");
                    harmonyInstance.MassPatchAllWithin(typeof(PatchStartup), ModName);
                    Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(false);
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on Prepping Quick Start: " + e);
            }
            if (!OfficialEarlyInited)
            {
                ManModChunks.RenewOldChunks();
            }
#if DEBUG
            PrepExternalChunksAndScenery();
#endif
            OfficialEarlyInited = true;
        }

        private static bool BypassStartupSkip = false;
        private static void DoBypassStartupSkip()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
            BypassStartupSkip = true;
            ManModGUI.RemoveEscapeableCallback(DoBypassStartupSkip, true);
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
                ManMusicEnginesExt.Subscribe();
                DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt - Sub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManMusicEnginesExt");
                DebugRandAddi.Log(e);
            }
#else
            DebugRandAddi.Log("RandomAdditions: Startup was invoked by TTSMM!  Set-up to handle LATE initialization");
            OfficialEarlyInit();
#endif

            //Initiate the madness
            InsurePatches();
            CursorChanger.AddNewCursors();
            GlobalClock.ClockManager.Initiate();
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            ManTethers.Initiate();
            ModHelpers.Initiate();
            GUIClock.Initiate();
            ManTileLoader.Initiate();
            ManPhysicsExt.InsureInit();
            ManRails.Initiate();
            RandAddDebugGUI.Initiate();
            ModHelpers.ClickEventSimple.Subscribe(ExtModuleClickable.OnClick);

            // Net hooks
            ModuleHangar.InsureNetHooks();
            ModuleModeSwitch.InsureNetHooks();
            ManRails.InsureNetHooks();

            // etc
            IngameQuit.Initiate();
            ManIngameWiki.RecurseCheckWikiBlockExtModule<ModuleReinforced>();
            ManIngameWiki.RecurseCheckWikiBlockExtModule<SFXAddition>();
            ResourcesHelper.ModsPreLoadEvent.Subscribe(ReplaceManager.RemoveAllBlocks);
            RandAddiWiki.InitWiki();
            RandAddiExtendWiki.InitWiki();


            // Doesn't work, TT is too spagetti coded
            /*
            if (smrtCol)
                TechColliderIgnorer.Init();
            */
            if (smrtHov > 0)
                HoverOpti.Init();

            if (quickData.failedLastBoot)
            {
                DebugRandAddi.Log("RandomAdditions: We failed on last startup, so we skipped loading this time");
                ManModGUI.ShowErrorPopup("The game failed to boot last time.  Skip Title Screen is disabled until you press \"Fix\"", true,
                    () =>
                    {
                        quickData.failedLastBoot = false;
                        quickData.TrySaveToDisk();
                    });
            }
            else if (ForceIntoModeStartup > 0)
            {
                DebugRandAddi.Log("RandomAdditions: Blocking attract loading...(2)");
                ManModGUI.AddEscapeableCallback(DoBypassStartupSkip, true);
                Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(false);
                doQuickstart = true;
            }
            ManGameMode.inst.ModeStartEvent.Subscribe(OnModeStart);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(OnModeEnd);

            ManModChunks.enabled = true;
            ManModScenery.enabled = true;



#if DEBUG
            var tableCached = SpawnHelper.GetAllTerrainObjectPrefabList();
            DebugRandAddi.Log("Getting scenery...");
            foreach (var kvp in tableCached)
            {
                DebugRandAddi.Log("- " + kvp.Key + ": " + (kvp.Value.name.NullOrEmpty() ? "<NULL>" : kvp.Value.name));
            }
            DebugExtUtilities.AllowEnableDebugGUIMenu_KeypadEnter = true;
            //PrintDataBase();
#endif
            CheckShouldDisableEOS();
            //ResourcesHelper.PostBlocksLoadEvent.Subscribe(ManSFXExtRand.GetAllSoundsRegistered);
            // crash testers
            //ResourcesHelper.SerialGO goCrash = new ResourcesHelper.SerialGO(null, null);
            //object blockCRasher = null;
            //blockCRasher.GetType();

            //ResourcesHelper.BlocksPostChangeEvent.Subscribe(GiveMeTheStrongest);
        }
        internal static void OnModeStart(Mode mode)
        {
#if DEBUG
            
            //Optimax.SetActive(true);
            //ComponentPool.GeneratePoolingAnalysisTool();//!!!!!!!!!!!!!!!!!!!!!!!!!
            //InvokeHelper.InvokeSingleRepeat(CheckTheCalls, 0.01f);
            //ManFTUE.inst.StartShowSequence();
            //ManRandDScript.inst.ActiveScriptObject = null;
#endif
        }
        private static void CheckTheCalls()
        {
            /*
            if (Patches.UpdateConnexionLinksCalls > 0)
            {
                DebugRandAddi.Log("UpdateConnexionLinksCalls count " + Patches.UpdateConnexionLinksCalls);
                Patches.UpdateConnexionLinksCalls = 0;
            }//*/
        }
        internal static void OnModeEnd(Mode mode)
        {
        }


        private static float DPSGet(Transform value)
        {
            var weapon = value.GetComponent<ModuleWeapon>();
            var weaponI = value.GetComponent<IModuleDamager>();
            if (weapon && weaponI != null)
            {
                float DPS = weaponI.GetHitDamage() * weaponI.GetHitsPerSec();
                return DPS;
            }
            return -1;
        }
        private static float AlphaGet(Transform value)
        {
            var weapon = value.GetComponent<ModuleWeapon>();
            var weaponI = value.GetComponent<IModuleDamager>();
            if (weapon && weaponI != null)
            {
                float DPS = weaponI.GetHitDamage();
                return DPS;
            }
            return -1;
        }
#if DEBUG
        private static void GiveMeTheStrongest()
        {
            var allPrefabs = (Dictionary<int, Transform>)AccessTools.Field(typeof(ManSpawn), "m_BlockPrefabs").GetValue(ManSpawn.inst);

            DebugRandAddi.Log("Best DPS:");
            foreach (var caser in allPrefabs.Where(x => x.Value?.GetComponent<ModuleWeapon>() != null).OrderByDescending(x =>
            {
                return DPSGet(x.Value);
            }))
                DebugRandAddi.Log(caser.Value.name + ": " + DPSGet(caser.Value).ToString("0.000"));
            DebugRandAddi.Log("\nBest Alpha:");
            foreach (var caser in allPrefabs.Where(x => x.Value?.GetComponent<ModuleWeapon>() != null).OrderByDescending(x =>
            {
                return AlphaGet(x.Value);
            }))
                DebugRandAddi.Log(caser.Value.name + ": " + AlphaGet(caser.Value).ToString("0.000"));
        }
#endif

        public static void PrepExternalChunksAndScenery(bool reload = false)
        {
            try
            {
                ManModChunks.SanityCheck();
                ManModChunks.PrepareAllChunks(reload);
            }
            catch (Exception e)
            {
                DebugRandAddi.FatalError("Failed to launch " + nameof(ManModChunks) + " - " + e);
                ManModChunks.enabled = false;
            }
            try
            {
                ManModScenery.SanityCheck();
                ManModScenery.PrepareAllScenery(reload);
            }
            catch (Exception e)
            {
                DebugRandAddi.FatalError("Failed to launch " + nameof(ManModScenery) + " - " + e);
                ManModScenery.enabled = false;
            }
        }

#if DEBUG
        public static void PrintSoundDataBase()
        {
            var m_ParamDatabase = typeof(FMODEventInstance).GetField("m_ParamDatabase", BindingFlags.Static | BindingFlags.NonPublic);
            var database = (Dictionary<string, Dictionary<string, int>>)m_ParamDatabase.GetValue(null);
            DebugRandAddi.Log("FMODEventInstance");
            foreach (var item in database)
            {
                DebugRandAddi.Log(" " + item.Key);
                foreach (var item2 in item.Value)
                {
                    DebugRandAddi.Log(" - " + item2.Key + ", [" + item2.Value + "]");
                }
            }
        }
#endif

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
                ManMusicEnginesExt.UnSub();
                DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt - UnSub");
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Error on ManMusicEnginesExt");
                DebugRandAddi.Log(e);
            }
            ModHelpers.ClickEventSimple.Unsubscribe(ExtModuleClickable.OnClick);
            ManGameMode.inst.ModeFinishedEvent.Unsubscribe(OnModeEnd);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(OnModeStart);

            if (smrtHov > 0)
                HoverOpti.DeInit();
            // Doesn't work, TT is too spagetti coded
            /*
            if (smrtCol)
                TechColliderIgnorer.DeInit();
            */
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
            ResourcesHelper.ModsPreLoadEvent.Unsubscribe(ReplaceManager.RemoveAllBlocks);
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
                ManModChunks.inst.PrepareForSaving();
                ManModChunks.inst.PrepareForSaving();
                ManModScenery.inst.PrepareForSaving();
                ManTileLoader.OnWorldSave();
                ManRails.PrepareForSaving();
                RandomWorld.PrepareForSaving();
                EmergPatches.PrepareForSaving();
                bool PRUNED = false;
                while (ManTechs.inst.IterateTechsWhere(x => x.blockman.blockCount == 0).FirstOrDefault() != null)
                {
                    ManTechs.inst.IterateTechsWhere(x => x.blockman.blockCount == 0).FirstOrDefault().blockman.RecycleAll();
                    PRUNED = true;
                }
                if (PRUNED)
                    ManModGUI.ShowErrorPopup("RandomAdditions - WARNING: Techs with no blocks detected\n" +
                        "and removed in the scene when saving.  This is fixed automatically");
            }
            else
            {
                EmergPatches.FinishedSaving();
                ManRails.FinishedSaving();
                ManTileLoader.OnWorldFinishSave();
                ManModChunks.inst.FinishedSaving();
                ManModScenery.inst.FinishedSaving();
                RandomWorld.FinishedSaving();
                string prevName = quickData.lastMPSaveName;
                int prevMode = quickData.gameMode;
                if (ManNetwork.IsNetworked)
                {
                    quickData.lastMPSaveName = ManSaveGame.inst.GetCurrentSaveName(false);
                    quickData.gameMode = (int)ManGameMode.inst.GetCurrentGameType();
                }
                else
                {
                    quickData.lastMPSaveName = null;
                    quickData.gameMode = 0;
                }
                if (prevName != quickData.lastMPSaveName || prevMode != quickData.gameMode)
                {
                    if (!quickData.TrySaveToDisk())
                        DebugRandAddi.Assert("Failed to save RandomAdditions quickLoad data to disk.");
                }
            }
        }
        public static void OnLoadManagers(bool Doing)
        {
            if (Doing)
            {
                ManModChunks.inst.PrepareForLoading();
                ManModScenery.inst.PrepareForLoading();
                EmergPatches.PrepareForLoading();
            }
            else
            {
                EmergPatches.FinishedLoading();
                ManTileLoader.OnWorldLoad();
                ManRails.FinishedLoading();
                ManModChunks.inst.FinishedLoading();
                ManModScenery.inst.FinishedLoading();
                RandomWorld.FinishedLoading();
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

        internal static void OpenInExplorer(string directory)
        {
            switch (SystemInfo.operatingSystemFamily)
            {
                case OperatingSystemFamily.MacOSX:
                    Process.Start(new ProcessStartInfo("file://" + directory));
                    break;
                case OperatingSystemFamily.Linux:
                case OperatingSystemFamily.Windows:
                    Process.Start(new ProcessStartInfo("explorer.exe", directory));
                    break;
                default:
                    throw new Exception("This operating system is UNSUPPORTED by RandomAdditions");
            }
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
        public static bool didQuickstart = false;
        private static bool doQuickstart = false;
        public static bool ShouldHoldOffWeAreQuickStarting() => doQuickstart;
        public static bool QuickStartGame()
        {
            if (didQuickstart)
                return true;
            didQuickstart = true;
            if (!doQuickstart)
                return true;
#if DEBUG
            DebugExtUtilities.AllowEnableDebugGUIMenu_KeypadEnter = true;
#endif
            bool vanillaStartup = true;
            bool LoadSPSaveOrNewGame = false;
            try
            {
                if (ForceIntoModeStartup > 0)
                {
                    ManModGUI.RemoveEscapeableCallback(DoBypassStartupSkip, true);
                    if (Input.GetKey(KeyCode.Backspace) || Input.GetKey(KeyCode.Escape) || BypassStartupSkip || Environment.CommandLine.Contains("NoQuickStart"))
                    {
                        // We boot vanilla
                    }
                    else
                    {
                        InvokeHelper.Invoke(PostQuickstart, 2f);
                        ManProfile.Profile prof = ManProfile.inst.GetCurrentUser();
                        if (prof != null)
                        {
                            DebugRandAddi.Log("RandomAdditions: Quick-Starting...");
                            Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(false);
                            ManGameMode.GameType GT = ManGameMode.GameType.Creative;
                            string saveName = null;
                            switch (ForceIntoModeStartup)
                            {
                                case 1:
                                    if (prof.m_LastUsedSaveName != null)
                                    {
                                        if (prof.m_LastUsedSaveType == ManGameMode.GameType.RaD && !ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                                            break;
                                        saveName = prof.m_LastUsedSaveName;
                                        GT = prof.m_LastUsedSaveType;
                                        LoadSPSaveOrNewGame = true;
                                        vanillaStartup = false;
                                    }
                                    else
                                    {
                                        DebugRandAddi.Log("RandomAdditions: Last used save not found.  Aborting...");
                                    }
                                    break;
                                case 2:
                                    GT = ManGameMode.GameType.Creative;
                                    LoadSPSaveOrNewGame = true;
                                    vanillaStartup = false;
                                    break;
                                case 3:
                                    if (!ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                                        break;
                                    GT = ManGameMode.GameType.RaD;
                                    LoadSPSaveOrNewGame = true;
                                    vanillaStartup = false;
                                    break;
                                case 4:
                                    if (quickData.lastMPSaveName != null)
                                    {
                                        TryMakeMPSaveLobby((ManGameMode.GameType)quickData.gameMode, quickData.lastMPSaveName);
                                        vanillaStartup = false;
                                    }
                                    else if (prof.m_LastUsedSaveName != null)
                                    {
                                        if (prof.m_LastUsedSaveType == ManGameMode.GameType.RaD && !ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                                            break;
                                        saveName = prof.m_LastUsedSaveName;
                                        GT = prof.m_LastUsedSaveType;
                                        LoadSPSaveOrNewGame = true;
                                        vanillaStartup = false;
                                    }
                                    else
                                    {
                                        DebugRandAddi.Log("RandomAdditions: Last used save not found.  Aborting...");
                                    }
                                    break;
                                default:
                                    break;
                            }
                            if (LoadSPSaveOrNewGame)
                            {
                                var targFile = ManSaveGame.CreateGameSaveFilePath(GT, prof.m_LastUsedSaveName);

                                if (File.Exists(targFile))
                                {
                                    DebugRandAddi.Log("RandomAdditions: Next mode, " + GT.ToString());
                                    ManUI.inst.ExitAllScreens();
                                    ManGameMode.inst.ClearModeInitSettings();
                                    if (saveName.NullOrEmpty())
                                        ManGameMode.inst.SetupModeSwitchAction(ManGameMode.inst.NextModeSetting, GT);
                                    else
                                        ManGameMode.inst.SetupSaveGameToLoad(GT, prof.m_LastUsedSaveName, prof.m_LastUsedSave_WorldGenVersionData);
                                    ManGameMode.inst.NextModeSetting.SwitchToMode();
                                    ManGameMode.inst.ModeStartEvent.Subscribe(OnFinishedQuickstart);
                                    //ManUI.inst.FadeToBlack(0.25f, false);
                                    DebugRandAddi.Log("RandomAdditions: Success on QuickStartGame");
                                }
                                else
                                {
                                    ManModGUI.ShowErrorPopup("RandomAdditions: Failed to find save file \"" + 
                                        prof.m_LastUsedSaveName + "\", was it deleted or misplaced?");
                                }
                            }
                        }
                        else
                            DebugRandAddi.Log("RandomAdditions: QuickStartGame failed!  User profile is not set!  Cannot Execute!");
                    }
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Failed in quickstarting " + e);
                quickData.failedLastBoot = true;
                quickData.TrySaveToDisk();
            }
            if (vanillaStartup)
            {
                ManGameMode.inst.ModeStartEvent.Subscribe(OnFinishedQuickstart);
                Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(true);
            }
            return vanillaStartup;
        }
        private static void PostQuickstart()
        {
            doQuickstart = false;
        }
        private static void OnJoinLobby(Lobby lobby)
        {
            ManNetworkLobby.inst.LobbySystem.TriggerGameStart();
            ManNetworkLobby.inst.LobbySystem.LobbyJoinedEvent.Unsubscribe(OnJoinLobby);
            ManNetworkLobby.inst.LobbySystem.LobbyCreateFailedEvent.Unsubscribe(OnLobbyFail);
        }
        private static bool triedWithCrappyEOS = false;
        private static void OnLobbyFail(LobbySystem.LobbyErrorCode lobbyError)
        {
            ManNetworkLobby.inst.LobbySystem.LobbyJoinedEvent.Unsubscribe(OnJoinLobby);
            ManNetworkLobby.inst.LobbySystem.LobbyCreateFailedEvent.Unsubscribe(OnLobbyFail);
            DebugRandAddi.Assert("FAILED on startup boot to lobby - " + lobbyError.ToString());
            ManUI.inst.ClearFade(0.25f);
            Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(true);
            if (!triedWithCrappyEOS && lobbyError == LobbySystem.LobbyErrorCode.FailedToConnect)
            {
                DebugRandAddi.Log("SCREW CRAPPY EOS - Bypassing BY FORCE.  Epic DOES NOT SUPPORT MODS ANYWAYS");
                triedWithCrappyEOS = true;
                ManEOS.inst.SetEOSCrossplayRequested(false);
                TryMakeMPSaveLobby((ManGameMode.GameType)quickData.gameMode, quickData.lastMPSaveName);
            }
        }


        private static MethodInfo stopIt = typeof(ManEOS).GetMethod("SetOfflineMode", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void CheckShouldDisableEOS()
        {
            if (IDontTrustEpicAtAll && !ManEOS.inst.IsCrossplayRequestedActive)
            {
                if (SKU.IsSteam)
                {
                    DebugRandAddi.Log("RandomAdditions: EpicOnlineServices attempted to start up and continue tracking even after crossplay is disabled ON STEAM.  The attempt was f*broning stopped.");
                    stopIt.Invoke(ManEOS.inst, new object[] { true });
                }
                else
                {
                    DebugRandAddi.Log("RandomAdditions: Uhh the mod was launched on a non-Steam platform (This Steam mod was launched on a non-Steam client!?!).  We will let EpicOnlineServices continue working because it is needed.");
                }
            }
        }

        private static void TryMakeMPSaveLobby(ManGameMode.GameType mode, string saveName)
        {
            Singleton.Manager<ManUI>.inst.ExitAllScreens();
            ManSaveGame.LoadSaveDataInfoAsync(mode, saveName, (ManSaveGame.SaveInfo info) =>
            {
                if (info == null)
                    return;
                ManGameMode.inst.SetupSaveGameToLoad(info.m_GameType, info.m_SaveName, info.WorldGenVersion);
                ManNetwork.inst.WorldSeed = info.m_WorldSeed;
                ManNetwork.inst.BiomeChoice = info.m_BiomeChoice;
                ManNetwork.inst.SetPiecePlacements = null;
                ManNetwork.inst.WorldGenVersionID = info.m_WorldGenVersionID;
                ManNetwork.inst.WorldGenVersionType = info.m_WorldGenVersioningType;
                ManNetworkLobby.inst.LobbySystem.LobbyJoinedEvent.Subscribe(OnJoinLobby);
                ManNetworkLobby.inst.LobbySystem.LobbyCreateFailedEvent.Subscribe(OnLobbyFail);
                switch (info.m_GameType)
                {
                    case ManGameMode.GameType.CoOpCreative:
                        ManNetworkLobby.inst.LobbySystem.CreateLobby(MultiplayerModeType.CoOpCreative, info.m_LobbyVisibility);
                        break;
                    case ManGameMode.GameType.CoOpCampaign:
                        ManNetworkLobby.inst.LobbySystem.CreateLobby(MultiplayerModeType.CoOpCampaign, info.m_LobbyVisibility);
                        break;
                    default:
                        ManNetworkLobby.inst.LobbySystem.CreateLobby(MultiplayerModeType.Deathmatch, info.m_LobbyVisibility);
                        break;
                }
                //ManUI.inst.FadeToBlack(0.25f, false);
            });
        }
        public static void OnFinishedQuickstart(Mode unused)
        {
            ManUI.inst.ClearFade(1);
            harmonyInstance.MassUnPatchAllWithin(typeof(PatchStartup), ModName);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(OnFinishedQuickstart);
            doQuickstart = false;
            if (BugReportPatches.UIScreenBugReportPatches.Crashed)
            {
                quickData.failedLastBoot = true;
                quickData.TrySaveToDisk();
            }
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
                        DebugRandAddi.Info("RandomAdditions: FetchAnimette - fetched " + MAs.Count + " animettes");
                        return MAs.ToArray(); // RARE CALL
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
                            DebugRandAddi.Info("RandomAdditions: FetchAnimette - fetched animette in " + item.name);
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
        public static Nuterra.NativeOptions.OptionToggle allowPopups;
        public static Nuterra.NativeOptions.OptionList<string> startup;
        public static Nuterra.NativeOptions.OptionToggle fastPhysics;
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
                thisModConfig.BindConfig<KickStart>(null, "AllowIngameQuitToDesktop");
                thisModConfig.BindConfig<KickStart>(null, "FastestPhysics");
                thisModConfig.BindConfig<KickStart>(null, "ColliderDisable2");

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
                fastPhysics = new Nuterra.NativeOptions.OptionToggle("Fast Physics (MIGHT BREAK GAME)", RandomDev, KickStart.FastestPhysics);
                fastPhysics.onValueSaved.AddListener(() =>
                {
                    KickStart.FastestPhysics = fastPhysics.SavedValue;
                    Optimax.PrematureOptimization(KickStart.FastestPhysics);
                });
                disColliders = new Nuterra.NativeOptions.OptionToggle("Disable Tech Colliders", RandomDev, KickStart.ColliderDisable2);
                disColliders.onValueSaved.AddListener(() =>
                {
                    KickStart.ColliderDisable2 = disColliders.SavedValue;
                    Optimax.UpdateColliders();
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
                

                var LagSolutions = KickStart.ModName + " - Lag Reduction";
                smartCircuits = new Nuterra.NativeOptions.OptionToggle("Smart Circuits (MIGHT BREAK C&S DESIGNS) [REDUCE BIG LAG]", LagSolutions, KickStart.smrtCircuits);
                smartCircuits.onValueSaved.AddListener(() =>
                {
                    KickStart.smrtCircuits = smartCircuits.SavedValue;
                });
                disableCircuits = new Nuterra.NativeOptions.OptionToggle("Disable Circuits Entirely (DISABLES C&S) [REDUCES HUGE LAG]", LagSolutions, KickStart.noCircuits);
                disableCircuits.onValueSaved.AddListener(() =>
                {
                    KickStart.noCircuits = disableCircuits.SavedValue;
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


                try
                {
                    KickStartOptionsSafeSaves.TryInitOptionAndConfig(RandomDev, thisModConfig);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: Error on Option & Config setup SafeSaves");
                    DebugRandAddi.Log(e);
                }
                allowPopups = new Nuterra.NativeOptions.OptionToggle("Enable custom block debug popups", RandomDev, BlockDebug.DebugPopups);
                allowPopups.onValueSaved.AddListener(() => { BlockDebug.DebugPopups = allowPopups.SavedValue; });
                blockSnap = new Nuterra.NativeOptions.OptionKey("Snapshot Block Hotkey [+ Left Click]", RandomDev, KickStart.SnapBlockButton);
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
                gamemodeSwitch.Add("Last Save & MP");
                startup = new Nuterra.NativeOptions.OptionList<string>("Skip Title Screen", RandomDev, gamemodeSwitch, KickStart.ForceIntoModeStartup);
                startup.onValueSaved.AddListener(() =>
                {
                    KickStart.ForceIntoModeStartup = startup.SavedValue;
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

    internal static class ExtraExtensions
    {
        /// <summary>
        /// CREATES GARBAGE
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="GO"></param>
        /// <returns></returns>
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
            IEnumerable<T> insure = depthTracker.OrderBy(x => x.Key).Select(x => x.Value);
            if (insure.Any())
                return insure.ToArray(); // RARE CALL
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

        public static IEnumerable<T> IterateExtModules<T>(this BlockManager BM) where T : ExtModule
        {
            foreach (var item in BM.IterateBlocks())
            {
                T get = item.GetComponent<T>();
                if (get)
                    yield return get;
            }
        }
        public static IEnumerable<T> IterateChildModules<T>(this BlockManager BM) where T : ExtModule
        {
            foreach (var item in BM.IterateBlocks())
            {
                T[] get = item.GetComponentsInChildren<T>();
                if (get != null)
                {
                    foreach (var child in get)
                    {
                        if (child)
                            yield return child;
                    }
                }
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
            if (val.GetSetFlag(flag, trueState))
            {
                blockFlags.SetValue(block, val);
                DebugRandAddi.Info("TrySetBlockFlag " + val + ", value: " + trueState);
            }
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


        public static bool SubToLogicReceiverCircuitUpdate<T>(this T module, Action<Circuits.BlockChargeData> OnRec, 
            bool unsub, bool collectAPSpecificData) where T : ExtModule
        {
            if (module.block.CircuitNode?.Receiver)
            {
                if (unsub)
                    module.block.CircuitNode?.Receiver.UnSubscribeFromChargeData(null, OnRec, null, null);
                else
                    module.block.CircuitNode?.Receiver.SubscribeToChargeData(null, OnRec, null, null, collectAPSpecificData);
                return true;
            }
            return false;
        }
        public static bool SubToLogicReceiverFrameUpdate<T>(this T module, Action<Circuits.BlockChargeData> OnRec, 
            bool unsub, bool collectAPSpecificData) where T : ExtModule
        {
            if (module.block.CircuitNode?.Receiver)
            {
                if (unsub)
                    module.block.CircuitNode?.Receiver.UnSubscribeFromChargeData(null, null, null, OnRec);
                else
                    module.block.CircuitNode?.Receiver.SubscribeToChargeData(null, null, null, OnRec, collectAPSpecificData);
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
        internal static class ModeAttractPatches
        {
            internal static Type target = typeof(ModeAttract);
            private static bool EnterGenerateTerrain_Prefix()
            {
                return !KickStart.ShouldHoldOffWeAreQuickStarting();
            }
            private static bool UpdateModeImpl_Prefix()
            {
                return !KickStart.ShouldHoldOffWeAreQuickStarting();
            }
            /// <summary>
            /// Startup tech spawn blocker
            /// </summary>
            //[HarmonyPatch(MethodType.Normal, new Type[1] { typeof(Type)})]
            private static bool ExitModeImpl_Prefix()
            {
                return !KickStart.ShouldHoldOffWeAreQuickStarting();
            }
        }
    }
}
