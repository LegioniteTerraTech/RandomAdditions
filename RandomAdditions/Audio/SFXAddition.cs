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

public class SFXAddition : RandomAdditions.SFXAddition { };
namespace RandomAdditions
{
    /// <summary>
    /// This modules job is to add new SFX to the game! 
    ///    well, sorta.  It's extremely hacky lol
    /// </summary>
    public class SFXAddition : MonoBehaviour
    {
        public string[] Names = new string[0];
        public float[] Volume = new float[0];
        public float[] PitchVariance = new float[0];
        public float[] Cooldown = new float[0];
        public int[] MaxInstances = new int[0];
        public string[] Targets = new string[0];
        public string[] TargetFields = new string[0];

        [JsonIgnore]
        public bool Assigned = false;
        [JsonIgnore]
        public ManSFXExtRand.ExtSound[] Additions = null;

        private static Dictionary<Type, Dictionary<string, FieldInfo>> ValidSFXHooks = new Dictionary<Type, Dictionary<string, FieldInfo>>();

        private static Dictionary<string, KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>, Action<Transform, ManSFXExtRand.ExtSound>>> ValidNamesWithAssigners =
            new Dictionary<string, KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>, Action<Transform, ManSFXExtRand.ExtSound>>>()
        {
        { "m_BoosterAudioType", new KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>,
            Action<Transform, ManSFXExtRand.ExtSound>> (ManSFXExtRand.AssignSoundBooster, ManSFXExtRand.UnassignSoundBooster) },

        };
        private static Dictionary<Type, KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>, Action<Transform, ManSFXExtRand.ExtSound>>> ValidTypesWithAssigners =
            new Dictionary<Type, KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>, Action<Transform, ManSFXExtRand.ExtSound>>>()
        {
        { typeof(TechAudio.SFXType), new KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>,
            Action<Transform, ManSFXExtRand.ExtSound>> (ManSFXExtRand.AssignSoundTechAudio, ManSFXExtRand.UnassignSoundTechAudio) },
        { typeof(ManSFX.ProjectileFlightType),  new KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>,
            Action<Transform, ManSFXExtRand.ExtSound>> (ManSFXExtRand.AssignSoundProjectile,  ManSFXExtRand.UnassignSoundProjectile)},
        { typeof(ManSFX.ExplosionType),  new KeyValuePair<Func<Transform, object, FieldInfo, ManSFXExtRand.ExtSound, int>,
            Action<Transform, ManSFXExtRand.ExtSound>> (ManSFXExtRand.AssignSoundExplosion,  ManSFXExtRand.UnassignSoundExplosion)}
        };

        public void OnSpawn()
        {
            if (Additions != null)
                return;
            if (Names.Length != Volume.Length || Volume.Length != PitchVariance.Length || Cooldown.Length != Volume.Length ||
                PitchVariance.Length != Targets.Length || Targets.Length != TargetFields.Length || MaxInstances.Length != Names.Length)
            {
                enabled = false;
                BlockDebug.ThrowWarning(true, "RandomAdditions: SFXAddition - WARNING: Block has mismatched Names, Volume, PitchVariance, Targets, TargetFields (they must have same count) \n Problem block name: " + name);
                return;
            }

            var tempL = new List<ManSFXExtRand.ExtSound>();
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i].NullOrEmpty())
                {
                    enabled = false;
                    BlockDebug.ThrowWarning(true, "RandomAdditions: SFXAddition - WARNING: Name cannot be null or empty \n Problem block name: " + name);
                    return;
                }
                if (Targets[i].NullOrEmpty())
                {
                    enabled = false;
                    BlockDebug.ThrowWarning(true, "RandomAdditions: SFXAddition - WARNING: Target cannot be null or empty \n Problem block name: " + name);
                    return;
                }
                if (TargetFields[i].NullOrEmpty())
                {
                    enabled = false;
                    BlockDebug.ThrowWarning(true, "RandomAdditions: SFXAddition - WARNING: TargetField cannot be null or empty \n Problem block name: " + name);
                    return;
                }
                ManSFXExtRand.ExtSound exist = tempL.Find(x => x.Name == Names[i]);
                if (exist != null)
                {
                    if (exist.Targets.TryGetValue(Targets[i], out var list))
                    {
                        list.Add(TargetFields[i]);
                    }
                    else
                        exist.Targets.Add(Targets[i], new List<string>() { TargetFields[i] });
                }
                else
                {
                    tempL.Add(new ManSFXExtRand.ExtSound()
                    {
                        Name = Names[i],
                        Volume = Volume[i],
                        MaxInstances = MaxInstances[i],
                        PitchVariance = PitchVariance[i],
                        Cooldown = Cooldown[i],
                        Targets = new Dictionary<string, List<string>>()
                        {
                            { Targets[i], new List<string>(){ TargetFields[i] } }
                        }
                    });
                }
            }
            Additions = tempL.ToArray();
            Projectile proj = GetComponent<Projectile>();
            if (proj)
            {
                OnFire();
                enabled = false;
                DebugRandAddi.Info("Projectile " + (transform.name.NullOrEmpty() ? "<NULL>" : transform.name) + " has SFXAddition");
            }
            else
                DebugRandAddi.Info("Block " + (transform.name.NullOrEmpty() ? "<NULL>" : transform.name) + " has SFXAddition");
            enabled = true;
        }
        /// <summary>
        /// Only should fire once per block!
        /// </summary>
        public void Update()
        {
            OnSpawn();
            DebugRandAddi.Info("Block " + (transform.name.NullOrEmpty() ? "<NULL>" : transform.name) + " has SFXAddition ACTIVE");
            TankBlock block = GetComponent<TankBlock>();
            if (block)
            {
                if (block.tank)
                    OnAttach();
                block.AttachedEvent.Subscribe(OnAttach);
                block.DetachingEvent.Subscribe(OnDetach);
            }
            else
            {
                Projectile proj = GetComponent<Projectile>();
                if (proj)
                {
                    OnFire();
                }
            }
            enabled = false;
        }
        private void DoAssign()
        {
            try
            {
                if (Assigned)
                    return;
                //Assigned = true;
                if (Additions == null || Additions.Length == 0)
                {  // Display every possible canidate for sound in logs
                    DebugRandAddi.Log("Block " + (transform.name.NullOrEmpty() ? "<NULL>" : transform.name) + " has Addition but it is not set.");
                    DebugRandAddi.Log("Possible canidates include:");
                    foreach (var item in GetComponents<MonoBehaviour>())
                    {
                        Type type = item.GetType();
                        if (!ValidSFXHooks.TryGetValue(type, out var FIs))
                        {
                            FIs = new Dictionary<string, FieldInfo>();
                            ValidSFXHooks.Add(type, FIs);
                        }
                        foreach (var fieldC in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (ValidTypesWithAssigners.ContainsKey(fieldC.FieldType))
                            {
                                if (!FIs.ContainsKey(fieldC.Name))
                                    FIs.Add(fieldC.Name, fieldC);
                                DebugRandAddi.Log("\t\"" + type.FullName + "\", \"" + fieldC.Name + "\"");
                            }
                        }
                    }
                    ManSFXExtRand.ExtSound[] soundsTemp = new ManSFXExtRand.ExtSound[]{
                    new ManSFXExtRand.ExtSound(){
                        Name = "basic",
                        MaxInstances = 1,
                        PitchVariance = 0.25f,
                        Targets = new Dictionary<string, List<string>>
                        {
                            { "ModuleWeapon", new List<string>{ "m_FireSFXType" } }
                        },
                    }
                };
                    DebugRandAddi.Log(JsonConvert.SerializeObject(soundsTemp, Formatting.Indented));
                    return;
                }
                for (int i = 0; i < Additions.Length; i++)
                {
                    var adder = Additions[i];
                    var targs = adder.Targets;
                    if (targs != null && targs.Count > 0)
                    {
                        foreach (var targetComponents in targs)
                        {
                            if (ValidSFXHooks.TryGetValue(KickStart.LookForType(targetComponents.Key), out var vals))
                            {
                                if (vals != null && targetComponents.Value != null)
                                {
                                    foreach (var fieldName in targetComponents.Value)
                                    {
                                        var comp = GetComponent(targetComponents.Key);
                                        if (comp != null && vals.TryGetValue(fieldName, out FieldInfo FI))
                                        {
                                            if (ValidNamesWithAssigners.TryGetValue(FI.Name, out var funcN))
                                            {
                                                Assigned = true;
                                                funcN.Key.Invoke(transform, comp, FI, adder);
                                            }
                                            else if (ValidTypesWithAssigners.TryGetValue(FI.FieldType, out var funcC))
                                            {
                                                Assigned = true;
                                                funcC.Key.Invoke(transform, comp, FI, adder);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {   // Get all possible hooks in GameObject
                                foreach (var item in GetComponents<Component>())
                                {
                                    Type type = item.GetType();
                                    if (!ValidSFXHooks.TryGetValue(type, out var FIs))
                                    {
                                        FIs = new Dictionary<string, FieldInfo>();
                                        ValidSFXHooks.Add(type, FIs);
                                    }
                                    foreach (var fieldC in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                                    {
                                        if (ValidNamesWithAssigners.ContainsKey(fieldC.Name))
                                        {
                                            if (!FIs.ContainsKey(fieldC.Name))
                                                FIs.Add(fieldC.Name, fieldC);
                                        }
                                        else if (ValidTypesWithAssigners.ContainsKey(fieldC.FieldType))
                                        {
                                            if (!FIs.ContainsKey(fieldC.Name))
                                                FIs.Add(fieldC.Name, fieldC);
                                        }
                                    }
                                }
                                if (ValidSFXHooks.TryGetValue(KickStart.LookForType(targetComponents.Key), out vals))
                                {
                                    if (vals != null && targetComponents.Value != null)
                                    {
                                        foreach (var fieldName in targetComponents.Value)
                                        {
                                            var comp = GetComponent(targetComponents.Key);
                                            if (comp != null && vals.TryGetValue(fieldName, out FieldInfo FI))
                                            {
                                                if (ValidNamesWithAssigners.TryGetValue(FI.Name, out var funcN))
                                                {
                                                    Assigned = true;
                                                    funcN.Key.Invoke(transform, comp, FI, adder);
                                                }
                                                else if (ValidTypesWithAssigners.TryGetValue(FI.FieldType, out var funcC))
                                                {
                                                    Assigned = true;
                                                    funcC.Key.Invoke(transform, comp, FI, adder);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (!Assigned)
                    DebugRandAddi.Assert("Failed to assign sounds for block " + name);
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("Failed to assign sounds for block due to exception: " + e);
            }
        }
        private void DoUnassign()
        {
            try
            {
                if (!Assigned)
                    return;
                for (int i = 0; i < Additions.Length; i++)
                {
                    var adder = Additions[i];
                    var targs = adder.Targets;
                    if (targs != null && targs.Count > 0)
                    {
                        foreach (var targetComponents in targs)
                        {
                            if (ValidSFXHooks.TryGetValue(KickStart.LookForType(targetComponents.Key), out var vals) &&
                                vals != null && targetComponents.Value != null)
                            {
                                foreach (var fieldName in targetComponents.Value)
                                {
                                    var comp = GetComponent(targetComponents.Key);
                                    if (comp != null && vals.TryGetValue(fieldName, out FieldInfo FI))
                                    {
                                        if (ValidNamesWithAssigners.TryGetValue(FI.Name, out var funcN))
                                        {
                                            Assigned = false;
                                            funcN.Value.Invoke(transform, adder);
                                        }
                                        else if (ValidTypesWithAssigners.TryGetValue(FI.FieldType, out var funcC))
                                        {
                                            Assigned = false;
                                            funcC.Value.Invoke(transform, adder);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (Assigned)
                    DebugRandAddi.Assert("Failed to un-assign sounds for block " + name);
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("Failed to un-assign sounds for block due to exception: " + e);
            }
        }
        public void OnFire()
        {
            DoAssign();
        }
        public void OnDetach()
        {
            DoUnassign();
        }
        public void OnAttach()
        {
            TankBlock block = GetComponent<TankBlock>();
            ModContainer MC = ResourcesHelper.GetModContainer(ManMods.inst.GetModNameForBlockID(
                (BlockTypes)block.GetComponent<Visible>().m_ItemType.ItemType), out _);
            TechAudio.SFXType SFX;
            Tank tank = block.tank;
            if (MC != null)
            {
                DoAssign();
            }
            else
                DebugRandAddi.Assert("Could not find ModContainer for " + name + " will guess!");
            enabled = false;
        }
    }
}

