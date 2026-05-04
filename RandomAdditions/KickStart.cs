using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RandomAdditions.Minimap;
using RandomAdditions.PatchBatch;
using RandomAdditions.PhysicsTethers;
using RandomAdditions.RailSystem;
using TerraTech.Network;
using TerraTechETCUtil;
using UnityEngine;


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
        public static bool OcculsionCulling = false;
        public static bool IDontTrustEpicAtAll = false;

        internal static bool OcculsionCullingInit = false;
        public static bool TrySaveMyTechs = false;

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

        internal static float WaterHeight
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
        private static bool VALIDATE_MODS()
        {
            isWaterModPresent = false;
            isTweakTechPresent = false;
            isNuterraSteamPresent = false;

            isSteamManaged = LookForMod("NLogManager");

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
        private static void InitOptionConfig()
        {
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
        internal static void OfficialEarlyInit()
        {
            //Where the fun begins
            DebugRandAddi.Log("RandomAdditions: OfficialEarlyInit");
#if STEAM
            if (!VALIDATE_MODS())
            {
                return;
            }

            gamemodeSwitch = new List<string> {
                    "Don't Skip",
                    "Last Save",
                    "Creative",
                };
            if (ManDLC.inst.HasAnyDLCOfType(ManDLC.DLCType.RandD))
                gamemodeSwitch.Add("R&D");
            gamemodeSwitch.Add("Last Save & MP");

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
                if (ModStatusChecker.IsNativeOptionsPresent && ModStatusChecker.IsConfigHelperPresent)
                    InitOptionConfig();
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
            OfficialEarlyInited = true;
        }


        private static bool IsNowQuickStarting = false;
        internal static List<string> gamemodeSwitch = new List<string>();
        private static DebugGUIQuickStart quickStartLabelTemp = null;
        internal class DebugGUIQuickStart : MonoBehaviour
        {
            public void OnGUI()
            {
                AltUI.StartUI();
                try
                {
                    GUILayout.Label("Quickstarting with setting " + gamemodeSwitch[ForceIntoModeStartup] + "...", AltUI.LabelWhiteTitle);
                    if (!IsNowQuickStarting)
                        GUILayout.Label("Press [ESC] to cancel", AltUI.LabelGoldTitle);
                }
                finally
                {
                    AltUI.EndUI();
                }
            }
        }
        private static void SetQuickStartPopup(bool exist)
        {
            if (exist)
            {
                if (quickStartLabelTemp == null)
                {
                    IsNowQuickStarting = false;
                    quickStartLabelTemp = new GameObject("TempQuickstart").AddComponent<DebugGUIQuickStart>();
                }
            }
            else
            {
                if (quickStartLabelTemp != null)
                {
                    UnityEngine.Object.Destroy(quickStartLabelTemp.gameObject);
                    quickStartLabelTemp = null;
                }
            }
        }

        private static bool BypassStartupSkip = false;
        private static void DoBypassStartupSkip()
        {
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
            BypassStartupSkip = true;
            SetQuickStartPopup(false);
            ManModGUI.RemoveEscapeableCallback(DoBypassStartupSkip, true);
        }
        internal static void MainOfficialInit()
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
            ModuleLudicrousSpeedButton.Initiate();
            ManModeSwitch.Initiate();
            ModHelpers.Initiate();
            GUIClock.Initiate();
            ManTileLoader.Initiate();
            ManPhysicsExt.InsureInit();
            ManRails.Initiate();
            RandAddDebugGUI.Initiate();
            MinimapExtRandi.InitThis();
            ResourcesHelper.ModsUpdateEvent.Subscribe(UpdateManaged);
            ManTechs.inst.PlayerTankChangedEvent.Subscribe(PlayerTechUpdate);

            // Net hooks
            ModuleHangar.InsureNetHooks();
            ModuleModeSwitch.InsureNetHooks();
            ManRails.InsureNetHooks();

            // etc
            IngameQuit.Initiate();
            ManIngameWiki.RecurseCheckWikiBlockExtModule<ModuleReinforced>();
            ManIngameWiki.RecurseCheckWikiBlockExtModule<SFXAddition>();
            ResourcesHelper.ModsPreLoadEvent.Subscribe(ReplaceManager.RemoveAllBlocks);
            ManIngameWiki.OnWikiOpened.Subscribe(InsureWikiIsUpToDate);
            RandAddiWiki.InitWiki();
            RandAddiExtendWiki.InitWiki();
            SlowUpdateEvent.Subscribe(Patches.GrabUIEntryDetails.SlowUpdateThis);


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
                SetQuickStartPopup(true);
                ManModGUI.AddEscapeableCallback(DoBypassStartupSkip, true);
                Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(false);
                doQuickstart = true;
            }
            ManGameMode.inst.ModeStartEvent.Subscribe(OnModeStart);
            ManGameMode.inst.ModeFinishedEvent.Subscribe(OnModeEnd);

#if DEBUG
            InvokeHelper.InvokeSingleRepeat(GraphicsPhysicsCulling.UpdateCulling, 0f);
            /*
            var tableCached = SpawnHelper.GetAllTerrainObjectPrefabList();
            DebugRandAddi.Log("Getting scenery...");
            foreach (var kvp in tableCached)
            {
                DebugRandAddi.Log("- " + kvp.Key + ": " + (kvp.Value.name.NullOrEmpty() ? "<NULL>" : kvp.Value.name));
            }//*/
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
        internal static void InsureWikiIsUpToDate()
        {
            foreach (var item in ResourcesHelper.GetAllMods())
                if (item.Value?.Contents?.m_Blocks != null && item.Value.Contents.m_Blocks.Any())
                    ManIngameWiki.InsureWiki(item.Key);
        }
        internal static void OnModeStart(Mode mode)
        {
#if DEBUG
            //DebugRandAddi.LogAll = true;
            //Debug_TTExt.LogAll = true;
            //ExplosionHelper.SpawnExplosionByStrength(ExplosionHelper.Type.Oil, Vector3.zero, true, 3000, true);

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


#if DEBUG
        internal static void PrintSoundDataBase()
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
        internal static void DeInitALL()
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
            ManTechs.inst.PlayerTankChangedEvent.Unsubscribe(PlayerTechUpdate);
            ResourcesHelper.ModsUpdateEvent.Unsubscribe(UpdateManaged);
            ManGameMode.inst.ModeFinishedEvent.Unsubscribe(OnModeEnd);
            ManGameMode.inst.ModeStartEvent.Unsubscribe(OnModeStart);
            SlowUpdateEvent.Unsubscribe(Patches.GrabUIEntryDetails.SlowUpdateThis);
            MinimapExtRandi.DeInitThis();

            if (smrtHov > 0)
                HoverOpti.DeInit();
            // Doesn't work, TT is too spagetti coded
            /*
            if (smrtCol)
                TechColliderIgnorer.DeInit();
            */
            RandAddDebugGUI.DeInit();
            ManRadio.DeInit();
            //CircuitExt.Unload();
            ManMinimapExt.DeInitAll();
            ManRails.DeInit();
            ManTileLoader.DeInit();
            GUIClock.DeInit();
            ManModeSwitch.DeInit();
            ManTethers.DeInit();
            ReplaceManager.RemoveAllBlocks();
            ManIngameWiki.OnWikiOpened.Unsubscribe(InsureWikiIsUpToDate);
            ResourcesHelper.ModsPreLoadEvent.Unsubscribe(ReplaceManager.RemoveAllBlocks);

            //if (SKU.UsesEOS)
            //    throw new Exception("Uses EOS is active");
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

        public static void PlayerTechUpdate(Tank tank, bool state)
        {
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item)
                    RandomTank.Insure(item).ReevaluateLoadingDiameter();
            }
        }

        public static EventNoParams SlowUpdateEvent = new EventNoParams();

        private const float SlowUpdateTime = 0.6f;
        private static float SlowUpdate = 0;
        private static void UpdateManaged()
        {
            try
            {
                TankBlockScaler.UpdateAll();
            }
            catch (Exception e)
            {
                DebugRandAddi.LogError("Exception on updating " + nameof(TankBlockScaler) + " - " + e);
            }
            if (SlowUpdate < Time.time)
            {
                SlowUpdate = Time.time + SlowUpdateTime;
                try
                {
                    SlowUpdateEvent.Send();
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("Exception on updating " + nameof(SlowUpdateEvent) + " - " + e);
                }
            }
            if (Input.GetKeyDown(KeyCode.F11))
                Optimax.SetActive(!Optimax.State);
        }

        private static void OnSaveManagers(bool Doing)
        {
            if (Doing)
            {
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
        private static void OnLoadManagers(bool Doing)
        {
            if (Doing)
            {
                EmergPatches.PrepareForLoading();
            }
            else
            {
                EmergPatches.FinishedLoading();
                ManTileLoader.OnWorldLoad();
                ManRails.FinishedLoading();
                RandomWorld.FinishedLoading();
            }
        }

        internal static bool LookForMod(string name) => ModStatusChecker.LookForMod(name);

        internal static Type LookForType(string name) => ModStatusChecker.LookForType(name);
        internal static Transform HeavyTransformSearch(Transform trans, string name) 
        {
            if (name.NullOrEmpty())
                return null;
            foreach (var cand in trans.gameObject.GetComponentsInChildren<Transform>())
            {
                if (!cand.name.NullOrEmpty() && cand.name.CompareTo(name) == 0)
                    return cand;
            }
            return null;
        }
        internal static Transform HeavyTransformSearchGARBAGE(Transform trans, string name) =>
            Utilities.HeavyTransformSearch(trans, name);

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
        internal static bool ShouldHoldOffWeAreQuickStarting() => doQuickstart;
        internal static bool QuickStartGame()
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
                    IsNowQuickStarting = true;
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
                                        SetQuickStartPopup(false);
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
                                        SetQuickStartPopup(false);
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
                        {
                            DebugRandAddi.Log("RandomAdditions: QuickStartGame failed!  User profile is not set!  Cannot Execute!");
                            SetQuickStartPopup(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: Failed in quickstarting " + e);
                quickData.failedLastBoot = true;
                quickData.TrySaveToDisk();
                SetQuickStartPopup(false);
            }
            if (vanillaStartup)
            {
                ManGameMode.inst.ModeStartEvent.Subscribe(OnFinishedQuickstart);
                Mode<ModeAttract>.inst.SetCanStartNewAttractModeSequence(true);
                SetQuickStartPopup(false);
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
                SetQuickStartPopup(false);
                //ManUI.inst.FadeToBlack(0.25f, false);
            });
        }
        private static void OnFinishedQuickstart(Mode unused)
        {
            ManUI.inst.ClearFade(1);
            harmonyInstance.MassUnPatchAllWithin(typeof(PatchStartup), ModName);
            SetQuickStartPopup(false);
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
