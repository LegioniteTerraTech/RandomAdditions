using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;

public class ModuleHangar : RandomAdditions.ModuleHangar { };
namespace RandomAdditions
{
    /// <summary>
    /// Can store and deploy Techs in an active combat zone.
    /// Stores Techs within. Has a predefined volume space.
    /// Does not work in MP.
    /// <para>
    /// <b>Note:</b> If the hangar blows up, all of the techs stored within will explode into a shower of blocks.
    /// </para>
    /// </summary>
    [AutoSaveComponent]
    public class ModuleHangar : Module
    {
        private const float DelayedUpdateDelay = 0.5f;

        internal Tank tank => block.tank;
        private bool isSaving = false;

        private Transform HangarEntry;
        private Transform HangarInside;
        private ModuleTractorBeam TracBeam;
        private ModuleEnergy energyMan;
                                                // If there's a ModuleTractorBeam in the same block, this will be able to queue 
                                                //  and manage the "garrison" of stored Techs in combat when spacebar is pressed
        public float MaxDockingRadius = 3;      // The maximum distance from GameObject "_Entry" before the hangar begins trying to dock
        public int MaxTechCapacity = 1;         // The maximum Techs this hangar can store
        public int MaxTechExtents = 6;          // The max length/width/height a Tech can have before being stored in the hangar
        public int MaxTechBlocksPerSpot = 12;   // The max blocks a stored Tech is allowed to have in storage
        public float DockDeploySpeed = 50;      // The velocity of which to launch the Techs deployed from this hangar
        public bool AllowHammerspace = false;   // Hangars in hangars.  Matroska dolls

        // REQUIRES ModuleEnergy!
        public float MinimumEnergyPercent = 0.5f;// If our energy is below this percent then we don't use energy
        public int ChargeStoredTechsRate = 0;   // The rate this will drain from the host Tech to charge the stored Techs
        public int RepairStoredTechsRate = 0;   // The rate this will heal the blocks of the Techs stored
        public float EnergyToRepairRatio = 0.5f;// repair/energy.  The lower this is the less energy it will consume per update


        private Tank TankWantsToDock;
        private bool shouldTaxThisFrame = false;
        private bool isDeploying = false;
        private bool isEjecting = false;
        [SSaveField]
        public int HangarExistTime = 0;
        [SSaveField]
        public List<KeyValuePair<STechDetails, TechData>> GarrisonTechs = new List<KeyValuePair<STechDetails, TechData>>();
        [SSaveField]
        public List<int> LinkedTechs = new List<int>();


        private Tank LaunchAnimating;
        private Vector3 LaunchAnimatingPos;
        private List<TankBlock> AbsorbAnimating = new List<TankBlock>();
        private List<Vector3> AbsorbAnimatingPos = new List<Vector3>();
        private float nextTime = 0;
        private float HangarReturnRequestDelay = 0;

        public bool IsDocking => TankWantsToDock;
        public bool HasRoom { get { return GarrisonTechs.Count < MaxTechCapacity; } }
        public bool HasStoredTechs { get { return GarrisonTechs.Count > 0; } }


        internal void OnPool()
        {
            enabled = true;
            try
            {
                block.AttachEvent.Subscribe(OnAttach);
                block.DetachEvent.Subscribe(OnDetach);
            }
            catch
            {
                Debug.LogError("RandomAdditions: ModuleHangar - TankBlock is null");
            }
            try
            {
                HangarEntry = KickStart.HeavyObjectSearch(transform, "_Entry");
            }
            catch { }
            if (HangarEntry == null)
            {
                HangarEntry = this.transform;
                LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar NEEDS a GameObject in hierarchy named \"_Entry\" for the hangar enterance!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }
            try
            {
                HangarInside = KickStart.HeavyObjectSearch(transform, "_Inside");
            }
            catch { }
            if (HangarInside == null)
            {
                HangarInside = this.transform;
                LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar NEEDS a GameObject in hierarchy named \"_Inside\" for the hangar inside!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }
            TracBeam = gameObject.GetComponent<ModuleTractorBeam>();
            if (ChargeStoredTechsRate >= -1 || RepairStoredTechsRate > 0)
            {
                if (EnergyToRepairRatio <= 0)
                {
                    EnergyToRepairRatio = 0.5f;
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar field EnergyToRepairRatio cannot be a value below or equal to zero.\nCause of error - Block " + gameObject.name);
                }
                energyMan = transform.GetComponent<ModuleEnergy>();
                if (!energyMan)
                {
                    ChargeStoredTechsRate = 0;
                    RepairStoredTechsRate = 0;
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar NEEDS ModuleEnergy in the base GameObject layer if ChargeStoredTechsRate and/or RepairStoredTechsRate is greater than 0!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                }
                else
                    energyMan.UpdateConsumeEvent.Subscribe(OnDrain);
            }
            enabled = false;
        }
        public void OnDrain()
        {
            if (shouldTaxThisFrame)
            {
                try
                {
                    foreach (KeyValuePair<STechDetails, TechData> pair in GarrisonTechs)
                    {
                        UpdateGains(pair.Key);
                    }
                }
                catch { }
                shouldTaxThisFrame = false;
            }
        }

        public void OnAttach()
        {
            enabled = true;
            block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //block.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            block.MouseDownEvent.Subscribe(new Action<TankBlock, int>(RequestDockingPlayer));
            ExtUsageHint.ShowExistingHint(4009);
        }
        public void OnDetach()
        {
            block.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //block.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            block.MouseDownEvent.Unsubscribe(new Action<TankBlock, int>(RequestDockingPlayer));
            TankWantsToDock = null;
            if (TracBeam)
                TracBeam.ReleaseTech();
            isDeploying = false;
            int fireTimes = AbsorbAnimating.Count;
            for (int step = 0; step < fireTimes;)
            {
                ManLooseBlocks.inst.RequestDespawnBlock(AbsorbAnimating[step], DespawnReason.Host);
                AbsorbAnimating.RemoveAt(step);
                AbsorbAnimatingPos.RemoveAt(step);
                fireTimes--;
            }
            if (LaunchAnimating)
            {
                LaunchAnimating.trans.localScale = Vector3.one;
                foreach (TankBlock TB in LaunchAnimating.blockman.IterateBlocks())
                {
                    TB.visible.SetInteractionTimeout(0);
                    TB.visible.ColliderSwapper.EnableCollision(true);
                }
                LaunchAnimating.enabled = true;
                LaunchAnimating.visible.Teleport(HangarEntry.position, HangarEntry.rotation, false, false);
            }
            for (int step = 0; step < fireTimes;)
            {
                fireTimes--;
            }
            if (!isSaving)
            {
                fireTimes = GarrisonTechs.Count;
                for (int step = 0; step < fireTimes;)
                {
                    LaunchTech(true);
                    fireTimes--;
                }
            }
            enabled = false;
        }
        internal void Update()
        {
            if (nextTime < Time.time)
            {
                isDeploying = tank.control.FireControl;
                if (!ManNetwork.IsNetworked)
                    DelayedUpdate();
                nextTime = Time.time + DelayedUpdateDelay;
                HangarExistTime++;
            }
            if (!ManNetwork.IsNetworked)// || ManNetwork.IsHost)
            {
                int fireTimes = AbsorbAnimating.Count;
                for (int step = 0; step < fireTimes; step++)
                {
                    TankBlock toManage = AbsorbAnimating.ElementAt(step);
                    Vector3 toManagePos = AbsorbAnimatingPos.ElementAt(step);
                    Vector3 fL = HangarInside.localPosition;
                    toManagePos = ((fL - toManagePos) * Time.deltaTime * 3) + toManagePos;
                    AbsorbAnimatingPos[step] = toManagePos;
                    toManage.visible.centrePosition = transform.TransformPoint(toManagePos);
                    Vector3 item = toManagePos;
                    float distDiff = 1 / (HangarEntry.localPosition - HangarInside.localPosition).magnitude;
                    float scaleMulti = 0.25f;
                    if (toManage.GetComponent<TankBlockScaler>())
                        scaleMulti = toManage.GetComponent<TankBlockScaler>().AimedDownscale;
                    float dynamicScaling = 1f - scaleMulti;
                    toManage.trans.localScale = (Vector3.one * scaleMulti) + (Vector3.one * Mathf.Min(Mathf.Max(0, (fL - toManagePos).magnitude * distDiff * dynamicScaling), dynamicScaling));
                    
                    if (fL.x - 0.06f < item.x && item.x < fL.x + 0.06f && fL.y - 0.06f < item.y && item.y < fL.y + 0.06f && fL.z - 0.06f < item.z && item.z < fL.z + 0.06f)
                    {
                        AbsorbAnimating.RemoveAt(step);
                        AbsorbAnimatingPos.RemoveAt(step);
                        toManage.visible.SetInteractionTimeout(0);
                        toManage.visible.ColliderSwapper.EnableCollision(true);
                        ManLooseBlocks.inst.RequestDespawnBlock(toManage, DespawnReason.Host);
                        step--;
                        fireTimes--;
                    }
                }
                if (LaunchAnimating)
                {
                    try
                    {
                        if (!LaunchAnimating.visible.isActive)
                        {
                            try
                            {
                                LaunchAnimating.enabled = true;
                                LaunchAnimating.trans.localScale = Vector3.one;
                                foreach (TankBlock TB in LaunchAnimating.blockman.IterateBlocks())
                                {
                                    TB.visible.SetInteractionTimeout(0);
                                    TB.visible.ColliderSwapper.EnableCollision(true);
                                }
                            }
                            catch { }
                            LaunchAnimating = null;
                            return;
                        }
                        Vector3 fL = HangarEntry.localPosition;
                        LaunchAnimatingPos = ((fL - LaunchAnimatingPos) * Time.deltaTime * 8) + LaunchAnimatingPos;
                        LaunchAnimating.visible.Teleport(transform.TransformPoint(LaunchAnimatingPos), HangarEntry.rotation, false, true);
                        float distDiff = 1 / (HangarEntry.localPosition - HangarInside.localPosition).magnitude;
                        LaunchAnimating.trans.localScale = (Vector3.one / 4) + (Vector3.one * Mathf.Min(Mathf.Max(0, 0.75f - ((fL - LaunchAnimatingPos).magnitude * distDiff * 0.75f)), 0.75f));
                        Vector3 item = LaunchAnimatingPos;
                        if (fL.x - 2f < item.x && item.x < fL.x + 2f && fL.y - 2f < item.y && item.y < fL.y + 2f && fL.z - 2f < item.z && item.z < fL.z + 2f)
                        {
                            foreach (TankBlock TB in LaunchAnimating.blockman.IterateBlocks())
                            {
                                TB.visible.SetInteractionTimeout(0);
                                TB.visible.ColliderSwapper.EnableCollision(true);
                            }
                            LaunchAnimating.trans.localScale = Vector3.one;
                            LaunchAnimating.enabled = true;
                            try
                            {
                                Vector3 ExitPosScene = tank.boundsCentreWorld;
                                if (!ManWorld.inst.TryProjectToGround(ref ExitPosScene))
                                {   // Illegal existance error
                                    LaunchAnimating.visible.Teleport(tank.boundsCentreWorld + (Vector3.up * 64), Quaternion.identity, false);
                                }
                                LaunchAnimating.visible.MoveAboveGround();
                                if (tank.rbody)
                                    LaunchAnimating.rbody.velocity = tank.rbody.velocity;
                                LaunchAnimating.rbody.velocity += HangarEntry.forward * DockDeploySpeed;
                            }
                            catch { }
                            try
                            {
                                foreach (var caseI in TankWantsToDock.blockman.IterateBlockComponents<ModuleBooster>())
                                {
                                    foreach (var caseII in caseI.GetComponentsInChildren<FanJet>())
                                        caseII.SetSpin(1);
                                    foreach (var caseII in caseI.GetComponentsInChildren<BoosterJet>())
                                        caseII.SetFiring(true);
                                }
                                ForceAllAIsToEscort(LaunchAnimating);
                                LaunchAnimating.control.TestBoostControl();
                            }
                            catch { }
                            LaunchAnimating = null;
                            nextTime = 0;
                        }
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("RandomAdditions: ModuleHangar encountered a serious error when animating deploy for a Tech " + e);
                    }
                    try
                    {
                        LaunchAnimating.enabled = true;
                        LaunchAnimating.trans.localScale = Vector3.one;
                        foreach (TankBlock TB in LaunchAnimating.blockman.IterateBlocks())
                        {
                            TB.visible.SetInteractionTimeout(0);
                            TB.visible.ColliderSwapper.EnableCollision(true);
                        }
                    }
                    catch { }
                    LaunchAnimating = null;
                }
            }

        }
        private static Bitfield<ObjectTypes> searchBit = new Bitfield<ObjectTypes>(new ObjectTypes[2] { ObjectTypes.Scenery, ObjectTypes.Vehicle });
        private void DelayedUpdate()
        {
            shouldTaxThisFrame = true;
            if (tank.beam.IsActive)
                return; // cannot grab techs whilist in beam
            if (isDeploying || isEjecting)
            {
                HangarReturnRequestDelay = 4;
                TankWantsToDock = null;
                if (TracBeam)
                    TracBeam.ReleaseTech();
                if (GarrisonTechs.Count > 0)
                {
                    LaunchTech();
                }
                else
                    isEjecting = false;
            }
            else
            {
                if (TankWantsToDock != null)
                {
                    StoreTech();
                }
                else if (HangarReturnRequestDelay <= 0)
                    ReturnLinkedTechsToHangar();
                else
                    HangarReturnRequestDelay -= DelayedUpdateDelay;
            }
        }


        private void LaunchTech(bool killTechs = false)
        {
            Vector3 ExitPosScene = HangarEntry.position;
            if (!ManWorld.inst.TryProjectToGround(ref ExitPosScene))
                return; // Cannot animate more than one or we will have clipping Techs in the end!!!
            else if (ExitPosScene.y > HangarEntry.position.y)
                return; // Cannot fire into the ground
            if (LaunchAnimating)
                return; // Cannot animate more than one or we will have clipping Techs in the end!!!
            KeyValuePair<STechDetails, TechData> garrisonedTech;

            bool canRepair = RepairStoredTechsRate > 0;
            bool canCharge = ChargeStoredTechsRate > 0;
            if (canRepair || canCharge)
            {
                garrisonedTech = new KeyValuePair<STechDetails, TechData>(null, null);
                int length = GarrisonTechs.Count;
                for (int step = 0; step < length; step++)
                {
                    STechDetails detail = GarrisonTechs[step].Key;
                    if (detail.ID != -1337)
                    {
                        garrisonedTech = GarrisonTechs[step];
                        break;
                    }
                    else
                    {
                        bool valid = true;
                        if (canRepair && detail.HealthAdded < detail.HealthDamage)
                            valid = false;
                        if (canCharge && detail.EnergyCurrent < detail.EnergyCapacity)
                            valid = false;
                        if (valid)
                        {
                            garrisonedTech = GarrisonTechs[step];
                            break;
                        }
                    }
                }
                if (garrisonedTech.Value == null)
                    return;
            }
            else
                garrisonedTech = GarrisonTechs[0];


            if (garrisonedTech.Value == null)
            {
                Debug.Log("RandomAdditions: ModuleHangar - SaveData corrupted!!!  Tech was erased to prevent crash.");
                GarrisonTechs.Remove(garrisonedTech);
                return;
            }

            if (!killTechs)
            {
                foreach (Visible Vis in ManVisible.inst.VisiblesTouchingRadius(HangarEntry.position, MaxTechExtents, searchBit))
                {
                    if (Vis.ID != tank.visible.ID)
                    {
                        Debug.Log("Hangar on " + tank.name + " ID: " + tank.visible.ID);
                        Debug.Log("Hangar blocked by " + Vis.name + " ID: " + Vis.ID);
                        return;
                    }
                }
            }
            ManSpawn.TankSpawnParams TSP = new ManSpawn.TankSpawnParams
            {
                blockIDs = new int[0],
                forceSpawn = false,
                isInvulnerable = false,
                isPopulation = tank.IsPopulation,
                teamID = tank.Team,
                techData = garrisonedTech.Value,
                position = HangarEntry.position,
                placement = ManSpawn.TankSpawnParams.Placement.BoundsCentredAtPosition,
                hideMarker = false,
                hasRewardValue = false,
                explodeDetachingBlocksDelay = 0,
                ignoreSceneryOnSpawnProjection = true,
                inventory = null,
                rotation = HangarEntry.rotation,
                shouldExplodeDetachingBlocks = false,
            };
            try
            {
                Tank newTech = ManSpawn.inst.SpawnTank(TSP, true);
                if (!newTech)
                {
                    if (killTechs)
                        Debug.Log("RandomAdditions: ModuleHangar - Could not deploy Tech on block alteration so tech was lost!");
                    else
                        Debug.Log("RandomAdditions: ModuleHangar - Could not deploy Tech at this time.");
                    return;
                }
                try
                {
                    if (newTech.Anchors)
                        newTech.Anchors.UnanchorAll(false);
                    LoadToTech(newTech, garrisonedTech.Key);
                }
                catch { }
                if (killTechs)
                {
                    newTech.visible.Teleport(HangarEntry.position, HangarEntry.rotation, false, false);
                    newTech.blockman.Disintegrate();
                    LinkedTechs.Remove(garrisonedTech.Key.ID);
                    GarrisonTechs.Remove(garrisonedTech);
                    return;
                }
                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCCab, block.centreOfMassWorld);
                if (garrisonedTech.Key.ID != -1337)
                {
                    LinkedTechs.Remove(garrisonedTech.Key.ID);
                    if (!isEjecting)
                        LinkedTechs.Add(newTech.visible.ID);
                }

                // PREPARE TO ANIMATE!!!
                newTech.enabled = false;
                foreach (TankBlock TB in newTech.blockman.IterateBlocks())
                {
                    TB.visible.SetInteractionTimeout(4);
                    TB.visible.ColliderSwapper.EnableCollision(false);
                }
                newTech.trans.localScale = Vector3.one / 4;
                newTech.visible.Teleport(HangarInside.position, HangarInside.rotation, false);
                LaunchAnimating = newTech;
                LaunchAnimatingPos = HangarInside.localPosition;
            }
            catch (Exception e)
            {
                LinkedTechs.Clear();
                Debug.Log("RandomAdditions: ModuleHangar - SaveData corrupted!!!  Tech was erased to prevent crash." + e);
            }
            GarrisonTechs.Remove(garrisonedTech);
        }
        private void StoreTech()
        {
            int error = 0;
            try
            {
                if (TracBeam != null)
                {
                    Vector3 ExitPosScene = HangarEntry.position;
                    if (!ManWorld.inst.TryProjectToGround(ref ExitPosScene))
                    {
                        TracBeam.ReleaseTech();
                        return; // Cannot drag tech into the ground
                    }
                    else if (ExitPosScene.y > HangarEntry.position.y)
                    {
                        TracBeam.ReleaseTech();
                        return; // Cannot drag tech into the ground
                    }
                    else if (!TracBeam.IsInRange(TankWantsToDock))
                    {
                        TankWantsToDock = null;
                        TracBeam.ReleaseTech();
                        return;
                    }
                    else
                    {
                        TracBeam.SetTargetWorld(HangarEntry.position);
                        TracBeam.GrabTech(TankWantsToDock, false);
                    }
                }
                error++;
                if (!CanDock(TankWantsToDock))
                {
                    TankWantsToDock = null;
                    if (TracBeam)
                        TracBeam.ReleaseTech();
                    return;
                }
                error++;
                if (TankWantsToDock == Singleton.playerTank)
                {
                    ManTechs.inst.RequestSetPlayerTank(tank);
                }
                error++;
                if ((HangarEntry.position - TankWantsToDock.boundsCentreWorld).magnitude <= MaxDockingRadius)
                {
                    TechData TD = new TechData();
                    error++;
                    TD.SaveTech(TankWantsToDock, false, true);
                    error++;
                    if (TD != null)
                    {
                        long TechHP = 0;
                        long TechHPMax = 0;
                        long TechBatt = 0;
                        long TechBattMax = 0;
                        foreach (TankBlock TB in TankWantsToDock.blockman.IterateBlocks())
                        {
                            var dmg = TB.GetComponent<Damageable>();
                            if (dmg)
                            {
                                TechHPMax += Mathf.CeilToInt(dmg.MaxHealth);
                                TechHP += Mathf.CeilToInt(dmg.Health);
                            }
                        }
                        foreach (ModuleEnergyStore MES in TankWantsToDock.blockman.IterateBlockComponents<ModuleEnergyStore>())
                        {
                            if (MES.m_EnergyType == EnergyRegulator.EnergyType.Electric)
                            {
                                TechBattMax += Mathf.CeilToInt(MES.m_Capacity);
                                TechBatt += Mathf.CeilToInt(MES.CurrentAmount);
                            }
                        }
                        STechDetails TechD = new STechDetails {
                            HealthDamage = TechHPMax - TechHP,
                            EnergyCapacity = TechBattMax,
                            EnergyCurrent = TechBatt,
                            HangarExistTime = HangarExistTime,
                        };

                        if (LinkedTechs.Remove(TankWantsToDock.visible.ID))
                        {
                            TechD.ID = TankWantsToDock.visible.ID;
                        }
                        else
                            TechD.ID = -1337;
                        try
                        {
                            TechD.ExtSerial = TankWantsToDock.GetSerialization();
                            TankWantsToDock.SetSerialization(null);
                        }
                        catch { }

                        GarrisonTechs.Add(new KeyValuePair<STechDetails, TechData>(TechD, TD));

                        foreach (TankBlock TB in TankWantsToDock.blockman.IterateBlocks())
                        {
                            TB.visible.SetInteractionTimeout(4);
                            TB.visible.ColliderSwapper.EnableCollision(false);
                            AbsorbAnimating.Add(TB);
                            AbsorbAnimatingPos.Add(transform.InverseTransformPoint(TB.visible.centrePosition));
                        }
                        if (TracBeam)
                            TracBeam.ReleaseTech();
                        TankWantsToDock.blockman.Disintegrate(false);
                        TankWantsToDock = null;
                        ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCFabricator, block.centreOfMassWorld);
                        if (HangarReturnRequestDelay <= 0)
                            ReturnLinkedTechsToHangar();
                    }
                }
            }
            catch { Debug.Log("Error level " + error); }
        }


        public bool CanDock(Tank tech, bool ChangeTarget = true, bool debugLog = false)
        {
            if (!tech)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - TECH IS NULL - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!tech.rbody)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Tech is static or anchored - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (TankWantsToDock != null && !ChangeTarget)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar is busy docking another tech already and ChangeTarget is false - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (isDeploying && HasStoredTechs)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar is deploying stored Techs. Cannot Dock and Deploy at the same time - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech.Team != tank.Team)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Hangars can only store Techs of the same team - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech == tank)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Tech tried to store itself in it's own hangar - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            Vector3 techExt = tech.blockBounds.extents;
            bool canFit = Mathf.Max(techExt.x, techExt.y, techExt.z)
                <= MaxTechExtents;
            bool enoughSpaceInSlot = tech.blockman.blockCount <= MaxTechBlocksPerSpot;
            if (!HasRoom)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar is at or over maximum capacity - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!canFit)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar of size " + MaxTechExtents + " cannot fit tech of size " + techExt + " - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!enoughSpaceInSlot)
            {
                if (debugLog)
                    Debug.Log("RandomAdditions: ModuleHangar - Tech has more blocks than a slot allows: Hangar block limit " + MaxTechBlocksPerSpot + " VS Tech block count " + tech.blockman.blockCount + " - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }

            foreach (TankBlock TB in tech.blockman.IterateBlocks())
            {
                ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                if (MH)
                {
                    if (MH.IsDocking)
                    {
                        if (debugLog)
                            Debug.Log("RandomAdditions: ModuleHangar - Grabbed tech has active ModuleHangar(s) docking other tech(s) - \n" + StackTraceUtility.ExtractStackTrace());
                        return false;
                    }
                    if (MH.HasStoredTechs && !AllowHammerspace)
                    {
                        if (debugLog)
                            Debug.Log("RandomAdditions: ModuleHangar - Grabbed tech already has Techs within and this ModuleHangar does not have AllowHammerspace set to true - \n" + StackTraceUtility.ExtractStackTrace());
                        return false;
                    }
                    // Techs with docked Techs are normally not allowed to dock inside another tech
                    // this is for balance reasons!
                }
            }
            return true;
        }

        /// <summary>
        /// Send a request to the docking station to dock
        /// </summary>
        /// <param name="tech">The tech to store</param>
        public bool AssignToDock(Tank tech, bool ChangeTarget = true)
        {
            if (TracBeam)
            {
                if (!LinkedTechs.Contains(tech.visible.ID))
                    LinkedTechs.Add(tech.visible.ID);
                if (RequestDocking(tech, ChangeTarget))
                {
                    return true;
                }
                LinkedTechs.Remove(tech.visible.ID);
                return false;
            }
            else
                return RequestDocking(tech, ChangeTarget);
        }
        internal void RequestDockingPlayer(TankBlock block, int num)
        {
            if (Singleton.playerTank == tank)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
                isEjecting = !isEjecting;
            }
            else
            {
                if (block == this.block)
                {
                    if (AssignToDock(Singleton.playerTank, true))
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                    else
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                }
            }
        }
        private bool RequestDocking(Tank tech, bool ChangeTarget = true)
        {
            if (CanDock(tech, ChangeTarget, true))
            {
                return DockTech(tech);
            }
            // Else we cannot store it!
            return false;
        }
        private void ReturnLinkedTechsToHangar()
        {
            int length = LinkedTechs.Count;
            for (int step = 0; step < length;)
            {
                TrackedVisible TV = ManVisible.inst.GetTrackedVisible(LinkedTechs[step]);
                if (TV != null)
                {
                    if (TV.visible?.tank)
                    {
                        if (!HasRoom)
                        {
                            LinkedTechs.RemoveAt(step);
                            step++;
                            continue;
                        }
                        if (CanDock(TV.visible.tank, false))
                        {
                            if (DockTech(TV.visible.tank))
                                return;
                        }
                        step++;
                        continue;
                    }
                }
                LinkedTechs.RemoveAt(step);
                length--;
            }
        }

        private bool DockTech(Tank tech)
        {
            if (TracBeam)
            {
                if (TracBeam.IsInRange(tech))
                {
                    TankWantsToDock = tech;
                    nextTime = 0;// Make it update ASAP
                    Debug.Log("RandomAdditions: ModuleHangar - Docking " + tech.name + "...");
                    return true;
                }
                else
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar has a ModuleTractorBeam and Tech is outside the ModuleTractorBeam's MaxRange");
            }
            else
            {
                if ((HangarEntry.position - tech.boundsCentreWorld).magnitude <= MaxDockingRadius)
                {
                    TankWantsToDock = tech;
                    nextTime = 0;// Make it update ASAP
                    Debug.Log("RandomAdditions: ModuleHangar - Docking " + tech.name + "...");
                    return true;
                }
                else
                    Debug.Log("RandomAdditions: ModuleHangar - Hangar has NO ModuleTractorBeam and Tech is outside the MaxDockingRadius");
            }
            return false;
        }


        private void UpdateGains(STechDetails sTech)
        {
            EnergyRegulator.EnergyType ET = EnergyRegulator.EnergyType.Electric;
            if (ChargeStoredTechsRate > 0)
            {
                if (sTech.EnergyCurrent < sTech.EnergyCapacity)
                {
                    long delta = Math.Min(ChargeStoredTechsRate, sTech.EnergyCapacity - sTech.EnergyCurrent);
                    if ((energyMan.GetCurrentAmount(ET) - delta) / (energyMan.GetTotalCapacity(ET) + 0.1f) > MinimumEnergyPercent)
                        sTech.EnergyCurrent += Mathf.CeilToInt(energyMan.ConsumeUpToMax(EnergyRegulator.EnergyType.Electric, delta));
                }
            }
            if (RepairStoredTechsRate > 0)
            {
                if (sTech.HealthDamage > sTech.HealthAdded)
                {
                    long delta = Math.Min(RepairStoredTechsRate, sTech.HealthDamage - sTech.HealthAdded);
                    float ER = delta / EnergyToRepairRatio;
                    if ((energyMan.GetCurrentAmount(ET) - ER) / (energyMan.GetTotalCapacity(ET) + 0.1f) > MinimumEnergyPercent)
                        sTech.HealthAdded += Mathf.CeilToInt(energyMan.ConsumeUpToMax(EnergyRegulator.EnergyType.Electric, ER) * EnergyToRepairRatio);
                } 
            }
        }
        private void LoadToTech(Tank tech, STechDetails sTech)
        {
            try
            {
                if (ChargeStoredTechsRate > 0)
                {   // Charge stored Tech energy
                    if (sTech.EnergyCapacity > 0)
                        tech.EnergyRegulator.SetAllStoresAmount((float)((double)sTech.EnergyCurrent / (double)sTech.EnergyCapacity));
                }
                else if (ChargeStoredTechsRate < 0)
                {   // Deplete stored Tech energy
                    if (sTech.EnergyCapacity > 0)
                        tech.EnergyRegulator.SetAllStoresAmount(0);
                }
                if (RepairStoredTechsRate > 0)
                {   // Repair stored Tech
                    long healsToDeal = sTech.HealthAdded;
                    foreach (var item in tech.blockman.IterateBlocks())
                    {
                        var dmg = item.GetComponent<Damageable>();
                        if (dmg)
                        {
                            if (!dmg.IsAtFullHealth)
                            {
                                float delta = dmg.MaxHealth - dmg.Health;
                                if (delta < healsToDeal)
                                {
                                    dmg.Repair(delta);
                                    healsToDeal -= (int)delta;
                                }
                                else
                                {
                                    dmg.Repair(healsToDeal);
                                    break;
                                }
                            }
                        }
                    }
                }
                if (sTech.ExtSerial != null)
                {
                    tech.SetSerialization(sTech.ExtSerial);
                }
            }
            catch { }
        }

        /// <summary>
        /// Modified copy from TAC_AI
        /// </summary>
        /// <param name="Do"></param>
        public void ForceAllAIsToEscort(Tank tank, bool Do = true)
        {
            //Needed to return AI mode back to Escort on unanchor as unanchoring causes it to go to idle
            try
            {
                if (Do)
                {
                    if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && tank.netTech.IsNotNull())
                    {
                        Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.SetAIMode, new SetAIModeMessage
                        {
                            m_AIAction = AITreeType.AITypes.Escort
                        }, tank.netTech.netId);
                    }
                    else
                    {
                        tank.AI.SetBehaviorType(AITreeType.AITypes.Escort);
                    }
                    if (tank.AI.TryGetCurrentAIType(out AITreeType.AITypes type))
                        Debug.Log("RandomAdditions: ModuleHangar(DeployedTech) - AI type is " + type.ToString());
                }
                else
                {
                    if (Singleton.Manager<ManNetwork>.inst.IsMultiplayer() && tank.netTech.IsNotNull())
                    {
                        Singleton.Manager<ManNetwork>.inst.SendToServer(TTMsgType.SetAIMode, new SetAIModeMessage
                        {
                            m_AIAction = AITreeType.AITypes.Idle
                        }, tank.netTech.netId);
                    }
                    else
                    {
                        tank.AI.SetBehaviorType(AITreeType.AITypes.Idle);
                    }
                }
            }
            catch { }
        }


        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {   // On general saving
                    if (Singleton.Manager<ManPointer>.inst.targetVisible)
                    {
                        if (!Singleton.Manager<ManPointer>.inst.targetVisible.block == block)
                        {
                            isSaving = true;// only disable ejecting when the world is removed
                        }
                        // The block saves every time it is grabbed, but for what purpose if it's being removed?!
                    }
                    else
                        isSaving = true;// only disable ejecting when the world is removed
                    if (ManSaveGame.Storing)
                    {   // Only save on world 
                        if (GarrisonTechs.Count > 0)
                            this.SerializeToSafeObject(GarrisonTechs);
                        if (LinkedTechs.Count > 0)
                            this.SerializeToSafeObject(LinkedTechs);
                    }
                }
                else
                {   //Load from snap
                    try
                    {
                        isSaving = false;
                        if (!this.DeserializeFromSafeObject(ref GarrisonTechs))
                        {
                            Debug.Log("RandomAdditions: Hangar of tech " + block.tank.name + " is empty.");
                        }
                        else
                            Debug.Log("RandomAdditions: Hangar of tech contains " + GarrisonTechs.Count + " Techs.");
                        this.DeserializeFromSafeObject(ref LinkedTechs);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
    [Serializable]
    public class STechDetails
    {
        public int ID = -1337;
        public long EnergyCapacity = 0;
        public long EnergyCurrent = 0;
        public long HealthDamage = 0;
        public long HealthAdded = 0;
        public float HangarExistTime = 0;
        public string ExtSerial;
    }
}
