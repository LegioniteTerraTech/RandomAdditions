using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

[RequireComponent(typeof(ModuleEnergy))]
[RequireComponent(typeof(TargetAimer))]
public class ModuleRepairAimer : RandomAdditions.ModuleRepairAimer { };

namespace RandomAdditions
{
    [RequireComponent(typeof(ModuleEnergy))]
    [RequireComponent(typeof(TargetAimer))]
    public class ModuleRepairAimer : ExtModule
    {
        // The module that moves a beam to an allied block's damaged location
        internal ModuleEnergy Energy;

        internal TankBlock aimTargBlock;
        private Transform Targeter;        // the transform that rests at the aim position
        private LineRenderer HealBeam;
        private TargetAimer TargetAimer;      // the controller that controls the GimbalAimers
        private List<GimbalAimer> gimbals;
        private Spinner spinner;

        private float targeterZOffset = 0;
        private float lastZDist = 0;
        private float healStep = 0;
        private float healTargetRad = 0;
        private float animPulse = 0;
        private float animSpeedMulti = 30;
        private const float DistDeviance = 12;
        private const float distVar = 1.5f;


        public float MaxLockOnRange = 70;   // Max range to seek Techs
        public float MaxExtendRange = 110;  // Max range for the transform to move out
        public float ExtendSpeed = 30;      // How fast to extend by
        public float RotateSpeed = 30;      // How fast to aim by
        public float HealPulseCost = 10;    // Cost of healing pulses
        public float HealPulseDelay = 1;    // Delay between healing pulses
        public float HealHealthRate = 40;   // How much to heal by
        public bool UseCircularEnds = false;// Round off the ends of the line


        protected override void Pool()
        {
            Energy = GetComponent<ModuleEnergy>();
            if (Energy == null)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRepairAimer NEEDS a valid ModuleEnergy!\nCause of error - Block " + gameObject.name);
                return;
            }
            //Invoke("DelayedSub", 0.001f);

            TargetAimer = GetComponent<TargetAimer>();
            if (TargetAimer == null)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRepairAimer NEEDS a valid TargetAimer in hierarchy!\nCause of error - Block " + gameObject.name);
                return;
            }
            gimbals = GetComponentsInChildren<GimbalAimer>().ToList();

            Targeter = KickStart.HeavyTransformSearch(transform, "_Target");
            if (Targeter == null)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRepairAimer NEEDS a GameObject in hierarchy named \"_Target\" for the aiming position!\nCause of error - Block " + gameObject.name);
                return;
            }
            if (!(bool)Targeter.parent)
            {
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRepairAimer NEEDS a GameObject in hierarchy named \"_Target\" WITHIN the GimbalAimer containing GameObjects!\nCause of error - Block " + gameObject.name);
                return;
            }
            if (!(bool)spinner)
                spinner = GetComponentInChildren<Spinner>(true);
            animSpeedMulti =  1 / Mathf.Max(HealPulseDelay, 0.25f);
            targeterZOffset = Targeter.localPosition.z;
            lastZDist = targeterZOffset;
            TargetAimer.Init(block, 60, null);
            InitHealBeamEffect();
        }

        private static LocExtStringMod LOC_ModuleRepairAimer_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, AltUI.HighlightString("Repair Arms") + " repair blocks from far away at the cost of " +
                        AltUI.BlueStringMsg("Energy") + "."},
            { LocalisationEnums.Languages.Japanese, AltUI.HighlightString("『Repair Arm』")  + "は泡よりもさらに遠くまで修復"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRepairAimer", LOC_ModuleRepairAimer_desc);
        public override void OnAttach()
        {
            TankRepairer.stat.HandleAddition(this);
            hint.Show();
        }

        public override void OnDetach()
        {
            aimTargBlock = null;
            TankRepairer.stat.HandleRemoval(this);
            StopBeam();
        }


        private bool CanAimAt(Vector3 posWorld)
        {
            bool canDo = false;
            if (gimbals != null)
            {
                canDo = true;
                for (int step = 0; step < gimbals.Count; step++)
                {
                    if (aimTargBlock && canDo)
                        canDo = gimbals.ElementAt(step).CanAim(posWorld);
                }
            }
            return canDo;
        }

        public bool IsTargBlockValid(TankBlock aimTargBlock) => aimTargBlock && aimTargBlock.visible.isActive && aimTargBlock.tank 
            && !aimTargBlock.visible.damageable.IsAtFullHealth;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="NewTargBlock"></param>
        /// <returns>true if it did something</returns>
        public bool UpdateRepair(TankBlock NewTargBlock, ref float energyPool)
        {
            if (TargetAimer == null || Targeter == null)
                return false;
            bool healed = false;
            //DebugRandAddi.Log("RandomAdditions: ModuleRepairAimer - UPDATING
            if (!IsTargBlockValid(aimTargBlock))
            {
                if (NewTargBlock == null)
                    DebugRandAddi.Log(block.name + " - Not given a valid target!!!");
                else
                {
                    aimTargBlock = NewTargBlock;
                    DebugRandAddi.Log(block.name + " - New target " + NewTargBlock.name);
                    if (!IsTargBlockValid(aimTargBlock))
                        DebugRandAddi.Log(aimTargBlock.name + " -> Target is INVALID - null? " + aimTargBlock + ", Vis active " + aimTargBlock.visible.isActive +
                            ", attached to Tech " + aimTargBlock.tank + ", is not at full health " + !aimTargBlock.visible.damageable.IsAtFullHealth);
                }
            }
            if (IsTargBlockValid(aimTargBlock))
            {
                Vector3 targPos = aimTargBlock.centreOfMassWorld;
                if (CanAimAt(targPos))
                {
                    DebugRandAddi.Log(block.name + " - Update heal beam");
                    StartBeam();
                    TargetAimer.AimAtWorldPos(targPos, RotateSpeed);
                    float aimZDist = Targeter.parent.InverseTransformPoint(targPos).z;
                    UpdateTargetDist(aimZDist);
                    UpdateHealBeamEffect();
                }
                else
                {
                    DebugRandAddi.Log(block.name + " - Cannot aim!!!");
                    aimTargBlock = NewTargBlock;
                }
                healStep += Time.deltaTime;
                if (healStep >= HealPulseDelay)
                {
                    healStep = 0;
                    if (Targeter.InverseTransformPoint(aimTargBlock.centreOfMassWorld).Approximately(Vector3.zero, DistDeviance))
                    {
                        if (ManNetwork.IsHost || !ManNetwork.IsNetworked)
                        {
                            if (energyPool >= HealPulseCost)
                            {
                                StartBeam();
                                var DMG = aimTargBlock.visible.damageable;
                                DMG.Repair(HealHealthRate, true);
                                aimTargBlock.visible.KeepAwake();
                                energyPool -= HealPulseCost;
                                healed = true;
                            }
                            else
                            {
                                DebugRandAddi.Log(block.name + " - Not enough energy to repair [" + energyPool + "] vs needed [" + HealPulseCost + "]");
                                StopBeam();
                                //DebugRandAddi.Log("RandomAdditions: ModuleRepairAimer - UPDATING - not enough energy " + Energy.GetCurrentAmount(EnergyRegulator.EnergyType.Electric));
                            }
                        }
                        else
                        {
                            healed = true;
                            StartBeam();
                        }
                    }
                    else
                    {
                        DebugRandAddi.Log(block.name + " - Still extending to reach target " + aimTargBlock.name);
                    }
                }
            }
            else
            {
                StopBeam();
                Vector3 defaultAim = Targeter.position + (25 * block.trans.forward);
                TargetAimer.AimAtWorldPos(defaultAim, RotateSpeed);
                UpdateTargetDist(targeterZOffset);
            }

            return healed;
        }

        private void InitHealBeamEffect()
        {
            Transform TO = transform.Find("HealLine");
            GameObject gO = null;
            if ((bool)TO)
                gO = TO.gameObject;
            if (!(bool)gO)
            {
                gO = Instantiate(new GameObject("HealLine"), transform, false);
                gO.transform.localPosition = Vector3.zero;
                gO.transform.localRotation = Quaternion.identity;
            }
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.endWidth = 0.1f;
                lr.startWidth = 1;
                lr.useWorldSpace = true;
                lr.startColor = new Color(0.25f, 1, 0.25f, 0.5f);
                lr.endColor = new Color(0.1f, 1, 0.1f, 0.75f);
                if (UseCircularEnds)
                    lr.numCapVertices = 8;
                lr.SetPositions(new Vector3[2] { new Vector3(0, 0, -1), Vector3.zero });
            }
            HealBeam = lr;
            HealBeam.gameObject.SetActive(false);
        }
        private void UpdateHealBeamEffect()
        {
            float aimedScale = (bool)aimTargBlock ? Mathf.Max(aimTargBlock.trans.TransformVector(aimTargBlock.BlockCellBounds.size).y / 2, 1) : 1;

            float sizeChangeRate = 0.25f;
            float sizeChange = aimedScale - healTargetRad;
            healTargetRad += sizeChange * sizeChangeRate;
            HealBeam.startWidth = healTargetRad;
            animPulse += Time.deltaTime * animSpeedMulti;
            if (animPulse > 6.28f)
                animPulse -= 6.28f;
            float pulse = (Mathf.Cos(animPulse) / 4) + 0.7f;
            HealBeam.startColor = new Color(0.25f, 1, 0.25f, 0.5f * pulse);
            HealBeam.endColor = new Color(0.1f, 1, 0.1f, 0.75f * pulse);
            HealBeam.positionCount = 2;
            HealBeam.SetPositions(new Vector3[2] { Targeter.transform.position + (Vector3.up * (pulse - 0.5f) * healTargetRad), Targeter.transform.TransformPoint(Vector3.forward * -(lastZDist - targeterZOffset))});
        }
        private void StartBeam()
        {
            if ((bool)spinner)
                spinner.SetAutoSpin(true);
            HealBeam.gameObject.SetActive(true);
        }
        private void StopBeam()
        {
            if ((bool)spinner)
                spinner.SetAutoSpin(false);
            HealBeam.gameObject.SetActive(false);
        }

        private void UpdateTargetDist(float aimZDist)
        {
            float distChange;
            if (aimZDist > lastZDist + distVar)
            {
                distChange = lastZDist + ExtendSpeed * Time.deltaTime;
                if (distChange > MaxExtendRange + 111)
                    distChange = MaxExtendRange + 111;
                Targeter.localPosition = Targeter.localPosition.SetZ(distChange);
                lastZDist = distChange;
            }
            else if (aimZDist < lastZDist - distVar)
            {
                /*
                distChange = lastZDist - ExtendSpeed * Time.deltaTime;
                if (distChange < targeterZOffset)
                    distChange = targeterZOffset;
                Targeter.localPosition = Targeter.localPosition.SetZ(distChange);*/
                distChange = aimZDist;
                Targeter.localPosition = Targeter.localPosition.SetZ(distChange);
                lastZDist = distChange;
            }
        }
    }


    public class TankRepairer : MonoBehaviour, ITankCompAuto<TankRepairer, ModuleRepairAimer>
    {
        public static TankRepairer stat => null;
        public TankRepairer Inst => this;
        public Tank tank { get; set; }
        public HashSet<ModuleRepairAimer> Modules { get; set; } = new HashSet<ModuleRepairAimer>();
        private ModuleRepairAimer primary;
        private bool dirty = false;

        private Tank aimTargetTank = null;
        private List<TankBlock> TargetBlocks = new List<TankBlock>();
        private float MaxLockOnRange = 500;
        public bool TargTankValid => aimTargetTank && aimTargetTank.visible.isActive;
        private static List<Tank> techsCollect = new List<Tank>();

        public void StartManagingPre()
        {
        }
        public void StartManagingPost()
        {
            primary = Modules.FirstOrDefault();
            primary.Energy.UpdateConsumeEvent.Subscribe(UpdatePower);
        }
        public void StopManaging()
        {
            if (primary)
                primary.Energy.UpdateConsumeEvent.Unsubscribe(UpdatePower);
        }
        public void AddModule(ModuleRepairAimer rep)
        {
            dirty = true;
        }
        public void RemoveModule(ModuleRepairAimer rep)
        {
            dirty = true;
            if (primary == rep)
            {
                primary.Energy.UpdateConsumeEvent.Unsubscribe(UpdatePower);
                primary = Modules.FirstOrDefault(x => x != rep);
                if (primary)
                    primary.Energy.UpdateConsumeEvent.Subscribe(UpdatePower);
            }
        }
        private float eCost = 0;
        public void UpdatePower()
        {
            //DebugRandAddi.Log("TankRepairer.UpdatePower()");
            try
            {
                if (eCost > 0)
                {
                    DebugRandAddi.Log("Consuming " + eCost);
                    gameObject.GetComponent<ModuleEnergy>().ConsumeIfEnough(TechEnergy.EnergyType.Electric, eCost);
                    eCost = 0;
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("TankRepairer.UpdatePower() ERROR - " + e);
            }
        }
        public float GetCurrentEnergy()
        {
            if (tank != null)
            {
                var reg = tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
                return reg.storageTotal - reg.spareCapacity;
            }
            return 0;
        }
        private bool WasLocked = false;
        public void Update()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                if (dirty)
                {
                    dirty = false;
                    MaxLockOnRange = 0;
                    foreach (var module in Modules)
                    {
                        if (MaxLockOnRange < module.MaxLockOnRange)
                            MaxLockOnRange = module.MaxLockOnRange;
                    }
                    MaxLockOnRange += tank.blockman.CheckRecalcBlockBounds().extents.magnitude;
                }
                if (!TargTankValid)
                    TryInsureTargetTank();
                if (TargTankValid && 
                    (aimTargetTank.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude < MaxLockOnRange * MaxLockOnRange)
                {
                    bool targetFetchFail = false;
                    bool healedOnce = false;
                    float prevBudget = GetCurrentEnergy();
                    float budget = prevBudget;
                    int stepper = 0;
                    while (TargetBlocks.Count > stepper)
                    {
                        var caseB = TargetBlocks.ElementAt(stepper);
                        if (caseB == null)
                        {
                            //DebugRandAddi.Log("Removed stupid block");
                            TargetBlocks.RemoveAt(stepper);
                        }
                        else
                            stepper++;
                    }
                    foreach (var item in Modules)
                    {
                        //if (!TargetBlocks.Any())
                        //    DebugRandAddi.Log("no stupid blocks left");
                        if (item.UpdateRepair(TargetBlocks.GetRandomEntry(), ref budget))
                            healedOnce = true;
                        if (!TargetBlocks.Any())
                        {
                            if (!targetFetchFail && !TryInsureTargetTank())
                                targetFetchFail = true;
                        }
                    }
                    eCost += prevBudget - budget;
                    WasLocked = true;
                    if (healedOnce)
                    {
                        if (Time.time > timestep)
                        {
                            timestep = Time.time + UpdateIntervalSecLong;
                            if (TargTankValid)
                                TryFetchNewTargetBlocks(aimTargetTank);
                        }
                        return;
                    }

                }
                else if (WasLocked)
                {
                    WasLocked = false;
                    foreach (var item in Modules)
                    {
                        item.aimTargBlock = null;
                    }
                }
                TryInsureTargetTank();
            }
        }

        private const float UpdateIntervalSec = 0.75f;
        private const float UpdateIntervalSecLong = 1.5f;
        private float timestep = 0;
        private bool TryInsureTargetTank()
        {
            if (Time.time > timestep)
            {
                techsCollect.Clear();
                foreach (Visible tech in ManVisible.inst.VisiblesTouchingRadius(tank.boundsCentreWorld, MaxLockOnRange, new Bitfield<ObjectTypes>( new ObjectTypes
                   [] { ObjectTypes.Vehicle })))
                {
                    if (tech.isActive && (bool)tech.tank)
                    {
                        var RT = RandomTank.Insure(tech.tank);
                        if (tech.tank.Team == tank.Team && RT.Damaged)
                        {
                            techsCollect.Add(tech.tank);
                        }
                    }
                }
                if (techsCollect.Count == 0)
                {
                    timestep = Time.time + UpdateIntervalSec;
                    return false;
                }
                else
                {
                    DebugRandAddi.Log(tank.name + " - Found " + techsCollect.Count + " Techs to repair.");
                    if (techsCollect.Contains(tank))
                        DebugRandAddi.Log(tank.name + " - Including self!");
                    timestep = Time.time + UpdateIntervalSecLong;
                }
                aimTargetTank = techsCollect.GetRandomEntry();
                if (TargTankValid && TryFetchNewTargetBlocks(aimTargetTank))
                    return true;
            }
            return false;
        }

        private bool TryFetchNewTargetBlocks(Tank tank)
        {
            var rt = RandomTank.Insure(tank);
            if (rt.Damaged)
            {
                TargetBlocks.Clear();
                rt.GetDamagedBlocks(TargetBlocks);
                DebugRandAddi.Log(tank.name + " - Found " + TargetBlocks.Count + " blocks on target Tech to repair.");
                return TargetBlocks.Any();
            }
            return false;
        }

    }
}
