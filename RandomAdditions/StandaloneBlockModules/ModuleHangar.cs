﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;
using UnityEngine.Networking;

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
    public class ModuleHangar : ExtModule
    {
        public class HangarCommand : MessageBase
        {
            public HangarCommand() { }
            public HangarCommand(ModuleHangar techBay, Tank TechToDock, bool ChangeTarget)
            {
                BlockIndex = techBay.block.GetBlockIndexAndTechNetID(out TechID);
                this.TechToDock = TechToDock.netTech.netId.Value;
                this.ChangeTarget = ChangeTarget;
            }

            public uint TechID;
            public int BlockIndex;
            public uint TechToDock;
            public bool ChangeTarget;
        }
        private static NetworkHook<HangarCommand> netHook = new NetworkHook<HangarCommand>(OnReceiveDockingRequest, NetMessageType.ToServerOnly);

        public class HangarLaunchCommand : MessageBase
        {
            public HangarLaunchCommand() { }
            public HangarLaunchCommand(ModuleHangar techBay)
            {
                BlockIndex = techBay.block.GetBlockIndexAndTechNetID(out TechID);
            }

            public uint TechID;
            public int BlockIndex;
        }
        private static NetworkHook<HangarLaunchCommand> netHookLaunch = new NetworkHook<HangarLaunchCommand>(OnReceiveLaunchRequest, NetMessageType.ToServerOnly);

        internal static void InsureNetHooks()
        {
            netHook.Register();
            netHookLaunch.Register();
        }

        private const float DelayedUpdateDelay = 0.5f;

        internal Tank tank => block.tank;
        private bool isSaving = false;

        private Transform HangarEntry;
        private Transform HangarExit;
        private Transform HangarInside;
        private Transform HangarInsideExit;
        private ModuleTractorBeam TracBeam;
        private ModuleEnergy energyMan;
                                                // If there's a ModuleTractorBeam in the same block, this will be able to queue 
                                                //  and manage the "garrison" of stored Techs in combat when spacebar is pressed
        public float MaxDockingRadius = 3;      // The maximum distance from GameObject "_Entry" before the hangar begins trying to dock
        public int MaxTechCapacity = 1;         // The maximum Techs this hangar can store
        public int MaxTechExtents = 6;          // The max length/width/height a Tech can have before being stored in the hangar
        public int MaxTechBlocksPerSpot = 12;   // The max blocks a stored Tech is allowed to have in storage
        public int MaxVolumeCapacity = int.MaxValue; // The max block cell volume across ALL spots the hangar can EVER store
        public float DockDeploySpeed = 50;      // The velocity of which to launch the Techs deployed from this hangar
        public bool AllowHammerspace = false;   // Hangars in hangars.  Matroska dolls.

        // REQUIRES ModuleEnergy!
        public float MinimumEnergyPercent = 0.5f;// If our energy is below this percent then we don't use energy
        public int ChargeStoredTechsRate = 0;   // The rate this will drain from the host Tech to charge the stored Techs
        public int RepairStoredTechsRate = 0;   // The rate this will heal the blocks of the Techs stored
        public float EnergyToRepairRatio = 0.5f;// repair/energy.  The lower this is the less energy it will consume per heal


        private Tank TankWantsToDock;
        private bool shouldTaxThisFrame = false;
        private bool isDeploying = false;
        private bool isEjecting = false;
        [SSaveField]
        public int HangarStoredVolume = 0;
        [SSaveField]
        public int HangarExistTime = 0;
        [SSaveField]
        public List<KeyValuePair<STechDetails, TechData>> GarrisonTechs = new List<KeyValuePair<STechDetails, TechData>>();
        [SSaveField]
        public List<int> LinkedTechs = new List<int>();


        private Tank LaunchAnimating;
        private Vector3 LaunchAnimatingPos;
        private readonly List<TankBlock> AbsorbAnimating = new List<TankBlock>();
        private readonly List<Vector3> AbsorbAnimatingPos = new List<Vector3>();
        private float nextTime = 0;
        private float HangarReturnRequestDelay = 0;

        private ModuleUIButtons buttonGUI;
        private GUI_BM_Element[] GUILocal = new GUI_BM_Element[2];
        private GUI_BM_Element[] GUIOther = new GUI_BM_Element[2];

        public bool IsDocking => TankWantsToDock;
        public bool HasRoom { get { return GarrisonTechs.Count < MaxTechCapacity; } }
        public bool HasStoredTechs { get { return GarrisonTechs.Count > 0; } }

        public static void OnBlockSelect(Visible targVis, ManPointer.Event mEvent, bool DOWN, bool yes2)
        {
            if (Singleton.playerTank && mEvent == ManPointer.Event.LMB && Input.GetKey(KickStart.HangarButton))
            {
                Tank tech = targVis.trans.root.GetComponent<Tank>();
                if (tech)
                {
                    if (tech.Team == Singleton.playerTank.Team && Singleton.playerTank != tech)
                    {
                        foreach (TankBlock TB in Singleton.playerTank.blockman.IterateBlocks())
                        {
                            ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                            if (MH)
                            {
                                if (MH.HasRoom && (!MH.IsDocking || Input.GetKey(KeyCode.LeftShift)))
                                {
                                    if (MH.RequestAssignToDock(tech))
                                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                                    else
                                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void InsureGUI()
        {
            if (buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Hangar", true);
                buttonGUI.OnGUIOpenAttemptEvent.Subscribe(BeforeGUI);
                GUILocal[0] = ModuleUIButtons.MakeElement("Free Deployed Techs", RequestFreeLinkedTechs, GetIconEject);
                GUILocal[1] = ModuleUIButtons.MakeElement("Release Tech", RequestLaunchTech, GetIconRelease);
                GUIOther[0] = ModuleUIButtons.MakeElement("Free Deployed Techs", RequestFreeLinkedTechs, GetIconEject);
                GUIOther[1] = ModuleUIButtons.MakeElement("Dock", RequestDockingPlayer, GetIconGrab);
            }
        }
        public Sprite GetIconRelease()
        {
            return UIHelpersExt.GetGUIIcon("ICON_TECHLOADER");
        }
        public Sprite GetIconGrab()
        {
            return UIHelpersExt.GetGUIIcon("Icon_AI_SCU");
        }
        public Sprite GetIconEject()
        {
            return UIHelpersExt.GetGUIIcon("GUI_Reset");
        }
        public void BeforeGUI()
        {
            if (tank != Singleton.playerTank)
            {
                buttonGUI.SetElementsInst(GUIOther);
            }
            else
            {
                if (GarrisonTechs.Count > 0)
                    buttonGUI.SetElementsInst(GUILocal);
                else
                    buttonGUI.DenyShow();
            }
        }

        protected override void Pool()
        {
            enabled = true;
            HangarStoredVolume = 0;
            try
            {
                HangarEntry = KickStart.HeavyTransformSearch(transform, "_Entry");
            }
            catch { }
            if (HangarEntry == null)
            {
                HangarEntry = this.transform;
                LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar NEEDS a GameObject in hierarchy named \"_Entry\" for the hangar enterance!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }

            try
            {
                HangarExit = KickStart.HeavyTransformSearch(transform, "_Exit");
            }
            catch { }
            if (HangarExit == null)
            {
                HangarExit = HangarEntry;
            }

            try
            {
                HangarInside = KickStart.HeavyTransformSearch(transform, "_Inside");
            }
            catch { }
            if (HangarInside == null)
            {
                HangarInside = this.transform;
                LogHandler.ThrowWarning("RandomAdditions: \nModuleHangar NEEDS a GameObject in hierarchy named \"_Inside\" for the hangar inside!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }

            try
            {
                HangarInsideExit = KickStart.HeavyTransformSearch(transform, "_InsideExit");
            }
            catch { }
            if (HangarInsideExit == null)
            {
                HangarInsideExit = HangarInside;
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

        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleHangar",
            AltUI.HighlightString("Hangars") + " hold Techs!  " + AltUI.HighlightString("H + Right-Click") + 
            " on a Tech to store or " + AltUI.HighlightString("move close and right-click on it") + " from another.");
        public override void OnAttach()
        {
            enabled = true;
            block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //block.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            hint.Show();
        }
        public override void OnDetach()
        {
            block.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            //block.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
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
                    TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);// disable ALL
                    TB.visible.ColliderSwapper.EnableCollision(true);
                }
                LaunchAnimating.enabled = true;
                LaunchAnimating.visible.Teleport(HangarEntry.position, HangarEntry.rotation, false, false);
            }
            for (int step = 0; step < fireTimes;)
            {
                fireTimes--;
            }
            if (!isSaving && ManNetwork.IsHost)
            {
                fireTimes = GarrisonTechs.Count;
                for (int step = 0; step < fireTimes;)
                {
                    DoLaunchTech(true);
                    fireTimes--;
                }
            }
            DebugRandAddi.Assert(HangarStoredVolume != 0, "RandomAdditions: ModuleHangar - HangarStoredBlocks was not exactly zero (" + HangarStoredVolume + ") when unloading Techs was finished. \nMaybe some blocks weren't able to be deployed correctly?!");
            HangarStoredVolume = 0;
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
            if (!ManNetwork.IsNetworked || ManNetwork.IsHost)
            {
                int fireTimes = AbsorbAnimating.Count;
                for (int step = 0; step < fireTimes; step++)
                {
                    TankBlock toManage = AbsorbAnimating.ElementAt(step);
                    if (toManage.IsNotNull())
                    {
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
                            toManage.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);// disable ALL
                            toManage.visible.ColliderSwapper.EnableCollision(true);
                            ManLooseBlocks.inst.RequestDespawnBlock(toManage, DespawnReason.Host);
                            step--;
                            fireTimes--;
                        }
                    }
                    else
                    {
                        AbsorbAnimatingPos.RemoveAt(step);
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
                                    TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);// disable ALL
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
                                TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);// disable ALL
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
                                    foreach (var caseII in caseI.GetComponentsInChildren<Thruster>())
                                        caseII.SetThrustRate(1, false);
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
                        DebugRandAddi.LogError("RandomAdditions: ModuleHangar encountered a serious error when animating deploy for a Tech " + e);
                    }
                    try
                    {
                        LaunchAnimating.enabled = true;
                        LaunchAnimating.trans.localScale = Vector3.one;
                        foreach (TankBlock TB in LaunchAnimating.blockman.IterateBlocks())
                        {
                            TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 0);// disable ALL
                            TB.visible.ColliderSwapper.EnableCollision(true);
                        }
                    }
                    catch
                    {
                        DebugRandAddi.LogError("RandomAdditions: ModuleHangar WAS NOT ABLE TO FINALIZE DEPLOYMENT ANIMATION!");
                    }
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
                    TryLaunchTech();
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


        public float RequestFreeLinkedTechs(float unused)
        {
            if (LinkedTechs.Any())
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Close);
            LinkedTechs.Clear();
            return 0;
        }
        public float RequestLaunchTech(float unused)
        {
            TryLaunchTech();
            return 0;
        }
        public void TryLaunchTech()
        {
            if (ManNetwork.IsHost)
                DoLaunchTech();
            else
            {
                netHookLaunch.TryBroadcast(new HangarLaunchCommand(this));
            }
        }
        private static bool OnReceiveLaunchRequest(HangarLaunchCommand command, bool isServer)
        {
            NetTech NT = ManNetTechs.inst.FindTech(command.TechID);
            if (NT?.tech)
            {
                TankBlock TB = NT.tech.blockman.GetBlockWithIndex(command.BlockIndex);
                if (TB)
                {
                    ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                    if (MH)
                    {
                        MH.DoLaunchTech();
                        return true;
                    }
                }
            }
            // Else we cannot launch it!
            return false;
        }
        private bool GetBestGarrisonedTech(out KeyValuePair<STechDetails, TechData> garrisonedTech)
        {
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
            }
            else
                garrisonedTech = GarrisonTechs[0];
            return garrisonedTech.Value != null;
        }
        private void DoLaunchTech(bool Disassemble = false)
        {
            if (ManNetwork.IsHost && GarrisonTechs.Count > 0)
            {
                Vector3 ExitPosScene = HangarExit.position;
                if (!ManWorld.inst.TryProjectToGround(ref ExitPosScene))
                    return; // Cannot fire into the ground 
                else if (ExitPosScene.y > HangarExit.position.y)
                    return; // Cannot fire into the ground
                if (LaunchAnimating)
                    return; // Cannot animate more than one or we will have clipping Techs in the end!!!

                if (!GetBestGarrisonedTech(out KeyValuePair<STechDetails, TechData> garrisonedTech))
                {
                    DebugRandAddi.LogError("RandomAdditions: ModuleHangar - SaveData corrupted!!!  Tech was erased to prevent crash.");
                    GarrisonTechs.Remove(garrisonedTech);
                    return;
                }

                if (!Disassemble)
                {
                    foreach (Visible Vis in ManVisible.inst.VisiblesTouchingRadius(HangarExit.position, MaxTechExtents, searchBit))
                    {
                        if (Vis.ID != tank.visible.ID)
                        {
                            DebugRandAddi.Info("Hangar on " + tank.name + " ID: " + tank.visible.ID);
                            DebugRandAddi.Info("Hangar blocked by " + Vis.name + " ID: " + Vis.ID);
                            return;
                        }
                    }
                }
                try
                {
                    if (ManNetwork.IsNetworked)
                        DoLaunchTechImmedeate(garrisonedTech, Disassemble);
                    else
                        DoLaunchTechWithAnimation(garrisonedTech, Disassemble);
                }
                catch (Exception e)
                {
                    LinkedTechs.Clear();
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - SaveData corrupted!!!  Tech was erased to prevent crash." + e);
                }
                GarrisonTechs.Remove(garrisonedTech);
            }
        }
        private void DoLaunchTechWithAnimation(KeyValuePair<STechDetails, TechData> garrisonedTech, bool Disassemble)
        {
            ManSpawn.TankSpawnParams TSP = new ManSpawn.TankSpawnParams
            {
                blockIDs = new int[0],
                forceSpawn = false,
                isInvulnerable = false,
                isPopulation = tank.IsPopulation,
                teamID = tank.Team,
                techData = garrisonedTech.Value,
                position = HangarExit.position,
                placement = ManSpawn.TankSpawnParams.Placement.BoundsCentredAtPosition,
                hideMarker = false,
                hasRewardValue = false,
                explodeDetachingBlocksDelay = 0,
                ignoreSceneryOnSpawnProjection = true,
                inventory = null,
                rotation = HangarExit.rotation,
                shouldExplodeDetachingBlocks = false,
            };
            Tank newTech = ManSpawn.inst.SpawnTank(TSP, true);
            if (!newTech)
            {
                if (Disassemble)
                    DebugRandAddi.LogError("RandomAdditions: ModuleHangar - Could not deploy Tech on block alteration so tech was lost!");
                else
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Could not deploy Tech at this time.");
                return;
            }
            try
            {
                if (newTech.Anchors)
                    newTech.Anchors.UnanchorAll(false);
                LoadToTech(newTech, garrisonedTech.Key);
            }
            catch { }
            if (Disassemble)
            {
                newTech.visible.Teleport(HangarExit.position, HangarExit.rotation, false, false);
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
                TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 4);// disable ALL
                TB.visible.ColliderSwapper.EnableCollision(false);
                HangarStoredVolume -= TB.filledCells.Length;
            }
            if (HangarStoredVolume < 0)
            {
                DebugRandAddi.Log("RandomAdditions: ModuleHangar - Stored volume on Tech deployment left a negative value in HangarStoredVolume!  Assuming block changes and snapping to 0...");
                HangarStoredVolume = 0;
            }
            newTech.trans.localScale = Vector3.one / 4;
            newTech.visible.Teleport(HangarExit.position, HangarExit.rotation, false);
            LaunchAnimating = newTech;
            LaunchAnimatingPos = HangarExit.localPosition;
        }
        private void DoLaunchTechImmedeate(KeyValuePair<STechDetails, TechData> garrisonedTech, bool Disassemble)
        {
            SpawnTechMessage STM = new SpawnTechMessage
            {
                m_CheatBypassInventory = false,
                m_IsPopulation = false,
                m_IsSpawnedByPlayer = false,
                m_PlayerNetID = UnityEngine.Networking.NetworkInstanceId.Invalid,
                m_PlayerWhoCalledSpawn = UnityEngine.Networking.NetworkInstanceId.Invalid,
                m_TechData = garrisonedTech.Value,
                m_Position = WorldPosition.FromScenePosition(HangarExit.position),
                m_Rotation = HangarExit.rotation,
                m_Team = tank.Team,
            };
            ManNetwork.inst.SendToServer(TTMsgType.SpawnTech, STM);

        }


        private void StoreTech()
        {
            int error = 0;
            try
            {
                if (ManNetwork.IsHost)
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
                                HangarStoredVolume += TB.filledCells.Length;
                            }
                            foreach (ModuleEnergyStore MES in TankWantsToDock.blockman.IterateBlockComponents<ModuleEnergyStore>())
                            {
                                if (MES.m_EnergyType == TechEnergy.EnergyType.Electric)
                                {
                                    TechBattMax += Mathf.CeilToInt(MES.m_Capacity);
                                    TechBatt += Mathf.CeilToInt(MES.CurrentAmount);
                                }
                            }
                            STechDetails TechD = new STechDetails
                            {
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

                            if (ManNetwork.IsNetworked)
                                DoStoreTechImmedeate(TankWantsToDock);
                            else
                                DoStoreTechWithAnimation(TankWantsToDock);

                                TankWantsToDock = null;
                            ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGCFabricator, block.centreOfMassWorld);
                            if (HangarReturnRequestDelay <= 0)
                                ReturnLinkedTechsToHangar();
                        }
                    }
                }
            }
            catch { DebugRandAddi.Log("Error level " + error); }
        }
        private void DoStoreTechWithAnimation(Tank tech)
        {
            foreach (TankBlock TB in tech.blockman.IterateBlocks())
            {
                TB.visible.SetLockTimout(Visible.LockTimerTypes.Interactible, 4);// disable ALL
                TB.visible.ColliderSwapper.EnableCollision(false);
                AbsorbAnimating.Add(TB);
                AbsorbAnimatingPos.Add(transform.InverseTransformPoint(TB.visible.centrePosition));
            }
            if (TracBeam)
                TracBeam.ReleaseTech();
            tech.blockman.Disintegrate(false);
        }
        private void DoStoreTechImmedeate(Tank tech)
        {
            TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(tech.netTech.HostID);
            ManNetwork.inst.SendToServer(TTMsgType.UnspawnTech, new UnspawnTechMessage
            {
                m_CheatBypassInventory = true,
                m_HostID = TV.HostID,
            }
            );
        }


        public bool CanDock(Tank tech, bool ChangeTarget = true, bool debugLog = false)
        {
            if (!tech)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - TECH IS NULL - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!tech.rbody)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Tech is static or anchored - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (TankWantsToDock != null && !ChangeTarget)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Hangar is busy docking another tech already and ChangeTarget is false - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (isDeploying && HasStoredTechs)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Hangar is deploying stored Techs. Cannot Dock and Deploy at the same time - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech.Team != tank.Team)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Hangars can only store Techs of the same team - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech == tank)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Tech tried to store itself in it's own hangar - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            Vector3 techExt = tech.blockBounds.extents;
            bool canFit = Mathf.Max(techExt.x, techExt.y, techExt.z)
                <= MaxTechExtents;
            bool enoughSpaceInSlot = tech.blockman.blockCount <= MaxTechBlocksPerSpot;
            if (!HasRoom)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Hangar is at or over maximum capacity - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!canFit)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Hangar of size " + MaxTechExtents + " cannot fit tech of size " + techExt + " - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!enoughSpaceInSlot)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Tech has more blocks than a slot allows: Hangar block limit " + MaxTechBlocksPerSpot + " VS Tech block count " + tech.blockman.blockCount + " - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            int techVolume = 0;
            foreach (var item in tech.blockman.IterateBlocks())
            {
                techVolume += item.filledCells.Length;
            }
            bool enoughSpaceInHangar = techVolume + HangarStoredVolume <= MaxVolumeCapacity;
            if (!enoughSpaceInHangar)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleHangar - Tech has more blocks than the whole hangar has room for: Hangar volume limit " + MaxVolumeCapacity + " + stored " + HangarStoredVolume + " VS Tech block count " + tech.blockman.blockCount + " - \n" + StackTraceUtility.ExtractStackTrace());
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
                            DebugRandAddi.Log("RandomAdditions: ModuleHangar - Grabbed tech has active ModuleHangar(s) docking other tech(s) - \n" + StackTraceUtility.ExtractStackTrace());
                        return false;
                    }
                    if (MH.HasStoredTechs && !AllowHammerspace)
                    {
                        if (debugLog)
                            DebugRandAddi.Log("RandomAdditions: ModuleHangar - Grabbed tech already has Techs within and this ModuleHangar does not have AllowHammerspace set to true - \n" + StackTraceUtility.ExtractStackTrace());
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
        public bool RequestAssignToDock(Tank tech, bool ChangeTarget = true)
        {
            if (ManNetwork.IsNetworked)
            {
                if (netHook.CanBroadcastTech(tank) && tech?.netTech)
                {
                    netHook.TryBroadcast(new HangarCommand(this, tech, ChangeTarget));
                }
                return true;
            }
            else
            {
                return DoAssignToDock(tech, ChangeTarget);
            }
        }
        internal float RequestDockingPlayer(float unused)
        {
            if (Singleton.playerTank == tank)
            {
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Back);
                isEjecting = !isEjecting;
            }
            else
            {
                if (RequestAssignToDock(Singleton.playerTank, true))
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                else
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
            }
            return 0;
        }
        private static bool OnReceiveDockingRequest(HangarCommand command, bool isServer)
        {
            NetTech NT = ManNetTechs.inst.FindTech(command.TechID);
            NetTech target = ManNetTechs.inst.FindTech(command.TechToDock);
            if (NT?.tech && target?.tech)
            {
                TankBlock TB = NT.tech.blockman.GetBlockWithIndex(command.BlockIndex);
                if (TB)
                {
                    ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                    if (MH)
                    {
                        MH.DoAssignToDock(target.tech, command.ChangeTarget);
                        return true;
                    }
                }
            }
            // Else we cannot store it!
            return false;
        }
        public bool DoAssignToDock(Tank tech, bool ChangeTarget = true)
        {
            if (TracBeam)
            {
                if (!LinkedTechs.Contains(tech.visible.ID))
                    LinkedTechs.Add(tech.visible.ID);
                if (TryDocking(tech, ChangeTarget))
                {
                    return true;
                }
                LinkedTechs.Remove(tech.visible.ID);
                return false;
            }
            else
                return TryDocking(tech, ChangeTarget);
        }
        private bool TryDocking(Tank tech, bool ChangeTarget = true)
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
                    DebugRandAddi.Info("RandomAdditions: ModuleHangar - Docking " + tech.name + "...");
                    return true;
                }
                else
                    DebugRandAddi.Info("RandomAdditions: ModuleHangar - Hangar has a ModuleTractorBeam and Tech is outside the ModuleTractorBeam's MaxRange");
            }
            else
            {
                if ((HangarEntry.position - tech.boundsCentreWorld).magnitude <= MaxDockingRadius)
                {
                    TankWantsToDock = tech;
                    nextTime = 0;// Make it update ASAP
                    DebugRandAddi.Info("RandomAdditions: ModuleHangar - Docking " + tech.name + "...");
                    return true;
                }
                else
                    DebugRandAddi.Info("RandomAdditions: ModuleHangar - Hangar has NO ModuleTractorBeam and Tech is outside the MaxDockingRadius");
            }
            return false;
        }


        private void UpdateGains(STechDetails sTech)
        {
            TechEnergy.EnergyType ET = TechEnergy.EnergyType.Electric;
            if (ChargeStoredTechsRate > 0)
            {
                if (sTech.EnergyCurrent < sTech.EnergyCapacity)
                {
                    long delta = Math.Min(ChargeStoredTechsRate, sTech.EnergyCapacity - sTech.EnergyCurrent);
                    if ((energyMan.GetCurrentAmount(ET) - delta) / (energyMan.GetTotalCapacity(ET) + 0.1f) > MinimumEnergyPercent)
                        sTech.EnergyCurrent += Mathf.CeilToInt(energyMan.ConsumeUpToMax(TechEnergy.EnergyType.Electric, delta));
                }
            }
            if (RepairStoredTechsRate > 0)
            {
                if (sTech.HealthDamage > sTech.HealthAdded)
                {
                    long delta = Math.Min(RepairStoredTechsRate, sTech.HealthDamage - sTech.HealthAdded);
                    float ER = delta / EnergyToRepairRatio;
                    if ((energyMan.GetCurrentAmount(ET) - ER) / (energyMan.GetTotalCapacity(ET) + 0.1f) > MinimumEnergyPercent)
                        sTech.HealthAdded += Mathf.CeilToInt(energyMan.ConsumeUpToMax(TechEnergy.EnergyType.Electric, ER) * EnergyToRepairRatio);
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
                        DebugRandAddi.Info("RandomAdditions: ModuleHangar(DeployedTech) - AI type is " + type.ToString());
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
                            DebugRandAddi.Info("RandomAdditions: Hangar of tech " + block.tank.name + " is empty.");
                        }
                        else
                            DebugRandAddi.Info("RandomAdditions: Hangar of tech contains " + GarrisonTechs.Count + " Techs.");
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
