using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using TerraTechETCUtil;
using FMOD;
using UnityEngine;
using Newtonsoft.Json;
using RandomAdditions;
using static ManMap;

/// <summary>
/// Note: Assigning sounds should only happen ONCE - That is the first time it is needed.
///   It should NEVER be unassigned since other blocks WILL be using it!
/// </summary>
public static class ManSFXExtRand
{
    private static bool init = false;
    /// <summary>
    /// Stores every entry that was registered for quick lookup later
    /// </summary>
    private static Dictionary<Type, Dictionary<string, int>> EntrySetLookup =
        new Dictionary<Type, Dictionary<string, int>>();

    private static ManSFX.ExplosionType NextEntryExplode = (ManSFX.ExplosionType)(-1);
    internal static Dictionary<Transform, Dictionary<ManSFX.ExplosionType, ExtSoundGroup>> libExplode =
        new Dictionary<Transform, Dictionary<ManSFX.ExplosionType, ExtSoundGroup>>();

    private static TechBooster.BoosterType NextEntryBooster = (TechBooster.BoosterType)(-1);
    internal static Dictionary<Tank, Dictionary<TechBooster.BoosterType, ExtSoundGroup>> libBoosters =
        new Dictionary<Tank, Dictionary<TechBooster.BoosterType, ExtSoundGroup>>();

    private static TechAudio.SFXType NextEntryTech = (TechAudio.SFXType)(-1);
    internal static Dictionary<Tank, Dictionary<TechAudio.SFXType, ExtSoundGroupMulti>> libTechs =
        new Dictionary<Tank, Dictionary<TechAudio.SFXType, ExtSoundGroupMulti>>();

    private static ManSFX.ProjectileFlightType NextEntryProj = (ManSFX.ProjectileFlightType)(-1);
    internal static Dictionary<Transform, Dictionary<ManSFX.ProjectileFlightType, ExtSoundGroup>> libProjs =
        new Dictionary<Transform, Dictionary<ManSFX.ProjectileFlightType, ExtSoundGroup>>();

    internal static void InsureInit()
    {
        if (init)
            return;
        init = true;
        ManAudioExt.OnRebuildSounds.Subscribe(OnRebuildSounds);
    }
    internal static void OnRebuildSounds()
    {
        ManWorldTileExt.ReloadENTIREScene();
        foreach (var item in libTechs.Values)
        {
            foreach (var item2 in item)
            {
                item2.Value.Silence();
            }
        }
        libTechs.Clear();
        foreach (var item in libProjs.Values)
        {
            foreach (var item2 in item)
            {
                item2.Value.Silence();
            }
        }
        libProjs.Clear();
    }
    internal static void LogAllSoundsRegistered()
    {
        DebugRandAddi.Log("---- ALL REGISTERED SOUNDS ----");
        foreach (var layer1 in ManAudioExt.AllSounds)
        {
            DebugRandAddi.Log("mod: " + layer1.Key.ModID);
            foreach (var Dict in layer1.Value)
            {
                DebugRandAddi.Log("\tval: " + Dict.Key);
            }
        }
    }
    internal static int AssignSoundBlockMulti<T>(Transform block, object target, FieldInfo field, ExtSound sound,
        Dictionary<Tank, Dictionary<T, ExtSoundGroupMulti>> lib, ref T increment) where T : struct
    {
        InsureInit();
        Tank tank = block.GetComponent<TankBlock>().tank;
        ModContainer MC = ResourcesHelper.GetModContainer(ManMods.inst.GetModNameForBlockID(
            (BlockTypes)block.GetComponent<Visible>().m_ItemType.ItemType), out _);
        //if (MC != null)
        //    DebugRandAddi.Log("ModContainer " + MC.ModID);
        T type = default;
        Dictionary<T, ExtSoundGroupMulti> techAudioExt;
        if (!lib.TryGetValue(tank, out techAudioExt))
        {
            techAudioExt = new Dictionary<T, ExtSoundGroupMulti>();
            lib.Add(tank, techAudioExt);
        }
        foreach (var item in techAudioExt)
        {
            if (item.Value.Name == sound.Name)
            {
                DebugRandAddi.Info("Assigned STACKED sound " + sound.Name);
                type = item.Key;
                item.Value.assignments++;
                field.SetValue(target, type);
                return (int)(object)type;
            }
        }
        ManAudioExt.AudioGroup inst;
        if (MC != null && ManAudioExt.AllSounds.TryGetValue(MC, out var wavNameWithExt) &&
            wavNameWithExt.TryGetValue(sound.Name, out inst))
        {
            if (sound.MaxInstances <= 0)
                sound.MaxInstances = 1;
            if (!EntrySetLookup.TryGetValue(typeof(T), out var dictRegi))
            {
                DebugRandAddi.Info("Init soundgroup for " + typeof(T).ToString());
                dictRegi = new Dictionary<string, int>();
                EntrySetLookup.Add(typeof(T), dictRegi);
            }
            if (!dictRegi.TryGetValue(sound.Name, out int IDSet))
            {
                DebugRandAddi.Info("Created new sound " + sound.Name);
                IDSet = increment.GetHashCode();
                dictRegi.Add(sound.Name, IDSet);
            }
            type = (T)(object)IDSet;
            if (techAudioExt.TryGetValue(type, out ExtSoundGroupMulti group))
            {
                DebugRandAddi.Info("Added more sound " + sound.Name);
                group.assignments++;
            }
            else
            {
                DebugRandAddi.Info("Assigned sound " + sound.Name);
                foreach (var item in inst.main)
                {
                    item.Volume = sound.Volume;
                    item.PitchVariance = sound.PitchVariance;
                }
                if (inst.startup != null)
                    inst.startup.Volume = sound.Volume;
                if (inst.engage != null)
                    inst.engage.Volume = sound.Volume;
                if (inst.stop != null)
                    inst.stop.Volume = sound.Volume;
                techAudioExt.Add(type, new ExtSoundGroupMulti(sound, inst));
                increment = (T)(object)(increment.GetHashCode() - 1);
            }
            field.SetValue(target, type);
            return (int)(object)type;
        }
        else
        {
            DebugRandAddi.Info("Guess sound");
            foreach (var wavNameWithExtNSound in ManAudioExt.AllSounds)
            {
                /*
                DebugRandAddi.Log("- sound (guess) " + wavNameWithExtNSound.Key.ModID);
                foreach (var item in wavNameWithExtNSound.Value)
                {
                    DebugRandAddi.Log("- sound2 (guess) " + item.Key);
                }
                 */
                if (wavNameWithExtNSound.Value.TryGetValue(sound.Name, out inst))
                {
                    //DebugRandAddi.Info("Assigned sound (guess) " + sound.Name);
                    if (sound.MaxInstances <= 0)
                        sound.MaxInstances = 1;
                    if (!EntrySetLookup.TryGetValue(typeof(T), out var dictRegi))
                    {
                        DebugRandAddi.Info("Init soundgroup for " + typeof(T).ToString());
                        dictRegi = new Dictionary<string, int>();
                        EntrySetLookup.Add(typeof(T), dictRegi);
                    }
                    if (!dictRegi.TryGetValue(sound.Name, out int IDSet))
                    {
                        DebugRandAddi.Info("Created new sound " + sound.Name);
                        IDSet = increment.GetHashCode();
                        dictRegi.Add(sound.Name, IDSet);
                    }
                    type = (T)(object)IDSet;
                    if (techAudioExt.TryGetValue(type, out ExtSoundGroupMulti group))
                    {
                        DebugRandAddi.Info("Added more sound " + sound.Name); ;
                        group.assignments++;
                    }
                    else
                    {
                        DebugRandAddi.Info("Assigned sound " + sound.Name);
                        foreach (var item in inst.main)
                        {
                            item.Volume = sound.Volume;
                            item.PitchVariance = sound.PitchVariance;
                        }
                        if (inst.startup != null)
                            inst.startup.Volume = sound.Volume;
                        if (inst.engage != null)
                            inst.engage.Volume = sound.Volume;
                        if (inst.stop != null)
                            inst.stop.Volume = sound.Volume;
                        techAudioExt.Add(type, new ExtSoundGroupMulti(sound, inst));
                        increment = (T)(object)(increment.GetHashCode() - 1);
                    }
                    field.SetValue(target, type);
                    return (int)(object)type;
                }
            }
        }
        DebugRandAddi.Log("Failed to assign sound " + sound.Name);
        return (int)(object)type;
    }
    internal static void UnassignSoundBlockMulti<T>(Transform block, ExtSound sound,
        Dictionary<Tank, Dictionary<T, ExtSoundGroupMulti>> lib) where T : struct
    {
        Tank tank = block.GetComponent<TankBlock>().tank;
        Dictionary<T, ExtSoundGroupMulti> techAudioExt;
        if (lib.TryGetValue(tank, out techAudioExt))
        {
            for (int i = 0; i < techAudioExt.Count; i++)
            {
                var item = techAudioExt.ElementAt(i);
                if (item.Value.Name == sound.Name)
                {
                    item.Value.assignments--;
                    if (item.Value.sounders.TryGetValue(block.GetComponent<TankBlock>(), out int index))
                        item.Value.Sounds[index].SilenceIfLooping();
                    if (item.Value.assignments == 0)
                    {
                        //DebugRandAddi.Info("Removed ENTIRE sound " + sound.Name);
                        for (int j = 0; j < ExtSoundGroupMulti.MaxGroups; j++)
                            item.Value.Sounds[j].SilenceIfLooping();
                        techAudioExt.Remove(item.Key);
                        DebugRandAddi.Info("Removed one sound " + sound.Name);
                    }
                    else
                        DebugRandAddi.Info("Removed STACKED sound " + sound.Name);
                    break;
                }
            }
            if (!techAudioExt.Any())
            {
                DebugRandAddi.Info("Removed ExtSounds from " + sound.Name);
                lib.Remove(tank);
            }
        }
    }
    internal static int AssignSoundBlock<T>(Transform block, object target, FieldInfo field, ExtSound sound,
        Dictionary<Tank, Dictionary<T, ExtSoundGroup>> lib, ref T increment) where T : struct
    {
        InsureInit();
        Tank tank = block.GetComponent<TankBlock>().tank;
        ModContainer MC = ResourcesHelper.GetModContainer(ManMods.inst.GetModNameForBlockID(
            (BlockTypes)block.GetComponent<Visible>().m_ItemType.ItemType), out _);
        //if (MC != null)
        //    DebugRandAddi.Log("ModContainer " + MC.ModID);
        T type = default;
        Dictionary<T, ExtSoundGroup> techAudioExt;
        if (!lib.TryGetValue(tank, out techAudioExt))
        {
            techAudioExt = new Dictionary<T, ExtSoundGroup>();
            lib.Add(tank, techAudioExt);
        }
        foreach (var item in techAudioExt)
        {
            if (item.Value.Name == sound.Name)
            {
                DebugRandAddi.Info("Assigned STACKED sound " + sound.Name);
                type = item.Key;
                item.Value.assignments++;
                field.SetValue(target, type);
                return (int)(object)type;
            }
        }
        ManAudioExt.AudioGroup inst;
        if (MC != null && ManAudioExt.AllSounds.TryGetValue(MC, out var wavNameWithExt) &&
            wavNameWithExt.TryGetValue(sound.Name, out inst))
        {
            if (sound.MaxInstances <= 0)
                sound.MaxInstances = 1;
            if (!EntrySetLookup.TryGetValue(typeof(T), out var dictRegi))
            {
                DebugRandAddi.Info("Init soundgroup for " + typeof(T).ToString());
                dictRegi = new Dictionary<string, int>();
                EntrySetLookup.Add(typeof(T), dictRegi);
            }
            if (!dictRegi.TryGetValue(sound.Name, out int IDSet))
            {
                DebugRandAddi.Info("Created new sound " + sound.Name);
                IDSet = increment.GetHashCode();
                dictRegi.Add(sound.Name, IDSet);
            }
            type = (T)(object)IDSet;
            if (techAudioExt.TryGetValue(type, out ExtSoundGroup group))
            {
                DebugRandAddi.Info("Added more sound " + sound.Name);
                group.assignments++;
            }
            else
            {
                DebugRandAddi.Info("Assigned sound " + sound.Name);
                foreach (var item in inst.main)
                {
                    item.Volume = sound.Volume;
                    item.PitchVariance = sound.PitchVariance;
                }
                if (inst.startup != null)
                    inst.startup.Volume = sound.Volume;
                if (inst.engage != null)
                    inst.engage.Volume = sound.Volume;
                if (inst.stop != null)
                    inst.stop.Volume = sound.Volume;
                techAudioExt.Add(type, new ExtSoundGroup(sound, inst));
                increment = (T)(object)(increment.GetHashCode() - 1);
            }
            field.SetValue(target, type);
            return (int)(object)type;
        }
        else
        {
            DebugRandAddi.Info("Guess sound");
            foreach (var wavNameWithExtNSound in ManAudioExt.AllSounds)
            {
                //*
                DebugRandAddi.Log("- sound (guess) " + wavNameWithExtNSound.Key.ModID);
                foreach (var item in wavNameWithExtNSound.Value)
                {
                    DebugRandAddi.Log("- sound2 (guess) " + item.Key);
                }
                // */
                if (wavNameWithExtNSound.Value.TryGetValue(sound.Name, out inst))
                {
                    //DebugRandAddi.Info("Assigned sound (guess) " + sound.Name);
                    if (sound.MaxInstances <= 0)
                        sound.MaxInstances = 1;
                    if (!EntrySetLookup.TryGetValue(typeof(T), out var dictRegi))
                    {
                        DebugRandAddi.Info("Init soundgroup for " + typeof(T).ToString());
                        dictRegi = new Dictionary<string, int>();
                        EntrySetLookup.Add(typeof(T), dictRegi);
                    }
                    if (!dictRegi.TryGetValue(sound.Name, out int IDSet))
                    {
                        DebugRandAddi.Info("Created new sound " + sound.Name);
                        IDSet = increment.GetHashCode();
                        dictRegi.Add(sound.Name, IDSet);
                    }
                    type = (T)(object)IDSet;
                    if (techAudioExt.TryGetValue(type, out ExtSoundGroup group))
                    {
                        DebugRandAddi.Info("Added more sound " + sound.Name); ;
                        group.assignments++;
                    }
                    else
                    {
                        DebugRandAddi.Info("Assigned sound " + sound.Name);
                        foreach (var item in inst.main)
                        {
                            item.Volume = sound.Volume;
                            item.PitchVariance = sound.PitchVariance;
                        }
                        if (inst.startup != null)
                            inst.startup.Volume = sound.Volume;
                        if (inst.engage != null)
                            inst.engage.Volume = sound.Volume;
                        if (inst.stop != null)
                            inst.stop.Volume = sound.Volume;
                        techAudioExt.Add(type, new ExtSoundGroup(sound, inst));
                        increment = (T)(object)(increment.GetHashCode() - 1);
                    }
                    field.SetValue(target, type);
                    return (int)(object)type;
                }
            }
        }
        DebugRandAddi.Log("Failed to assign sound " + sound.Name);
        return (int)(object)type;
    }
    internal static void UnassignSoundBlock<T>(Transform block, ExtSound sound,
        Dictionary<Tank, Dictionary<T, ExtSoundGroup>> lib) where T : struct
    {
        Tank tank = block.GetComponent<TankBlock>().tank;
        Dictionary<T, ExtSoundGroup> techAudioExt;
        if (lib.TryGetValue(tank, out techAudioExt))
        {
            for (int i = 0; i < techAudioExt.Count; i++)
            {
                var item = techAudioExt.ElementAt(i);
                if (item.Value.Name == sound.Name)
                {
                    item.Value.assignments--;
                    if (item.Value.assignments == 0)
                    {
                        //DebugRandAddi.Info("Removed ENTIRE sound " + sound.Name);
                        item.Value.SilenceIfLooping();
                        techAudioExt.Remove(item.Key);
                        DebugRandAddi.Info("Removed one sound " + sound.Name);
                    }
                    else
                        DebugRandAddi.Info("Removed STACKED sound " + sound.Name);
                    break;
                }
            }
            if (!techAudioExt.Any())
            {
                DebugRandAddi.Info("Removed ExtSounds from " + sound.Name);
                lib.Remove(tank);
            }
        }
    }

    internal static int AssignSoundTransform<T>(Transform proj, object target, FieldInfo field, ExtSound sound,
        Dictionary<Transform, Dictionary<T, ExtSoundGroup>> lib, ref T increment) where T : struct
    {
        InsureInit();
        T type = default;
        Dictionary<T, ExtSoundGroup> projAudioExt;
        if (!lib.TryGetValue(proj, out projAudioExt))
        {
            projAudioExt = new Dictionary<T, ExtSoundGroup>();
            lib.Add(proj, projAudioExt);
        }
        foreach (var item in projAudioExt)
        {
            if (item.Value.Name == sound.Name)
            {
                type = item.Key;
                item.Value.assignments++;
                field.SetValue(target, type);
                return (int)(object)type;
            }
        }
        foreach (var wavNameWithExtNSound in ManAudioExt.AllSounds)
        {
            if (wavNameWithExtNSound.Value.TryGetValue(sound.Name, out ManAudioExt.AudioGroup inst))
            {
                DebugRandAddi.Info("Assigned sound (guess) " + sound.Name);
                if (sound.MaxInstances <= 0)
                    sound.MaxInstances = 1;
                if (!EntrySetLookup.TryGetValue(typeof(T), out var dictRegi))
                {
                    DebugRandAddi.Info("Init soundgroup for " + typeof(T).ToString());
                    dictRegi = new Dictionary<string, int>();
                    EntrySetLookup.Add(typeof(T), dictRegi);
                }
                if (!dictRegi.TryGetValue(sound.Name, out int IDSet))
                {
                    DebugRandAddi.Info("Created new sound " + sound.Name);
                    IDSet = increment.GetHashCode();
                    dictRegi.Add(sound.Name, IDSet);
                }
                type = (T)(object)IDSet;
                if (projAudioExt.TryGetValue(type, out ExtSoundGroup group))
                {
                    group.assignments++;
                }
                else
                {
                    foreach (var item in inst.main)
                    {
                        item.Volume = sound.Volume;
                        item.PitchVariance = sound.PitchVariance;
                    }
                    projAudioExt.Add(type, new ExtSoundGroup(sound, inst));
                }
                field.SetValue(target, type);
                NextEntryProj--;
                return (int)(object)type;
            }
        }
        DebugRandAddi.Log("Failed to assign sound " + sound.Name);
        return (int)(object)type;
    }
    internal static void UnassignSoundTransform<T>(Transform proj, ExtSound sound,
        Dictionary<Transform, Dictionary<T, ExtSoundGroup>> lib) where T : struct
    {
        Dictionary<T, ExtSoundGroup> techAudioExt;
        if (lib.TryGetValue(proj, out techAudioExt))
        {
            for (int i = 0; i < techAudioExt.Count; i++)
            {
                var item = techAudioExt.ElementAt(i);
                if (item.Value.Name == sound.Name)
                {
                    item.Value.assignments--;
                    if (item.Value.assignments == 0)
                    {
                        foreach (var item2 in item.Value.main)
                        {
                            foreach (var item3 in item2)
                                item3.Reset();
                        }
                        if (item.Value.startup != null)
                            item.Value.startup.Reset();
                        if (item.Value.stop != null)
                            item.Value.stop.Reset();
                        techAudioExt.Remove(item.Key);
                        DebugRandAddi.Info("Removed one sound " + sound.Name);
                    }
                    else
                        DebugRandAddi.Info("Removed STACKED sound " + sound.Name);
                }
            }
            if (techAudioExt.Count == 0)
            {
                lib.Remove(proj);
            }
        }
    }


    internal static int AssignSoundExplosion(Transform block, object target, FieldInfo field, ExtSound sound) =>
        AssignSoundTransform(block, target, field, sound, libExplode, ref NextEntryExplode);
    internal static void UnassignSoundExplosion(Transform block, ExtSound sound) =>
        UnassignSoundTransform(block, sound, libExplode);

    internal static int AssignSoundBooster(Transform block, object target, FieldInfo field, ExtSound sound) =>
        AssignSoundBlock(block, target, field, sound, libBoosters, ref NextEntryBooster);
    internal static void UnassignSoundBooster(Transform block, ExtSound sound) =>
        UnassignSoundBlock(block, sound, libBoosters);

    internal static int AssignSoundTechAudio(Transform block, object target, FieldInfo field, ExtSound sound) => AssignSoundBlockMulti<TechAudio.SFXType>
        (block, target, field, sound, libTechs, ref NextEntryTech);
    internal static void UnassignSoundTechAudio(Transform block, ExtSound sound) => UnassignSoundBlockMulti<TechAudio.SFXType>
        (block, sound, libTechs);

    internal static int AssignSoundProjectile(Transform proj, object target, FieldInfo field, ExtSound sound) =>
        AssignSoundTransform(proj, target, field, sound, libProjs, ref NextEntryProj);
    internal static void UnassignSoundProjectile(Transform proj, ExtSound sound) =>
        UnassignSoundTransform(proj, sound, libProjs);


    public static bool PlaySound(TechAudio.AudioTickData data)
    {
        if (data.block?.tank)
        {
            if (!libTechs.TryGetValue(data.block.tank, out var soundBatch))
            {
                soundBatch = new Dictionary<TechAudio.SFXType, ExtSoundGroupMulti>();
                libTechs.Add(data.block.tank, soundBatch);
            }
            if (soundBatch.TryGetValue(data.sfxType, out var soundGroup))
            {
                ExtSoundGroup sound = null;
                if (soundGroup.sounders.TryGetValue(data.block, out int index))
                {
                    sound = soundGroup.Sounds[index];
                }
                else
                {
                    if (data.isNoteOn && soundGroup.sounders.Count <= ExtSoundGroupMulti.MaxGroups)
                    {   // Try and reserve a sound maker
                        if (soundGroup.sounders.Any() && soundGroup.sounders.First().Value == 0)
                        {
                            sound = soundGroup.Sounds[1];
                            soundGroup.sounders.Add(data.block, 1);
                        }
                        else
                        {
                            sound = soundGroup.Sounds[0];
                            soundGroup.sounders.Add(data.block, 0);
                        }
                    }
                }
                if (sound != null)
                {
                    if (data.numTriggered > 0)
                    {
                        if (sound.stop != null)
                            sound.stop.Stop();
                        if (sound.engage != null)
                        {
                            sound.engage.Position = data.block.tank.boundsCentreWorldNoCheck;
                            sound.engage.PlayFromBeginning();
                            if (sound.startup != null)
                                sound.startup.Stop();
                        }
                        if (Time.time > sound.lastFireTime)
                        {
                            sound.lastFireTime = Time.time + sound.delay;
                            int randomSelect = UnityEngine.Random.Range(0, sound.main.Length - 1);
                            for (int i = 0; sound.main.Length > i; i++)
                            {
                                if (randomSelect != i)
                                    sound.main[i][sound.step].Reset();
                            }
                            var sounder = sound.main[randomSelect][sound.step];
                            sounder.Position = data.block.tank.boundsCentreWorldNoCheck;
                            sounder.PlayFromBeginning();
                            //sounder.Play(false, data.block.tank.boundsCentreWorldNoCheck);
                            sound.step++;
                            if (sound.step >= sound.main[randomSelect].Length)
                                sound.step = 0;
                        }
                        sound.Active = data.isNoteOn;
                    }
                    else if (sound.Active != data.isNoteOn)
                    {
                        if (data.isNoteOn && sound.startup != null)
                        {
                            sound.stop.Stop();
                            sound.startup.Position = data.block.tank.boundsCentreWorldNoCheck;
                            sound.startup.PlayFromBeginning();
                        }
                        else if (!data.isNoteOn && sound.stop != null)
                        {
                            for (int i = 0; sound.main.Length > i; i++)
                            {
                                for (int j = 0; j < sound.main[i].Length; j++)
                                {
                                    sound.main[i][j].Stop();
                                }
                            }
                            sound.startup.Stop();
                            sound.stop.Position = data.block.tank.boundsCentreWorldNoCheck;
                            sound.stop.PlayFromBeginning();
                        }
                        sound.Active = data.isNoteOn;
                    }
                    else if (sound.Active && data.isNoteOn && sound.Looping)
                    {
                        if ((sound.startup == null || !sound.startup.IsPlaying) && Time.time > sound.lastFireTime)
                        {
                            sound.lastFireTime = Time.time + 0.05f;
                            bool playing = false;
                            for (int i = 0; sound.main.Length > i; i++)
                            {
                                for (int j = 0; j < sound.main[i].Length; j++)
                                {
                                    var sounder = sound.main[i][j];
                                    if (sounder.IsPlaying)
                                    {
                                        sounder.Position = data.block.tank.boundsCentreWorldNoCheck;
                                        playing = true;
                                        break;
                                    }
                                }
                            }
                            if (!playing)
                            {
                                int randomSelect = UnityEngine.Random.Range(0, sound.main.Length - 1);
                                for (int i = 0; sound.main.Length > i; i++)
                                {
                                    if (randomSelect != i)
                                        sound.main[i][sound.step].Reset();
                                }
                                var sounder = sound.main[randomSelect][sound.step];
                                sounder.Position = data.block.tank.boundsCentreWorldNoCheck;
                                sounder.PlayFromBeginning();
                                //sounder.Play(false, data.block.tank.boundsCentreWorldNoCheck);
                                sound.step++;
                                if (sound.step >= sound.main.Length)
                                    sound.step = 0;
                            }
                        }
                    }
                    else
                        sound.Active = data.isNoteOn;
                    if (!data.isNoteOn && (sound.stop == null || !sound.stop.IsPlaying))
                    {
                        soundGroup.sounders.Remove(data.block);
                    }
                }
                else
                {   // It's a low-priority.  We still play sounds for non-looping though
                    sound = soundGroup.Sounds[0];
                    if (!sound.Looping)
                    {
                        if (data.numTriggered > 0 && Time.time > sound.lastFireTime)
                        {
                            sound.lastFireTime = Time.time + sound.delay;
                            int randomSelect = UnityEngine.Random.Range(0, sound.main.Length - 1);
                            for (int i = 0; sound.main.Length > i; i++)
                            {
                                if (randomSelect != i)
                                    sound.main[i][sound.step].Reset();
                            }
                            var sounder = sound.main[randomSelect][sound.step];
                            sounder.Position = data.block.tank.boundsCentreWorldNoCheck;
                            sounder.PlayFromBeginning();
                            //sounder.Play(false, data.block.tank.boundsCentreWorldNoCheck);
                            sound.step++;
                            if (sound.step >= sound.main[randomSelect].Length)
                                sound.step = 0;
                        }
                    }
                }
                return false;
            }
        }
        return true;
    }
    public static bool PlaySound(TechAudio.SFXType type, Vector3 scenePos)
    {
        if (Singleton.playerTank)
        {
            if (libTechs.TryGetValue(Singleton.playerTank, out var soundG) &&
                soundG.TryGetValue(type, out var sound))
            {
                sound.Sounds[1].main.GetRandomEntry()[0].Play(true, scenePos);
                return true;
            }
        }
        return false;
    }
    public static bool PlaySound(ManSFX.ExplosionType type, Vector3 scenePos)
    {
        if (Singleton.playerTank)
        {
            foreach (var item in libExplode)
            {
                var soundG = item.Value;
                if (soundG.TryGetValue(type, out var sound))
                {
                    sound.main.GetRandomEntry()[0].Play(true, scenePos);
                    return true;
                }
            }
        }
        return false;
    }
    public static bool PlaySound(ManSFX.ProjectileFlightType type, Transform trans)
    {
        if (libProjs.TryGetValue(trans, out var soundG) &&
            soundG.TryGetValue(type, out var sound))
        {
            var rand = sound.main.GetRandomEntry();
            rand[0].Looped = true;
            rand[0].transform = trans;
            rand[0].Play(true);
            return false;
        }
        return true;
    }
    public static bool StopSound(ManSFX.ProjectileFlightType type, Transform trans)
    {
        if (libProjs.TryGetValue(trans, out var soundG) &&
            soundG.TryGetValue(type, out var sound))
        {
            foreach (var item in sound.main)
            {
                item[0].Pause();
            }
            return false;
        }
        return true;
    }
    public class ExtSoundGroupMulti
    {
        /// <summary>ONLY SUPPORTS 2, WILL NEED OVERHAUL FOR MORE</summary>
        public const int MaxGroups = 2;
        public string Name;
        public int assignments;
        public Dictionary<TankBlock, int> sounders;
        public ExtSoundGroup[] Sounds;
        public ExtSoundGroupMulti(ExtSound sound, ManAudioExt.AudioGroup inst)
        {
            Name = sound.Name;
            sounders = new Dictionary<TankBlock, int>();
            Sounds = new ExtSoundGroup[MaxGroups];
            for (int i = 0; i < MaxGroups; i++)
            {
                Sounds[i] = new ExtSoundGroup(sound, inst); 
            }
            assignments = 1;
        }
        public void SilenceIfLooping()
        {
            for (int i = 0; i < MaxGroups; i++)
                Sounds[i].SilenceIfLooping();
        }
        public void Silence()
        {
            for (int i = 0; i < MaxGroups; i++)
                Sounds[i].Silence();
        }
    }
    public class ExtSoundGroup
    {
        public const float ValueToSetLooped = 20f;

        public string Name;
        public int assignments;
        public int step;
        public float delay;
        public float lastFireTime;
        public bool Looping => delay >= ValueToSetLooped;
        public bool Active;
        /// <summary>
        /// [] 
        /// 1. Variants
        /// 2. Instances
        /// </summary>
        public AudioInst[][] main;
        public AudioInst startup;
        public AudioInst engage;
        public AudioInst stop;
        public ExtSoundGroup(ExtSound sound, ManAudioExt.AudioGroup inst)
        {
            Name = sound.Name;
            main = new AudioInst[inst.main.Length][];
            for (int i = 0; i < main.Length; i++)
            {
                var instVariations = new AudioInst[sound.MaxInstances];
                for (int j = 0; instVariations.Length > j; j++)
                {
                    AudioInst AIn = inst.main[i].Copy();
                    if (sound.Cooldown >= ValueToSetLooped)
                        AIn.Looped = true;
                    instVariations[j] = AIn;
                }
                main[i] = instVariations;
            }
            if (inst.startup != null)
            {
                startup = inst.startup.Copy();
                startup.Volume = sound.Volume;
            }
            else
                startup = null;
            if (inst.engage != null)
            {
                engage = inst.engage.Copy();
                engage.Volume = sound.Volume;
            }
            else
                engage = null;
            if (inst.stop != null)
            {
                stop = inst.stop.Copy();
                stop.Volume = sound.Volume;
            }
            else
                stop = null;
            assignments = 1;
            Active = false;
            step = 0;
            delay = sound.Cooldown;
            lastFireTime = 0;
        }
        public void SilenceIfLooping()
        {
            if (Looping)
                foreach (var item in main)
                    foreach (var item2 in item)
                        item2.Reset();
            else
                foreach (var item in main)
                    foreach (var item2 in item)
                        item2.StopPosUpdates();
            if (startup != null)
                startup.StopPosUpdates();
            if (engage != null)
                engage.StopPosUpdates();
            if (stop != null)
                stop.StopPosUpdates();
        }
        public void Silence()
        {
            foreach (var item in main)
            {
                foreach (var item2 in item)
                {
                    item2.Reset();
                }
            }
            if (startup != null)
                startup.Stop();
            if (engage != null)
                engage.Stop();
            if (stop != null)
                stop.Stop();
        }
    }
    public class ExtSound
    {
        public string Name = "unset";
        public float Volume = 1;
        public float PitchVariance = 0;
        public float Cooldown = 0.1f;
        public int MaxInstances = 1;
        public Dictionary<string, List<string>> Targets = new Dictionary<string, List<string>>();
    }
}
