using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using FMODUnity;
using TerraTechETCUtil;
using FMOD;

namespace RandomAdditions
{
    internal class ManMusicEnginesExt : MonoBehaviour
    {

        private const float EngineSFXDistance = 50;

        public static Dictionary<int, CorpExtAudio> corps;


        internal void RefreshModCorpAudio_Queued(Mode mode)
        {
            RefreshModCorpAudio();
        }
        public static AudioInst FetchSoundFast(ModContainer MC, string wavNameWithExt)
        {
            if (MC != null && ManAudioExt.AllSounds.TryGetValue(MC, out var value) && value != null && value.TryGetValue(wavNameWithExt, out var value2))
            {
                return value2.main[0];
            }
            return null;
        }
        /// <summary>
        /// Credit to Exund for looking to FMOD!
        /// </summary>
        internal void RefreshModCorpAudio()
        {   //
            sys = RuntimeManager.LowlevelSystem;
            TechExtAudio.ResetAll();
            corps.Clear();
            int count = 0;
            foreach (var item in ResourcesHelper.IterateAllMods())
            {
                ModContainer MC = item.Value;
                if (MC != null)
                {
                    count++;
                    if (MC.Contents?.m_Corps != null)
                    {
                        foreach (var corp in MC.Contents.m_Corps)
                        {
                            int cCorp = (int)ManMods.inst.GetCorpIndex(corp.m_ShortName);
                            if (!corps.ContainsKey(cCorp))
                            {
                                CorpExtAudio newCase = new CorpExtAudio
                                {
                                    ID = cCorp,
                                    combatMusicLoaded = new List<FMOD.Sound>()
                                };
                                string shortName = corp.m_ShortName;
                                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Attempting to register " + shortName + ".");
                                try
                                {
                                    string fileData = ResourcesHelper.FetchTextData(MC, shortName + ".json", MC.AssetBundlePath);

                                    CorpExtAudioJSON ext = JsonConvert.DeserializeObject<CorpExtAudioJSON>(fileData);
                                    if (ext.MusicLoopStartOffset != null)
                                        newCase.MusicLoopStartOffset = ext.MusicLoopStartOffset;
                                    newCase.CorpEngine = ext.CorpEngine;
                                    newCase.EnginePitchDeepMulti = ext.EnginePitchDeepMulti;
                                    newCase.FallbackMusic = ext.CorpFallbackMusic;
                                    newCase.EnginePitchMax = ext.EnginePitchMax;
                                    newCase.EngineIdealSpeed = ext.EngineIdealSpeed;
                                    newCase.EngineVolumeMulti = ext.EngineVolumeMulti;
                                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp json for " + shortName + " (" + cCorp.ToString() + ") loaded correctly");
                                }
                                catch (Exception e)
                                {
                                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on Json search " + cCorp + " | " + e);
                                }
                                FMOD.Sound addSound;
                                if (!newCase.CorpEngineAudioIdle.hasHandle() &&
                                    GetSound2(MC, shortName, "EngineIdle", true, out addSound))
                                    newCase.CorpEngineAudioIdle = addSound;

                                if (!newCase.CorpEngineAudioRunning.hasHandle() &&
                                    GetSound2(MC, shortName, "EngineRun", true, out addSound))
                                    newCase.CorpEngineAudioRunning = addSound;

                                if (!newCase.CorpEngineAudioStart.hasHandle() &&
                                    GetSound2(MC, shortName, "EngineStart", false, out addSound))
                                    newCase.CorpEngineAudioStart = addSound;

                                if (!newCase.CorpEngineAudioStop.hasHandle() &&
                                    GetSound2(MC, shortName, "EngineStop", false, out addSound))
                                    newCase.CorpEngineAudioStop = addSound;
                                DebugRandAddi.LogDevOnly("RandomAdditions: RegisterModCorpAudio - Looking in " + MC.AssetBundlePath);
                                newCase.hasEngineAudio = newCase.CorpEngineAudioIdle.hasHandle() && newCase.CorpEngineAudioRunning.hasHandle() &&
                                    newCase.CorpEngineAudioStart.hasHandle() && newCase.CorpEngineAudioStop.hasHandle();
                                int attempts = 0;
                                while (true)
                                {
                                    try
                                    {
                                        string fileName = shortName + (attempts == 0 ? "" : attempts.ToString()) + ".wav";
                                        var newSound = FetchSoundFast(MC, fileName);
                                        if (newSound != null)
                                        {
                                            if (newCase.MusicLoopStartOffset.Length > attempts)
                                            {
                                                newSound.SoundAdvanced.setLoopCount(-1);
                                                newSound.SoundAdvanced.getLength(out uint pos, FMOD.TIMEUNIT.MS);
                                                uint loopStart = (uint)Mathf.RoundToInt(Mathf.Clamp(newCase.MusicLoopStartOffset[attempts] * 1000f, 0, pos - 2));
                                                newSound.SoundAdvanced.setLoopPoints(loopStart, FMOD.TIMEUNIT.MS, pos - 1, FMOD.TIMEUNIT.MS);
                                                newSound.SoundAdvanced.setMode(FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D);
                                                newCase.combatMusicLoaded.Add(newSound.SoundAdvanced);
                                                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp music for " + shortName + " named " + fileName + " loaded correctly with special loop [" +
                                                    loopStart + " ~ " + (pos - 1) + "]ms");
                                            }
                                            else
                                            {
                                                newSound.SoundAdvanced.setLoopCount(-1);
                                                newSound.SoundAdvanced.getLength(out uint pos, FMOD.TIMEUNIT.MS);
                                                newSound.SoundAdvanced.setLoopPoints(0, FMOD.TIMEUNIT.MS, pos - 1, FMOD.TIMEUNIT.MS);
                                                newSound.SoundAdvanced.setMode(FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D);
                                                newCase.combatMusicLoaded.Add(newSound.SoundAdvanced);
                                                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp music for " + shortName + " named " + fileName + " loaded correctly");
                                            }
                                            attempts++;
                                        }
                                        else
                                            break;
                                    }
                                    catch (Exception e)
                                    {
                                        DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on Music search " + shortName + " | " + e);
                                        break;
                                    }
                                }
                                if (newCase.CorpEngine == FactionSubTypes.NULL)
                                    newCase.CorpEngine = FactionSubTypes.GSO;
                                if (newCase.combatMusicLoaded.Count == 0)
                                {
                                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - No custom corp music built for " + shortName + ".");
                                }
                                corps.Add(cCorp, newCase);
                            }
                        }
                    }
                }
            }
            DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - searched (" + count + ") available mods");

            OnProfileDelta(ManProfile.inst.GetCurrentUser());
            TechExtAudio.CalibrateAll();
        }
        private bool GetSound2(ModContainer MC, string shortName, string searchName, bool looping, out FMOD.Sound newSound)
        {
            try
            {
                var sound = FetchSoundFast(MC, shortName + searchName + ".wav");
                //DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Trying at " + GO);
                if (sound != null)
                {
                    sound.Looped = looping;
                    newSound = sound.SoundAdvanced;
                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp " + searchName +
                        " for " + shortName + " named " + shortName + searchName + ".wav loaded correctly");
                    return true;
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on " + searchName + " search " + shortName + " | " + e);
            }
            newSound = default;
            return false;
        }

        internal void RefreshModCorpAudio_LEGACY() => RegisterModCorpAudio_LEGACY(Assembly.GetExecutingAssembly().Location);
        private void RegisterModCorpAudio_LEGACY(string location)
        {   //
            sys = RuntimeManager.LowlevelSystem;
            location = new DirectoryInfo(location).Parent.Parent.ToString();// Go to the cluster directory
            TechExtAudio.ResetAll();
            corps.Clear();

            DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - searching in " + location);
            foreach (int cCorp in ManMods.inst.GetCustomCorpIDs())
            {
                int attempts = 0;
                CorpExtAudio newCase = new CorpExtAudio { ID = cCorp, combatMusicLoaded = new List<FMOD.Sound>() };
                string shortName = ManMods.inst.FindCorpShortName((FactionSubTypes)cCorp);
                foreach (string directoryLoc in Directory.GetDirectories(location))
                {
                    if (newCase.CorpEngine == FactionSubTypes.NULL)
                    {
                        try
                        {
                            string GO;
                            string fileName = shortName + ".json";
                            GO = directoryLoc + "\\" + fileName;
                            if (File.Exists(GO))
                            {
                                CorpExtAudioJSON ext = JsonConvert.DeserializeObject<CorpExtAudioJSON>(File.ReadAllText(GO));
                                newCase.CorpEngine = ext.CorpEngine;
                                newCase.EnginePitchDeepMulti = ext.EnginePitchDeepMulti;
                                newCase.FallbackMusic = ext.CorpFallbackMusic;
                                newCase.EnginePitchMax = ext.EnginePitchMax;
                                newCase.EngineIdealSpeed = ext.EngineIdealSpeed;
                                newCase.EngineVolumeMulti = ext.EngineVolumeMulti;
                                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp json for " + shortName + " (" + cCorp.ToString() + ") named " + fileName + " loaded correctly");
                            }
                        }
                        catch (Exception e)
                        {
                            DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on Json search " + cCorp + " | " + e);
                        }
                        FMOD.Sound addSound;
                        if (!newCase.CorpEngineAudioIdle.hasHandle() &&
                            GetSound(directoryLoc, shortName, "EngineIdle", true, out addSound))
                            newCase.CorpEngineAudioIdle = addSound;

                        if (!newCase.CorpEngineAudioRunning.hasHandle() &&
                            GetSound(directoryLoc, shortName, "EngineRun", true, out addSound))
                            newCase.CorpEngineAudioRunning = addSound;

                        if (!newCase.CorpEngineAudioStart.hasHandle() &&
                            GetSound(directoryLoc, shortName, "EngineStart", false, out addSound))
                            newCase.CorpEngineAudioStart = addSound;

                        if (!newCase.CorpEngineAudioStop.hasHandle() &&
                            GetSound(directoryLoc, shortName, "EngineStop", false, out addSound))
                            newCase.CorpEngineAudioStop = addSound;

                        newCase.hasEngineAudio = newCase.CorpEngineAudioIdle.hasHandle() && newCase.CorpEngineAudioRunning.hasHandle() &&
                            newCase.CorpEngineAudioStart.hasHandle() && newCase.CorpEngineAudioStop.hasHandle();
                        while (true)
                        {
                            try
                            {
                                string GO;
                                string fileName = shortName + (attempts == 0 ? "" : attempts.ToString()) + ".wav";
                                GO = directoryLoc + "\\" + fileName;
                                //DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Trying at " + GO);
                                if (File.Exists(GO))
                                {
                                    // FMOD.MODE.CREATESTREAM removed because it was stuttering
                                    sys.createSound(GO, FMOD.MODE.CREATESTREAM | FMOD.MODE.LOOP_NORMAL, out FMOD.Sound newSound);
                                    newCase.combatMusicLoaded.Add(newSound);
                                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp music for " + shortName + " named " + fileName + " loaded correctly");
                                    attempts++;
                                }
                                else
                                    break;
                            }
                            catch (Exception e)
                            {
                                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on Music search " + shortName + " | " + e);
                                break;
                            }
                        }
                    }
                }
                if (newCase.CorpEngine == FactionSubTypes.NULL)
                    newCase.CorpEngine = FactionSubTypes.GSO;
                if (newCase.combatMusicLoaded.Count == 0)
                {
                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - No custom corp music built for " + shortName + ".");
                }
                if (!corps.ContainsKey(cCorp))
                    corps.Add(cCorp, newCase);
            }
            OnProfileDelta(ManProfile.inst.GetCurrentUser());
            TechExtAudio.CalibrateAll();
        }
        private bool GetSound(string directoryLoc, string shortName, string searchName, bool looping, out FMOD.Sound newSound)
        {
            try
            {
                string GO;
                string fileName = shortName + searchName + ".wav";
                GO = directoryLoc + "\\" + fileName;
                //DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Trying at " + GO);
                if (File.Exists(GO))
                {
                    if (looping)
                        sys.createSound(GO, FMOD.MODE.CREATESAMPLE | FMOD.MODE.LOOP_NORMAL, out newSound);
                    else
                        sys.createSound(GO, FMOD.MODE.CREATESAMPLE, out newSound);
                    DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - The corp " + searchName + " for " + shortName + " named " + fileName + " loaded correctly");
                    return true;
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("RandomAdditions: RegisterModCorpAudio - Error on " + searchName + " search " + shortName + " | " + e);
            }
            newSound = default;
            return false;
        }
        
        internal void UnRegisterModCorpAudio()
        {
            DebugRandAddi.Log("RandomAdditions: UnRegisterModCorpAudio");
            foreach (var item in TechExtAudio.techA)
            {
                foreach (var item2 in item.engines)
                {
                    item2.Value.EngineStop();
                }
                item.engines.Clear();
            }
            corps = null;
        }

        // AUDIO
        public const float MusicOff = 0.00001f;
        public static bool MuteOGWhenPlayingMod = true;
        private const float fadeINRatePreDelay = 0.14f;//0.04f;//0.02f;
        private const float fadeINRate = 1.2f;
        private const float fadeRate = 0.6f;
        internal static float FadeoutTime = 7f;
        private static float ModCombatMusicFaderPercent = 0; // the music from 0 to one
        internal static float currentMusicVol = 0;        // the player's settings
        internal static float currentSFXVol = 0;        // the player's settings

        public static ManMusicEnginesExt inst = null;
        internal static FMOD.System sys;
        internal FMOD.ChannelGroup CG;
        internal FMOD.Sound MusicCurrent;
        internal FMOD.Channel ActiveMusic;
        internal int enemyBlockCountCurrent = 0;
        internal int enemyID = int.MinValue;
        public FactionSubTypes currentFaction = FactionSubTypes.NULL;
        public static bool isModCorpDangerValid = false;
        public static bool isVanillaCorpDangerValid = false;
        private bool queuedChange = false;
        private float justAssignedTime = 0;
        private static bool GameIsPaused = false;
        private static void Initiate()
        {
            inst = Instantiate(new GameObject("ManMusicEnginesExt")).AddComponent<ManMusicEnginesExt>();
            DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt initated");
            inst.gameObject.SetActive(true);
            //HelperGUI.Init();
        }
        private static void DeInitiate()
        {
            Destroy(inst);
            inst = null;
        }
        public static void Subscribe()
        {
            if (inst)
                return;
            Initiate();
            corps = new Dictionary<int, CorpExtAudio>();
            Singleton.Manager<ManPauseGame>.inst.PauseEvent.Subscribe(inst.OnPause);
            Singleton.Manager<ManProfile>.inst.OnProfileSaved.Subscribe(inst.OnProfileDelta);
            ManAudioExt.OnRebuildSounds.Subscribe(inst.RefreshModCorpAudio);
            ManGameMode.inst.ModeStartEvent.Subscribe(inst.RefreshModCorpAudio_Queued);
            FadeoutTime = Mathf.Max(FadeoutTime, (float)typeof(ManMusic).GetField("m_DangerMusicHoldTime", 
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ManMusic.inst));
        }
        public static void UnSub()
        {
            if (!inst)
                return;
            ForceHalt();
            ManGameMode.inst.ModeStartEvent.Unsubscribe(inst.RefreshModCorpAudio_Queued);
            ManAudioExt.OnRebuildSounds.Unsubscribe(inst.RefreshModCorpAudio);
            Singleton.Manager<ManProfile>.inst.OnProfileSaved.Unsubscribe(inst.OnProfileDelta);
            Singleton.Manager<ManPauseGame>.inst.PauseEvent.Unsubscribe(inst.OnPause);
            TechExtAudio.ResetAll();
            inst.UnRegisterModCorpAudio();
            DeInitiate();
            DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt De-Init");
        }
        public void OnProfileDelta(ManProfile.Profile prof)
        {
            currentMusicVol = prof.m_SoundSettings.m_MusicVolume;
            currentSFXVol = prof.m_SoundSettings.m_SFXVolume;
        }
        private static FieldInfo vca = typeof(ManMusic).GetField("m_MusicVCA", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo vcaSet = typeof(ManMusic).GetMethod("SetVCAFaderLevel", BindingFlags.NonPublic | BindingFlags.Instance);

        private static object vcaInst;
        private static object[] tempSounder = new object[2];
        private static void SetBaseGameVolume(float value)
        {
            if (vcaInst == null)
                vcaInst = vca.GetValue(ManMusic.inst);
            tempSounder[0] = vcaInst;
            tempSounder[1] = value;
            vcaSet.Invoke(ManMusic.inst, tempSounder);
        }
        public void OnPause(bool paused)
        {
            if (paused)
            {
                foreach (var item in TechExtAudio.techA)
                {
                    item.VolumeUpdate(0);
                    foreach (var item2 in item.engines)
                        item2.Value.EnginePause(true);
                }
                ActiveMusic.isPlaying(out bool currentlyPlaying);
                if (currentlyPlaying)
                    ActiveMusic.setPaused(true);
                if (MuteOGWhenPlayingMod)
                    SetBaseGameVolume(currentMusicVol);
            }
            else
            {
                foreach (var item in TechExtAudio.techA)
                {
                    item.VolumeUpdate(currentSFXVol);
                    foreach (var item2 in item.engines)
                        item2.Value.EnginePause(false);
                }
                if (isModCorpDangerValid)
                {
                    ActiveMusic.isPlaying(out bool currentlyPlaying);
                    if (currentlyPlaying)
                    {
                        ActiveMusic.setVolume(currentMusicVol);
                        ActiveMusic.setPaused(false);
                    }
                    if (MuteOGWhenPlayingMod)
                        SetBaseGameVolume(MusicOff);
                }
                else if (MuteOGWhenPlayingMod)
                    SetBaseGameVolume(currentMusicVol);
            }
            GameIsPaused = paused;
        }

        public static void SetDangerContextVanilla()
        {
            isVanillaCorpDangerValid = true;
            inst.justAssignedTime = FadeoutTime;
        }

        public static void SetDangerContext(CorpExtAudio CL, int enemyBlockCount, int enemyVisID)
        {
            inst.SetDangerContextInternal(CL, enemyBlockCount, enemyVisID);
        }
        private void SetDangerContextInternal(CorpExtAudio CL, int enemyBlockCountNew, int enemyVisID)
        {
            FactionSubTypes newFaction = (FactionSubTypes)CL.ID;
            if (justAssignedTime <= 0)
            {   // Set the music
                MusicCurrent = CL.combatMusicLoaded.GetRandomEntry();
                isModCorpDangerValid = true;
                currentFaction = newFaction;
                enemyBlockCountCurrent = enemyBlockCountNew;
                enemyID = enemyVisID;
                ModCombatMusicFaderPercent = 0.01f;
                ForceReboot();
                DebugRandAddi.Info("RandomAdditions: ManMusicEnginesExt Playing danger music for " + CL.ID);
                if (MuteOGWhenPlayingMod)
                    SetBaseGameVolume(MusicOff);
            }

            justAssignedTime = FadeoutTime;
            /*
            if (queuedChange)
            {
                //if (!isModCorpDangerValid && currentFaction != newFaction)
                if (justAssignedTime <= 0 && currentFaction != newFaction)
                {
                    MusicCurrent = CL.combatMusicLoaded.GetRandomEntry();
                    isModCorpDangerValid = true;
                    currentFaction = newFaction;
                    enemyBlockCountCurrent = enemyBlockCountNew;
                    enemyID = enemyVisID;
                    ModCombatMusicFaderPercent = 0.01f;
                    ForceReboot();
                    DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Playing danger music (Transition) for " + CL.ID);
                    if (MuteOGWhenPlayingMod)
                        SetBaseGameVolume(MusicOff);
                    queuedChange = false;
                }
                else if (justAssignedTime > 0)
                    queuedChange = false;
            }
            else if ((justAssignedTime > 0 || enemyBlockCountNew > enemyBlockCountCurrent) && 
                (currentFaction != newFaction || enemyID != enemyVisID))
            {   // Set the music
                if (isModCorpDangerValid)
                {
                    if (!queuedChange)
                    {
                        DebugRandAddi.Info("RandomAdditions: ManMusicEnginesExt Transitioning danger music...");
                        queuedChange = true;
                    }
                }
                else
                {
                    MusicCurrent = CL.combatMusicLoaded.GetRandomEntry();
                    isModCorpDangerValid = true;
                    currentFaction = newFaction;
                    enemyBlockCountCurrent = enemyBlockCountNew;
                    enemyID = enemyVisID;
                    ModCombatMusicFaderPercent = 0.01f;
                    ForceReboot();
                    DebugRandAddi.Info("RandomAdditions: ManMusicEnginesExt Playing danger music for " + CL.ID);
                    if (MuteOGWhenPlayingMod)
                        SetBaseGameVolume(MusicOff);
                }
            }
            if (currentFaction == newFaction && !queuedChange)
            {   // Sustain the music
                //DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Sustaining danger music for " + CL.ID);
                justAssignedTime = FadeoutTime;
            }
            */
        }
        public static void ForceHalt()
        {
            if (!inst)
                return;
            isModCorpDangerValid = false;
            inst.currentFaction = FactionSubTypes.NULL;
            inst.enemyBlockCountCurrent = 0;
            inst.enemyID = -1;
            inst.ActiveMusic.stop();
            DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Stopping danger music");
            if (MuteOGWhenPlayingMod)
                SetBaseGameVolume(ManPauseGame.inst.IsPaused ? MusicOff : currentMusicVol);
        }
        public static void HaltDanger()
        {
            if (!inst)
                return;
            if (isModCorpDangerValid && inst.justAssignedTime <= 0)
            {
                isModCorpDangerValid = false;
                inst.currentFaction = FactionSubTypes.NULL;
                inst.enemyBlockCountCurrent = 0;
                inst.enemyID = -1;
                inst.ActiveMusic.stop();
                DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Stopping danger music");
                ManProfile.Profile Prof = ManProfile.inst.GetCurrentUser();
                if (MuteOGWhenPlayingMod)
                    SetBaseGameVolume(ManPauseGame.inst.IsPaused ? MusicOff : Prof.m_SoundSettings.m_MusicVolume);
            }
        }

        private void StartMusic(FMOD.Sound sound)
        {
            sys.getMasterChannelGroup(out CG);
            if (!GameIsPaused)
            {
                sys.playSound(sound, CG, false, out ActiveMusic);

                ActiveMusic.set3DMinMaxDistance(9001000000f, 90010000000f);
                ActiveMusic.setMode(FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D);
                ActiveMusic.setPosition(0, FMOD.TIMEUNIT.MS);
                ActiveMusic.setVolume(currentMusicVol);
                DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Playing danger music at " + currentMusicVol);
            }
        }

        private static bool initSoundGroup = false;
        internal static FMOD.ChannelGroup soundHost;
        internal static void InsureSFXGroup()
        {
            if (!initSoundGroup)
            {
                sys.createChannelGroup("ExtSFX", out soundHost);
                soundHost.setMode(FMOD.MODE._3D_LINEARROLLOFF);
                soundHost.set3DMinMaxDistance(0, EngineSFXDistance);
                soundHost.set3DLevel(1);
                initSoundGroup = true;
            }
        }
        internal static void StartSound(FMOD.Sound sound, ref FMOD.Channel player, float soundDelta)
        {
            InsureSFXGroup();
            if (!GameIsPaused)
            {
                try
                {
                    sound.set3DMinMaxDistance(0, EngineSFXDistance);
                    sound.setLoopCount(0);

                    sys.playSound(sound, soundHost, true, out player);
                    player.setMode(FMOD.MODE._3D | FMOD.MODE._3D_WORLDRELATIVE | FMOD.MODE._3D_LINEARROLLOFF);
                    player.set3DLevel(1f);
                    player.set3DMinMaxDistance(0, EngineSFXDistance);
                    player.setPosition(0, FMOD.TIMEUNIT.MS);
                    player.setVolume(currentSFXVol * soundDelta);
                    player.setLoopCount(0);
                    player.setPaused(false);
                    //DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Playing audio at " + Prof.m_SoundSettings.m_SFXVolume);
                }
                catch { }
            }
        }
        internal static void StartSoundLooping(FMOD.Sound sound, ref FMOD.Channel player, float soundDelta, bool playNow = true)
        {
            InsureSFXGroup();
            if (!GameIsPaused)
            {
                try
                {
                    sound.set3DMinMaxDistance(0, EngineSFXDistance);
                    sound.setLoopCount(-1);
                    sound.getLength(out uint pos, FMOD.TIMEUNIT.MS);
                    sound.setLoopPoints(1, FMOD.TIMEUNIT.MS, pos - 2, FMOD.TIMEUNIT.MS);

                    sys.playSound(sound, soundHost, true, out player);
                    player.setMode(FMOD.MODE.LOOP_NORMAL | FMOD.MODE._3D | FMOD.MODE._3D_WORLDRELATIVE | FMOD.MODE._3D_LINEARROLLOFF);
                    player.set3DLevel(1f);
                    player.set3DMinMaxDistance(0, EngineSFXDistance);
                    player.setPosition(0, FMOD.TIMEUNIT.MS);
                    player.setVolume(currentSFXVol * soundDelta);
                    player.setLoopCount(-1);
                    if (playNow)
                        player.setPaused(false);
                    //DebugRandAddi.Log("RandomAdditions: ManMusicEnginesExt Playing looped audio at " + Prof.m_SoundSettings.m_SFXVolume);
                }
                catch { }
            }
        }
        private void ForceReboot()
        {
            sys.getMasterChannelGroup(out CG);

            ActiveMusic.stop();
            StartMusic(MusicCurrent);
        }
        internal void Update()
        {
            RunMusic();
            if (inst.justAssignedTime < 1f)
            {
                if (ModCombatMusicFaderPercent > 0)
                    ModCombatMusicFaderPercent -= fadeRate * Time.deltaTime;
            }
            if (justAssignedTime > 0)
                justAssignedTime -= Time.deltaTime;
            else
                isVanillaCorpDangerValid = false;
        }


        private void RunMusic()
        {
            if (isModCorpDangerValid)
            {
                if (justAssignedTime > 0)
                {
                    if (ModCombatMusicFaderPercent < 0.03f)
                        ModCombatMusicFaderPercent += fadeINRatePreDelay;
                    else if (ModCombatMusicFaderPercent < 1)
                        ModCombatMusicFaderPercent += Time.deltaTime * fadeINRate;
                    else
                        ModCombatMusicFaderPercent = 1;
                    /*
                    if (currentVolPercent < 0.03f)
                        currentVolPercent += Time.deltaTime * fadeINRatePreDelay;
                    else if (currentVolPercent < 1)
                        currentVolPercent += Time.deltaTime * fadeINRate;
                    else
                        currentVolPercent = 1;
                    */
                }
                ActiveMusic.isPlaying(out bool currentlyPlaying);
                try
                {
                    if (currentlyPlaying)
                    {
                        ActiveMusic.getPosition(out uint pos, FMOD.TIMEUNIT.MS);
                        if (pos == 1)
                        {
                            ForceReboot();
                        }
                        ActiveMusic.setVolume(ModCombatMusicFaderPercent * currentMusicVol);
                    }
                    else
                    {
                        ForceReboot();
                    }
                }
                catch { }
                if (ModCombatMusicFaderPercent <= 0)
                    HaltDanger();
                return;
            }
            else
            {
                ActiveMusic.stop();
                GameIsPaused = false;
            }
        }



    }
}
