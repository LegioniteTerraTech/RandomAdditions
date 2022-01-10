using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ModuleModeSwitch : RandomAdditions.ModuleModeSwitch { };

namespace RandomAdditions
{
    public enum ModeSwitchCondition
    {
        //AimedAtTarget,              // - (The gun is aiming directly at the Target)
        PrimarySecondary,           // - (Fire slow-firing primary and then fire secondary while waiting for primary to cool down)
        PrimarySecondarySalvo,      // - (Fire all of the burst shots of the slow-recharging primary and then fire secondary while waiting for primary BurstCooldown to finish)
        DistanceFar,                // - (Switch when target is beyond SetValue)
        DistanceClose,              // - (Switch when target is below SetValue)
        TargetSpeedFast,            // - (Target speed exceeds SetValue or projectile max velocity)
        TargetSpeedSlow,            // - (Target speed below SetValue or projectile max velocity)
        TargetHeightHigh,           // - (Target is above SetValue altitude above terrain)
        TargetHeightLow,            // - (Target is below SetValue altitude above terrain)
        TargetChargePercentAbove,   // - (Target has shields up)
        TargetChargePercentBelow,   // - (Target does not have shields or enemy batts out)
    }
    public class ManModeSwitch : MonoBehaviour
    {
        public static ManModeSwitch inst;
        private static float clockInterval = 1;
        private float clock = 0;

        public EventNoParams UpdateSwitchCheckFast = new EventNoParams();
        public EventNoParams UpdateSwitchCheck = new EventNoParams();

        public static void Initiate()
        {
            inst = new GameObject("ManModeSwitch").AddComponent<ManModeSwitch>();
            Debug.Log("RandomAdditions: Created ManModeSwitch.");
        }

        public void Update()
        {
            clock += Time.deltaTime;
            UpdateSwitchCheckFast.Send();
            if (clock > clockInterval)
            {
                UpdateSwitchCheck.Send();
                clock = 0;
            }
        }
    }
    public class ModuleModeSwitch : Module
    {
        private FireData FireDataAlt;       // 
        private ModuleWeapon MW;            //
        private ModuleWeaponGun MWG;        //
        private CannonBarrel MainGun;       //
        private CannonBarrel[] BarrelsMain; //
        private CannonBarrel[] BarrelsAux;  //
        private float m_ShotTimer = 0;      //

        private bool working = false;
        private bool sharedBarrels = false;

        public ModeSwitchCondition ModeSwitch = ModeSwitchCondition.PrimarySecondary;
        public float SetValue = 0;

        public int AuxillaryBarrelsStartIndex = 0; //The indexes of barrels after which
        //  should be used for the Auxillary weapon. Leave at 0 to use all barrels for both types.

        public float m_ShotCooldown = 1f;
        public float m_CooldownVariancePct = 0.05f;
        public int m_BurstShotCount = 0;
        public float m_BurstCooldown = 1f;
        public bool m_ResetBurstOnInterrupt = true;
        public bool m_SeekingRounds = false;
        public ModuleWeaponGun.FireControlMode m_FireControlMode = ModuleWeaponGun.FireControlMode.Sequenced;

        // Audio
        public TechAudio.SFXType m_FireSFXType = TechAudio.SFXType.Default;
        // AudioETC
        public bool m_DisableMainAudioLoop = false;
        public float m_AudioLoopDelay = 0;

        // ETC
        public float m_RegisterWarningAfter = 1f;
        public float m_ResetFiringTAfterNotFiredFor = 1f;
        public bool m_HasSpinUpDownAnim = false;
        public bool m_HasCooldownAnim = false;
        public bool m_CanInterruptSpinUpAnim = false;
        public bool m_CanInterruptSpinDownAnim = false;
        public int m_SpinUpAnimLayerIndex = 0;
        public float m_OverheatTime = 0.01f;
        public float m_OverheatPauseWindow = 0.01f;


        private static readonly FieldInfo MWGMunitions = typeof(ModuleWeaponGun).GetField("m_FiringData", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo MWGBarrels = typeof(ModuleWeaponGun).GetField("m_CannonBarrels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo MWGBarrelCount = typeof(ModuleWeaponGun).GetField("m_NumCannonBarrels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo MWGBarrelTrans = typeof(ModuleWeaponGun).GetField("m_BarrelTransform", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo MWGCooldown = typeof(ModuleWeaponGun).GetField("m_ShotTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo MWGShotsLeft = typeof(ModuleWeaponGun).GetField("m_BurstShotsRemaining", BindingFlags.NonPublic | BindingFlags.Instance);


        public void OnPool()
        {
            //block.MouseDownEvent.Subscribe(OnClick);
            block.AttachEvent.Subscribe(OnAttach);
            block.DetachEvent.Subscribe(OnDetach);
            working = false;

            FireData[] batch = GetComponentsInChildren<FireData>();
            if (batch.Length < 2)
            {
                LogHandler.ThrowWarning("RandomAdditions: \nModuleModeSwitch NEEDS a second FireData somewhere present to operate!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                return;
            }
            else
            {
                for (int step = 0; step < batch.Length; step++)
                {
                    //Debug.Log("RandomAdditions: ModuleModeSwitch - FireData in " + batch[step].gameObject.name);
                    if (GetComponent<FireData>() != batch[step])
                    {
                        FireDataAlt = batch[step];
                        Debug.Log("RandomAdditions: ModuleModeSwitch - Picked alt FireData in " + FireDataAlt.gameObject.name);
                        break;
                    }
                }
            }
            MW = GetComponent<ModuleWeapon>();
            if (!(bool)MW)
            {
                LogHandler.ThrowWarning("RandomAdditions: \nModuleModeSwitch NEEDS \"ModuleWeapon\" present to operate!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                return;
            }
            MWG = GetComponent<ModuleWeaponGun>();
            if (!(bool)MWG)
            {
                LogHandler.ThrowWarning("RandomAdditions: \nModuleModeSwitch NEEDS \"ModuleWeaponGun\" present to operate!\nThis operation cannot be handled automatically.\n  Cause of error - Block " + block.name);
                return;
            }

            List<CannonBarrel> BarrelsFetched = block.GetComponentsInChildren<CannonBarrel>().ToList();
            int barrelCount = BarrelsFetched.Count;
            List<CannonBarrel> barrelsTemp = new List<CannonBarrel>();
            sharedBarrels = (barrelCount <= AuxillaryBarrelsStartIndex || AuxillaryBarrelsStartIndex <= 0) ? true : false;

            if (!sharedBarrels)
            {
                BarrelsMain = new CannonBarrel[barrelCount];
                BarrelsAux = new CannonBarrel[barrelCount];

                for (int step = 0; step < barrelCount; step++)
                {
                    CannonBarrel CB = BarrelsFetched.ElementAt(step);
                    if (step < AuxillaryBarrelsStartIndex)
                    {
                        BarrelsMain[step] = CB;
                    }
                    else
                    {
                        BarrelsAux[step - AuxillaryBarrelsStartIndex] = CB;
                    }
                }
                MWGBarrels.SetValue(MWG, BarrelsMain);

                var FD = GetComponent<FireData>();
                var FD2 = FireDataAlt;
                foreach (CannonBarrel CB in BarrelsMain)
                {
                    CB.Setup(FD, MW);
                }
                foreach (CannonBarrel CB in BarrelsAux)
                {
                    CB.Setup(FD2, MW);
                }
            }
            else
            {
                BarrelsMain = BarrelsFetched.ToArray();
                BarrelsAux = BarrelsFetched.ToArray();
            }
            Debug.Log("RandomAdditions: ModuleModeSwitch - Prepped a gun");
        }
        public void OnAttach()
        {
            if ((int)ModeSwitch > (int)ModeSwitchCondition.PrimarySecondarySalvo)
                ManModeSwitch.inst.UpdateSwitchCheck.Subscribe(OnCheckUpdate);
            else
                ManModeSwitch.inst.UpdateSwitchCheckFast.Subscribe(OnCheckUpdateFast);
        }
        public void OnDetach()
        {
            if ((int)ModeSwitch > (int)ModeSwitchCondition.PrimarySecondarySalvo)
                ManModeSwitch.inst.UpdateSwitchCheck.Unsubscribe(OnCheckUpdate);
            else
                ManModeSwitch.inst.UpdateSwitchCheckFast.Unsubscribe(OnCheckUpdateFast);
            if (working) 
            {
                SwitchMode();
            }
        }

        public bool GetTargetInfo(out Visible target)
        {
            var weap = block.tank.Weapons.GetFirstWeapon();
            target = weap.GetComponent<TargetAimer>()?.Target;
            return target;
        }
        public bool GetTargetInfoTank(out Tank enemy)
        {
            enemy = null;
            if (GetTargetInfo(out Visible target))
            {
                enemy = target.tank;
            }
            return enemy;
        }
        public float GetEnergyPercent(Tank target)
        {
            if (target != null)
            {
                var reg = target.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
                if (reg.storageTotal <= 0)
                return (reg.storageTotal - reg.spareCapacity) / reg.storageTotal;
            }
            return 0;
        }


        public void OnCheckUpdateFast()
        {
            float num = Time.deltaTime;
            if (m_ShotTimer > 0f)
            {
                if (num > 0.04f)
                {
                    if (num > 0.083333336f)
                    {
                        num = 0f;
                    }
                    else
                    {
                        float num2 = Mathf.InverseLerp(0.083333336f, 0.04f, num);
                        num *= num2;
                    }
                }
                m_ShotTimer -= num;
            }
            switch (ModeSwitch)
            {
                case ModeSwitchCondition.PrimarySecondary:
                    if (working)
                    {
                        if (m_ShotTimer <= Mathf.Epsilon)
                        {
                            m_ShotTimer = (float)MWGCooldown.GetValue(MWG);
                            ResetCooldowns(true);
                            SetMode(false);
                        }
                    }
                    else
                    {
                        if (!MWG.ReadyToFire() && m_ShotTimer <= Mathf.Epsilon)
                        {
                            m_ShotTimer = (float)MWGCooldown.GetValue(MWG);
                            ResetCooldowns(true);
                            SetMode(true);
                        }
                    }
                    break;

                case ModeSwitchCondition.PrimarySecondarySalvo:
                    if (working)
                    {
                        if (m_ShotTimer <= Mathf.Epsilon)
                        {
                            m_ShotTimer = (float)MWGCooldown.GetValue(MWG);
                            ResetCooldowns(true);
                            SetMode(false);
                        }
                    }
                    else
                    {
                        if (!MWG.ReadyToFire() && m_ShotTimer <= Mathf.Epsilon)
                        {
                            float val = (float)MWGCooldown.GetValue(MWG);
                            if (val > (MWG.m_ShotCooldown > MWG.m_BurstCooldown ? MWG.m_BurstCooldown : MWG.m_ShotCooldown))
                            {
                                m_ShotTimer = val;
                                ResetCooldowns(true);
                                SetMode(true);
                            }
                        }
                    }
                    break;

                //case ModeSwitchCondition.AimedAtTarget:
                //    break;

                // All of the others are handled in SwitchMode()
                default:
                    break;
            }
        }
        public void OnCheckUpdate()
        {
            Tank tech = block.tank;
            Tank target;
            float val;
            float height;
            switch (ModeSwitch) 
            {
                case ModeSwitchCondition.DistanceClose:
                    if (GetTargetInfoTank(out target))
                    {
                        val = (target.boundsCentreWorldNoCheck - tech.boundsCentreWorldNoCheck).magnitude;
                        SetMode(val <= SetValue);
                    }
                    else
                        SetMode(false);
                    break;
                case ModeSwitchCondition.DistanceFar:
                    if (GetTargetInfoTank(out target))
                    {
                        val = (target.boundsCentreWorldNoCheck - tech.boundsCentreWorldNoCheck).magnitude;
                        SetMode(val >= SetValue);
                    }
                    else
                        SetMode(false);
                    break;

                case ModeSwitchCondition.TargetHeightHigh:
                    if (GetTargetInfoTank(out target))
                    {
                        if (ManWorld.inst.GetTerrainHeight(target.boundsCentreWorldNoCheck, out height))
                            val = target.boundsCentreWorldNoCheck.y - height;
                        else
                            val = target.boundsCentreWorldNoCheck.y;
                        SetMode(val >= SetValue);
                    }
                    else
                        SetMode(false);
                    break;
                case ModeSwitchCondition.TargetHeightLow:
                    if (GetTargetInfoTank(out target))
                    {
                        if (ManWorld.inst.GetTerrainHeight(target.boundsCentreWorldNoCheck, out height))
                            val = target.boundsCentreWorldNoCheck.y - height;
                        else
                            val = target.boundsCentreWorldNoCheck.y;
                        SetMode(val <= SetValue);
                    }
                    else
                        SetMode(false);
                    break;

                case ModeSwitchCondition.TargetChargePercentAbove:
                    if (GetTargetInfoTank(out target))
                    {
                        SetMode(GetEnergyPercent(target) < SetValue);
                    }
                    else
                        SetMode(false);
                    break;
                case ModeSwitchCondition.TargetChargePercentBelow:
                    if (GetTargetInfoTank(out target))
                    {
                        SetMode(GetEnergyPercent(target) > SetValue);
                    }
                    else
                        SetMode(false);
                    break;

                case ModeSwitchCondition.TargetSpeedFast:
                    if (GetTargetInfoTank(out target))
                    {
                        Vector3 tankVelo;
                        Vector3 targVelo;
                        if (tech.rbody)
                            tankVelo = tech.rbody.velocity;
                        else
                            tankVelo = Vector3.zero;
                        if (target.rbody)
                            targVelo = target.rbody.velocity;
                        else
                            targVelo = Vector3.zero;
                        SetMode((targVelo - tankVelo).magnitude * Globals.inst.MilesPerGameUnit >= SetValue);
                    }
                    else
                        SetMode(false);
                    break;
                case ModeSwitchCondition.TargetSpeedSlow:
                    if (GetTargetInfoTank(out target))
                    {
                        Vector3 tankVelo;
                        Vector3 targVelo;
                        if (tech.rbody)
                            tankVelo = tech.rbody.velocity;
                        else
                            tankVelo = Vector3.zero;
                        if (target.rbody)
                            targVelo = target.rbody.velocity;
                        else
                            targVelo = Vector3.zero;
                        SetMode((targVelo - tankVelo).magnitude * Globals.inst.MilesPerGameUnit <= SetValue);
                    }
                    else
                        SetMode(false);
                    break;

                // Cases handled elsewhere
                //case ModeSwitchCondition.PrimarySecondary:
                //case ModeSwitchCondition.PrimarySecondarySalvo:
                //case ModeSwitchCondition.AimedAtTarget:
                default:
                    break;
            }
        }

        /*
        public void OnClick(TankBlock blockIn, int input)
        {
            if (blockIn != block)
                return;
            if (input == 1 && Input.GetKey(KeyCode.LeftShift))
            {
                
            }
        }*/

        public void ResetCooldowns(bool PreSwitch)
        {
            MWGCooldown.SetValue(MWG, 0);
            if (PreSwitch)
            {
                MWGShotsLeft.SetValue(MWG, working ? MWG.m_BurstShotCount : m_BurstShotCount);
            }
            else
            {
                MWGShotsLeft.SetValue(MWG, working ? m_BurstShotCount : MWG.m_BurstShotCount);
            }
        }

        // Switching
        public void SwitchMode()
        {
            working = !working;
            SwitchModeModuleWeapon();
        }
        public void SetMode(bool setWorking)
        {
            if (working != setWorking)
            {
                working = !working;
                SwitchModeModuleWeapon();
            }
        }
        private void SwitchModeModuleWeapon()
        {
            var FD = GetComponent<FireData>();
            var FD2 = FireDataAlt;
            if (!(bool)FD || !(bool)FD2)
            {
                return;
            }

            // Main
            Exchange(ref MW.m_FireSFXType, ref m_FireSFXType);

            Exchange(ref MWG.m_ShotCooldown, ref m_ShotCooldown);
            Exchange(ref MWG.m_CooldownVariancePct, ref m_CooldownVariancePct);
            Exchange(ref MWG.m_BurstCooldown, ref m_BurstCooldown);
            Exchange(ref MWG.m_BurstShotCount, ref m_BurstShotCount);
            Exchange(ref MWG.m_ResetBurstOnInterrupt, ref m_ResetBurstOnInterrupt);
            Exchange(ref MWG.m_ResetFiringTAfterNotFiredFor, ref m_ResetFiringTAfterNotFiredFor);
            Exchange(ref MWG.m_SeekingRounds, ref m_SeekingRounds);
            Exchange(ref MWG.m_FireControlMode, ref m_FireControlMode);

            if (!sharedBarrels)
            {
                // Audio
                Exchange(ref MWG.m_AudioLoopDelay, ref m_AudioLoopDelay);
                Exchange(ref MWG.m_DisableMainAudioLoop, ref m_DisableMainAudioLoop);

                // Extra
                Exchange(ref MWG.m_CanInterruptSpinDownAnim, ref m_CanInterruptSpinDownAnim);
                Exchange(ref MWG.m_CanInterruptSpinUpAnim, ref m_CanInterruptSpinUpAnim);
                Exchange(ref MWG.m_HasCooldownAnim, ref m_HasCooldownAnim);
                Exchange(ref MWG.m_HasSpinUpDownAnim, ref m_HasSpinUpDownAnim);
                Exchange(ref MWG.m_OverheatPauseWindow, ref m_OverheatPauseWindow);
                Exchange(ref MWG.m_OverheatTime, ref m_OverheatTime);
                Exchange(ref MWG.m_RegisterWarningAfter, ref m_RegisterWarningAfter);
                Exchange(ref MWG.m_SpinUpAnimLayerIndex, ref m_SpinUpAnimLayerIndex);

                if (!working)
                {
                    MWGMunitions.SetValue(MWG, FD);
                    MWGBarrels.SetValue(MWG, BarrelsMain);
                    MWGBarrelCount.SetValue(MWG, BarrelsMain.Length);
                    MainGun = BarrelsMain[0];
                    MWGBarrelTrans.SetValue(MWG, MainGun.transform);
                }
                else
                {
                    MWGMunitions.SetValue(MWG, FD2);
                    MWGBarrels.SetValue(MWG, BarrelsAux);
                    MWGBarrelCount.SetValue(MWG, BarrelsAux.Length);
                    MainGun = BarrelsAux[0];
                    MWGBarrelTrans.SetValue(MWG, MainGun.transform);
                }
            }
            else
            {   // Shared Barrels
                if (!working)
                {
                    MWGMunitions.SetValue(MWG, FD);
                    foreach (CannonBarrel CB in BarrelsMain)
                    {
                        CB.Setup(FD, MW);
                    }
                }
                else
                {
                    MWGMunitions.SetValue(MWG, FD2);
                    foreach (CannonBarrel CB in BarrelsMain)
                    {
                        CB.Setup(FD2, MW);
                    }
                }
            }
            if (KickStart.isTweakTechPresent)
            {
                try
                {
                    TweakTech.ReAimer.UpdateExisting(block);
                }
                catch { }
            }
        }


        // Utilities
        private object shiftHold1 = null;   //
        private void Exchange<T>(ref T obj1, ref T obj2)
        {
            shiftHold1 = obj1;
            obj1 = obj2;
            obj2 = (T)shiftHold1;
            shiftHold1 = null;
        }
    }
}
