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

        private TankBlock aimTargBlock;
        private Transform Targeter;        // the transform that rests at the aim position
        private LineRenderer HealBeam;
        private TargetAimer TargetAimer;      // the controller that controls the GimbalAimers
        private List<GimbalAimer> gimbals;
        private Spinner spinner;

        private float targeterZOffset = 0;
        private float lastZDist = 0;
        private float healStep = 0;
        private float healTargetRad = 0;
        private int timestep = UpdateInterval;
        private float animPulse = 0;
        private float animSpeedMulti = 3;
        private const int UpdateInterval = 60;
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
                LogHandler.ThrowWarning("RandomAdditions: ModuleRepairAimer NEEDS a valid ModuleEnergy!\nCause of error - Block " + gameObject.name);
                return;
            }
            //Invoke("DelayedSub", 0.001f);

            TargetAimer = GetComponent<TargetAimer>();
            if (TargetAimer == null)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleRepairAimer NEEDS a valid TargetAimer in hierarchy!\nCause of error - Block " + gameObject.name);
                return;
            }
            gimbals = GetComponentsInChildren<GimbalAimer>().ToList();

            Targeter = KickStart.HeavyTransformSearch(transform, "_Target");
            if (Targeter == null)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleRepairAimer NEEDS a GameObject in hierarchy named \"_Target\" for the aiming position!\nCause of error - Block " + gameObject.name);
                return;
            }
            if (!(bool)Targeter.parent)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleRepairAimer NEEDS a GameObject in hierarchy named \"_Target\" WITHIN the GimbalAimer containing GameObjects!\nCause of error - Block " + gameObject.name);
                return;
            }
            if (!(bool)spinner)
                spinner = GetComponentInChildren<Spinner>(true);
            animSpeedMulti =  1 / Mathf.Max(HealPulseDelay, 0.25f);
            targeterZOffset = Targeter.localPosition.z;
            lastZDist = targeterZOffset;
            TargetAimer.Init(block, 60, null);
            InitHealBeam();
        }

        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRepairAimer",
            AltUI.HighlightString("Repair Arms") + " repair blocks from far away at the cost of " +
            AltUI.BlueString("Energy") + ".");
        public override void OnAttach()
        {
            TankRepairer.stat.HandleAddition(this);
            hint.Show();
        }

        public override void OnDetach()
        {
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

        public bool TargBlockValid => aimTargBlock && aimTargBlock.visible.isActive && aimTargBlock.tank 
            && !aimTargBlock.visible.damageable.IsAtFullHealth;

        public void UpdateRepair(TankBlock TargBlock)
        {
            if (TargetAimer == null || Targeter == null)
                return;
            //DebugRandAddi.Log("RandomAdditions: ModuleRepairAimer - UPDATING
            if (TargBlockValid)
            {
                if (CanAimAt(aimTargBlock.centreOfMassWorld))
                {
                    StartBeam();
                    TargetAimer.AimAtWorldPos(aimTargBlock.centreOfMassWorld, RotateSpeed);
                    float aimZDist = Targeter.parent.InverseTransformPoint(aimTargBlock.centreOfMassWorld).z;
                    UpdateTargetDist(aimZDist);
                    UpdateHealBeam();
                }
                else
                {
                    aimTargBlock = TargBlock;
                }
            }
            else
            {
                StopBeam();
                Vector3 defaultAim = Targeter.position + (25 * block.trans.forward);
                TargetAimer.AimAtWorldPos(defaultAim, RotateSpeed);
                UpdateTargetDist(targeterZOffset);
            }

            timestep++;
            if (timestep > UpdateInterval)
            {
                timestep = 0;
            }
            if (TargBlockValid)
            {
                healStep += Time.deltaTime;
                if (healStep >= HealPulseDelay)
                {
                    healStep = 0;
                    UpdateHeals();
                }
            }
            else
                aimTargBlock = TargBlock;
        }
        private void UpdateHeals()
        {
            if (Targeter.InverseTransformPoint(aimTargBlock.centreOfMassWorld).Approximately(Vector3.zero, DistDeviance))
            {
                if (ManNetwork.IsHost || !ManNetwork.IsNetworked)
                {
                    if (Energy.ConsumeIfEnough(TechEnergy.EnergyType.Electric, HealPulseCost))
                    {
                        StartBeam();
                        var DMG = aimTargBlock.visible.damageable;
                        DMG.Repair(HealHealthRate);
                        aimTargBlock.visible.KeepAwake();
                    }
                    else
                    {
                        StopBeam();
                        //DebugRandAddi.Log("RandomAdditions: ModuleRepairAimer - UPDATING - not enough energy " + Energy.GetCurrentAmount(EnergyRegulator.EnergyType.Electric));
                    }
                }
                else
                    StartBeam();
            }
        }

        private void InitHealBeam()
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
        private void UpdateHealBeam()
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

        private Tank aimTarget = null;
        private TankBlock aimTargBlock;
        private float MaxLockOnRange = 500;
        public bool TargTankValid => aimTarget && aimTarget.visible.isActive;
        public bool TargBlockValid => aimTargBlock && aimTargBlock.visible.isActive && aimTargBlock.tank
            && !aimTargBlock.visible.damageable.IsAtFullHealth;
        private Stack<Tank> techsCollect = new Stack<Tank>();

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
        }
        public void RemoveModule(ModuleRepairAimer rep)
        {
            if (primary == rep)
            {
                primary.Energy.UpdateConsumeEvent.Unsubscribe(UpdatePower);
                primary = Modules.FirstOrDefault(x => x != rep);
                if (primary)
                    primary.Energy.UpdateConsumeEvent.Subscribe(UpdatePower);
            }
        }

        public void UpdatePower()
        {
            IterateTargets();
        }

        private const int UpdateInterval = 60;
        private int timestep = UpdateInterval;
        private void RefreshTargets()
        {
            timestep++;
            if (timestep > UpdateInterval)
            {
                timestep = 0;
                techsCollect.Clear();
                foreach (Visible tech in ManVisible.inst.VisiblesTouchingRadius(tank.boundsCentreWorld, MaxLockOnRange, new Bitfield<ObjectTypes>()))
                {
                    if (tech.isActive && (bool)tech.tank)
                    {
                        if (tech.tank.Team == tank.Team)
                        {
                            techsCollect.Push(tech.tank);
                        }
                    }
                }
            }
        }

        static List<TankBlock> blockC = new List<TankBlock>();
        private bool TryFindNewTargetBlock(Tank tank)
        {
            aimTargBlock = null;
            var rt = RandomTank.Insure(tank);
            if (rt.Damaged)
            {
                blockC.Clear();
                RandomTank.Insure(tank).GetDamagedBlocks(blockC);
                foreach (TankBlock TB in blockC)
                {
                    Damageable damage = TB.GetComponent<Damageable>();
                    if (!damage.IsAtFullHealth)
                    {
                        if ((bool)TB)
                        {
                            aimTargBlock = TB;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool TryInsureTarget()
        {
            if (TargTankValid && TryFindNewTargetBlock(aimTarget))
                return true;
            if (techsCollect.Count == 0)
            {
                RefreshTargets();
                if (techsCollect.Count == 0)
                    return false;
            }
            aimTarget = techsCollect.Pop();
            if (TargTankValid && TryFindNewTargetBlock(aimTarget))
                return true;
            return false;
        }

        private void IterateTargets()
        {
            if (TargTankValid && (aimTarget.boundsCentreWorld - tank.boundsCentreWorld).sqrMagnitude > MaxLockOnRange * MaxLockOnRange)
            {
                bool targetFetchFail = false;
                foreach (var item in Modules)
                {
                    item.UpdateRepair(aimTargBlock);
                    if (!TargBlockValid)
                    {
                        if (!targetFetchFail && !TryInsureTarget())
                            targetFetchFail = true;
                    }
                }
            }
            else
            {
                TryInsureTarget();
            }
        }
    }
}
